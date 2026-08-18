using SimpleKVM.Input;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using System.Runtime.Versioning;

namespace SimpleKVM.Platform.win
{
    /// <summary>
    /// System-wide hotkeys via RegisterHotKey. WM_HOTKEY is posted to the thread that owns the
    /// registration window, so a dedicated background thread runs a message loop for a
    /// message-only window — independent of whatever framework drives the UI thread.
    /// </summary>
    [SupportedOSPlatform("windows6.1")]
    public class WindowsHotkeys : IHotkeyBackend
    {
        static readonly object pumpLock = new();
        static HotkeyPump? pump;

        static HotkeyPump Pump
        {
            get
            {
                lock (pumpLock)
                {
                    return pump ??= HotkeyPump.Start();
                }
            }
        }

        public IDisposable Register(HotkeyGesture gesture, Action action)
        {
            var modifiers = ToModifiers(gesture);
            var virtualKey = WindowsKeyNames.ToVirtualKey(gesture.KeyName)
                                ?? throw new Exception($"Could not parse hotkey key: {gesture.KeyName}");

            var pump = Pump;
            int hotkeyId = HotkeyPump.GenerateUniqueHotkeyId();

            bool registered = pump.Invoke(() =>
            {
                if (!PInvoke.RegisterHotKey(pump.Handle, hotkeyId, modifiers, virtualKey)) return false;
                pump.Actions[hotkeyId] = action;
                return true;
            });

            if (!registered) throw new Exception($"Could not register hotkey: {gesture}");

            return new Registration(pump, hotkeyId);
        }

        static HOT_KEY_MODIFIERS ToModifiers(HotkeyGesture gesture)
        {
            HOT_KEY_MODIFIERS result = 0;
            if (gesture.Alt) result |= HOT_KEY_MODIFIERS.MOD_ALT;
            if (gesture.Ctrl) result |= HOT_KEY_MODIFIERS.MOD_CONTROL;
            if (gesture.Shift) result |= HOT_KEY_MODIFIERS.MOD_SHIFT;
            if (gesture.Win) result |= HOT_KEY_MODIFIERS.MOD_WIN;
            return result;
        }

        sealed class Registration(HotkeyPump pump, int hotkeyId) : IDisposable
        {
            bool disposed;

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;

                pump.Invoke(() =>
                {
                    pump.Actions.Remove(hotkeyId);
                    PInvoke.UnregisterHotKey(pump.Handle, hotkeyId);
                    return true;
                });
            }
        }

        /// <summary>
        /// Owns a message-only window on a dedicated thread and pumps its messages. Work that must
        /// happen on that thread (RegisterHotKey binds to the calling thread) is posted to it via
        /// Invoke and run from the message loop.
        /// </summary>
        sealed unsafe class HotkeyPump
        {
            const uint WM_INVOKE = PInvoke.WM_USER + 1;
            static int nextHotkeyId = 0;

            //Kept alive for the life of the process: Win32 holds a raw pointer to it
            static readonly WNDPROC windowProc = WindowProc;
            static readonly Dictionary<HWND, HotkeyPump> pumps = [];

            readonly Queue<(Func<bool> Work, ManualResetEventSlim Done, bool[] Result)> invokeQueue = new();
            readonly uint threadId;

            public HWND Handle { get; private set; }
            public Dictionary<int, Action> Actions { get; } = [];

            HotkeyPump(uint threadId)
            {
                this.threadId = threadId;
            }

            public static int GenerateUniqueHotkeyId()
            {
                return Interlocked.Increment(ref nextHotkeyId);
            }

            public static HotkeyPump Start()
            {
                HotkeyPump? created = null;
                using var ready = new ManualResetEventSlim();

                var thread = new Thread(() =>
                {
                    created = new HotkeyPump(PInvoke.GetCurrentThreadId());
                    created.CreateMessageWindow();
                    ready.Set();
                    created.RunMessageLoop();
                })
                {
                    IsBackground = true,
                    Name = "Hotkey message pump"
                };
                thread.Start();

                ready.Wait();
                return created!;
            }

            /// <summary>Runs work on the pump thread and waits for it.</summary>
            public bool Invoke(Func<bool> work)
            {
                if (PInvoke.GetCurrentThreadId() == threadId) return work();

                using var done = new ManualResetEventSlim();
                var result = new bool[1];

                lock (invokeQueue)
                {
                    invokeQueue.Enqueue((work, done, result));
                }
                PInvoke.PostMessage(Handle, WM_INVOKE, default, default);

                done.Wait();
                return result[0];
            }

            void CreateMessageWindow()
            {
                fixed (char* className = "SimpleKVMHotkeyWindow")
                {
                    var wndClass = new WNDCLASSEXW
                    {
                        cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                        lpfnWndProc = windowProc,
                        hInstance = (HINSTANCE)PInvoke.GetModuleHandle((PCWSTR)null),
                        lpszClassName = className
                    };
                    PInvoke.RegisterClassEx(wndClass);   //idempotent: a second registration just fails harmlessly

                    Handle = PInvoke.CreateWindowEx(
                        0, className, className, 0,
                        0, 0, 0, 0,
                        HWND.HWND_MESSAGE, HMENU.Null, wndClass.hInstance, null);
                }

                if (Handle.IsNull) throw new Exception("Could not create the hotkey message window");

                lock (pumps)
                {
                    pumps[Handle] = this;
                }
            }

            void RunMessageLoop()
            {
                while (PInvoke.GetMessage(out MSG msg, HWND.Null, 0, 0))
                {
                    PInvoke.TranslateMessage(msg);
                    PInvoke.DispatchMessage(msg);
                }
            }

            static LRESULT WindowProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
            {
                HotkeyPump? pump;
                lock (pumps)
                {
                    pumps.TryGetValue(hwnd, out pump);
                }

                if (pump != null)
                {
                    switch (msg)
                    {
                        case PInvoke.WM_HOTKEY:
                            if (pump.Actions.TryGetValue((int)wParam.Value, out var action))
                            {
                                action();
                            }
                            return (LRESULT)0;

                        case WM_INVOKE:
                            pump.DrainInvokeQueue();
                            return (LRESULT)0;
                    }
                }

                return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
            }

            void DrainInvokeQueue()
            {
                while (true)
                {
                    (Func<bool> Work, ManualResetEventSlim Done, bool[] Result) item;
                    lock (invokeQueue)
                    {
                        if (invokeQueue.Count == 0) return;
                        item = invokeQueue.Dequeue();
                    }

                    try
                    {
                        item.Result[0] = item.Work();
                    }
                    catch
                    {
                        item.Result[0] = false;
                    }
                    finally
                    {
                        item.Done.Set();
                    }
                }
            }
        }
    }
}

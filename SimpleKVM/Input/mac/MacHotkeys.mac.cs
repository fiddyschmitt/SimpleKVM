using SimpleKVM.Platform;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SimpleKVM.Input.mac
{
    /// <summary>
    /// System-wide hotkeys via Carbon RegisterEventHotKey (no permissions required).
    /// Register must be called on a thread that pumps a CFRunLoop — the Avalonia UI
    /// thread or a CLI thread that follows up with CFRunLoopRun.
    /// </summary>
    [SupportedOSPlatform("macos")]
    public class MacHotkeys : IHotkeyBackend
    {
        const string Carbon = "/System/Library/Frameworks/Carbon.framework/Carbon";

        [StructLayout(LayoutKind.Sequential)]
        struct EventHotKeyID
        {
            public uint signature;
            public uint id;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct EventTypeSpec
        {
            public uint eventClass;
            public uint eventKind;
        }

        delegate int EventHandlerProc(IntPtr nextHandler, IntPtr theEvent, IntPtr userData);

        [DllImport(Carbon)]
        static extern int RegisterEventHotKey(uint keyCode, uint modifiers, EventHotKeyID hotKeyId, IntPtr eventTarget, uint options, out IntPtr hotKeyRef);

        [DllImport(Carbon)]
        static extern int UnregisterEventHotKey(IntPtr hotKeyRef);

        [DllImport(Carbon)]
        static extern IntPtr GetApplicationEventTarget();

        [DllImport(Carbon)]
        static extern int InstallEventHandler(IntPtr target, EventHandlerProc handler, uint numTypes, EventTypeSpec[] typeList, IntPtr userData, out IntPtr handlerRef);

        [DllImport(Carbon)]
        static extern void RunApplicationEventLoop();

        [DllImport(Carbon)]
        static extern int GetEventParameter(IntPtr theEvent, uint name, uint desiredType, IntPtr outActualType, uint bufferSize, IntPtr outActualSize, out EventHotKeyID data);

        const uint kEventClassKeyboard = 0x6B657962;    //'keyb'
        const uint kEventHotKeyPressed = 5;
        const uint kEventParamDirectObject = 0x2D2D2D2D;    //'----'
        const uint typeEventHotKeyID = 0x686B6964;      //'hkid'
        const uint HotkeySignature = 0x534B564D;        //'SKVM'

        //Carbon modifier masks
        const uint cmdKey = 0x0100;
        const uint shiftKey = 0x0200;
        const uint optionKey = 0x0800;
        const uint controlKey = 0x1000;

        static readonly object registrationLock = new();
        static readonly Dictionary<uint, Action> actionsById = [];
        static uint nextId = 1;

        static EventHandlerProc? keepAliveHandler;
        static bool handlerInstalled;

        public IDisposable Register(HotkeyGesture gesture, Action action)
        {
            uint modifiers = 0;
            if (gesture.Win) modifiers |= cmdKey;
            if (gesture.Ctrl) modifiers |= controlKey;
            if (gesture.Alt) modifiers |= optionKey;
            if (gesture.Shift) modifiers |= shiftKey;

            if (!KeyNameToCarbonKeyCode.TryGetValue(gesture.KeyName, out uint keyCode))
                throw new Exception($"Could not map hotkey key to a macOS key code: {gesture.KeyName}");

            lock (registrationLock)
            {
                EnsureHandlerInstalled();

                var hotKeyId = new EventHotKeyID { signature = HotkeySignature, id = nextId++ };

                int status = RegisterEventHotKey(keyCode, modifiers, hotKeyId, GetApplicationEventTarget(), 0, out IntPtr hotKeyRef);
                if (status != 0) throw new Exception($"Could not register hotkey: {gesture} (status {status})");

                actionsById[hotKeyId.id] = action;

                return new Registration(hotKeyId.id, hotKeyRef);
            }
        }

        static void EnsureHandlerInstalled()
        {
            if (handlerInstalled) return;

            keepAliveHandler = HandleHotkeyEvent;
            EventTypeSpec[] eventTypes = [new() { eventClass = kEventClassKeyboard, eventKind = kEventHotKeyPressed }];

            InstallEventHandler(GetApplicationEventTarget(), keepAliveHandler, 1, eventTypes, IntPtr.Zero, out _);
            handlerInstalled = true;
        }

        /// <summary>
        /// Pumps Carbon events forever. In a plain console process CFRunLoopRun alone never
        /// dispatches hotkey events — the Carbon event queue must be run. A Cocoa/Avalonia app
        /// doesn't need this; its NSApplication run loop dispatches Carbon events itself.
        /// </summary>
        public static void RunEventLoop()
        {
            RunApplicationEventLoop();
        }

        static int HandleHotkeyEvent(IntPtr nextHandler, IntPtr theEvent, IntPtr userData)
        {
            GetEventParameter(theEvent, kEventParamDirectObject, typeEventHotKeyID, IntPtr.Zero,
                (uint)Marshal.SizeOf<EventHotKeyID>(), IntPtr.Zero, out EventHotKeyID hotKeyId);

            Action? action;
            lock (registrationLock)
            {
                actionsById.TryGetValue(hotKeyId.id, out action);
            }

            action?.Invoke();
            return 0;   //noErr
        }

        sealed class Registration(uint id, IntPtr hotKeyRef) : IDisposable
        {
            bool disposed;

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;

                lock (registrationLock)
                {
                    actionsById.Remove(id);
                }
                UnregisterEventHotKey(hotKeyRef);
            }
        }

        /// <summary>
        /// WinForms Keys enum names (the stored hotkey string contract) to Carbon virtual key codes
        /// (ANSI layout).
        /// </summary>
        static readonly Dictionary<string, uint> KeyNameToCarbonKeyCode = new(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = 0, ["S"] = 1, ["D"] = 2, ["F"] = 3, ["H"] = 4, ["G"] = 5, ["Z"] = 6, ["X"] = 7,
            ["C"] = 8, ["V"] = 9, ["B"] = 11, ["Q"] = 12, ["W"] = 13, ["E"] = 14, ["R"] = 15, ["Y"] = 16,
            ["T"] = 17, ["O"] = 31, ["U"] = 32, ["I"] = 34, ["P"] = 35, ["L"] = 37, ["J"] = 38, ["K"] = 40,
            ["N"] = 45, ["M"] = 46,

            ["D1"] = 18, ["D2"] = 19, ["D3"] = 20, ["D4"] = 21, ["D5"] = 23, ["D6"] = 22, ["D7"] = 26,
            ["D8"] = 28, ["D9"] = 25, ["D0"] = 29,

            ["F1"] = 122, ["F2"] = 120, ["F3"] = 99, ["F4"] = 118, ["F5"] = 96, ["F6"] = 97, ["F7"] = 98,
            ["F8"] = 100, ["F9"] = 101, ["F10"] = 109, ["F11"] = 103, ["F12"] = 111,

            ["NumPad0"] = 82, ["NumPad1"] = 83, ["NumPad2"] = 84, ["NumPad3"] = 85, ["NumPad4"] = 86,
            ["NumPad5"] = 87, ["NumPad6"] = 88, ["NumPad7"] = 89, ["NumPad8"] = 91, ["NumPad9"] = 92,
            ["Multiply"] = 67, ["Add"] = 69, ["Subtract"] = 78, ["Divide"] = 75, ["Decimal"] = 65,

            ["Left"] = 123, ["Right"] = 124, ["Down"] = 125, ["Up"] = 126,
            ["Home"] = 115, ["End"] = 119, ["PageUp"] = 116, ["PageDown"] = 121,
            ["Space"] = 49, ["Tab"] = 48, ["Return"] = 36, ["Enter"] = 36, ["Escape"] = 53,
            ["Back"] = 51, ["Delete"] = 117,
            ["OemMinus"] = 27, ["Oemplus"] = 24, ["Oemcomma"] = 43, ["OemPeriod"] = 47,
            ["OemQuestion"] = 44, ["OemSemicolon"] = 41, ["Oem1"] = 41, ["OemQuotes"] = 39, ["Oem7"] = 39,
            ["OemOpenBrackets"] = 33, ["OemCloseBrackets"] = 30, ["Oem5"] = 42, ["OemPipe"] = 42,
            ["Oemtilde"] = 50, ["Oem3"] = 50,
        };
    }
}

using SimpleKVM.Platform;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;

namespace SimpleKVM.Input.linux
{
    /// <summary>
    /// Reads keyboard and mouse events straight from /dev/input/event* (never grabbing them),
    /// which works the same under Wayland and X11. Serves the idle time and the hotkeys where
    /// the desktop itself offers neither. Needs read access to the event devices: add the user
    /// to the "input" group.
    /// Devices are rescanned periodically, since a USB switch unplugs and replugs the keyboard.
    /// </summary>
    [SupportedOSPlatform("linux")]
    public sealed class EvdevInput
    {
        public static readonly EvdevInput Instance = new();

        const string InputDir = "/dev/input";
        const int EventSize = 24;       //struct input_event on 64-bit: timeval(16) + type(2) + code(2) + value(4)
        const ushort EV_KEY = 1, EV_REL = 2, EV_ABS = 3;
        static readonly TimeSpan RescanInterval = TimeSpan.FromSeconds(2);

        readonly object stateLock = new();
        readonly HashSet<string> openDevices = [];
        readonly HashSet<ushort> pressedKeys = [];
        readonly List<(HotkeyGesture Gesture, ushort KeyCode, Action Action)> hotkeys = [];

        long lastInputTicks = Environment.TickCount64;
        bool started;

        EvdevInput() { }

        public int ReadableDeviceCount
        {
            get
            {
                EnsureStarted();
                lock (stateLock) return openDevices.Count;
            }
        }

        public TimeSpan IdleTime => TimeSpan.FromMilliseconds(Environment.TickCount64 - Interlocked.Read(ref lastInputTicks));

        void EnsureStarted()
        {
            lock (stateLock)
            {
                if (started) return;
                started = true;
            }

            Rescan();
            new Thread(() =>
            {
                while (true)
                {
                    Thread.Sleep(RescanInterval);
                    Rescan();
                }
            })
            {
                IsBackground = true,
                Name = "evdev rescan"
            }.Start();
        }

        void Rescan()
        {
            string[] paths;
            try
            {
                paths = Directory.GetFiles(InputDir, "event*");
            }
            catch
            {
                return;
            }

            foreach (var path in paths)
            {
                lock (stateLock)
                {
                    if (openDevices.Contains(path)) continue;
                }

                if (!EvdevDeviceFilter.IsWanted(path)) continue;

                FileStream stream;
                try
                {
                    stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1, FileOptions.None);
                }
                catch
                {
                    continue;   //no permission, or vanished
                }

                lock (stateLock) openDevices.Add(path);

                new Thread(() => ReadDevice(path, stream))
                {
                    IsBackground = true,
                    Name = $"evdev {Path.GetFileName(path)}"
                }.Start();
            }
        }

        void ReadDevice(string path, FileStream stream)
        {
            var buffer = new byte[EventSize * 64];
            try
            {
                using (stream)
                {
                    while (true)
                    {
                        int read = stream.Read(buffer, 0, buffer.Length);
                        if (read <= 0) break;

                        for (int offset = 0; offset + EventSize <= read; offset += EventSize)
                        {
                            ushort type = BitConverter.ToUInt16(buffer, offset + 16);
                            ushort code = BitConverter.ToUInt16(buffer, offset + 18);
                            int value = BitConverter.ToInt32(buffer, offset + 20);
                            HandleEvent(type, code, value);
                        }
                    }
                }
            }
            catch
            {
                //device unplugged (ENODEV)
            }
            finally
            {
                lock (stateLock)
                {
                    openDevices.Remove(path);
                    pressedKeys.Clear();    //a key held on the vanished device would otherwise stick
                }
            }
        }

        void HandleEvent(ushort type, ushort code, int value)
        {
            if (type != EV_KEY && type != EV_REL && type != EV_ABS) return;

            Interlocked.Exchange(ref lastInputTicks, Environment.TickCount64);

            if (type != EV_KEY) return;

            List<Action>? toFire = null;
            lock (stateLock)
            {
                if (value == 0)
                {
                    pressedKeys.Remove(code);
                    return;
                }

                if (value == 2) return;     //autorepeat

                pressedKeys.Add(code);
                if (LinuxKeyCodes.IsModifier(code)) return;

                bool ctrl = pressedKeys.Contains(LinuxKeyCodes.LeftCtrl) || pressedKeys.Contains(LinuxKeyCodes.RightCtrl);
                bool alt = pressedKeys.Contains(LinuxKeyCodes.LeftAlt) || pressedKeys.Contains(LinuxKeyCodes.RightAlt);
                bool shift = pressedKeys.Contains(LinuxKeyCodes.LeftShift) || pressedKeys.Contains(LinuxKeyCodes.RightShift);
                bool meta = pressedKeys.Contains(LinuxKeyCodes.LeftMeta) || pressedKeys.Contains(LinuxKeyCodes.RightMeta);

                foreach (var (gesture, keyCode, action) in hotkeys)
                {
                    if (keyCode == code && gesture.Ctrl == ctrl && gesture.Alt == alt && gesture.Shift == shift && gesture.Win == meta)
                    {
                        (toFire ??= []).Add(action);
                    }
                }
            }

            //Run off the reader thread: a monitor switch takes a few hundred ms of DDC traffic
            if (toFire != null)
            {
                foreach (var action in toFire.Distinct())
                {
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        try { action(); } catch (Exception ex) { Console.WriteLine($"Hotkey action failed: {ex.Message}"); }
                    });
                }
            }
        }

        public IDisposable AddHotkey(HotkeyGesture gesture, Action action)
        {
            if (!LinuxKeyCodes.TryGet(gesture.KeyName, out ushort keyCode))
                throw new ArgumentException($"Key '{gesture.KeyName}' is not supported for hotkeys on Linux");

            if (ReadableDeviceCount == 0)
                throw new InvalidOperationException($"Cannot read keyboard events. {EvdevHotkeys.AccessHint}");

            var entry = (gesture, keyCode, action);
            lock (stateLock) hotkeys.Add(entry);

            return new Registration(() =>
            {
                lock (stateLock) hotkeys.Remove(entry);
            });
        }

        sealed class Registration(Action onDispose) : IDisposable
        {
            int disposed;
            public void Dispose()
            {
                if (Interlocked.Exchange(ref disposed, 1) == 0) onDispose();
            }
        }
    }

    /// <summary>
    /// Which event devices are worth reading. Anything with keys, relative or absolute axes
    /// counts as user input, except accelerometers: those stream absolute events whenever the
    /// machine so much as vibrates, which would keep the idle time at zero forever.
    /// </summary>
    public static class EvdevDeviceFilter
    {
        const int EV_KEY = 1, EV_REL = 2, EV_ABS = 3;
        const int INPUT_PROP_ACCELEROMETER = 6;

        /// <summary>Decides from the device's sysfs capability bitmasks; a device whose sysfs can't be read is kept.</summary>
        public static bool IsWanted(string eventDevicePath)
        {
            var sysfs = Path.Combine("/sys/class/input", Path.GetFileName(eventDevicePath), "device");
            var capabilities = ReadBitmask(Path.Combine(sysfs, "capabilities", "ev"));
            var properties = ReadBitmask(Path.Combine(sysfs, "properties"));
            return capabilities == null || IsWanted(capabilities.Value, properties ?? 0);
        }

        public static bool IsWanted(ulong eventCapabilities, ulong properties)
        {
            bool hasInput = (eventCapabilities & ((1UL << EV_KEY) | (1UL << EV_REL) | (1UL << EV_ABS))) != 0;
            bool accelerometer = (properties & (1UL << INPUT_PROP_ACCELEROMETER)) != 0;
            return hasInput && !accelerometer;
        }

        /// <summary>sysfs prints bitmasks as space-separated 64-bit hex words, most significant first; only the low word matters here.</summary>
        public static ulong? ParseBitmask(string text)
        {
            var words = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return null;
            return ulong.TryParse(words[^1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var low) ? low : null;
        }

        static ulong? ReadBitmask(string path)
        {
            try
            {
                return ParseBitmask(File.ReadAllText(path));
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Hotkeys read from /dev/input: what is left where the desktop offers nothing better (a
    /// Wayland desktop without the GlobalShortcuts portal). Needs the input group, and can only
    /// watch the keys, so the combination also reaches the focused application.
    /// </summary>
    [SupportedOSPlatform("linux")]
    public class EvdevHotkeys : IHotkeyBackend
    {
        public const string AccessHint = "Add your user to the 'input' group (sudo usermod -aG input $USER) and log in again.";

        public IDisposable Register(HotkeyGesture gesture, Action action)
        {
            return EvdevInput.Instance.AddHotkey(gesture, action);
        }

        public string? Note => EvdevInput.Instance.ReadableDeviceCount == 0
            ? $"Hotkeys on this desktop are read from /dev/input, which your user can't open. {AccessHint}"
            : "Hotkeys on this desktop are read from /dev/input, so the key combination also reaches the application in front.";
    }
}

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
    /// which works the same under Wayland and X11. Serves both the global hotkeys and the idle
    /// time. Needs read access to the event devices: add the user to the "input" group.
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
                throw new InvalidOperationException("Cannot read keyboard events. Add your user to the 'input' group (sudo usermod -aG input $USER) and log in again.");

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

    [SupportedOSPlatform("linux")]
    public class LinuxHotkeys : IHotkeyBackend
    {
        public IDisposable Register(HotkeyGesture gesture, Action action)
        {
            return EvdevInput.Instance.AddHotkey(gesture, action);
        }
    }

    /// <summary>Linux input-event-codes.h key codes for the .NET Keys names that rules.json stores.</summary>
    public static class LinuxKeyCodes
    {
        public const ushort LeftCtrl = 29, RightCtrl = 97, LeftShift = 42, RightShift = 54,
                            LeftAlt = 56, RightAlt = 100, LeftMeta = 125, RightMeta = 126;

        public static bool IsModifier(ushort code) =>
            code is LeftCtrl or RightCtrl or LeftShift or RightShift or LeftAlt or RightAlt or LeftMeta or RightMeta;

        public static bool TryGet(string keyName, out ushort code) => map.TryGetValue(keyName, out code);

        static readonly Dictionary<string, ushort> map = BuildMap();

        static Dictionary<string, ushort> BuildMap()
        {
            var m = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase)
            {
                ["A"] = 30, ["B"] = 48, ["C"] = 46, ["D"] = 32, ["E"] = 18, ["F"] = 33, ["G"] = 34, ["H"] = 35,
                ["I"] = 23, ["J"] = 36, ["K"] = 37, ["L"] = 38, ["M"] = 50, ["N"] = 49, ["O"] = 24, ["P"] = 25,
                ["Q"] = 16, ["R"] = 19, ["S"] = 31, ["T"] = 20, ["U"] = 22, ["V"] = 47, ["W"] = 17, ["X"] = 45,
                ["Y"] = 21, ["Z"] = 44,

                ["D1"] = 2, ["D2"] = 3, ["D3"] = 4, ["D4"] = 5, ["D5"] = 6, ["D6"] = 7, ["D7"] = 8,
                ["D8"] = 9, ["D9"] = 10, ["D0"] = 11,

                ["F11"] = 87, ["F12"] = 88,

                ["NumPad0"] = 82, ["NumPad1"] = 79, ["NumPad2"] = 80, ["NumPad3"] = 81, ["NumPad4"] = 75,
                ["NumPad5"] = 76, ["NumPad6"] = 77, ["NumPad7"] = 71, ["NumPad8"] = 72, ["NumPad9"] = 73,
                ["Multiply"] = 55, ["Add"] = 78, ["Subtract"] = 74, ["Divide"] = 98, ["Decimal"] = 83,

                ["Left"] = 105, ["Right"] = 106, ["Down"] = 108, ["Up"] = 103,
                ["Home"] = 102, ["End"] = 107, ["PageUp"] = 104, ["PageDown"] = 109, ["Prior"] = 104, ["Next"] = 109,
                ["Insert"] = 110, ["Delete"] = 111, ["Pause"] = 119, ["Scroll"] = 70,
                ["Space"] = 57, ["Tab"] = 15, ["Return"] = 28, ["Enter"] = 28, ["Escape"] = 1, ["Back"] = 14,
                ["PrintScreen"] = 99, ["Snapshot"] = 99, ["CapsLock"] = 58, ["Capital"] = 58, ["NumLock"] = 69,
                ["Apps"] = 127, ["Help"] = 138, ["Sleep"] = 142,

                ["VolumeMute"] = 113, ["VolumeDown"] = 114, ["VolumeUp"] = 115,
                ["MediaNextTrack"] = 163, ["MediaPreviousTrack"] = 165, ["MediaStop"] = 166, ["MediaPlayPause"] = 164,
                ["LaunchMail"] = 155, ["BrowserHome"] = 172, ["BrowserBack"] = 158, ["BrowserForward"] = 159,
                ["BrowserRefresh"] = 173, ["BrowserStop"] = 128, ["BrowserSearch"] = 217, ["BrowserFavorites"] = 156,

                ["OemMinus"] = 12, ["Oemplus"] = 13, ["Oemcomma"] = 51, ["OemPeriod"] = 52,
                ["OemQuestion"] = 53, ["Oem2"] = 53, ["OemSemicolon"] = 39, ["Oem1"] = 39, ["OemQuotes"] = 40, ["Oem7"] = 40,
                ["OemOpenBrackets"] = 26, ["Oem4"] = 26, ["OemCloseBrackets"] = 27, ["Oem6"] = 27, ["Oem5"] = 43, ["OemPipe"] = 43,
                ["Oemtilde"] = 41, ["Oem3"] = 41, ["OemBackslash"] = 86, ["Oem102"] = 86,
            };

            for (int i = 1; i <= 10; i++) m[$"F{i}"] = (ushort)(58 + i);        //F1=59 .. F10=68
            for (int i = 13; i <= 24; i++) m[$"F{i}"] = (ushort)(183 + i - 13); //F13=183 .. F24=194

            return m;
        }
    }
}

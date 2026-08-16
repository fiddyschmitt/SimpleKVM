using SimpleKVM.Displays;
using SimpleKVM.Input;
using SimpleKVM.Platform;
using SimpleKVM.Utilities;
using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace SimpleKVM.Cli
{
    /// <summary>
    /// Headless diagnostic commands, shared by all platforms. They exercise the platform
    /// backends through the same facades the GUI uses, so they double as a regression
    /// harness on Windows and as the only interface on macOS until the GUI is ported.
    /// </summary>
    public static class DiagnosticCli
    {
        public static int Run(string[] args)
        {
            try
            {
                return RunCommand(args);
            }
            catch (PlatformNotSupportedException)
            {
                Console.WriteLine("This command's platform backend is not implemented yet on this OS.");
                return 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        static int RunCommand(string[] args)
        {
            switch (args[0].ToLowerInvariant())
            {
                case "--probe-ddc":
#if !WINDOWS
                    if (OperatingSystem.IsMacOS())
                    {
                        return Displays.mac.DdcProbe.Run();
                    }
#endif
                    Console.WriteLine("--probe-ddc is only available on macOS.");
                    return 1;

                case "--list-monitors":
                    return ListMonitors();

                case "--get-source" when args.Length >= 2:
                    return GetSource(int.Parse(args[1]));

                case "--set-source" when args.Length >= 3:
                    return SetSource(int.Parse(args[1]), ParseInt(args[2]));

                case "--watch-usb":
                    return WatchUsb();

                case "--watch-idle":
                    return WatchIdle();

                case "--test-hotkey" when args.Length >= 2:
                    return TestHotkey(args[1]);

                case "--verify-rules" when args.Length >= 2:
                    return VerifyRules(args[1]);

                case "--set-startup" when args.Length >= 2:
                    return SetStartup(args[1]);

                default:
                    Console.WriteLine("""
                        SimpleKVM diagnostic commands:
                          --probe-ddc                 macOS: test DDC/CI on the attached monitor
                          --list-monitors             enumerate monitors, ids and sources
                          --get-source <n>            read monitor n's current input (1-based)
                          --set-source <n> <id>       switch monitor n to input <id> (decimal or 0xHEX)
                          --watch-usb                 print USB insert/remove events until Ctrl+C
                          --watch-idle                print system idle time every second
                          --test-hotkey "<gesture>"   register a hotkey (e.g. "Ctrl+Alt+F1") and wait
                          --verify-rules <file>       parse a rules.json and print its rules
                          --set-startup on|off|status control the run-at-startup registration
                        """);
                    return 1;
            }
        }

        static int ParseInt(string value)
        {
            return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? Convert.ToInt32(value[2..], 16)
                    : int.Parse(value);
        }

        static int ListMonitors()
        {
            var bounds = PlatformServices.Current.Displays.GetScreenBounds();
            foreach (var rect in bounds)
            {
                Console.WriteLine($"Screen bounds: {rect.Left},{rect.Top},{rect.Right},{rect.Bottom} -> id {MonitorIdentity.FromBounds(rect.Left, rect.Top, rect.Right, rect.Bottom)}");
            }

            var monitors = DisplaySystem.GetMonitors();
            Console.WriteLine($"{monitors.Count} monitor(s):");

            for (int i = 0; i < monitors.Count; i++)
            {
                var mon = monitors[i];
                Console.WriteLine($"[{i + 1}] {mon.Model} id={mon.MonitorUniqueId} lgAltMode={mon.UseLgAltMode}");
                foreach (var (sourceId, sourceName) in mon.ValidSources)
                {
                    Console.WriteLine($"      source 0x{sourceId:X2} ({sourceId}) = {sourceName}");
                }
            }

            return 0;
        }

        static int GetSource(int monitorNumber)
        {
            var monitors = DisplaySystem.GetMonitors();
            var mon = monitors[monitorNumber - 1];
            var current = mon.GetCurrentSource();
            Console.WriteLine($"Monitor [{monitorNumber}] {mon.Model}: current source = 0x{current:X2} ({current}) {VcpSourceNames.SourceIdToName(current)}");
            return current > 0 ? 0 : 1;
        }

        static int SetSource(int monitorNumber, int sourceId)
        {
            var monitors = DisplaySystem.GetMonitors();
            var mon = monitors[monitorNumber - 1];

            var current = mon.GetCurrentSource();
            if (current > 0 && current == sourceId && !Configuration.AppSettingsManager.Current.ForceInputChange)
            {
                Console.WriteLine($"Monitor [{monitorNumber}] {mon.Model} is already on source 0x{sourceId:X2}; nothing to do.");
                return 0;
            }

            Console.WriteLine($"Switching monitor [{monitorNumber}] {mon.Model} to source 0x{sourceId:X2}...");
            bool ok = mon.SetSource(sourceId);
            Console.WriteLine(ok ? "OK" : "FAILED");
            return ok ? 0 : 1;
        }

        static int WatchUsb()
        {
            var usb = USB.USBSystem.INSTANCE;
            if (usb == null)
            {
                Console.WriteLine("No USB backend on this platform.");
                return 1;
            }

            Console.WriteLine("Watching USB events (Ctrl+C to stop)...");
            usb.UsbEvent += (sender, e) =>
            {
                Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {e.UsbEvent}: {e.Device.DeviceID} [{e.Device.DeviceClass}]");
            };

            Thread.Sleep(Timeout.Infinite);
            return 0;
        }

        static int WatchIdle()
        {
            Console.WriteLine("Printing idle time every second (Ctrl+C to stop)...");
            while (true)
            {
                Console.WriteLine($"idle: {IdleUtility.GetIdleTimeSpan().TotalSeconds:F1} s");
                Thread.Sleep(1000);
            }
        }

        static int TestHotkey(string gesture)
        {
            using var registration = HotkeySystem.Register(gesture, () => Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} hotkey fired: {gesture}"));
            Console.WriteLine($"Registered {gesture}; press it now (Ctrl+C to stop)...");

            RunEventLoopForever();
            return 0;
        }

        static void RunEventLoopForever()
        {
#if WINDOWS
            System.Windows.Forms.Application.Run();
#else
            if (OperatingSystem.IsMacOS())
            {
                //A console process must pump the Carbon event queue for hotkey events to arrive
                Input.mac.MacHotkeys.RunEventLoop();
            }
            else
            {
                Thread.Sleep(Timeout.Infinite);
            }
#endif
        }

        static int SetStartup(string mode)
        {
            var startup = PlatformServices.Current.Startup;
            if (startup == null)
            {
                Console.WriteLine("Run-at-startup is not supported on this platform.");
                return 1;
            }

            switch (mode.ToLowerInvariant())
            {
                case "on": startup.SetEnabled(true); break;
                case "off": startup.SetEnabled(false); break;
                case "status": break;
                default:
                    Console.WriteLine("Expected on, off or status.");
                    return 1;
            }

            Console.WriteLine($"Run at startup: {(startup.IsEnabled() ? "enabled" : "disabled")}");
            return 0;
        }

        static int VerifyRules(string filename)
        {
            var json = File.ReadAllText(filename);
            var rules = json.DeserializJson<System.Collections.Generic.List<Rules.Rule>>() ?? [];

            Console.WriteLine($"Parsed {rules.Count} rule(s):");
            foreach (var rule in rules)
            {
                var actions = string.Join("; ", rule.Actions.Select(a => a.ToString()));
                Console.WriteLine($"- \"{rule.Name}\" [{rule.Status}] trigger: {rule.GetTriggerAsFriendlyString()}, delay {rule.DelaySeconds}s, runs {rule.RunCount}");
            }

            return 0;
        }
    }
}

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
    /// harness on both platforms and as the release script's smoke test.
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
                    if (OperatingSystem.IsMacOS())
                    {
                        return Displays.mac.DdcProbe.Run();
                    }
                    if (OperatingSystem.IsLinux())
                    {
                        return ListMonitors();
                    }
                    Console.WriteLine("--probe-ddc is only available on macOS and Linux.");
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

                case "--set-menu-entry" when args.Length >= 2:
                    return SetMenuEntry(args[1]);

                case "--quit":
                    return Quit();

                case "--get-caps" when args.Length >= 2:
                    return GetCaps(int.Parse(args[1]));

                default:
                    Console.WriteLine("""
                        SimpleKVM diagnostic commands:
                          --probe-ddc                 macOS/Linux: test DDC/CI on the attached monitor
                          --list-monitors             enumerate monitors, ids and sources
                          --get-source <n>            read monitor n's current input (1-based)
                          --set-source <n> <id>       switch monitor n to input <id> (decimal or 0xHEX)
                          --watch-usb                 print USB insert/remove events until Ctrl+C
                          --watch-idle                print system idle time every second
                          --test-hotkey "<gesture>"   register a hotkey (e.g. "Ctrl+Alt+F1") and wait
                          --verify-rules <file>       parse a rules.json and print its rules
                          --set-startup on|off|status control the run-at-startup registration
                          --set-menu-entry on|hidden|off|status
                                                      Linux: the app's entry in the applications menu
                          --quit                      ask the running SimpleKVM to exit (exit code 1: none was running)
                          --get-caps <n>              macOS/Linux: read and parse monitor n's capabilities string
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

            var displays = PlatformServices.Current.Displays;
            if (displays.LayoutSource != null) Console.WriteLine($"Layout source: {displays.LayoutSource}");
            if (displays.StatusMessage != null) Console.WriteLine($"Note: {displays.StatusMessage.Replace("\n", "\nNote: ")}");

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
                Console.WriteLine($"USB device watching is unavailable: {USB.USBSystem.InitializationError ?? "no backend on this platform"}");
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
            bool noted = false;
            string? source = null;
            while (true)
            {
                var idle = IdleUtility.GetIdleTimeSpan();

                if (PlatformServices.Current.Idle.SourceName is string name && name != source)
                {
                    Console.WriteLine($"Idle source: {name}");
                    source = name;
                }

                Console.WriteLine($"idle: {idle.TotalSeconds:F1} s");

                if (!noted && PlatformServices.Current.Idle.StatusMessage is string note)
                {
                    Console.WriteLine($"Note: {note}");
                    noted = true;
                }

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
            if (OperatingSystem.IsMacOS())
            {
                //A console process must pump the Carbon event queue for hotkey events to arrive
                Input.mac.MacHotkeys.RunEventLoop();
            }
            else
            {
                //Windows hotkeys are pumped on their own thread; this thread only has to stay alive
                Thread.Sleep(Timeout.Infinite);
            }
        }

        static int GetCaps(int monitorNumber)
        {
            if (OperatingSystem.IsMacOS())
            {
                var monitors = DisplaySystem.GetMonitors();
                if (monitors[monitorNumber - 1] is not Displays.mac.Monitor mon || mon.Transport == null)
                {
                    Console.WriteLine("No DDC transport for that monitor.");
                    return 1;
                }

                var caps = mon.Transport.ReadCapabilitiesString(Console.WriteLine);
                if (caps == null)
                {
                    Console.WriteLine("No capabilities string.");
                    return 1;
                }

                Console.WriteLine($"Capabilities ({caps.Length} chars, last bytes {string.Join(" ", caps[^Math.Min(3, caps.Length)..].Select(c => ((int)c).ToString("X2")))}): {caps}");

                var parsed = CapabilitiesParser.Parse(caps);
                Console.WriteLine($"Parsed model: {parsed.Model ?? "(none)"}, MCCS {parsed.MccsVersion ?? "(none)"}");
                Console.WriteLine(parsed.VcpFeatures.TryGetValue(0x60, out var inputs)
                    ? $"VCP 0x60 sources: {string.Join(" ", inputs.Select(b => $"0x{b:X2}"))}"
                    : "VCP 0x60 not found in parsed features");
                return 0;
            }
            if (OperatingSystem.IsLinux())
            {
                var monitors = DisplaySystem.GetMonitors();
                if (monitors[monitorNumber - 1] is not Displays.linux.Monitor mon || mon.Transport == null)
                {
                    Console.WriteLine("No DDC transport for that monitor (check /dev/i2c-* permissions).");
                    return 1;
                }

                Console.WriteLine($"Bus: {(mon.Transport as Displays.linux.DdcTransport)?.DevicePath}");
                var caps = mon.Transport.ReadCapabilitiesString(Console.WriteLine);
                Console.WriteLine(caps == null ? "No capabilities string." : $"Capabilities: {caps}");
                return caps == null ? 1 : 0;
            }
            Console.WriteLine("--get-caps is only available on macOS and Linux (Windows reads capabilities via Dxva2).");
            return 1;
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

        /// <summary>
        /// Linux: the applications-menu entry. "hidden" keeps an entry the desktop can know the
        /// app by (name, icon, identity for the portals) without listing it; "off" removes it,
        /// which is what an uninstall wants.
        /// </summary>
        static int SetMenuEntry(string mode)
        {
            if (!OperatingSystem.IsLinux())
            {
                Console.WriteLine("The applications-menu entry is only managed by the app on Linux.");
                return 1;
            }

            switch (mode.ToLowerInvariant())
            {
                case "on": Platform.linux.LinuxMenuEntry.Write(shown: true); break;
                case "hidden": Platform.linux.LinuxMenuEntry.Write(shown: false); break;
                case "off": Platform.linux.LinuxMenuEntry.Remove(); break;
                case "status": break;
                default:
                    Console.WriteLine("Expected on, hidden, off or status.");
                    return 1;
            }

            Console.WriteLine($"Applications menu entry: {Platform.linux.LinuxMenuEntry.State.ToString().ToLowerInvariant()} ({Platform.linux.LinuxMenuEntry.DesktopFilePath})");
            return 0;
        }

        /// <summary>
        /// Asks the copy that is running to exit: the way out where there is neither a window
        /// nor a tray icon to quit from. The exit code tells a script whether there was one.
        /// </summary>
        static int Quit()
        {
            bool told = SingleInstance.Send(SingleInstance.QuitRequest);
            Console.WriteLine(told ? "Asked the running SimpleKVM to quit." : "SimpleKVM is not running.");
            return told ? 0 : 1;
        }

        static int VerifyRules(string filename)
        {
            var json = File.ReadAllText(filename);
            var rules = json.DeserializeJson<System.Collections.Generic.List<Rules.Rule>>() ?? [];

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

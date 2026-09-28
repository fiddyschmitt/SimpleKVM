using SimpleKVM.Configuration;
using SimpleKVM.Displays.I2C;
using SimpleKVM.Platform;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;

namespace SimpleKVM.Displays.linux
{
    /// <summary>
    /// Monitors on Linux: connected DRM connectors from sysfs, their desktop layout from the
    /// compositor (KDE's kscreen-doctor, or GNOME's Mutter DisplayConfig), and a DDC/CI
    /// transport on the /dev/i2c-N bus whose EDID matches the connector's.
    /// </summary>
    [SupportedOSPlatform("linux")]
    public static class DisplaySystem
    {
        /// <summary>See the macOS DisplaySystem: how often an unresponsive monitor is re-probed.</summary>
        static readonly TimeSpan ProbeRetryInterval = TimeSpan.FromSeconds(30);

        /// <summary>The layout query spawns a process, so it's reused for this long.</summary>
        static readonly TimeSpan LayoutCacheDuration = TimeSpan.FromSeconds(3);

        static readonly object cacheLock = new();
        static List<DisplayInfo>? cachedDisplays;
        static List<Monitor>? cachedMonitorList;
        static DateTime lastProbe = DateTime.MinValue;

        static readonly object layoutLock = new();
        static List<DisplayInfo>? cachedLayout;
        static DateTime lastLayoutQuery = DateTime.MinValue;

        public static IList<Monitor> GetMonitors()
        {
            lock (cacheLock)
            {
                return GetMonitorsUnsynchronized();
            }
        }

        /// <summary>Drops the cache so the next GetMonitors re-enumerates and re-probes every monitor.</summary>
        public static void InvalidateMonitors()
        {
            lock (cacheLock)
            {
                cachedDisplays = null;
                cachedMonitorList = null;
            }
            lock (layoutLock)
            {
                cachedLayout = null;
            }
        }

        public static List<ScreenRect> GetScreenBounds()
        {
            return GetLayout()
                    .Select(d => new ScreenRect(d.Left, d.Top, d.Right, d.Bottom))
                    .ToList();
        }

        static List<Monitor> GetMonitorsUnsynchronized()
        {
            var displays = GetLayout().Where(d => !d.BuiltIn).ToList();

            if (cachedDisplays == null || cachedMonitorList == null || LayoutChanged(displays))
            {
                cachedDisplays = AttachTransports(displays);
                cachedMonitorList = cachedDisplays.Select(BuildMonitor).ToList();
                lastProbe = DateTime.Now;
            }
            else if (cachedMonitorList.Any(mon => mon.ValidSources.Count == 0) && DateTime.Now - lastProbe >= ProbeRetryInterval)
            {
                //The bus may not have been matched yet either (monitor was off), so re-attach
                cachedDisplays = AttachTransports(cachedDisplays);
                var known = cachedDisplays;
                cachedMonitorList = cachedMonitorList
                                        .Select(mon => mon.ValidSources.Count > 0
                                                        ? mon
                                                        : BuildMonitor(known.First(d => d.UniqueId == mon.MonitorUniqueId)))
                                        .ToList();
                lastProbe = DateTime.Now;
            }

            return cachedMonitorList;
        }

        static bool LayoutChanged(List<DisplayInfo> displays)
        {
            var current = displays.Select(d => d.UniqueId);
            var cached = cachedDisplays!.Select(d => d.UniqueId);

            return current.Except(cached).Any() || cached.Except(current).Any();
        }

        record DisplayInfo(string Connector, string SysfsPath, byte[] Edid, bool BuiltIn, int Left, int Top, int Right, int Bottom, string UniqueId, int MonitorNumber, DdcTransport? Transport);

        record ConnectorInfo(string Name, string SysfsPath, byte[] Edid);

        record OutputGeometry(int X, int Y, int Width, int Height);

        /// <summary>
        /// Every enabled display, the built-in one included, numbered left to right then top to
        /// bottom like the other platforms so MonitorOverrides numbers stay meaningful.
        /// </summary>
        static List<DisplayInfo> GetLayout()
        {
            lock (layoutLock)
            {
                if (cachedLayout != null && DateTime.Now - lastLayoutQuery < LayoutCacheDuration)
                    return cachedLayout;

                var connectors = EnumerateConnectors();
                var geometry = QueryKScreen() ?? QueryMutter() ?? [];

                //Connectors the compositor didn't report (unknown desktop) are laid out left to right
                int nextX = geometry.Count > 0 ? geometry.Values.Max(g => g.X + g.Width) : 0;
                var placed = new List<(ConnectorInfo Connector, OutputGeometry Geometry)>();
                foreach (var connector in connectors)
                {
                    if (geometry.Count > 0)
                    {
                        //The compositor knows the layout; a connector it doesn't list is disabled
                        if (geometry.TryGetValue(connector.Name, out var g)) placed.Add((connector, g));
                        continue;
                    }

                    var (w, h) = PreferredMode(connector.SysfsPath);
                    placed.Add((connector, new OutputGeometry(nextX, 0, w, h)));
                    nextX += w;
                }

                cachedLayout = placed
                        .OrderBy(p => p.Geometry.X)
                        .ThenBy(p => p.Geometry.Y)
                        .Select((p, index) =>
                        {
                            var g = p.Geometry;
                            int right = g.X + g.Width, bottom = g.Y + g.Height;
                            return new DisplayInfo(
                                p.Connector.Name, p.Connector.SysfsPath, p.Connector.Edid,
                                BuiltIn: IsBuiltIn(p.Connector.Name),
                                g.X, g.Y, right, bottom,
                                MonitorIdentity.FromBounds(g.X, g.Y, right, bottom),
                                MonitorNumber: index + 1,
                                Transport: null);
                        })
                        .ToList();
                lastLayoutQuery = DateTime.Now;
                return cachedLayout;
            }
        }

        static bool IsBuiltIn(string connector)
        {
            return connector.StartsWith("eDP", StringComparison.OrdinalIgnoreCase)
                || connector.StartsWith("LVDS", StringComparison.OrdinalIgnoreCase)
                || connector.StartsWith("DSI", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Connected DRM connectors ("DP-1", "HDMI-A-1", ...) with their EDID.</summary>
        static List<ConnectorInfo> EnumerateConnectors()
        {
            var result = new List<ConnectorInfo>();
            const string drm = "/sys/class/drm";
            if (!Directory.Exists(drm)) return result;

            foreach (var path in Directory.GetDirectories(drm, "card*-*").OrderBy(p => p, StringComparer.Ordinal))
            {
                try
                {
                    if (File.ReadAllText(Path.Combine(path, "status")).Trim() != "connected") continue;

                    //sysfs reports size 0 for edid, so read it as a stream
                    byte[] edid;
                    using (var stream = File.OpenRead(Path.Combine(path, "edid")))
                    using (var memory = new MemoryStream())
                    {
                        stream.CopyTo(memory);
                        edid = memory.ToArray();
                    }

                    var dirName = Path.GetFileName(path);
                    var name = dirName[(dirName.IndexOf('-') + 1)..];       //card1-DP-1 -> DP-1
                    result.Add(new ConnectorInfo(name, path, edid));
                }
                catch
                {
                    //connector vanished or unreadable; skip it
                }
            }

            return result;
        }

        static (int Width, int Height) PreferredMode(string sysfsPath)
        {
            try
            {
                var first = File.ReadLines(Path.Combine(sysfsPath, "modes")).FirstOrDefault();
                var parts = first?.Split('x');
                if (parts?.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(new string(parts[1].TakeWhile(char.IsDigit).ToArray()), out int h))
                    return (w, h);
            }
            catch
            {
            }
            return (1920, 1080);
        }

        /// <summary>KDE Plasma: logical layout from kscreen-doctor -j.</summary>
        static Dictionary<string, OutputGeometry>? QueryKScreen()
        {
            var json = RunProcess("kscreen-doctor", "-j");
            if (json == null) return null;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var result = new Dictionary<string, OutputGeometry>();

                foreach (var output in doc.RootElement.GetProperty("outputs").EnumerateArray())
                {
                    if (!output.GetProperty("enabled").GetBoolean()) continue;
                    if (output.TryGetProperty("connected", out var connected) && !connected.GetBoolean()) continue;

                    var name = output.GetProperty("name").GetString();
                    if (name == null) continue;

                    var pos = output.GetProperty("pos");
                    var size = output.GetProperty("size");
                    double scale = output.TryGetProperty("scale", out var s) ? s.GetDouble() : 1.0;
                    if (scale <= 0) scale = 1.0;

                    int w = size.GetProperty("width").GetInt32();
                    int h = size.GetProperty("height").GetInt32();

                    //rotation: 1 none, 2 left, 4 inverted, 8 right
                    int rotation = output.TryGetProperty("rotation", out var r) ? r.GetInt32() : 1;
                    if (rotation == 2 || rotation == 8) (w, h) = (h, w);

                    result[name] = new OutputGeometry(
                        pos.GetProperty("x").GetInt32(),
                        pos.GetProperty("y").GetInt32(),
                        (int)Math.Round(w / scale),
                        (int)Math.Round(h / scale));
                }

                return result.Count > 0 ? result : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// GNOME: logical monitors from Mutter's DisplayConfig via gdbus. The GVariant text is
        /// parsed loosely: each logical monitor is "(x, y, scale, transform, primary, [('DP-1', ...".
        /// </summary>
        static Dictionary<string, OutputGeometry>? QueryMutter()
        {
            var text = RunProcess("gdbus", "call --session --dest org.gnome.Mutter.DisplayConfig --object-path /org/gnome/Mutter/DisplayConfig --method org.gnome.Mutter.DisplayConfig.GetCurrentState");
            if (text == null) return null;

            try
            {
                var result = new Dictionary<string, OutputGeometry>();

                //Current mode per connector: ('DP-1', 'vendor', 'product', 'serial'), [modes...] where
                //the current mode carries 'is-current': <true>
                var currentModes = new Dictionary<string, (int W, int H)>();
                var monitorRegex = new System.Text.RegularExpressions.Regex(@"\(\('([^']+)', '[^']*', '[^']*', '[^']*'\), \[(.*?)\], \{");
                var modeRegex = new System.Text.RegularExpressions.Regex(@"\('[^']*', (\d+), (\d+), [\d.]+, [\d.]+, \[[^\]]*\], \{([^}]*)\}\)");
                foreach (System.Text.RegularExpressions.Match m in monitorRegex.Matches(text))
                {
                    foreach (System.Text.RegularExpressions.Match mode in modeRegex.Matches(m.Groups[2].Value))
                    {
                        if (mode.Groups[3].Value.Contains("'is-current': <true>"))
                            currentModes[m.Groups[1].Value] = (int.Parse(mode.Groups[1].Value), int.Parse(mode.Groups[2].Value));
                    }
                }

                var logicalRegex = new System.Text.RegularExpressions.Regex(@"\((-?\d+), (-?\d+), ([\d.]+), (\d+), (?:true|false), \[\('([^']+)'");
                foreach (System.Text.RegularExpressions.Match m in logicalRegex.Matches(text))
                {
                    var name = m.Groups[5].Value;
                    if (!currentModes.TryGetValue(name, out var mode)) continue;

                    double scale = double.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
                    int transform = int.Parse(m.Groups[4].Value);
                    var (w, h) = mode;
                    if (transform % 2 == 1) (w, h) = (h, w);

                    result[name] = new OutputGeometry(
                        int.Parse(m.Groups[1].Value),
                        int.Parse(m.Groups[2].Value),
                        (int)Math.Round(w / scale),
                        (int)Math.Round(h / scale));
                }

                return result.Count > 0 ? result : null;
            }
            catch
            {
                return null;
            }
        }

        static string? RunProcess(string fileName, string arguments)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo(fileName, arguments)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                });
                if (process == null) return null;

                var output = process.StandardOutput.ReadToEndAsync();
                if (!process.WaitForExit(5000))
                {
                    try { process.Kill(); } catch { }
                    return null;
                }

                return process.ExitCode == 0 ? output.Result : null;
            }
            catch
            {
                return null;     //tool not installed
            }
        }

        /// <summary>
        /// Pairs each display with its DDC bus. Open-source drivers link the bus from the
        /// connector's sysfs node; for the others (NVIDIA's proprietary driver) every accessible
        /// display-adapter bus is asked for its EDID, which is matched against the connector's.
        /// </summary>
        static List<DisplayInfo> AttachTransports(List<DisplayInfo> displays)
        {
            Dictionary<string, string>? busByEdid = null;

            return displays
                    .Select(d =>
                    {
                        if (d.Transport != null) return d;

                        var bus = LinkedBus(d.SysfsPath);
                        if (bus == null && d.Edid.Length >= 128)
                        {
                            busByEdid ??= ScanBusesByEdid();
                            busByEdid.TryGetValue(EdidKey(d.Edid), out bus);
                        }

                        return d with { Transport = bus != null && DdcTransport.CanOpen(bus) ? new DdcTransport(bus) : null };
                    })
                    .ToList();
        }

        static string? LinkedBus(string connectorPath)
        {
            try
            {
                //i915/amdgpu/nouveau: "ddc" symlink to the i2c adapter, or an i2c-N child (DP AUX)
                var ddc = Path.Combine(connectorPath, "ddc");
                if (Directory.Exists(ddc))
                {
                    var target = new DirectoryInfo(ddc).ResolveLinkTarget(true)?.Name ?? Path.GetFileName(Path.GetFullPath(ddc));
                    if (target.StartsWith("i2c-")) return $"/dev/{target}";
                }

                var child = Directory.GetDirectories(connectorPath, "i2c-*").FirstOrDefault()
                         ?? Directory.GetDirectories(connectorPath, "drm_dp_aux*")
                                     .SelectMany(aux => Directory.GetDirectories(aux, "i2c-*"))
                                     .FirstOrDefault();
                if (child != null) return $"/dev/{Path.GetFileName(child)}";
            }
            catch
            {
            }

            return null;
        }

        static Dictionary<string, string> ScanBusesByEdid()
        {
            var result = new Dictionary<string, string>();
            const string adapters = "/sys/bus/i2c/devices";
            if (!Directory.Exists(adapters)) return result;

            foreach (var adapter in Directory.GetDirectories(adapters, "i2c-*"))
            {
                try
                {
                    //Never poke SMBus controllers: address 0x50 there is RAM SPD, not a monitor
                    var name = File.ReadAllText(Path.Combine(adapter, "name")).Trim();
                    if (name.Contains("SMBus", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("PIIX4", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("i801", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("SMU", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var dev = $"/dev/{Path.GetFileName(adapter)}";
                    if (!DdcTransport.CanOpen(dev)) continue;

                    var edid = new DdcTransport(dev).ReadEdid();
                    if (edid != null) result.TryAdd(EdidKey(edid), dev);
                }
                catch
                {
                }
            }

            return result;
        }

        static string EdidKey(byte[] edid)
        {
            return Convert.ToHexString(edid, 0, 128);
        }

        static Monitor BuildMonitor(DisplayInfo display)
        {
            List<(int SourceId, string SourceName)>? sources = null;

            //First the config file, in case the user specified a custom list of sources for this monitor
            var monitorOverride = ConfigManager
                        .Current?
                        .Overrides?
                        .MonitorOverrides?
                        .FirstOrDefault(ovr => ovr.MonitorNumber == display.MonitorNumber);

            sources = monitorOverride?
                        .Sources
                        .Select(src => (src.SourceId, src.SourceName))
                        .ToList();

            if (sources != null && sources.Count == 0)
                sources = null;

            bool userSpecifiedSources = sources != null;

            //Second, the monitor's capabilities string (model + valid sources)
            var model = "Unknown";
            ushort edidManufacturer = 0;

            var edid = display.Edid.Length >= 128 ? display.Edid : display.Transport?.ReadEdid();
            if (edid != null && edid.Length >= 128)
            {
                edidManufacturer = (ushort)((edid[8] << 8) | edid[9]);
                model = EdidModelName(edid) ?? model;
            }

            Action<string>? ddcDebug = Environment.GetEnvironmentVariable("SIMPLEKVM_DDC_DEBUG") == "1"
                                        ? msg => Console.Error.WriteLine($"[caps] {msg}")
                                        : null;
            var caps = display.Transport?.ReadCapabilitiesString(ddcDebug);
            if (caps != null)
            {
                var parsed = CapabilitiesParser.Parse(caps);

                //The EDID display name (e.g. "S240HL") is usually more specific than the
                //capabilities model (often just the brand), so only fill a gap here
                if (!string.IsNullOrEmpty(parsed.Model) && model == "Unknown")
                    model = parsed.Model;

                if (sources == null && parsed.VcpFeatures.TryGetValue(0x60, out var inputSources))
                {
                    sources = inputSources
                                .Select(sourceId => ((int)sourceId, VcpSourceNames.SourceIdToName(sourceId)))
                                .ToList();
                }
            }

            if ((sources == null || sources.Count == 0) &&
                display.Transport != null && display.Transport.GetVcp(0x60, out _))
            {
                sources =
                [
                    (0x11, "HDMI 1"),
                    (0x12, "HDMI 2"),
                    (0x0F, "DisplayPort 1"),
                    (0x10, "DisplayPort 2"),
                    (0x03, "DVI 1"),
                    (0x01, "VGA 1"),
                ];
            }

            //LG monitors ignore VCP 0x60 writes; use the 0xF4 sidechannel unless overridden
            bool useLgAltMode = false;
            if (monitorOverride?.UseLgAltMode == true)
            {
                useLgAltMode = true;
            }
            else if (monitorOverride?.UseLgAltMode == null)
            {
                useLgAltMode = edidManufacturer == LgInputSources.EdidManufacturerId
                || model.Contains("LG", StringComparison.OrdinalIgnoreCase);
            }

            if (useLgAltMode && !userSpecifiedSources)
            {
                sources = LgInputSources.GetDefaultSources();
            }

            sources ??= [];

            var newMonitor = new Monitor(display.UniqueId, model, sources)
            {
                UseLgAltMode = useLgAltMode,
                Transport = display.Transport
            };

            return newMonitor;
        }

        static string? EdidModelName(byte[] edid)
        {
            //Display name lives in an 18-byte descriptor block tagged 0xFC
            foreach (int offset in new[] { 54, 72, 90, 108 })
            {
                if (offset + 18 > edid.Length) break;
                if (edid[offset] != 0 || edid[offset + 1] != 0 || edid[offset + 3] != 0xFC) continue;

                var name = Encoding.ASCII.GetString(edid, offset + 5, 13);
                int newline = name.IndexOf('\n');
                if (newline >= 0) name = name[..newline];
                name = name.Trim();

                return name.Length > 0 ? name : null;
            }

            return null;
        }

        public static Dictionary<string, int> GetCurrentSources()
        {
            var result = new Dictionary<string, int>();

            foreach (var mon in GetMonitors())
            {
                if (mon.UseLgAltMode) continue;     //the sidechannel input can't be read back

                var current = mon.GetCurrentSource();
                if (current > 0)
                {
                    result[mon.MonitorUniqueId] = current;
                }
            }

            return result;
        }
    }
}

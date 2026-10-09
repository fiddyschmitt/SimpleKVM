using SimpleKVM.Configuration;
using SimpleKVM.Displays.Ddc;
using SimpleKVM.Displays.I2C;
using SimpleKVM.Platform;
using SimpleKVM.Platform.linux;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;

namespace SimpleKVM.Displays.linux
{
    /// <summary>
    /// Monitors on Linux: the connected DRM connectors from sysfs, where the desktop puts each
    /// of them (asked of the desktop: GNOME's Mutter over D-Bus, KDE's kscreen-doctor, or
    /// xrandr on X11), and a DDC/CI transport on the /dev/i2c-N bus that belongs to each connector.
    /// </summary>
    [SupportedOSPlatform("linux")]
    public static class DisplaySystem
    {
        /// <summary>See the macOS DisplaySystem: how often an unresponsive monitor is re-probed.</summary>
        static readonly TimeSpan ProbeRetryInterval = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Asking KDE or X11 starts a process, so between checks of the (cheap) connector set
        /// the last answer is reused for this long. A connector appearing or vanishing re-asks
        /// at once. GNOME announces its layout changes, so there the answer is kept until it does.
        /// </summary>
        static readonly TimeSpan LayoutRefreshInterval = TimeSpan.FromSeconds(10);

        static readonly object cacheLock = new();
        static List<DisplayInfo>? cachedDisplays;
        static List<Monitor>? cachedMonitorList;
        static DateTime lastProbe = DateTime.MinValue;

        static readonly object layoutLock = new();
        static List<DisplayInfo>? lastGoodLayout;
        static string? lastGoodSignature;
        static DateTime lastLayoutQuery = DateTime.MinValue;
        static int lastMutterChange;

        static readonly object busScanLock = new();
        static Dictionary<string, string?>? scannedBusByEdid;   //null value: two buses answered with this EDID
        static string? scannedSignature;

        /// <summary>How the current layout was determined: "mutter", "kscreen", "xrandr", "guess" or "none".</summary>
        public static string LayoutSource { get; private set; } = "none";

        /// <summary>Something the user should know about DDC access, or null when all is well.</summary>
        public static string? StatusMessage { get; private set; }

        public static IList<Monitor> GetMonitors()
        {
            lock (cacheLock)
            {
                return GetMonitorsUnsynchronized();
            }
        }

        /// <summary>Drops every cache so the next GetMonitors re-asks the desktop and re-probes every monitor.</summary>
        public static void InvalidateMonitors()
        {
            lock (cacheLock)
            {
                cachedDisplays = null;
                cachedMonitorList = null;
            }
            lock (layoutLock)
            {
                lastGoodLayout = null;
                lastGoodSignature = null;
            }
            lock (busScanLock)
            {
                scannedBusByEdid = null;
                scannedSignature = null;
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
                //A monitor that was off may have a bus now; re-attach the unmatched ones and re-probe the sourceless ones
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

        record DisplayInfo(ConnectorInfo Connector, bool BuiltIn, int Left, int Top, int Right, int Bottom, string UniqueId, int MonitorNumber, DdcTransport? Transport)
        {
            public string SysfsPath => Connector.SysfsPath;
            public byte[] Edid => Connector.Edid;
        }

        // ------------------------------------------------------------------ layout

        /// <summary>
        /// Every enabled display, the built-in one included, numbered left to right then top to
        /// bottom like the other platforms so MonitorOverrides numbers stay meaningful. A
        /// compositor answer that pairs with no connector, or no answer at all, keeps the last
        /// good layout rather than re-keying every monitor; only when there has never been one
        /// (or the connectors themselves changed) are they laid out left to right as a guess.
        /// </summary>
        static List<DisplayInfo> GetLayout()
        {
            lock (layoutLock)
            {
                var connectors = EnumerateConnectors();
                var signature = string.Join("|", connectors.Select(c => c.Name + ":" + Edid.Key(c.Edid)));
                bool connectorsChanged = signature != lastGoodSignature;

                if (lastGoodLayout != null && !connectorsChanged && !LayoutMayHaveChanged())
                    return lastGoodLayout;

                int mutterChange = MutterDisplayConfig.ChangeCount;
                var (outputs, source) = QueryCompositor();
                lastLayoutQuery = DateTime.Now;
                lastMutterChange = mutterChange;

                var pairs = outputs != null ? LayoutJoin.Match(connectors, outputs) : null;
                if (pairs == null)
                {
                    if (lastGoodLayout != null && !connectorsChanged) return lastGoodLayout;

                    pairs = GuessLayout(connectors);
                    source = "guess";
                }

                lastGoodLayout = Number(pairs);
                lastGoodSignature = signature;
                LayoutSource = source;
                return lastGoodLayout;
            }
        }

        /// <summary>Whether the desktop has to be asked again although the connectors are the same.</summary>
        static bool LayoutMayHaveChanged()
        {
            //Mutter says so itself; the others are simply asked again every so often
            if (LayoutSource == "mutter" && MutterDisplayConfig.ListeningForChanges)
                return MutterDisplayConfig.ChangeCount != lastMutterChange;

            return DateTime.Now - lastLayoutQuery >= LayoutRefreshInterval;
        }

        static (List<OutputGeometry>? Outputs, string Source) QueryCompositor()
        {
            var desktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "";
            bool x11 = LinuxSession.IsX11;

            //X11: xrandr knows the real geometry and each output's EDID whatever the desktop.
            //Wayland: the compositor is the only one who knows; try the likely one first.
            Func<(List<OutputGeometry>?, string)>[] attempts =
                x11 ? [Xrandr, KScreen, Mutter]
                : desktop.Contains("KDE", StringComparison.OrdinalIgnoreCase) ? [KScreen, Mutter, Xrandr]
                : [Mutter, KScreen, Xrandr];

            foreach (var attempt in attempts)
            {
                var (outputs, source) = attempt();
                if (outputs != null) return (outputs, source);
            }

            return (null, "none");
        }

        static (List<OutputGeometry>?, string) Mutter()
        {
            return (MutterDisplayConfig.GetLayout(), "mutter");
        }

        static (List<OutputGeometry>?, string) KScreen()
        {
            var result = ExternalTool.Run("kscreen-doctor", "-j");
            return (result?.Succeeded == true ? KScreenLayout.Parse(result.StandardOutput) : null, "kscreen");
        }

        static (List<OutputGeometry>?, string) Xrandr()
        {
            var result = ExternalTool.Run("xrandr", "--verbose");
            return (result?.Succeeded == true ? XrandrLayout.Parse(result.StandardOutput) : null, "xrandr");
        }

        /// <summary>No compositor answer: each connector at its preferred mode, left to right.</summary>
        static List<LayoutJoin.Pair> GuessLayout(List<ConnectorInfo> connectors)
        {
            var pairs = new List<LayoutJoin.Pair>();
            int nextX = 0;
            foreach (var connector in connectors)
            {
                var (w, h) = PreferredMode(connector.SysfsPath);
                pairs.Add(new LayoutJoin.Pair(connector, new OutputGeometry(connector.Name, nextX, 0, w, h)));
                nextX += w;
            }
            return pairs;
        }

        static List<DisplayInfo> Number(List<LayoutJoin.Pair> pairs)
        {
            return pairs
                    .OrderBy(p => p.Output.X)
                    .ThenBy(p => p.Output.Y)
                    .Select((p, index) => new DisplayInfo(
                        p.Connector,
                        BuiltIn: IsBuiltIn(p.Connector.Name),
                        p.Output.X, p.Output.Y, p.Output.Right, p.Output.Bottom,
                        MonitorIdentity.FromBounds(p.Output.X, p.Output.Y, p.Output.Right, p.Output.Bottom),
                        MonitorNumber: index + 1,
                        Transport: null))
                    .ToList();
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

        // ------------------------------------------------------------------ DDC buses

        /// <summary>
        /// Pairs each display with its DDC bus. Open-source drivers link the bus from the
        /// connector's sysfs node; for the others (NVIDIA's proprietary driver) the buses of
        /// display adapters are asked for their EDID, which is matched against the connector's.
        /// Two buses answering with the same EDID can't be told apart, so neither is used:
        /// driving the wrong monitor is worse than driving none.
        /// </summary>
        static List<DisplayInfo> AttachTransports(List<DisplayInfo> displays)
        {
            var notes = new List<string>();
            var buses = I2cBuses();
            if (buses.Count == 0)
            {
                notes.Add("No I2C buses (/dev/i2c-*): is the i2c-dev kernel module loaded?");
            }
            else if (!buses.Any(DdcTransport.CanOpen))
            {
                notes.Add("No access to /dev/i2c-*: add your user to the i2c group, or install ddcutil's udev rules (see the README's Linux notes).");
            }

            Dictionary<string, string?>? busByEdid = null;
            var result = displays
                    .Select(d =>
                    {
                        if (d.Transport != null) return d;

                        var bus = LinkedBus(d.SysfsPath);
                        if (bus == null && Edid.IsValid(d.Edid))
                        {
                            busByEdid ??= ScanDisplayBusesByEdid(displays);
                            if (busByEdid.TryGetValue(Edid.Key(d.Edid), out var candidate))
                            {
                                if (candidate == null) notes.Add($"{d.Connector.Name}: two buses answer with the same EDID, so DDC is left off for it.");
                                bus = candidate;
                            }
                        }

                        return d with { Transport = bus != null && DdcTransport.CanOpen(bus) ? new DdcTransport(bus) : null };
                    })
                    .ToList();

            StatusMessage = notes.Count > 0 ? string.Join("\n", notes.Distinct()) : null;
            return result;
        }

        static List<string> I2cBuses()
        {
            try
            {
                return Directory.GetFiles("/dev", "i2c-*").OrderBy(p => p, StringComparer.Ordinal).ToList();
            }
            catch
            {
                return [];
            }
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

        /// <summary>
        /// EDID (base block hex) to bus for every accessible bus of a display adapter. Cached
        /// per set of connectors: a monitor that never matches must not have every bus
        /// re-read on each re-probe.
        /// </summary>
        static Dictionary<string, string?> ScanDisplayBusesByEdid(List<DisplayInfo> displays)
        {
            var signature = string.Join("|", displays.Select(d => d.Connector.Name + ":" + Edid.Key(d.Edid)));

            lock (busScanLock)
            {
                if (scannedBusByEdid != null && scannedSignature == signature) return scannedBusByEdid;

                var result = new Dictionary<string, string?>();
                const string adapters = "/sys/bus/i2c/devices";
                if (Directory.Exists(adapters))
                {
                    foreach (var adapter in Directory.GetDirectories(adapters, "i2c-*"))
                    {
                        try
                        {
                            if (!BelongsToDisplayAdapter(adapter)) continue;

                            var dev = $"/dev/{Path.GetFileName(adapter)}";
                            if (!DdcTransport.CanOpen(dev)) continue;

                            var edid = new DdcTransport(dev).ReadEdid();
                            if (edid == null) continue;

                            var key = Edid.Key(edid);
                            result[key] = result.ContainsKey(key) ? null : dev;    //seen twice: ambiguous
                        }
                        catch
                        {
                        }
                    }
                }

                scannedBusByEdid = result;
                scannedSignature = signature;
                return result;
            }
        }

        /// <summary>
        /// ddcutil's rule: only buses whose parent device is a PCI display controller (class
        /// 0x03xxxx) carry monitors. SMBus controllers (RAM SPD lives at the same 0x50 address),
        /// touchpad and sensor buses are never probed.
        /// </summary>
        static bool BelongsToDisplayAdapter(string adapterPath)
        {
            try
            {
                var dir = new DirectoryInfo(adapterPath).ResolveLinkTarget(true) as DirectoryInfo ?? new DirectoryInfo(adapterPath);
                for (int depth = 0; depth < 5 && dir != null; depth++, dir = dir.Parent)
                {
                    var classFile = Path.Combine(dir.FullName, "class");
                    if (File.Exists(classFile))
                    {
                        return File.ReadAllText(classFile).Trim().StartsWith("0x03", StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        // ------------------------------------------------------------------ monitors

        static Monitor BuildMonitor(DisplayInfo display)
        {
            //The EDID from sysfs saves a read over the bus; the builder falls back to the bus if it's junk
            var probe = DdcMonitorBuilder.Run(display.MonitorNumber, display.Edid, display.Transport);

            return new Monitor(display.UniqueId, probe.Model, probe.Sources)
            {
                UseLgAltMode = probe.UseLgAltMode,
                Transport = display.Transport
            };
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

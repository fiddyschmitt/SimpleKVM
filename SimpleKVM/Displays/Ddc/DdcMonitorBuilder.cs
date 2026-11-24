using SimpleKVM.Configuration;
using SimpleKVM.Displays.I2C;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleKVM.Displays.Ddc
{
    /// <summary>
    /// Works out what a DDC monitor is and which inputs it has, from the config file's override
    /// for its monitor number, its EDID, its capabilities string, and failing those a probe of
    /// VCP 0x60 with a default input list. Also decides whether it is an LG that needs the 0xF4
    /// sidechannel. Shared by the macOS and Linux backends; Windows keeps its Dxva2 path.
    /// </summary>
    public static class DdcMonitorBuilder
    {
        public sealed record Probe(string Model, List<(int SourceId, string SourceName)> Sources, bool UseLgAltMode);

        /// <summary>
        /// The default input list offered when a monitor answers VCP 0x60 reads but publishes no
        /// capabilities string to learn its real inputs from.
        /// </summary>
        public static readonly List<(int SourceId, string SourceName)> DefaultSources =
        [
            (0x11, "HDMI 1"),
            (0x12, "HDMI 2"),
            (0x0F, "DisplayPort 1"),
            (0x10, "DisplayPort 2"),
            (0x03, "DVI 1"),
            (0x01, "VGA 1"),
        ];

        /// <param name="monitorNumber">The 1-based number the layout view shows, which MonitorOverrides in config.json are keyed by.</param>
        /// <param name="edid">The EDID already known for the monitor (from the OS), or null to read it over the transport.</param>
        public static Probe Run(int monitorNumber, byte[]? edid, IDdcTransport? transport)
        {
            //First the config file, in case the user specified a custom list of sources for this monitor
            var monitorOverride = ConfigManager
                        .Current?
                        .Overrides?
                        .MonitorOverrides?
                        .FirstOrDefault(ovr => ovr.MonitorNumber == monitorNumber);

            List<(int SourceId, string SourceName)>? sources = monitorOverride?
                        .Sources
                        .Select(src => (src.SourceId, src.SourceName))
                        .ToList();

            if (sources != null && sources.Count == 0)
                sources = null;

            bool userSpecifiedSources = sources != null;

            //Second, the EDID for the model name and manufacturer
            var model = "Unknown";
            ushort edidManufacturer = 0;

            if (!Edid.IsValid(edid)) edid = transport?.ReadEdid();
            if (Edid.IsValid(edid))
            {
                edidManufacturer = Edid.ManufacturerId(edid!);
                model = Edid.ModelName(edid!) ?? model;
            }

            //Third, the monitor's capabilities string (model + valid sources)
            Action<string>? ddcDebug = Environment.GetEnvironmentVariable("SIMPLEKVM_DDC_DEBUG") == "1"
                                        ? msg => Console.Error.WriteLine($"[caps] {msg}")
                                        : null;
            var caps = transport?.ReadCapabilitiesString(ddcDebug);
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

            if ((sources == null || sources.Count == 0) && transport != null && transport.GetVcp(0x60, out _))
            {
                sources = DefaultSources.ToList();
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

            //The sidechannel has its own source id namespace, so sources derived from the
            //capabilities string don't apply to it
            if (useLgAltMode && !userSpecifiedSources)
            {
                sources = LgInputSources.GetDefaultSources();
            }

            return new Probe(model, sources ?? [], useLgAltMode);
        }
    }
}

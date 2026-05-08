using System.Collections.Generic;
using System.Linq;

namespace SimpleKVM.Displays.win.I2C
{
    public static class I2CTransportManager
    {
        static readonly object _lock = new();
        static bool _initialized;
        static readonly List<II2CTransport> _transports = [];
        static readonly Dictionary<string, (II2CTransport Transport, object DisplayHandle)> _displayMap = [];

        public static void Initialize()
        {
            lock (_lock)
            {
                if (_initialized) return;
                _initialized = true;

                var amd = new AmdAdlTransport();
                if (amd.IsAvailable) _transports.Add(amd);

                var nv = new NvApiTransport();
                if (nv.IsAvailable) _transports.Add(nv);
            }
        }

        public static void BuildDisplayMap(List<EdidDisplayInfo> windowsDisplays)
        {
            lock (_lock)
            {
                _displayMap.Clear();

                var vendorDisplays = new List<(II2CTransport Transport, I2CDisplayInfo Info)>();
                foreach (var transport in _transports)
                {
                    foreach (var display in transport.EnumerateDisplays())
                    {
                        vendorDisplays.Add((transport, display));
                    }
                }

                var unmatched = vendorDisplays.ToList();

                foreach (var winDisplay in windowsDisplays)
                {
                    // Layer 1: manufacturer + product + serial
                    var candidates = unmatched.Where(v =>
                        v.Info.EdidManufacturerId == winDisplay.EdidManufacturerId &&
                        v.Info.EdidProductCode == winDisplay.EdidProductCode &&
                        v.Info.EdidSerial != 0 &&
                        v.Info.EdidSerial == GetEdidSerial(winDisplay)).ToList();

                    if (candidates.Count == 1)
                    {
                        Assign(winDisplay.UniqueId, candidates[0]);
                        unmatched.Remove(candidates[0]);
                        continue;
                    }

                    // Layer 2: manufacturer + product + connector type
                    candidates = unmatched.Where(v =>
                        v.Info.EdidManufacturerId == winDisplay.EdidManufacturerId &&
                        v.Info.EdidProductCode == winDisplay.EdidProductCode &&
                        v.Info.ConnectorType == winDisplay.ConnectorType &&
                        v.Info.ConnectorType != ConnectorType.Unknown).ToList();

                    if (candidates.Count == 1)
                    {
                        Assign(winDisplay.UniqueId, candidates[0]);
                        unmatched.Remove(candidates[0]);
                        continue;
                    }

                    // Layer 3: manufacturer + product, sequential fallback
                    candidates = unmatched.Where(v =>
                        v.Info.EdidManufacturerId == winDisplay.EdidManufacturerId &&
                        v.Info.EdidProductCode == winDisplay.EdidProductCode).ToList();

                    if (candidates.Count > 0)
                    {
                        Assign(winDisplay.UniqueId, candidates[0]);
                        unmatched.Remove(candidates[0]);
                    }
                }
            }
        }

        static void Assign(string uniqueId, (II2CTransport Transport, I2CDisplayInfo Info) match)
        {
            _displayMap[uniqueId] = (match.Transport, match.Info.VendorDisplayHandle);
        }

        static uint GetEdidSerial(EdidDisplayInfo _) => 0;

        public static (II2CTransport Transport, object DisplayHandle)? GetTransportForDisplay(string uniqueId)
        {
            lock (_lock)
            {
                if (_displayMap.TryGetValue(uniqueId, out var entry))
                    return entry;
                return null;
            }
        }
    }
}

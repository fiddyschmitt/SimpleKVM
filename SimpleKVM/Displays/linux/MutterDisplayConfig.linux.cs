using SimpleKVM.Platform.linux;
using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading;
using Tmds.DBus.Protocol;

namespace SimpleKVM.Displays.linux
{
    /// <summary>
    /// GNOME's display configuration, asked of Mutter over the session bus:
    /// <c>org.gnome.Mutter.DisplayConfig.GetCurrentState</c>, whose reply has the signature
    /// <c>(ua((ssss)a(siiddada{sv})a{sv})a(iiduba(ssss)a{sv})a{sv})</c>: a serial, the monitors
    /// (spec, modes, properties), the logical monitors (x, y, scale, transform, primary, the
    /// specs of the monitors showing it, properties) and the state's own properties. Mutter
    /// also says when the configuration changes, so the layout needn't be re-asked on a timer.
    /// </summary>
    [SupportedOSPlatform("linux")]
    public static class MutterDisplayConfig
    {
        const string Service = "org.gnome.Mutter.DisplayConfig";
        const string ObjectPath = "/org/gnome/Mutter/DisplayConfig";
        const string Interface = "org.gnome.Mutter.DisplayConfig";

        static readonly object sync = new();
        static IDisposable? changeSubscription;
        static int changes;

        /// <summary>
        /// True once Mutter's MonitorsChanged signal is being listened for: from then on
        /// <see cref="ChangeCount"/> moving is the only reason to ask for the layout again.
        /// </summary>
        public static bool ListeningForChanges
        {
            get { lock (sync) return changeSubscription != null; }
        }

        /// <summary>How many times Mutter has announced a changed monitor configuration.</summary>
        public static int ChangeCount => Volatile.Read(ref changes);

        /// <summary>The current layout, or null when this isn't GNOME or Mutter didn't answer.</summary>
        public static List<OutputGeometry>? GetLayout()
        {
            var bus = SessionBus.Connection;
            if (bus == null) return null;

            var layout = SessionBus.Wait(() => SessionBus.CallAsync(bus, Service, ObjectPath, Interface, "GetCurrentState", ReadState), out _);
            if (layout != null) ListenForChanges(bus);

            return layout;
        }

        static void ListenForChanges(DBusConnection bus)
        {
            lock (sync)
            {
                if (changeSubscription != null) return;

                changeSubscription = SessionBus.Wait(async () => await bus.WatchSignalAsync(
                    Service, ObjectPath, Interface, "MonitorsChanged",
                    (Notification notification) =>
                    {
                        //Completions (the connection closing) carry no change; a handler must not throw
                        if (!notification.IsCompletion) Interlocked.Increment(ref changes);
                    },
                    ObserverFlags.None, emitOnCapturedContext: false, state: null).ConfigureAwait(false), out _);
            }
        }

        static List<OutputGeometry>? ReadState(Message message, object? state)
        {
            var reader = message.GetBodyReader();
            reader.ReadUInt32();    //serial

            var monitors = new List<MutterMonitor>();
            var monitorsEnd = reader.ReadArrayStart(DBusType.Struct);
            while (reader.HasNext(monitorsEnd))
            {
                reader.AlignStruct();
                var (connector, vendor, product, serial) = ReadSpec(ref reader);

                (int, int)? currentMode = null;
                var modesEnd = reader.ReadArrayStart(DBusType.Struct);
                while (reader.HasNext(modesEnd))
                {
                    reader.AlignStruct();
                    reader.ReadString();            //mode id
                    int width = reader.ReadInt32();
                    int height = reader.ReadInt32();
                    reader.ReadDouble();            //refresh rate
                    reader.ReadDouble();            //preferred scale
                    reader.ReadArrayOfDouble();     //supported scales
                    var modeProperties = reader.ReadDictionaryOfStringToVariantValue();

                    if (modeProperties.TryGetValue("is-current", out var isCurrent) && isCurrent.Type == VariantValueType.Bool && isCurrent.GetBool())
                    {
                        currentMode = (width, height);
                    }
                }

                reader.ReadDictionaryOfStringToVariantValue();  //monitor properties

                monitors.Add(new MutterMonitor(connector, MutterLayout.Known(vendor), MutterLayout.Known(product), MutterLayout.Known(serial), currentMode));
            }

            var logicalMonitors = new List<MutterLogicalMonitor>();
            var logicalEnd = reader.ReadArrayStart(DBusType.Struct);
            while (reader.HasNext(logicalEnd))
            {
                reader.AlignStruct();
                int x = reader.ReadInt32();
                int y = reader.ReadInt32();
                double scale = reader.ReadDouble();
                uint transform = reader.ReadUInt32();
                reader.ReadBool();                  //primary

                var connectors = new List<string>();
                var specsEnd = reader.ReadArrayStart(DBusType.Struct);
                while (reader.HasNext(specsEnd))
                {
                    connectors.Add(ReadSpec(ref reader).Connector);
                }

                reader.ReadDictionaryOfStringToVariantValue();  //logical monitor properties

                logicalMonitors.Add(new MutterLogicalMonitor(x, y, scale, transform, connectors));
            }

            var properties = reader.ReadDictionaryOfStringToVariantValue();
            bool physicalLayout = properties.TryGetValue("layout-mode", out var mode)
                                  && mode.Type == VariantValueType.UInt32
                                  && mode.GetUInt32() == MutterLayout.PhysicalLayoutMode;

            return MutterLayout.Build(monitors, logicalMonitors, physicalLayout);
        }

        static (string Connector, string Vendor, string Product, string Serial) ReadSpec(ref Reader reader)
        {
            reader.AlignStruct();
            return (reader.ReadString(), reader.ReadString(), reader.ReadString(), reader.ReadString());
        }
    }
}

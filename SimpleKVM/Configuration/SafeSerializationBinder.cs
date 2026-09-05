using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SimpleKVM.Rules;
using SimpleKVM.Rules.Actions;
using SimpleKVM.Rules.Triggers;
using SimpleKVM.USB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleKVM.Configuration
{
    /// <summary>
    /// TypeNameHandling lets json files name arbitrary types to instantiate, which is a well-known
    /// remote-code-execution vector. This binder only resolves $type entries to the handful of
    /// types rules.json actually persists (and collections of them).
    /// </summary>
    public sealed class SafeSerializationBinder : ISerializationBinder
    {
        public static readonly SafeSerializationBinder Instance = new();

        static readonly DefaultSerializationBinder defaultBinder = new();

        /// <summary>
        /// Every type a rules.json may name. A new trigger, action or monitor type has to be
        /// added here, or files containing it refuse to load (the binder tests pin this).
        /// </summary>
        static readonly HashSet<Type> persistedTypes =
        [
            typeof(Rule),
            typeof(Trigger),
            typeof(HotkeyTrigger),
            typeof(USBTrigger),
            typeof(NoLongerIdle),
            typeof(USBDevice),
            typeof(IAction),
            typeof(SetMonitorSourceAction),
            typeof(Displays.Monitor),
            typeof(Displays.win.Monitor),
            typeof(Displays.mac.Monitor),
        ];

        /// <summary>
        /// rules.json names the concrete per-OS Monitor type it was written with; when a file
        /// crosses platforms, translate the type to this platform's equivalent. The mapped
        /// instance is only a MonitorUniqueId carrier — actions re-resolve the live monitor.
        /// </summary>
        static readonly Dictionary<string, string> crossPlatformTypeMap =
            OperatingSystem.IsWindows()
                ? new() { ["SimpleKVM.Displays.mac.Monitor"] = "SimpleKVM.Displays.win.Monitor" }
                : new() { ["SimpleKVM.Displays.win.Monitor"] = "SimpleKVM.Displays.mac.Monitor" };

        public Type BindToType(string? assemblyName, string typeName)
        {
            if (crossPlatformTypeMap.TryGetValue(typeName, out var mappedTypeName))
            {
                typeName = mappedTypeName;
            }

            //Refuse foreign names before resolving them: resolving loads whatever assembly the file names
            if (!typeName.StartsWith("SimpleKVM.", StringComparison.Ordinal) && !typeName.StartsWith("System.", StringComparison.Ordinal))
            {
                throw Refuse(typeName);
            }

            var type = defaultBinder.BindToType(assemblyName, typeName);

            if (!IsAllowed(type))
            {
                throw Refuse(typeName);
            }

            return type;
        }

        static JsonSerializationException Refuse(string typeName)
        {
            return new JsonSerializationException($"Refusing to deserialize type: {typeName}");
        }

        public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
        {
            defaultBinder.BindToName(serializedType, out assemblyName, out typeName);
        }

        static bool IsAllowed(Type type)
        {
            if (type.IsArray) return IsAllowed(type.GetElementType()!);

            if (type.IsGenericType)
            {
                return type.Namespace?.StartsWith("System.Collections") == true
                        && type.GetGenericArguments().All(IsAllowed);
            }

            return persistedTypes.Contains(type)
                    || type.IsPrimitive
                    || type == typeof(string)
                    || type == typeof(DateTime);
        }
    }
}

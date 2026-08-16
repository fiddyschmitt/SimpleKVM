using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleKVM.Configuration
{
    /// <summary>
    /// TypeNameHandling lets json files name arbitrary types to instantiate, which is a well-known
    /// remote-code-execution vector. This binder only resolves $type entries to types from this
    /// assembly (and collections of them).
    /// </summary>
    public sealed class SafeSerializationBinder : ISerializationBinder
    {
        public static readonly SafeSerializationBinder Instance = new();

        static readonly DefaultSerializationBinder defaultBinder = new();

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

            var type = defaultBinder.BindToType(assemblyName, typeName);

            if (!IsAllowed(type))
            {
                throw new JsonSerializationException($"Refusing to deserialize type: {typeName}");
            }

            return type;
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

            return type.Assembly == typeof(SafeSerializationBinder).Assembly
                    || type.IsPrimitive
                    || type == typeof(string)
                    || type == typeof(DateTime);
        }
    }
}

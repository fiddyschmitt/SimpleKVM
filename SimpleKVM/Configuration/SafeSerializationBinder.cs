using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
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

        public Type BindToType(string? assemblyName, string typeName)
        {
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

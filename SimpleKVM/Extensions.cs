using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;

namespace SimpleKVM
{
    public static class Extensions
    {
        public static string ToPrettyFormat(this TimeSpan span)
        {
            if (span == TimeSpan.Zero) return "0 minutes";

            var sb = new StringBuilder();
            if (span.Days > 0)
                sb.AppendFormat("{0} day{1} ", span.Days, span.Days > 1 ? "s" : String.Empty);
            if (span.Hours > 0)
                sb.AppendFormat("{0} hour{1} ", span.Hours, span.Hours > 1 ? "s" : String.Empty);
            if (span.Minutes > 0)
                sb.AppendFormat("{0} minute{1} ", span.Minutes, span.Minutes > 1 ? "s" : String.Empty);

            if (string.IsNullOrEmpty(sb.ToString())) return "0 minutes";

            return sb.ToString().Trim();
        }

        public static string CreateMD5(this string input)
        {
            var inputBytes = Encoding.UTF8.GetBytes(input);
            var hashBytes = System.Security.Cryptography.MD5.HashData(inputBytes);

            return Convert.ToHexString(hashBytes);
        }

        /// <summary>The next value of an enum, wrapping around to the first.</summary>
        public static T Next<T>(this T src) where T : struct, Enum
        {
            T[] values = Enum.GetValues<T>();
            int next = Array.IndexOf(values, src) + 1;
            return values.Length == next ? values[0] : values[next];
        }

        public static T? DeserializJson<T>(this string json) where T : class
        {
            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                SerializationBinder = Configuration.SafeSerializationBinder.Instance
            };
            var result = JsonConvert.DeserializeObject<T>(json, settings);
            return result;
        }

        public static void WriteTextFile(string filename, string content)
        {
            if (File.Exists(filename) && File.ReadAllText(filename) == content)
            {
                //nothing's changed
                return;
            }

            File.WriteAllText(filename, content);
        }
    }
}

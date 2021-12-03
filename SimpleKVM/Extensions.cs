using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml.Serialization;
using System.IO;
using Newtonsoft.Json;
using System.Xml;

namespace SimpleKVM
{
    public static class Extensions
    {
        public static string StartAndReadStdout(this ProcessStartInfo psi)
        {
            psi.RedirectStandardOutput = true;
            psi.UseShellExecute = false;
            psi.WindowStyle = ProcessWindowStyle.Hidden;

            var process = Process.Start(psi);

            if (process == null) return string.Empty;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return output;
        }

        public static string[] SplitAndKeep(this string input, string seperator)
        {
            var result = input
                    .Split(new[] { seperator }, StringSplitOptions.None)
                    .Skip(1)
                    .Select(block => $"{seperator}{block}")
                    .ToArray();

            return result;
        }

        public static string RemoveNonPrintable(this string input)
        {
            var result = Regex.Replace(input, @"\p{C}+", string.Empty);
            return result;
        }

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

        public static ColumnHeader? GetColumnByName(this ListView listView, string columnName)
        {
            var result = listView
                            .Columns
                            .Cast<ColumnHeader>()
                            .FirstOrDefault(col => col.Text.Equals(columnName));

            return result;
        }

        public static T Next<T>(this T src) where T : struct
        {
            if (!typeof(T).IsEnum) throw new ArgumentException(String.Format("Argument {0} is not an Enum", typeof(T).FullName));

            T[] Arr = (T[])Enum.GetValues(src.GetType());
            int j = Array.IndexOf<T>(Arr, src) + 1;
            return (Arr.Length == j) ? Arr[0] : Arr[j];
        }

        /*
        public static string? SerializeToXml<T>(this T obj)
        {
            if (obj is System.Collections.IEnumerable)
            {
                throw new Exception("XML must have a single root node, so this IEnumerable object cannot be serialized.");
            }

            if (obj == null) return "";

            var json = obj.SerializeToJson();
            var xmlDoc = JsonConvert.DeserializeXmlNode(json, "root");

            if (xmlDoc == null) return "";


            StringWriter sw = new StringWriter();
            xmlDoc.Save(sw);
            var result = sw.ToString();

            return result;
        }
        */

        /*
        public static T? DeserializeXml<T>(this string xml) where T : class
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);
            doc.RemoveChild(doc.FirstChild);

            string json = JsonConvert.SerializeXmlNode(doc, Newtonsoft.Json.Formatting.Indented);

            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };
            var result = JsonConvert.DeserializeObject<T>(json, settings);
            return result;
        }
        */

        public static T? DeserializJson<T>(this string json) where T : class
        {
            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };
            var result = JsonConvert.DeserializeObject<T>(json, settings);
            return result;
        }

        public static string SerializeToJson(this object obj)
        {
            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };

            string result = JsonConvert.SerializeObject(obj, Newtonsoft.Json.Formatting.Indented, settings);
            return result;
        }

        static readonly string[] possibleSettingsFolders = new[] {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? ""),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData))
            };

        public static void WriteTextFile(string folder, string filename, string content)
        {
            var settingsFolders = possibleSettingsFolders
                                    .Select(f => Path.Combine(f, folder))
                                    .ToList();

            foreach (var f in settingsFolders)
            {
                if (!Directory.Exists(f)) Directory.CreateDirectory(f);
                string fullFilename = Path.Combine(f, folder, filename);

                try
                {
                    File.WriteAllText(fullFilename, content);
                    break;
                }
                catch 
                { 
                
                }
            }
        }

        public static string? ReadTextFile(string folder, string filename)
        {
            var settingsFolders = possibleSettingsFolders
                                    .Select(f => Path.Combine(f, folder))
                                    .ToList();

            foreach (var f in settingsFolders)
            {
                string fullFilename = Path.Combine(f, folder, filename);

                try
                {
                    var content = File.ReadAllText(fullFilename);
                    return content;
                }
                catch
                {

                }
            }

            return null;
        }

        public static string ToString(this IEnumerable<string> list, string separator)
        {
            string result = string.Join(separator, list);
            return result;
        }
    }
}

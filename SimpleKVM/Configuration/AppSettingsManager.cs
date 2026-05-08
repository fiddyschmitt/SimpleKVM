using Newtonsoft.Json;
using System;
using System.IO;

namespace SimpleKVM.Configuration
{
    public static class AppSettingsManager
    {
        static readonly string SettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        public static AppSettings Current { get; private set; } = new();

        public static void Load()
        {
            if (!File.Exists(SettingsPath)) return;

            try
            {
                var json = File.ReadAllText(SettingsPath);
                Current = JsonConvert.DeserializeObject<AppSettings>(json) ?? new();
            }
            catch
            {
                Current = new();
            }
        }

        public static void Save()
        {
            var json = JsonConvert.SerializeObject(Current, Formatting.Indented);
            File.WriteAllText(SettingsPath, json);
        }
    }
}

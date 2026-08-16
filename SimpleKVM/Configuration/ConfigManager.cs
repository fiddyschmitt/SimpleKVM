using Newtonsoft.Json;
using System.IO;

namespace SimpleKVM.Configuration
{
    public static class ConfigManager
    {
        public static Config? Current { get; private set; }

        public static void Load()
        {
            if (!File.Exists(AppPaths.ConfigFile)) return;

            try
            {
                var configText = File.ReadAllText(AppPaths.ConfigFile);
                Current = JsonConvert.DeserializeObject<Config>(configText);
            }
            catch { }
        }
    }
}

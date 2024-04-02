using BepInEx.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RemotePlugins
{
    internal class RemotePluginsConfig
    {
        public bool Debug { get; set; } = false;

        internal static RemotePluginsConfig Load()
        {
            string baseFilePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string configPath = Path.GetFullPath(Path.Combine(baseFilePath, "..", "config", "RemotePlugins.json"));
            if (!File.Exists(configPath))
            {
                // make a default config, save it, and return it
                RemotePluginsConfig config = new RemotePluginsConfig();
                string defaultConfig = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(configPath, defaultConfig);
                return config;
            }

            string configJson = File.ReadAllText(configPath);
            RemotePluginsConfig configObj = JsonConvert.DeserializeObject<RemotePluginsConfig>(configJson);
            return configObj;
        }
    }
}

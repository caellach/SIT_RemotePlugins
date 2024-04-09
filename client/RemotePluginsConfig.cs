using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

namespace RemotePlugins
{
    internal class RemotePluginsConfig
    {
        public bool Debug { get; set; } = false;
        public bool KnownFileHashesOnly { get; set; } = true; // Enforce only allowing plugins in the KnownFileHashes.json
        public List<string> AllowedFileHashes { get; set; } = new List<string>();
        public int? Version = null;
        public UnknownFileHashAction UnknownFileHashAction { get; set; } = UnknownFileHashAction.Quarantine;

        const int CurrentVersion = 1;

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
            if (configObj == null || configObj.Version == null || configObj.Version != CurrentVersion)
            {
                var newConfigObj = new RemotePluginsConfig();

                // copy over the old values
                newConfigObj.Debug = configObj?.Debug ?? newConfigObj.Debug;
                newConfigObj.KnownFileHashesOnly = configObj?.KnownFileHashesOnly ?? newConfigObj.KnownFileHashesOnly;
                newConfigObj.AllowedFileHashes = configObj?.AllowedFileHashes ?? newConfigObj.AllowedFileHashes;
                newConfigObj.UnknownFileHashAction = configObj?.UnknownFileHashAction ?? newConfigObj.UnknownFileHashAction;
                if (newConfigObj.UnknownFileHashAction == UnknownFileHashAction.Unknown)
                {
                    newConfigObj.UnknownFileHashAction = UnknownFileHashAction.Quarantine;
                }

                // update the version
                newConfigObj.Version = CurrentVersion;

                string updatedConfig = JsonConvert.SerializeObject(newConfigObj, Formatting.Indented);
                File.WriteAllText(configPath, updatedConfig);
                return newConfigObj;
            }

            return configObj;
        }
    }

    [JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
    internal enum UnknownFileHashAction
    {
        [EnumMember(Value = "UNKNOWN")]
        Unknown,
        [EnumMember(Value = "QUARANTINE")]
        Quarantine,
        [EnumMember(Value = "DELETE")]
        Delete,
        [EnumMember(Value = "WARN")]
        Warn
    }
}

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace RemotePlugins
{
    static internal class PluginFileChecker
    {
        internal class CheckedFilesStatus
        {
            // Old style readonly since we don't have C# 9
            private bool isAllFilesExistSet;
            private bool allFilesExist;
            public bool AllFilesExist
            {
                get { return allFilesExist; }
                set
                {
                    if (!isAllFilesExistSet)
                    {
                        allFilesExist = value;
                        isAllFilesExistSet = true;
                    }
                }
            }

            private bool isAllFilesMatch;
            private bool allFilesMatch;
            public bool AllFilesMatch
            {
                get { return allFilesMatch; }
                set
                {
                    if (!isAllFilesMatch)
                    {
                        allFilesMatch = value;
                        isAllFilesMatch = true;
                    }
                }
            }

            private bool isContainsSitDll;
            private bool containsSitDll;
            public bool ContainsSitDll
            {
                get { return containsSitDll; }
                set
                {
                    if (!isContainsSitDll)
                    {
                        containsSitDll = value;
                        isContainsSitDll = true;
                    }
                }
            }



            private bool isFilesCheckedSet;
            private int filesChecked;
            public int FilesChecked
            {
                get { return filesChecked; }
                set
                {
                    if (!isFilesCheckedSet)
                    {
                        filesChecked = value;
                        isFilesCheckedSet = true;
                    }
                }
            }

            private bool isBadFileMapHashFilesSet;
            private List<string> badFileMapHashFiles;
            public List<string> BadFileMapHashFiles
            {
                get { return badFileMapHashFiles; }
                set
                {
                    if (!isBadFileMapHashFilesSet)
                    {
                        badFileMapHashFiles = value;
                        isBadFileMapHashFilesSet = true;
                    }
                }
            }
            
            private bool isBadKnownHashFilesSet;
            private List<string> badKnownHashFiles;
            public List<string> BadKnownHashFiles
            {
                get { return badKnownHashFiles; }
                set
                {
                    if (!isBadKnownHashFilesSet)
                    {
                        badKnownHashFiles = value;
                        isBadKnownHashFilesSet = true;
                    }
                }
            }

            private bool isFilesNotInWhitelistSet;
            private List<string> filesNotInWhitelist;
            public List<string> FilesNotInWhitelist
            {
                get { return filesNotInWhitelist; }
                set
                {
                    if (!isFilesNotInWhitelistSet)
                    {
                        filesNotInWhitelist = value;
                        isFilesNotInWhitelistSet = true;
                    }
                }
            }

            private bool isFilesWhitelistedInConfigSet;
            private List<string> filesWhitelistedInConfig;
            public List<string> FilesWhitelistedInConfig
            {
                get { return filesWhitelistedInConfig; }
                set
                {
                    if (!isFilesWhitelistedInConfigSet)
                    {
                        filesWhitelistedInConfig = value;
                        isFilesWhitelistedInConfigSet = true;
                    }
                }
            }

            private bool isMatchingFilesSet;
            private List<string> matchingFiles;
            public List<string> MatchingFiles
            {
                get { return matchingFiles; }
                set
                {
                    if (!isMatchingFilesSet)
                    {
                        matchingFiles = value;
                        isMatchingFilesSet = true;
                    }
                }
            }

            private bool isMissingFilesSet;
            private List<string> missingFiles;
            public List<string> MissingFiles
            {
                get { return missingFiles; }
                set
                {
                    if (!isMissingFilesSet)
                    {
                        missingFiles = value;
                        isMissingFilesSet = true;
                    }
                }
            }

            private bool isQuarantinedFilesSet;
            private List<string> quarantinedFiles;
            public List<string> QuarantinedFiles
            {
                get { return quarantinedFiles; }
                set
                {
                    if (!isQuarantinedFilesSet)
                    {
                        quarantinedFiles = value;
                        isQuarantinedFilesSet = true;
                    }
                }
            }
        }

        internal static List<string> WhitelistedDirectories { get; } = new List<string> { "config", "plugins" };

        internal static CheckedFilesStatus CheckFiles(string bepinexPath, PluginFileMap pluginFileMap, RemotePluginsConfig config)
        {
            bool allFilesExist = false;
            bool allFilesMatch = false;
            bool containsSitDll = false;
            int filesChecked = 0;
            List<string> badFileMapHashFiles = new List<string>();
            List<string> badKnownHashFiles = new List<string>();
            List<string> filesNotInWhitelist = new List<string>();
            List<string> filesWhitelistedInConfig = new List<string>();
            List<string> matchingFiles = new List<string>();
            List<string> missingFiles = new List<string>();
            List<string> quarantinedFiles = new List<string>();

            string quarantinePath = Path.Combine(bepinexPath, "remoteplugins", "quarantine");

            pluginFileMap.Files.ForEach(file =>
            {
                filesChecked++;
                if (!WhitelistedDirectories.Any(dir => file.Name.StartsWith(dir + "/")))
                {
                    filesNotInWhitelist.Add(file.Name);
                    return;
                }
                if (!containsSitDll && file.Name.Equals("plugins/StayInTarkov.dll"))
                {
                    containsSitDll = true;
                }
                string filePath = Path.GetFullPath(Path.Combine(bepinexPath, file.Name));

                bool isDirectory = file.Name.EndsWith("/") && file.Hash.Equals("") && file.Size == 0;
                
                if ((isDirectory && !Directory.Exists(filePath)) || (!isDirectory && !File.Exists(filePath)))
                {
                    string quarantineFilePath = Path.Combine(quarantinePath, file.Name);
                    if (config.KnownFileHashesOnly && config.UnknownFileHashAction == UnknownFileHashAction.Quarantine && File.Exists(quarantineFilePath) && Utilities.GenerateHash(quarantineFilePath) == file.Hash)
                    {
                        quarantinedFiles.Add(file.Name);
                    }
                    else
                    {
                        missingFiles.Add(file.Name + " " + isDirectory + " " + !Directory.Exists(filePath) + " ");
                    }
                }
                else
                {
                    if (isDirectory)
                    {
                        matchingFiles.Add(file.Name);
                        return;
                    }

                    // file exists
                    string fileHash = Utilities.GenerateHash(filePath);
                    if (fileHash != file.Hash)
                    {
                        badFileMapHashFiles.Add(file.Name);
                    }
                    else
                    {
                        if (config.KnownFileHashesOnly && file.Name.ToLower().EndsWith(".dll"))
                        {
                            if (IsKnownGoodFileHash(fileHash))
                            {
                                matchingFiles.Add(file.Name);
                                if (config.AllowedFileHashes.Contains(fileHash))
                                {
                                    filesWhitelistedInConfig.Add(file.Name);
                                }
                            }
                            else
                            {
                                badKnownHashFiles.Add(file.Name);
                                Console.WriteLine("Unknown hash: " + file.Name + " hash: " + fileHash);
                            }
                        }
                        else
                        {
                            matchingFiles.Add(file.Name);
                        }
                    }
                }
            });

            if (missingFiles.Count == 0)
            {
                allFilesExist = true;
            }

            if (matchingFiles.Count == pluginFileMap.Files.Count)
            {
                allFilesMatch = true;
            }

            return new CheckedFilesStatus
            {
                AllFilesExist = allFilesExist,
                AllFilesMatch = allFilesMatch,
                ContainsSitDll = containsSitDll,
                FilesChecked = filesChecked,
                BadFileMapHashFiles = badFileMapHashFiles,
                BadKnownHashFiles = badKnownHashFiles,
                FilesNotInWhitelist = filesNotInWhitelist,
                FilesWhitelistedInConfig = filesWhitelistedInConfig,
                MatchingFiles = matchingFiles,
                MissingFiles = missingFiles
            };
        }


        internal static Dictionary<string, HashData> knownGoodFileHashes;

        internal static void InitKnownGoodFileHashes(List<string> additionalHashes = null)
        {
            if (knownGoodFileHashes == null)
            {
                LoadRemoteKnownGoodFileHashes();

                if (knownGoodFileHashes == null)
                {
                    //Logger.LogInfo("Falling back to local known good file hashes");
                    var assembly = Assembly.GetExecutingAssembly();
                    using (Stream stream = assembly.GetManifestResourceStream("RemotePlugins.KnownFileHashes.json"))
                    {
                        if (stream == null)
                        {
                            knownGoodFileHashes = new Dictionary<string, HashData>();
                            Logger.LogFatal("Failed to load local known good file hashes");
                            throw new Exception("Failed to load local known good file hashes");
                        }

                        using (StreamReader reader = new StreamReader(stream))
                        {
                            string json = reader.ReadToEnd();
                            knownGoodFileHashes = JsonConvert.DeserializeObject<Dictionary<string, HashData>>(json);
                            Logger.LogInfo("Loaded " + knownGoodFileHashes.Count + " known good file hashes");
                        }
                    }
                }
            }

            if (additionalHashes != null)
            {
                foreach (string hash in additionalHashes)
                {
                    if (!knownGoodFileHashes.ContainsKey(hash))
                    {
                        knownGoodFileHashes.Add(hash, new HashData { Name = "-" });
                    }
                }
            }
        }
        internal static bool IsKnownGoodFileHash(string fileHash)
        {
            InitKnownGoodFileHashes();
            return knownGoodFileHashes.ContainsKey(fileHash);
        }

        private static void LoadRemoteKnownGoodFileHashes()
        {
            return;
            // INFO: This is disabled for now. Github uses TLS 1.3 which != supported by Unity's Mono. Workarounds like setting
            // ServicePointManager.SecurityProtocol do not work. Microsoft is also limiting official TLS 1.3 support to Windows 11, so we can't rely
            // on experimental features being enabled for other versions.
            // https://learn.microsoft.com/en-us/windows/win32/secauthn/protocols-in-tls-ssl--schannel-ssp-

            // load from github
            /*try
            {
                using (var client = new System.Net.WebClient())
                {
                    // always pull the latest version from master
                    string json = client.DownloadString("https://raw.githubusercontent.com/caellach/SIT_RemotePlugins/master/client/KnownFileHashes.json");
                    knownGoodFileHashes = JsonConvert.DeserializeObject<Dictionary<string, HashData>>(json);
                }
            }
            catch (Exception e)
            {
                Logger.LogInfo("Failed to load remote known good file hashes: " + e.Message);
            }*/
        }
    }
}

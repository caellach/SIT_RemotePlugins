using BepInEx.Logging;
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
        private readonly static ManualLogSource logger = Logger.CreateLogSource("RemotePlugins_PluginFileChecker");
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
        }

        internal static List<string> WhitelistedDirectories { get; } = new List<string> { "config", "plugins" };

        internal static CheckedFilesStatus CheckFiles(string bepinexPath, PluginFileMap pluginFileMap, bool knownFileHashesOnly)
        {
            bool allFilesExist = false;
            bool allFilesMatch = false;
            bool containsSitDll = false;
            int filesChecked = 0;
            List<string> badFileMapHashFiles = new List<string>();
            List<string> badKnownHashFiles = new List<string>();
            List<string> filesNotInWhitelist = new List<string>();
            List<string> matchingFiles = new List<string>();
            List<string> missingFiles = new List<string>();

            pluginFileMap.Files.ForEach(file =>
            {
                filesChecked++;
                if (!WhitelistedDirectories.Any(dir => file.Name.StartsWith(dir + "/")))
                {
                    filesNotInWhitelist.Add(file.Name);
                    return;
                }
                if (file.Name.Equals("plugins/StayInTarkov.dll"))
                {
                    containsSitDll = true;
                }
                string filePath = Path.GetFullPath(Path.Combine(bepinexPath, file.Name));
                if (!File.Exists(filePath))
                {
                    missingFiles.Add(file.Name);
                }
                else
                {
                    // file exists
                    string fileHash = GenerateFileHash(filePath);
                    if (fileHash != file.Hash)
                    {
                        badFileMapHashFiles.Add(file.Name);
                    }
                    else
                    {
                        if (knownFileHashesOnly && file.Name.ToLower().EndsWith(".dll"))
                        {
                            if (isKnownGoodFileHash(fileHash))
                            {
                                matchingFiles.Add(file.Name);
                            }
                            else
                            {
                                badKnownHashFiles.Add(file.Name);
                                Console.WriteLine("Bad known hash: " + file.Name + " hash: " + fileHash);
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
                MatchingFiles = matchingFiles,
                MissingFiles = missingFiles
            };
        }

        internal static string GenerateFileHash(string filePath)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty).ToLower();
                }
            }
        }


        internal class HashData
        {
            public string Name { get; set; }
            public List<string> ReleaseVersions { get; set; }
        }
        internal static Dictionary<string, HashData> knownGoodFileHashes;
        internal static bool isKnownGoodFileHash(string fileHash)
        {
            if (knownGoodFileHashes == null)
            {
                LoadRemoteKnownGoodFileHashes();

                if (knownGoodFileHashes == null)
                {
                    logger.LogInfo("Falling back to local known good file hashes");
                    var assembly = Assembly.GetExecutingAssembly();
                    using (Stream stream = assembly.GetManifestResourceStream("RemotePlugins.KnownFileHashes.json"))
                    {
                        if (stream == null)
                        {
                            knownGoodFileHashes = new Dictionary<string, HashData>();
                            Console.WriteLine("Failed to load local known good file hashes");
                            return false;
                        }

                        using (StreamReader reader = new StreamReader(stream))
                        {
                            string json = reader.ReadToEnd();
                            knownGoodFileHashes = JsonConvert.DeserializeObject<Dictionary<string, HashData>>(json);
                            Console.WriteLine("Loaded " + knownGoodFileHashes.Count + " known good file hashes");
                        }
                    }
                }
            }

            return knownGoodFileHashes.ContainsKey(fileHash);
        }

        private static void LoadRemoteKnownGoodFileHashes()
        {
            // load from github
            try
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
                logger.LogInfo("Failed to load remote known good file hashes: " + e.Message);
            }
        }
    }
}

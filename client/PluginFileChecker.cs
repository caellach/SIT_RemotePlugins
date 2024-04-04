using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

            private bool isBadHashFilesSet;
            private List<string> badHashFiles;
            public List<string> BadHashFiles
            {
                get { return badHashFiles; }
                set
                {
                    if (!isBadHashFilesSet)
                    {
                        badHashFiles = value;
                        isBadHashFilesSet = true;
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

        internal static List<string> WhiteListedDirectories { get; } = new List<string> { "config", "plugins" };

        internal static CheckedFilesStatus CheckFiles(string bepinexPath, PluginFileMap pluginFileMap)
        {
            bool allFilesExist = false;
            bool allFilesMatch = false;
            bool containsSitDll = false;
            int filesChecked = 0;
            List<string> badHashFiles = new List<string>();
            List<string> filesNotInWhitelist = new List<string>();
            List<string> matchingFiles = new List<string>();
            List<string> missingFiles = new List<string>();

            pluginFileMap.Files.ForEach(file =>
            {
                filesChecked++;
                if (!WhiteListedDirectories.Any(dir => file.Name.StartsWith(dir + "/")))
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
                    string fileHash = GetFileHash(filePath);
                    if (fileHash != file.Hash)
                    {
                        badHashFiles.Add(file.Name);
                    }
                    else
                    {
                        matchingFiles.Add(file.Name);
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
                BadHashFiles = badHashFiles,
                FilesNotInWhitelist = filesNotInWhitelist,
                MatchingFiles = matchingFiles,
                MissingFiles = missingFiles
            };
        }

        internal static string GetFileHash(string filePath)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty).ToLower();
                }
            }
        }
    }
}

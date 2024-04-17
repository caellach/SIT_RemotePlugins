using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Mono.Cecil;
using static RemotePlugins.Utilities;

namespace RemotePlugins
{
    public static class RemotePlugins
    {
        public static IEnumerable<string> TargetDLLs { get; } = new[] { "Assembly-CSharp.dll" }; // doesn't matter what DLL is specified

        public static void Patch(AssemblyDefinition assembly)
        {
            Logger.LogInfo("Patch called on " + assembly.FullName + " which was untouched; this is only a hook for execution.");
        }

        public static void Initialize()
        {
            int startTimeMs = Environment.TickCount;
            Logger.LogInfo("Initialize called");

            RemotePluginsConfig config = RemotePluginsConfig.Load();
            Logger.LogInfo("Config version: " + config.Version);

            BackendApi backendApi = new BackendApi();
            if (CheckAndLogFatal(!backendApi.CanConnect, "Cannot connect to backend. Skipping", startTimeMs)) return;

            ClientOptions clientOptions = backendApi.GetClientOptions();
            if (CheckAndLogFatal(clientOptions == null, "Cannot get client options. Skipping", startTimeMs)) return;

            PluginFileMap pluginFileMap = backendApi.GetFileList();
            if (CheckAndLogFatal(pluginFileMap == null, "Cannot get plugin file list. Skipping", startTimeMs)) return;

            PluginFileChecker.InitKnownGoodFileHashes(config.AllowedFileHashes);

            // get the base file path for our current directory
            string baseFilePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string bepinexPath = Path.GetFullPath(Path.Combine(baseFilePath, "..")); // we assume that we are in /EFT/BepInEx/patchers/
            
            // check the hashes of the files
            PluginFileChecker.CheckedFilesStatus checkedFilesStatus = CheckExistingFiles(config, pluginFileMap, bepinexPath);
            if (CheckAndLogFatal(checkedFilesStatus == null, "Failed to check files. Skipping", startTimeMs)) return;

            string remotePluginsPath = Path.GetFullPath(Path.Combine(bepinexPath, "remoteplugins"));
            DeleteEmptyDirectories(remotePluginsPath);

            if (!ShouldUpdate(checkedFilesStatus, clientOptions, pluginFileMap, bepinexPath, startTimeMs)) return;

            Logger.LogInfo("Update needed. Processing...");

            // download the plugin update file
            PluginUpdateFile pluginUpdateFile = DownloadPluginUpdateFile(backendApi, pluginFileMap, clientOptions, bepinexPath, startTimeMs);
            if (pluginUpdateFile == null) return;

            if (!ExtractPluginUpdateFile(config, pluginUpdateFile, pluginFileMap, clientOptions, bepinexPath, startTimeMs)) return;
            DeleteEmptyDirectories(remotePluginsPath);

            Logger.LogInfo("Complete. All files are up to date");
            PrintTimeTaken(startTimeMs);
        }

        private static PluginFileChecker.CheckedFilesStatus CheckExistingFiles(RemotePluginsConfig config, PluginFileMap pluginFileMap, string bepinexPath)
        {
            if (pluginFileMap == null || pluginFileMap.Files == null || pluginFileMap.Files.Count == 0)
            {
                Logger.LogFatal("No files in the plugin file map. Skipping");
                return null;
            }

            if (pluginFileMap.Zip == null || string.IsNullOrEmpty(pluginFileMap.Zip.Hash))
            {
                Logger.LogFatal("No zip file in the plugin file map. Skipping");
                return null;
            }

            if (string.IsNullOrEmpty(bepinexPath))
            {
                Logger.LogFatal("BepInEx path is empty. Skipping");
                return null;
            }

            Logger.LogInfo("Checking files in directory: " + bepinexPath);
            PluginFileChecker.CheckedFilesStatus checkedFilesStatus = PluginFileChecker.CheckFiles(bepinexPath, pluginFileMap, config);
            Logger.LogInfo("Checked files: " + checkedFilesStatus.FilesChecked);
            if (checkedFilesStatus.FilesNotInWhitelist.Count > 0)
            {
                Logger.LogInfo("Skipped files:");
                foreach (string file in checkedFilesStatus.FilesNotInWhitelist)
                {
                    Logger.LogInfo("\t" + file);
                }
            }

            if (checkedFilesStatus.BadFileMapHashFiles.Count > 0)
            {
                Logger.LogInfo("Bad file map hash files:");
                foreach (string file in checkedFilesStatus.BadFileMapHashFiles)
                {
                    Logger.LogInfo("\t" + file);
                }
            }

            if (config.KnownFileHashesOnly)
            {
                Logger.LogInfo("Unknown hash files: " + checkedFilesStatus.BadKnownHashFiles.Count);
                foreach (string file in checkedFilesStatus.BadKnownHashFiles)
                {
                    Logger.LogInfo("\t" + file);
                }

                Logger.LogInfo("Files manually whitelisted in the config: " + checkedFilesStatus.FilesWhitelistedInConfig.Count);
                foreach (string file in checkedFilesStatus.FilesWhitelistedInConfig)
                {
                    Logger.LogInfo("\t" + file);
                }
            }
            Logger.LogInfo("Missing files: " + checkedFilesStatus.MissingFiles.Count);

            if (!checkedFilesStatus.ContainsSitDll)
            {
                Logger.LogError("StayInTarkov.dll not found. Skipping");
                return null;
            }

            return checkedFilesStatus;
        }

        private static void ExtractToDirectory(RemotePluginsConfig config, string zipPath, string extractPath)
        {
            if (config.KnownFileHashesOnly)
            {
                string quarantinePath = Path.GetFullPath(Path.Combine(extractPath, "remoteplugins", "quarantine"));
                Dictionary<string, string> quarantinedFiles = new Dictionary<string, string>();

                Logger.LogInfo("Extracting known files" +
                    (config.UnknownFileHashAction == UnknownFileHashAction.Quarantine ? " and quarantining the rest"
                     : config.UnknownFileHashAction == UnknownFileHashAction.Warn ? " and unknown files"
                     : config.UnknownFileHashAction == UnknownFileHashAction.Delete ? " and deleting the rest"
                     : "")
                );
                using (ZipArchive archive = ZipFile.OpenRead(zipPath))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (entry.FullName.EndsWith("/"))
                        {
                            // create the directory
                            string outputDir = Path.GetFullPath(Path.Combine(extractPath, entry.FullName));
                            if (!Directory.Exists(outputDir))
                            {
                                Directory.CreateDirectory(outputDir);
                            }
                            continue;
                        }

                        using (Stream entryStream = entry.Open())
                        {
                            bool requiresHashCheck = entry.FullName.EndsWith(".dll") || entry.FullName.EndsWith(".exe");
                            string zippedFileHash = requiresHashCheck ? GenerateHash(entryStream) : "";

                            string outputPath = string.Empty;
                            if (!requiresHashCheck || PluginFileChecker.IsKnownGoodFileHash(zippedFileHash))
                            {
                                outputPath = Path.GetFullPath(Path.Combine(extractPath, entry.FullName));
                                string quarantinedPath = Path.GetFullPath(Path.Combine(quarantinePath, entry.FullName));
                                DeleteFileIfExists(quarantinedPath);
                            }
                            else if (config.UnknownFileHashAction == UnknownFileHashAction.Quarantine)
                            {
                                outputPath = Path.GetFullPath(Path.Combine(quarantinePath, entry.FullName));
                                quarantinedFiles.Add(entry.FullName, zippedFileHash);
                                DeleteFileIfExists(outputPath);
                            }
                            else if (config.UnknownFileHashAction == UnknownFileHashAction.Warn)
                            {
                                Logger.LogInfo("Extracting file with unknown hash: " + entry.FullName + " " + zippedFileHash);
                                outputPath = Path.GetFullPath(Path.Combine(extractPath, entry.FullName));
                            }

                            if (outputPath == string.Empty)
                            {
                                continue;
                            }

                            string outputDir = Path.GetDirectoryName(outputPath);
                            if (!Directory.Exists(outputDir))
                            {
                                Directory.CreateDirectory(outputDir);
                            }

                            entry.ExtractToFile(outputPath, true);
                        }
                    }
                }

                if (config.UnknownFileHashAction == UnknownFileHashAction.Quarantine)
                {
                    if (quarantinedFiles.Count > 0)
                    {
                        Logger.LogInfo("Quarantined files:");
                        foreach (KeyValuePair<string, string> entry in quarantinedFiles)
                        {
                            Logger.LogInfo("\t" + entry.Key + " " + entry.Value);
                        }
                        Logger.LogInfo("Add the hashes to the 'AllowedFileHashes' array in the file 'EFT/BepInEx/config/RemotePlugins.json' to accept these files.");
                    }
                    else
                    {
                        Logger.LogInfo("No files quarantined");
                    }
                }

                Logger.LogInfo("Extraction complete");
            }
            else
            {
                Logger.LogInfo("Extracting all files");
                ZipFile.ExtractToDirectory(zipPath, extractPath);
                Logger.LogInfo("Extraction complete");
            }
        }

        private static PluginUpdateFile DownloadPluginUpdateFile(BackendApi backendApi, PluginFileMap pluginFileMap, ClientOptions clientOptions, string bepinexPath, int startTimeMs)
        {
            if (backendApi == null || pluginFileMap == null || pluginFileMap.Zip == null || string.IsNullOrEmpty(pluginFileMap.Zip.Hash) || string.IsNullOrEmpty(bepinexPath))
            {
                Logger.LogFatal("Invalid parameters. Skipping");
                PrintTimeTaken(startTimeMs);
                return null;
            }


            string downloadPath = Path.GetFullPath(Path.Combine(bepinexPath, "remoteplugins", "downloads"));
            PluginUpdateFile pluginUpdateFile = backendApi.GetPluginUpdateFile(downloadPath, pluginFileMap.Zip.Hash);
            if (pluginUpdateFile == null || pluginUpdateFile.FileSize == 0)
            {
                Logger.LogInfo("No plugin update file found. Skipping");
                PrintTimeTaken(startTimeMs);
                return null;
            }
            else
            {
                Logger.LogInfo("Using plugin update file: " + pluginUpdateFile.FilePath + ":" + pluginUpdateFile.FileSize);

                // verify the zip file hash
                string zipHash = GenerateHash(pluginUpdateFile.FilePath);
                Logger.LogInfo("Zip file hash: " + zipHash);
                if (zipHash != pluginFileMap.Zip.Hash)
                {
                    Logger.LogFatal("Zip file hash does not match. Skipping");
                    PrintTimeTaken(startTimeMs);
                    return null;
                }
                else
                {
                    return pluginUpdateFile;
                }
            }
        }

        private static bool ExtractPluginUpdateFile(RemotePluginsConfig config, PluginUpdateFile pluginUpdateFile, PluginFileMap pluginFileMap, ClientOptions clientOptions, string bepinexPath, int startTimeMs)
        {
            if (pluginUpdateFile == null || pluginFileMap == null || clientOptions == null || string.IsNullOrEmpty(bepinexPath))
            {
                Logger.LogFatal("Invalid parameters. Skipping");
                PrintTimeTaken(startTimeMs);
                return false;
            }

            // extract the zip file
            try
            {
                // delete the files in the FileMap
                if (clientOptions.SyncType == ClientOptions.Synchronization.UpdateOnly)
                {
                    // delete only the files listed in the FileMap
                    foreach (PluginFileMap.PluginFile file in pluginFileMap.Files)
                    {
                        string filePath = Path.GetFullPath(Path.Combine(bepinexPath, file.Name));
                        DeleteFileIfExists(filePath);
                    }
                }
                else if (clientOptions.SyncType == ClientOptions.Synchronization.DeleteAndSync)
                {
                    // delete all files in the root directories listed in the FileMap
                    // ensuring that we are in full sync with the server
                    List<string> directories = new List<string>();
                    foreach (PluginFileMap.PluginFile file in pluginFileMap.Files)
                    {
                        string firstDirectory = file.Name.Split('/')[0];
                        if (!directories.Contains(firstDirectory))
                        {
                            directories.Add(firstDirectory);
                            // delete this directory
                            string directoryPath = Path.GetFullPath(Path.Combine(bepinexPath, firstDirectory));
                            if (Directory.Exists(directoryPath))
                            {
                                Directory.Delete(directoryPath, true);
                            }
                        }
                    }
                }

                foreach (PluginFileMap.PluginFile file in pluginFileMap.Files)
                {
                    string filePath = Path.GetFullPath(Path.Combine(bepinexPath, file.Name));
                    DeleteFileIfExists(filePath);
                }

                ExtractToDirectory(config, pluginUpdateFile.FilePath, bepinexPath);
                return true;
            }
            catch (Exception e)
            {
                Logger.LogFatal("Failed to extract zip file: " + e.Message);
                PrintTimeTaken(startTimeMs);
                return false;
            }
        }

        private static bool ShouldUpdate(PluginFileChecker.CheckedFilesStatus checkedFilesStatus, ClientOptions clientOptions, PluginFileMap pluginFileMap, string bepinexPath, int startTimeMs)
        {
            if (checkedFilesStatus.AllFilesExist && checkedFilesStatus.AllFilesMatch)
            {
                if (clientOptions.SyncType == ClientOptions.Synchronization.UpdateOnly)
                {
                    Logger.LogInfo("All files are up to date. Continuing");
                    PrintTimeTaken(startTimeMs);
                    return false;
                }
                else if (clientOptions.SyncType == ClientOptions.Synchronization.DeleteAndSync)
                {
                    // get root directories
                    List<string> directories = new List<string>();
                    foreach (PluginFileMap.PluginFile file in pluginFileMap.Files)
                    {
                        string firstDirectory = file.Name.Split('/')[0];
                        if (!directories.Contains(firstDirectory))
                        {
                            directories.Add(firstDirectory);
                        }
                    }

                    int fileCount = 0;
                    foreach (string directory in directories)
                    {
                        fileCount += Directory.GetFiles(Path.GetFullPath(Path.Combine(bepinexPath, directory)), "*", SearchOption.AllDirectories).Length;
                    }

                    // taking a shortcut here, if the file count is the same as the checked files count then we are in sync
                    // otherwise we'll just download the update and delete everything else
                    if (fileCount == checkedFilesStatus.FilesChecked)
                    {
                        Logger.LogInfo("All files are up to date. Continuing");
                        PrintTimeTaken(startTimeMs);
                        return false;
                    }
                    Logger.LogInfo("Unexpected files found, doing a full reset...");
                }
            }
            return true;
        }
    }
}

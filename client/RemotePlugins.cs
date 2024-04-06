using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using BepInEx.Logging;
using Mono.Cecil;

namespace RemotePlugins
{
    public static class RemotePlugins
    {
        private static RemotePluginsConfig config;
        private static ManualLogSource logger = Logger.CreateLogSource("RemotePlugins");
        public static IEnumerable<string> TargetDLLs { get; } = new[] { "Assembly-CSharp.dll" }; // doesn't matter what DLL is specified

        public static void Patch(AssemblyDefinition assembly)
        {
            logger.LogInfo("Patch called on " + assembly.FullName);
        }

        public static void Initialize()
        {
            int startTimeMs = Environment.TickCount;
            logger.LogInfo("Initialize called");

            config = RemotePluginsConfig.Load();

            BackendApi backendApi = new BackendApi();
            if (!backendApi.CanConnect)
            {
                logger.LogFatal("Cannot connect to backend. Skipping");
                PrintTimeTaken(startTimeMs);
                return;
            }

            ClientOptions clientOptions = backendApi.GetClientOptions();
            if (clientOptions == null)
            {
                logger.LogFatal("Cannot get client options. Skipping");
                PrintTimeTaken(startTimeMs);
                return;
            }

            PluginFileMap pluginFileMap = backendApi.GetFileList();
            if (pluginFileMap == null)
            {
                logger.LogFatal("Cannot get plugin file list. Skipping");
                PrintTimeTaken(startTimeMs);
                return;
            }

            // get the base file path for our current directory
            string baseFilePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string bepinexPath = Path.GetFullPath(Path.Combine(baseFilePath, "..")); // we assume that we are in /EFT/BepInEx/Patchers/
            // check the hashes of the files
            logger.LogInfo("Checking files in directory: " + bepinexPath);
            PluginFileChecker.CheckedFilesStatus checkedFilesStatus = PluginFileChecker.CheckFiles(bepinexPath, pluginFileMap, config.KnownFileHashesOnly);
            logger.LogInfo("Checked files: " + checkedFilesStatus.FilesChecked);
            logger.LogInfo("Skipped files: " + checkedFilesStatus.FilesNotInWhitelist.Count);
            logger.LogInfo("Bad file map hash files: " + checkedFilesStatus.BadFileMapHashFiles.Count);
            if (config.KnownFileHashesOnly)
            {
                logger.LogInfo("Bad known hash files: " + checkedFilesStatus.BadKnownHashFiles.Count);
                foreach (string file in checkedFilesStatus.BadKnownHashFiles)
                {
                    logger.LogInfo("\t" + file);
                }
            }
            logger.LogInfo("Missing files: " + checkedFilesStatus.MissingFiles.Count);

            if (!checkedFilesStatus.ContainsSitDll)
            {
                logger.LogError("StayInTarkov.dll not found. Skipping");
                return;
            }

            if (checkedFilesStatus.AllFilesExist && checkedFilesStatus.AllFilesMatch)
            {
                if (clientOptions.SyncType == ClientOptions.Synchronization.UpdateOnly)
                {
                    logger.LogInfo("All files are up to date. Continuing");
                    PrintTimeTaken(startTimeMs);
                    return;
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
                        logger.LogInfo("All files are up to date. Continuing");
                        PrintTimeTaken(startTimeMs);
                        return;
                    }
                    logger.LogInfo("Unexpected files found, doing a full reset...");
                }
            }

            logger.LogInfo("Update needed. Processing...");

            // download the plugin update file
            string downloadPath = Path.GetFullPath(Path.Combine(bepinexPath, "remoteplugins_downloads"));
            PluginUpdateFile pluginUpdateFile = backendApi.GetPluginUpdateFile(downloadPath, pluginFileMap.Zip.Hash);
            if (pluginUpdateFile == null || pluginUpdateFile.FileSize == 0)
            {
                logger.LogInfo("No plugin update file found. Skipping");
                PrintTimeTaken(startTimeMs);
                return;
            }
            else
            {
                logger.LogInfo("Using plugin update file: " + pluginUpdateFile.FilePath + ":" + pluginUpdateFile.FileSize);
            }

            // verify the zip file hash
            string zipHash = PluginFileChecker.GenerateFileHash(pluginUpdateFile.FilePath);
            logger.LogInfo("Zip file hash: " + zipHash);
            if (zipHash != pluginFileMap.Zip.Hash)
            {
                logger.LogFatal("Zip file hash does not match. Skipping");
                PrintTimeTaken(startTimeMs);
                return;
            }

            // extract the zip file
            try
            {
                /*string extractPath = Path.GetFullPath(Path.Combine(downloadPath, "extracted"));
                if (Directory.Exists(extractPath))
                {
                    Directory.Delete(extractPath, true);
                }*/

                // delete the files in the FileMap b/c ZipFile.ExtractToDirectory doesn't overwrite
                if (clientOptions.SyncType == ClientOptions.Synchronization.UpdateOnly)
                {
                    // delete only the files listed in the FileMap
                    foreach (PluginFileMap.PluginFile file in pluginFileMap.Files)
                    {
                        string filePath = Path.GetFullPath(Path.Combine(bepinexPath, file.Name));
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }
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
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                }
                string extractPath = bepinexPath;
                ZipFile.ExtractToDirectory(pluginUpdateFile.FilePath, extractPath);
                logger.LogInfo("Extracted zip file to: " + extractPath);
            }
            catch (Exception e)
            {
                logger.LogFatal("Failed to extract zip file: " + e.Message);
                PrintTimeTaken(startTimeMs);
                return;
            }

            // move the files to the correct location
            /*try
            {
                foreach (PluginFileMap.PluginFile file in pluginFileMap.Files)
                {
                    string sourcePath = Path.GetFullPath(Path.Combine(downloadPath, "extracted", file.Name));
                    string destPath = Path.GetFullPath(Path.Combine(bepinexPath, file.Name));
                    if (File.Exists(destPath))
                    {
                        File.Delete(destPath);
                    }
                    File.Move(sourcePath, destPath);
                    logger.LogInfo("Moved file: " + file.Name);
                }
            }
            catch (Exception e)
            {
                logger.LogFatal("Failed to move files: " + e.Message);
                return;
            }*/

            logger.LogInfo("Complete. All files are up to date");
            PrintTimeTaken(startTimeMs);
        }

        private static void PrintTimeTaken(int startTimeMs)
        {
            int endTimeMs = Environment.TickCount;
            logger.LogInfo("Time taken: " + (endTimeMs - startTimeMs) + "ms");
        }

        public static void Finish()
        {
            logger.LogInfo("Finish called");
        }
    }
}

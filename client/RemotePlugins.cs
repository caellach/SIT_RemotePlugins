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
            PluginFileChecker.CheckedFilesStatus checkedFilesStatus = PluginFileChecker.CheckFiles(bepinexPath, pluginFileMap);
            logger.LogInfo("Checked files: " + checkedFilesStatus.FilesChecked);
            logger.LogInfo("Skipped files: " + checkedFilesStatus.FilesNotInWhitelist.Count);
            logger.LogInfo("Bad hash files: " + checkedFilesStatus.BadHashFiles.Count);
            logger.LogInfo("Missing files: " + checkedFilesStatus.MissingFiles.Count);

            if (checkedFilesStatus.AllFilesExist && checkedFilesStatus.AllFilesMatch)
            {
                logger.LogInfo("All files are up to date. Continuing");
                PrintTimeTaken(startTimeMs);
                return;
            }

            logger.LogInfo("Update needed. Processing...");

            // download the plugin update file
            string downloadPath = Path.GetFullPath(Path.Combine(bepinexPath, "remoteplugins_downloads"));
            PluginUpdateFile pluginUpdateFile = backendApi.GetPluginUpdateFile(downloadPath);
            if (pluginUpdateFile == null || pluginUpdateFile.FileSize == 0)
            {
                logger.LogInfo("No plugin update file found. Skipping");
                PrintTimeTaken(startTimeMs);
                return;
            }
            else
            {
                logger.LogInfo("Plugin update file downloaded: " + pluginUpdateFile.FilePath + ":" + pluginUpdateFile.FileSize);
            }

            // verify the zip file hash
            string zipHash = PluginFileChecker.GetFileHash(pluginUpdateFile.FilePath);
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

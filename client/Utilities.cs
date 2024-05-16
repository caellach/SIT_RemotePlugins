using System;
using System.IO;

namespace RemotePlugins
{
    internal class Utilities
    {
        internal static string GenerateHash(string filePath)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty).ToLower();
                }
            }
        }

        internal static string GenerateHash(byte[] fileData)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                return BitConverter.ToString(sha256.ComputeHash(fileData)).Replace("-", string.Empty).ToLower();
            }
        }

        internal static string GenerateHash(Stream stream)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty).ToLower();
            }
        }

        internal static void DeleteEmptyDirectories(string filePath)
        {
            if (!Directory.Exists(filePath))
            {
                return;
            }

            // find all empty directories and delete them
            foreach (string dir in Directory.GetDirectories(filePath, "*", SearchOption.AllDirectories))
            {
                if (Directory.GetFiles(dir).Length == 0 && Directory.GetDirectories(dir).Length == 0)
                {
                    Directory.Delete(dir);
                }
            }
        }

        internal static void DeleteFileIfExists(string filePath)
        {
            if (File.Exists(filePath))
            {
                Logger.LogInfo("Deleting file: " + filePath);
                File.Delete(filePath);
            }
        }

        internal static bool CheckAndLogFatal(bool condition, string errorMessage, int startTimeMs)
        {
            if (condition)
            {
                Logger.LogFatal(errorMessage);
                PrintTimeTaken(startTimeMs);
            }
            return condition;
        }

        internal static void PrintTimeTaken(int startTimeMs)
        {
            int endTimeMs = Environment.TickCount;
            Logger.LogInfo("Time taken: " + (endTimeMs - startTimeMs) + "ms");
        }

        internal static string DecompressZlibString(byte[] responseBytes)
        {
            byte[] decompressedBytes = Elskom.Generic.Libs.MemoryZlib.Decompress(responseBytes);
            return System.Text.Encoding.UTF8.GetString(decompressedBytes);
        }
    }
}

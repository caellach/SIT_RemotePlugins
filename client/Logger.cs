using BepInEx.Logging;
using System.Runtime.CompilerServices;

namespace RemotePlugins
{
    internal class Logger
    {
        private readonly static ManualLogSource logger = BepInEx.Logging.Logger.CreateLogSource("RemotePlugins");

        public static void LogInfo(string message, [CallerMemberName] string callerMemberName = "")
        {
            logger.LogInfo($"[{callerMemberName}] {message}");
        }

        public static void LogWarning(string message, [CallerMemberName] string callerMemberName = "")
        {
            logger.LogWarning($"[{callerMemberName}] {message}");
        }

        public static void LogError(string message, [CallerMemberName] string callerMemberName = "")
        {
            logger.LogError($"[{callerMemberName}] {message}");
        }

        public static void LogFatal(string message, [CallerMemberName] string callerMemberName = "")
        {
            logger.LogFatal($"[{callerMemberName}] {message}");
        }
    }
}

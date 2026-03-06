using System;
using System.IO;

namespace Selvagen.GH
{
    public static class PluginLogger
    {
        private static readonly string LogFilePath;

        static PluginLogger()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var logDir = Path.Combine(appData, "Selvagen", "Logs");

            try
            {
                Directory.CreateDirectory(logDir);
            }
            catch
            {
                // Fall back to temp directory if AppData is inaccessible
                logDir = Path.GetTempPath();
            }

            LogFilePath = Path.Combine(logDir, "selvagen.log");

            // Clear the log file at the start of every new session
            try
            {
                if (File.Exists(LogFilePath))
                    File.Delete(LogFilePath);
            }
            catch
            {
                // Ignore if it's locked
            }
        }

        public static void Log(string message)
        {
            try
            {
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
                File.AppendAllText(LogFilePath, logEntry);

                // Write to Visual Studio output as well
                System.Diagnostics.Debug.WriteLine(logEntry);
            }
            catch
            {
                // Fail silently so logging issues don't crash Rhino
            }
        }
    }
}

using System;
using System.IO;

namespace LelbryBalanceFixes
{
    internal static class ModLog
    {
        private const string Prefix = "[LelbryBalanceFixes] ";
        private static readonly object Sync = new object();
        private static string _logPath;

        private static string LogPath
        {
            get
            {
                if (_logPath == null)
                {
                    try
                    {
                        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                        var dir = Path.Combine(docs, "Mount and Blade II Bannerlord", "Logs");
                        Directory.CreateDirectory(dir);
                        _logPath = Path.Combine(dir, "LelbryBalanceFixes.log");
                    }
                    catch
                    {
                        _logPath = string.Empty;
                    }
                }
                return _logPath;
            }
        }

        public static void Info(string message) => Write("INFO", message);
        public static void Error(string message) => Write("ERROR", message);

        private static void Write(string level, string message)
        {
            var line = $"{DateTime.Now:HH:mm:ss.fff} {level} {Prefix}{message}";
            try { Console.WriteLine(line); } catch { }
            if (string.IsNullOrEmpty(LogPath)) return;
            try
            {
                lock (Sync)
                {
                    File.AppendAllText(LogPath, line + Environment.NewLine);
                }
            }
            catch { }
        }
    }
}

using System;
using System.IO;
using System.Threading;

namespace LelbryBalanceFixes
{
    /// <summary>
    /// Hot-reloadable configuration for fix parameters.
    /// Launcher writes live.json next to the mod; this class watches the file and updates statics.
    /// Patch methods read these volatile fields on every invocation, so changes apply without restart.
    /// </summary>
    public static class LiveConfig
    {
        public const string FileName = "live.json";

        private static volatile int _partySizeBonus = 50;
        private static volatile bool _fullLootEnabled = true;

        public static int PartySizeBonus => _partySizeBonus;
        public static bool FullLootEnabled => _fullLootEnabled;

        public static event Action Reloaded;

        private static FileSystemWatcher _watcher;
        private static string _moduleDir;
        private static Timer _debounceTimer;
        private static readonly object Sync = new object();

        public static void Init(string moduleDir)
        {
            _moduleDir = moduleDir;
            Reload();

            try
            {
                _watcher = new FileSystemWatcher(moduleDir, FileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                    EnableRaisingEvents = true
                };
                _watcher.Changed += OnFileChanged;
                _watcher.Created += OnFileChanged;
                _watcher.Renamed += OnFileChanged;
                ModLog.Info("LiveConfig watcher started.");
            }
            catch (Exception ex)
            {
                ModLog.Error("LiveConfig watcher init failed: " + ex.Message);
            }
        }

        private static void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            // debounce — coalesce rapid sequential events into one Reload
            lock (Sync)
            {
                _debounceTimer?.Dispose();
                _debounceTimer = new Timer(_ => Reload(), null, 100, Timeout.Infinite);
            }
        }

        public static void Reload()
        {
            if (string.IsNullOrEmpty(_moduleDir)) return;
            var path = Path.Combine(_moduleDir, FileName);
            if (!File.Exists(path)) return;

            try
            {
                var raw = File.ReadAllText(path);
                int? bonus = MiniJson.GetInt(raw, "partySizeBonus");
                bool? fullLoot = MiniJson.GetBool(raw, "fullLootEnabled");

                if (bonus.HasValue)
                    Interlocked.Exchange(ref _partySizeBonus, bonus.Value);
                if (fullLoot.HasValue)
                    _fullLootEnabled = fullLoot.Value;

                ModLog.Info($"LiveConfig reloaded: bonus={_partySizeBonus}, fullLoot={_fullLootEnabled}");

                try { Reloaded?.Invoke(); } catch { }
            }
            catch (Exception ex)
            {
                ModLog.Error("LiveConfig.Reload failed: " + ex.Message);
            }
        }
    }
}

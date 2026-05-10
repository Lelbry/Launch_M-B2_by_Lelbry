using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using Launcher.Models;

namespace Launcher.Services;

/// <summary>
/// Watches live-status.json that the running mod writes, and raises StatusUpdated.
/// Detects "game alive" by file mtime — if status file is fresh (&lt; 30s old), the game is running.
/// </summary>
public sealed class LiveStatusReader : IDisposable
{
    public const string FileName = "live-status.json";
    private static readonly TimeSpan AliveWindow = TimeSpan.FromSeconds(30);

    private FileSystemWatcher? _watcher;
    private string? _path;
    private Timer? _debounce;
    private Timer? _aliveTicker;
    private readonly object _sync = new();

    public LiveStatus? LastStatus { get; private set; }
    public bool IsGameAlive => LastStatus != null && (DateTime.UtcNow - LastStatus.Ts) < AliveWindow;

    public event Action? Updated;

    public void SetGameDir(string? gameDir)
    {
        Stop();
        if (string.IsNullOrWhiteSpace(gameDir)) return;
        var modDir = Path.Combine(GamePathResolver.GetModulesDir(gameDir), ModDeployer.ModuleId);
        if (!Directory.Exists(modDir))
        {
            try { Directory.CreateDirectory(modDir); } catch { return; }
        }

        _path = Path.Combine(modDir, FileName);

        try
        {
            _watcher = new FileSystemWatcher(modDir, FileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };
            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileChanged;
            _watcher.Renamed += OnFileChanged;
        }
        catch { /* watcher optional; aliveTicker still polls */ }

        _aliveTicker = new Timer(_ => Updated?.Invoke(), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));

        ReloadInternal();
    }

    public void Stop()
    {
        lock (_sync)
        {
            _watcher?.Dispose();
            _watcher = null;
            _debounce?.Dispose();
            _debounce = null;
            _aliveTicker?.Dispose();
            _aliveTicker = null;
            _path = null;
            LastStatus = null;
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        lock (_sync)
        {
            _debounce?.Dispose();
            _debounce = new Timer(_ => ReloadInternal(), null, 80, Timeout.Infinite);
        }
    }

    private void ReloadInternal()
    {
        if (string.IsNullOrEmpty(_path) || !File.Exists(_path)) return;
        try
        {
            var json = File.ReadAllText(_path);
            var status = JsonSerializer.Deserialize<LiveStatus>(json);
            if (status != null) LastStatus = status;
            Updated?.Invoke();
        }
        catch
        {
            // ignore — half-written or invalid JSON, next event will retry
        }
    }

    public void Dispose() => Stop();
}

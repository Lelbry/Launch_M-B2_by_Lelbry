using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using Launcher.Models;
using Launcher.Services;
using ReactiveUI;

namespace Launcher.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly LauncherConfig _config;
    private string _gamePath = string.Empty;
    private LaunchMethod _launchMethod;
    private string _statusMessage = string.Empty;

    public ObservableCollection<FixViewModel> Fixes { get; } = new();
    public LiveTuningViewModel LiveTuning { get; } = new();

    public string GamePath
    {
        get => _gamePath;
        set
        {
            this.RaiseAndSetIfChanged(ref _gamePath, value);
            this.RaisePropertyChanged(nameof(IsGamePathValid));
            LiveTuning.SetState(_gamePath, _config.LiveTuning);
        }
    }

    public bool IsGamePathValid => GamePathResolver.IsValidGameDir(_gamePath);

    public LaunchMethod LaunchMethod
    {
        get => _launchMethod;
        set
        {
            this.RaiseAndSetIfChanged(ref _launchMethod, value);
            this.RaisePropertyChanged(nameof(IsDirectMethod));
            this.RaisePropertyChanged(nameof(IsSteamMethod));
        }
    }

    public bool IsDirectMethod
    {
        get => LaunchMethod == LaunchMethod.Direct;
        set { if (value) LaunchMethod = LaunchMethod.Direct; }
    }

    public bool IsSteamMethod
    {
        get => LaunchMethod == LaunchMethod.Steam;
        set { if (value) LaunchMethod = LaunchMethod.Steam; }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public ReactiveCommand<Unit, Unit> LaunchCommand { get; }
    public ReactiveCommand<Unit, Unit> LaunchVanillaCommand { get; }

    public MainWindowViewModel()
    {
        _config = LauncherConfig.Load();
        _gamePath = _config.GamePath ?? GamePathResolver.ResolveDefault() ?? string.Empty;
        _launchMethod = _config.LaunchMethod;

        LoadFixes();
        LiveTuning.SetState(_gamePath, _config.LiveTuning);
        LiveTuning.Applied += OnLiveTuningApplied;

        LaunchCommand = ReactiveCommand.Create(LaunchGame, this.WhenAnyValue(x => x.IsGamePathValid));
        LaunchVanillaCommand = ReactiveCommand.Create(LaunchVanilla, this.WhenAnyValue(x => x.IsGamePathValid));

        LaunchCommand.ThrownExceptions.Subscribe(ex => StatusMessage = "Ошибка: " + ex.Message);
        LaunchVanillaCommand.ThrownExceptions.Subscribe(ex => StatusMessage = "Ошибка: " + ex.Message);
    }

    private void OnLiveTuningApplied(LiveTuningConfig cfg)
    {
        _config.LiveTuning = cfg.Clone();
        _config.Save();
    }

    private void LoadFixes()
    {
        Fixes.Clear();
        var manifest = ManifestLoader.Load();
        if (manifest == null)
        {
            StatusMessage = "Не удалось загрузить manifest.json — проверьте, что мод собран (dotnet build).";
            return;
        }

        var savedEnabled = new HashSet<string>(_config.EnabledFixIds, StringComparer.OrdinalIgnoreCase);

        foreach (var entry in manifest.Fixes)
        {
            var enabled = _config.EnabledFixIds.Count > 0
                ? savedEnabled.Contains(entry.Id)
                : entry.DefaultEnabled;
            Fixes.Add(new FixViewModel(entry.Id, entry.Title, entry.Description, enabled));
        }

        if (Fixes.Count == 0)
            StatusMessage = "В manifest.json не указано ни одной правки.";
    }

    private void LaunchGame()
    {
        try
        {
            // All known fixes are always registered (their patches are essentially no-ops when
            // the relevant Live Tuning value is "neutral": bonus=0 / full-loot=false). The top
            // panel is now informational only — the user controls everything via Live Tuning.
            var enabledIds = Fixes.Select(f => f.Id).ToList();
            var liveTuning = new LiveTuningConfig
            {
                PartySizeBonus = LiveTuning.PartySizeBonus,
                FullLootEnabled = LiveTuning.FullLootEnabled
            };

            _config.GamePath = _gamePath;
            _config.LaunchMethod = _launchMethod;
            _config.EnabledFixIds = enabledIds;
            _config.LiveTuning = liveTuning.Clone();
            _config.Save();

            StatusMessage = "Деплой мода…";
            ModDeployer.Deploy(_gamePath, enabledIds, liveTuning);

            StatusMessage = _launchMethod == LaunchMethod.Direct
                ? "Запускаю Bannerlord…"
                : "Запускаю через Steam — поставьте галочку LelbryBalanceFixes в TaleWorlds Launcher.";

            GameLauncher.Launch(_gamePath, _launchMethod);
        }
        catch (Exception ex)
        {
            StatusMessage = "Ошибка: " + ex.Message;
        }
    }

    private void LaunchVanilla()
    {
        try
        {
            _config.GamePath = _gamePath;
            _config.LaunchMethod = _launchMethod;
            _config.Save();

            // Don't redeploy — vanilla launch is a "play my old saves untouched" mode.
            // Existing deployed mod folder stays on disk; we just don't include it in
            // the modules list passed to Bannerlord, and we untick it in LauncherData.xml.
            StatusMessage = "Запускаю Bannerlord без нашего мода…";
            GameLauncher.LaunchWithoutOurMod(_gamePath, _launchMethod);
        }
        catch (Exception ex)
        {
            StatusMessage = "Ошибка: " + ex.Message;
        }
    }
}

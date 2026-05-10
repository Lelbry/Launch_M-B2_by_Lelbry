using System;
using System.Globalization;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Threading;
using Launcher.Models;
using Launcher.Services;
using ReactiveUI;

namespace Launcher.ViewModels;

public sealed class LiveTuningViewModel : ViewModelBase, IDisposable
{
    private const int MinBonus = -200;
    private const int MaxBonus = 1000;

    private readonly LiveStatusReader _statusReader = new();
    private string? _gameDir;

    private int _partySizeBonus = 50;
    private bool _fullLootEnabled = true;
    private int _fullLootMultiplier = 10;
    private LiveTuningConfig _applied = new();
    private string _appliedAt = string.Empty;

    public int PartySizeBonus
    {
        get => _partySizeBonus;
        set
        {
            var clamped = Math.Max(MinBonus, Math.Min(MaxBonus, value));
            this.RaiseAndSetIfChanged(ref _partySizeBonus, clamped);
            this.RaisePropertyChanged(nameof(IsDirty));
        }
    }

    public bool FullLootEnabled
    {
        get => _fullLootEnabled;
        set
        {
            this.RaiseAndSetIfChanged(ref _fullLootEnabled, value);
            this.RaisePropertyChanged(nameof(IsDirty));
        }
    }

    public int FullLootMultiplier
    {
        get => _fullLootMultiplier;
        set
        {
            var clamped = Math.Max(1, Math.Min(100, value));
            this.RaiseAndSetIfChanged(ref _fullLootMultiplier, clamped);
            this.RaisePropertyChanged(nameof(IsDirty));
        }
    }

    public bool IsDirty =>
        _partySizeBonus != _applied.PartySizeBonus ||
        _fullLootEnabled != _applied.FullLootEnabled ||
        _fullLootMultiplier != _applied.FullLootMultiplier;

    public string AppliedAt
    {
        get => _appliedAt;
        private set => this.RaiseAndSetIfChanged(ref _appliedAt, value);
    }

    private string _liveHintText = "Игра не запущена";
    public string LiveHintText
    {
        get => _liveHintText;
        private set => this.RaiseAndSetIfChanged(ref _liveHintText, value);
    }

    // Avalonia binds CommandParameter as string by default. Taking string here avoids a cast
    // crash at click time (and lets the XAML stay simple — no x:Int32 wrappers needed).
    public ReactiveCommand<string, Unit> AdjustPartySizeCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetPartySizeCommand { get; }
    public ReactiveCommand<string, Unit> SetLootMultCommand { get; }
    public ReactiveCommand<Unit, Unit> ApplyCommand { get; }

    public event Action<LiveTuningConfig>? Applied;

    public LiveTuningViewModel()
    {
        AdjustPartySizeCommand = ReactiveCommand.Create<string>(s =>
        {
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var d))
                PartySizeBonus += d;
        });
        ResetPartySizeCommand = ReactiveCommand.Create(() => { PartySizeBonus = 0; });
        SetLootMultCommand = ReactiveCommand.Create<string>(s =>
        {
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                FullLootMultiplier = v;
        });
        ApplyCommand = ReactiveCommand.Create(Apply, this.WhenAnyValue(x => x.GameDirValid));

        // Without subscribing to ThrownExceptions, ReactiveUI rethrows on a default scheduler
        // and the Avalonia process can die. Surface errors into the hint instead.
        AdjustPartySizeCommand.ThrownExceptions.Subscribe(ex => AppliedAt = "Ошибка кнопки: " + ex.Message);
        ResetPartySizeCommand.ThrownExceptions.Subscribe(ex => AppliedAt = "Ошибка Reset: " + ex.Message);
        SetLootMultCommand.ThrownExceptions.Subscribe(ex => AppliedAt = "Ошибка множителя: " + ex.Message);
        ApplyCommand.ThrownExceptions.Subscribe(ex => AppliedAt = "Ошибка Apply: " + ex.Message);

        _statusReader.Updated += () => Dispatcher.UIThread.Post(UpdateHint);
    }

    public bool GameDirValid => !string.IsNullOrWhiteSpace(_gameDir) && GamePathResolver.IsValidGameDir(_gameDir);

    public void SetState(string? gameDir, LiveTuningConfig persisted)
    {
        _gameDir = gameDir;
        _applied = persisted.Clone();
        _partySizeBonus = persisted.PartySizeBonus;
        _fullLootEnabled = persisted.FullLootEnabled;
        _fullLootMultiplier = persisted.FullLootMultiplier;

        _statusReader.SetGameDir(gameDir);

        this.RaisePropertyChanged(nameof(PartySizeBonus));
        this.RaisePropertyChanged(nameof(FullLootEnabled));
        this.RaisePropertyChanged(nameof(FullLootMultiplier));
        this.RaisePropertyChanged(nameof(IsDirty));
        this.RaisePropertyChanged(nameof(GameDirValid));
        UpdateHint();
    }

    private void Apply()
    {
        if (string.IsNullOrWhiteSpace(_gameDir)) return;
        try
        {
            var cfg = new LiveTuningConfig
            {
                PartySizeBonus = _partySizeBonus,
                FullLootEnabled = _fullLootEnabled,
                FullLootMultiplier = _fullLootMultiplier
            };
            LiveConfigDeployer.Write(_gameDir, cfg);
            _applied = cfg.Clone();
            AppliedAt = "Применено в " + DateTime.Now.ToString("HH:mm:ss");
            this.RaisePropertyChanged(nameof(IsDirty));
            Applied?.Invoke(cfg);
        }
        catch (Exception ex)
        {
            AppliedAt = "Ошибка: " + ex.Message;
        }
    }

    private void UpdateHint()
    {
        var alive = _statusReader.IsGameAlive;
        var status = _statusReader.LastStatus;

        if (!alive)
        {
            LiveHintText = "Игра не запущена";
            return;
        }
        if (status?.MainParty == null)
        {
            LiveHintText = "Кампания не загружена (главное меню)";
            return;
        }

        var p = status.MainParty;
        LiveHintText = $"В игре сейчас: {p.MemberCount} / {p.Limit}  (база {p.VanillaLimit} + бонус {p.AppliedBonus})";
    }

    public void Dispose() => _statusReader.Dispose();
}

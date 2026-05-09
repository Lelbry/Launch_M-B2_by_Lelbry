using ReactiveUI;

namespace Launcher.ViewModels;

public sealed class FixViewModel : ViewModelBase
{
    private bool _isEnabled;

    public string Id { get; }
    public string Title { get; }
    public string Description { get; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => this.RaiseAndSetIfChanged(ref _isEnabled, value);
    }

    public FixViewModel(string id, string title, string description, bool enabled)
    {
        Id = id;
        Title = title;
        Description = description;
        _isEnabled = enabled;
    }
}

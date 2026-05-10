using System.Text.Json.Serialization;

namespace Launcher.Models;

public sealed class LiveTuningConfig
{
    [JsonPropertyName("partySizeBonus")]
    public int PartySizeBonus { get; set; } = 50;

    [JsonPropertyName("fullLootEnabled")]
    public bool FullLootEnabled { get; set; } = true;

    public LiveTuningConfig Clone() => new()
    {
        PartySizeBonus = PartySizeBonus,
        FullLootEnabled = FullLootEnabled
    };

    public bool Equals(LiveTuningConfig? other) =>
        other != null && PartySizeBonus == other.PartySizeBonus && FullLootEnabled == other.FullLootEnabled;
}

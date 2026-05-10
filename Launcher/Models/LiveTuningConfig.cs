using System.Text.Json.Serialization;

namespace Launcher.Models;

public sealed class LiveTuningConfig
{
    [JsonPropertyName("partySizeBonus")]
    public int PartySizeBonus { get; set; } = 50;

    [JsonPropertyName("fullLootEnabled")]
    public bool FullLootEnabled { get; set; } = true;

    [JsonPropertyName("fullLootMultiplier")]
    public int FullLootMultiplier { get; set; } = 10;

    public LiveTuningConfig Clone() => new()
    {
        PartySizeBonus = PartySizeBonus,
        FullLootEnabled = FullLootEnabled,
        FullLootMultiplier = FullLootMultiplier
    };

    public bool Equals(LiveTuningConfig? other) =>
        other != null
        && PartySizeBonus == other.PartySizeBonus
        && FullLootEnabled == other.FullLootEnabled
        && FullLootMultiplier == other.FullLootMultiplier;
}

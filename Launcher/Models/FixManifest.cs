using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Launcher.Models;

public sealed class FixManifest
{
    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("fixes")]
    public List<FixManifestEntry> Fixes { get; set; } = new();
}

public sealed class FixManifestEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("defaultEnabled")]
    public bool DefaultEnabled { get; set; }
}

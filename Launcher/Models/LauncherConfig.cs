using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Launcher.Models;

public enum LaunchMethod
{
    Direct = 0,
    Steam = 1
}

public sealed class LauncherConfig
{
    [JsonPropertyName("gamePath")]
    public string? GamePath { get; set; }

    [JsonPropertyName("launchMethod")]
    public LaunchMethod LaunchMethod { get; set; } = LaunchMethod.Direct;

    [JsonPropertyName("enabledFixIds")]
    public List<string> EnabledFixIds { get; set; } = new();

    [JsonPropertyName("liveTuning")]
    public LiveTuningConfig LiveTuning { get; set; } = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string ConfigDir
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Launch_M-B2_by_Lelbry");
        }
    }

    public static string ConfigPath => Path.Combine(ConfigDir, "launcher.json");

    public static LauncherConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return new LauncherConfig();
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<LauncherConfig>(json) ?? new LauncherConfig();
        }
        catch
        {
            return new LauncherConfig();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOpts));
    }
}

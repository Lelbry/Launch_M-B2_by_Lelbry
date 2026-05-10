using System.IO;
using System.Text.Json;
using Launcher.Models;

namespace Launcher.Services;

public static class LiveConfigDeployer
{
    public const string FileName = "live.json";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true
    };

    public static string GetTargetPath(string gameDir)
    {
        var modDir = Path.Combine(GamePathResolver.GetModulesDir(gameDir), ModDeployer.ModuleId);
        return Path.Combine(modDir, FileName);
    }

    /// <summary>
    /// Atomically writes live.json into the deployed mod folder.
    /// Creates the folder if it doesn't yet exist (deploy may not have been run).
    /// </summary>
    public static void Write(string gameDir, LiveTuningConfig cfg)
    {
        var path = GetTargetPath(gameDir);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(cfg, JsonOpts));

        if (File.Exists(path)) File.Delete(path);
        File.Move(tmp, path);
    }

    public static LiveTuningConfig? Read(string gameDir)
    {
        var path = GetTargetPath(gameDir);
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<LiveTuningConfig>(json);
        }
        catch
        {
            return null;
        }
    }
}

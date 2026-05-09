using System.IO;

namespace Launcher.Services;

public static class GamePathResolver
{
    private const string DefaultGamePath = @"E:\Top Programs\Steam\steamapps\common\Mount & Blade II Bannerlord";

    public static bool IsValidGameDir(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var exe = Path.Combine(path, "bin", "Win64_Shipping_Client", "Bannerlord.exe");
        return File.Exists(exe);
    }

    public static string? ResolveDefault()
    {
        return IsValidGameDir(DefaultGamePath) ? DefaultGamePath : null;
    }

    public static string GetExePath(string gameDir) =>
        Path.Combine(gameDir, "bin", "Win64_Shipping_Client", "Bannerlord.exe");

    public static string GetModulesDir(string gameDir) =>
        Path.Combine(gameDir, "Modules");
}

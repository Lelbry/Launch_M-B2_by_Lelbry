using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using Launcher.Models;

namespace Launcher.Services;

public static class ModDeployer
{
    public const string ModuleId = "LelbryBalanceFixes";
    private const string EnabledFile = "enabled.json";

    public static string ResolveModSourceDir()
    {
        var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrEmpty(asmDir)) throw new InvalidOperationException("Cannot resolve launcher dir.");

        var dir = new DirectoryInfo(asmDir);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "Module", ModuleId, "bin", "Debug");
            if (File.Exists(Path.Combine(candidate, ModuleId + ".dll"))) return candidate;
            var releaseCandidate = Path.Combine(dir.FullName, "Module", ModuleId, "bin", "Release");
            if (File.Exists(Path.Combine(releaseCandidate, ModuleId + ".dll"))) return releaseCandidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            $"Не найдена собранная DLL мода. Соберите проект {ModuleId} (dotnet build).");
    }

    /// <summary>
    /// True if any Bannerlord process is currently running. Used to refuse a redeploy that
    /// would otherwise fail with "file in use" mid-copy.
    /// </summary>
    public static bool IsBannerlordRunning()
    {
        try
        {
            return Process.GetProcessesByName("Bannerlord").Length > 0
                || Process.GetProcessesByName("Bannerlord.Native").Length > 0;
        }
        catch { return false; }
    }

    public static void Deploy(string gameDir, IEnumerable<string> enabledFixIds, LiveTuningConfig? liveTuning = null)
    {
        if (IsBannerlordRunning())
        {
            throw new InvalidOperationException(
                "Bannerlord сейчас запущен — закрой игру перед повторным деплоем мода. " +
                "(Файлы мода заблокированы, перезапись DLL невозможна.)");
        }

        var modSrc = ResolveModSourceDir();
        var modulesDir = GamePathResolver.GetModulesDir(gameDir);
        var modDest = Path.Combine(modulesDir, ModuleId);
        var binDest = Path.Combine(modDest, "bin", "Win64_Shipping_Client");

        DeleteWithRetry(modDest);

        Directory.CreateDirectory(binDest);

        File.Copy(Path.Combine(modSrc, "SubModule.xml"), Path.Combine(modDest, "SubModule.xml"), true);
        File.Copy(Path.Combine(modSrc, "manifest.json"), Path.Combine(modDest, "manifest.json"), true);

        foreach (var dll in Directory.EnumerateFiles(modSrc, "*.dll"))
        {
            var dest = Path.Combine(binDest, Path.GetFileName(dll));
            File.Copy(dll, dest, true);
        }

        foreach (var pdb in Directory.EnumerateFiles(modSrc, "*.pdb"))
        {
            var dest = Path.Combine(binDest, Path.GetFileName(pdb));
            File.Copy(pdb, dest, true);
        }

        var enabledList = enabledFixIds.ToList();
        var enabledJsonPath = Path.Combine(modDest, EnabledFile);
        File.WriteAllText(enabledJsonPath, JsonSerializer.Serialize(enabledList));

        if (liveTuning != null)
        {
            LiveConfigDeployer.Write(gameDir, liveTuning);
        }
    }

    /// <summary>
    /// Robust recursive delete: clears read-only bits, retries on transient locks
    /// (OneDrive sync, antivirus scan, file-system handles still in flight).
    /// </summary>
    private static void DeleteWithRetry(string path)
    {
        if (!Directory.Exists(path)) return;

        const int maxAttempts = 6;
        Exception? last = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                ClearReadOnlyAttributes(path);
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (Exception ex)
            {
                last = ex;
                Thread.Sleep(150 * attempt); // back off: 150 / 300 / 450 / 600 / 750 / 900ms
            }
        }

        throw new IOException(
            $"Не удалось удалить старую папку мода ({path}) за {maxAttempts} попыток: " +
            (last?.Message ?? "unknown"),
            last);
    }

    private static void ClearReadOnlyAttributes(string root)
    {
        try
        {
            var di = new DirectoryInfo(root);
            di.Attributes &= ~FileAttributes.ReadOnly;
            foreach (var f in di.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                try { f.Attributes &= ~FileAttributes.ReadOnly; } catch { }
            }
            foreach (var d in di.EnumerateDirectories("*", SearchOption.AllDirectories))
            {
                try { d.Attributes &= ~FileAttributes.ReadOnly; } catch { }
            }
        }
        catch { /* best-effort */ }
    }
}

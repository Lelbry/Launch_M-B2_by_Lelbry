using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

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

    public static void Deploy(string gameDir, IEnumerable<string> enabledFixIds)
    {
        var modSrc = ResolveModSourceDir();
        var modulesDir = GamePathResolver.GetModulesDir(gameDir);
        var modDest = Path.Combine(modulesDir, ModuleId);
        var binDest = Path.Combine(modDest, "bin", "Win64_Shipping_Client");

        if (Directory.Exists(modDest))
        {
            try { Directory.Delete(modDest, recursive: true); }
            catch (Exception ex) { throw new IOException($"Не удалось удалить старую папку мода ({modDest}): {ex.Message}", ex); }
        }

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
    }
}

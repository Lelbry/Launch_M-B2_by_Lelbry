using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Launcher.Models;

namespace Launcher.Services;

public static class ManifestLoader
{
    public static FixManifest? Load()
    {
        try
        {
            var modSrc = ModDeployer.ResolveModSourceDir();
            var path = Path.Combine(modSrc, "manifest.json");
            if (!File.Exists(path))
            {
                var srcPath = Path.GetFullPath(Path.Combine(modSrc, "..", "..", "manifest.json"));
                if (File.Exists(srcPath)) path = srcPath;
                else return null;
            }
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<FixManifest>(json);
        }
        catch
        {
            return null;
        }
    }
}

using System.Diagnostics;
using Launcher.Models;

namespace Launcher.Services;

public static class GameLauncher
{
    private const string SteamAppId = "261550";

    // NOTE: Bannerlord module IDs are case-sensitive. The folder is "SandBox" with capital 'B',
    // and so is its Id in SubModule.xml. Passing "Sandbox" silently skips the module, which then
    // breaks any dependent modules (incl. ours).
    private static readonly string[] DefaultModules =
    {
        "Native", "SandBoxCore", "SandBox", "StoryMode", "CustomBattle",
        ModDeployer.ModuleId
    };

    public static void Launch(string gameDir, LaunchMethod method)
    {
        switch (method)
        {
            case LaunchMethod.Direct:
                LaunchDirect(gameDir);
                break;
            case LaunchMethod.Steam:
                LaunchViaSteam();
                break;
        }
    }

    private static void LaunchDirect(string gameDir)
    {
        var exe = GamePathResolver.GetExePath(gameDir);
        var modules = "_MODULES_*" + string.Join("*", DefaultModules) + "*_MODULES_";
        var args = $"/singleplayer {modules}";
        var psi = new ProcessStartInfo(exe, args)
        {
            UseShellExecute = false,
            WorkingDirectory = System.IO.Path.GetDirectoryName(exe) ?? string.Empty
        };
        Process.Start(psi);
    }

    private static void LaunchViaSteam()
    {
        var psi = new ProcessStartInfo($"steam://rungameid/{SteamAppId}")
        {
            UseShellExecute = true
        };
        Process.Start(psi);
    }
}

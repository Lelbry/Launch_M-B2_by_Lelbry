using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Launcher.Models;
using Microsoft.Win32;

namespace Launcher.Services;

public static class GameLauncher
{
    private const string SteamAppId = "261550";
    private const string OurModVersion = "v0.2.0";

    // Used only as a last-resort fallback when LauncherData.xml is missing or empty.
    // In practice the user's real list is always read at launch time.
    private static readonly string[] FallbackModules =
    {
        "Native", "SandBoxCore", "Sandbox", "StoryMode", "CustomBattle",
        ModDeployer.ModuleId
    };

    /// <summary>
    /// Standard launch — plays with our mod active.
    /// </summary>
    public static void Launch(string gameDir, LaunchMethod method)
        => LaunchInternal(gameDir, method, withOurMod: true);

    /// <summary>
    /// Vanilla launch — plays the user's existing modules WITHOUT our mod added on top.
    /// Useful for old saves that don't tolerate adding new modules to their module list:
    /// Bannerlord's "module mismatch" reconciliation can stall on big saves, regardless of
    /// whether our mod's patches are enabled. Use this to play those saves untouched.
    /// </summary>
    public static void LaunchWithoutOurMod(string gameDir, LaunchMethod method)
        => LaunchInternal(gameDir, method, withOurMod: false);

    private static void LaunchInternal(string gameDir, LaunchMethod method, bool withOurMod)
    {
        if (withOurMod)
        {
            // Make sure the official launcher knows about our mod and treats it as enabled.
            // This matters for the Steam path (which uses LauncherData.xml directly) and is
            // harmless for the Direct path.
            LauncherDataReader.EnsureSingleplayerModuleEnabled(ModDeployer.ModuleId, OurModVersion);
        }
        else
        {
            // Vanilla path — make sure our mod is OFF in LauncherData.xml so the Steam route
            // also gives an unmodded experience.
            LauncherDataReader.SetSingleplayerModuleEnabled(ModDeployer.ModuleId, enabled: false);
        }

        switch (method)
        {
            case LaunchMethod.Direct:
                LaunchDirect(gameDir, withOurMod);
                break;
            case LaunchMethod.Steam:
                LaunchViaSteam();
                break;
        }
    }

    private static void LaunchDirect(string gameDir, bool withOurMod)
    {
        var modules = BuildEffectiveModuleList(withOurMod);
        var exe = GamePathResolver.GetExePath(gameDir);
        var arg = "/singleplayer _MODULES_*" + string.Join("*", modules) + "*_MODULES_";
        var psi = new ProcessStartInfo(exe, arg)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(exe) ?? string.Empty
        };
        Process.Start(psi);
    }

    /// <summary>
    /// Reads the user's actual mod selection from LauncherData.xml so saves keep loading
    /// (e.g. NavalDLC, BirthAndDeath, FastMode etc. that we don't know about).
    /// If <paramref name="includeOurMod"/> is true, appends LelbryBalanceFixes when missing;
    /// otherwise strips it out, so existing saves can load untouched.
    /// </summary>
    private static List<string> BuildEffectiveModuleList(bool includeOurMod)
    {
        var user = LauncherDataReader.ReadEnabledSingleplayerModules();
        if (user.Count == 0)
        {
            user = new List<string>(FallbackModules);
        }

        if (includeOurMod)
        {
            var contains = false;
            foreach (var m in user)
                if (string.Equals(m, ModDeployer.ModuleId, StringComparison.Ordinal)) { contains = true; break; }
            if (!contains) user.Add(ModDeployer.ModuleId);
        }
        else
        {
            user.RemoveAll(m => string.Equals(m, ModDeployer.ModuleId, StringComparison.Ordinal));
        }
        return user;
    }

    private static void LaunchViaSteam()
    {
        // Preferred: invoke steam.exe -applaunch directly. URI handlers can be flaky depending
        // on Default-Apps registration, OneDrive sandboxing, etc.
        var steamExe = ResolveSteamExe();
        if (!string.IsNullOrEmpty(steamExe) && File.Exists(steamExe))
        {
            var psi = new ProcessStartInfo(steamExe!, "-applaunch " + SteamAppId)
            {
                UseShellExecute = false
            };
            Process.Start(psi);
            return;
        }

        // Fallback to the URI handler.
        Process.Start(new ProcessStartInfo("steam://rungameid/" + SteamAppId)
        {
            UseShellExecute = true
        });
    }

    private static string? ResolveSteamExe()
    {
        try
        {
            // HKCU is the most reliable for the current user's Steam install.
            var v = Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\Valve\Steam", "SteamExe", null) as string;
            if (!string.IsNullOrEmpty(v)) return v;
            v = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string;
            if (!string.IsNullOrEmpty(v)) return Path.Combine(v, "steam.exe");
            v = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null) as string;
            if (!string.IsNullOrEmpty(v)) return Path.Combine(v, "steam.exe");
        }
        catch { }
        return null;
    }
}

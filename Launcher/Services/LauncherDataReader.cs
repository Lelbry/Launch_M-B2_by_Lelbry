using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Launcher.Services;

/// <summary>
/// Reads/writes %USERPROFILE%/Documents/Mount and Blade II Bannerlord/Configs/LauncherData.xml —
/// the file the official TaleWorlds Launcher uses to remember which modules the user enabled
/// and in what order. We honour that list so the user's existing save (built around their
/// modules) keeps working.
/// </summary>
public static class LauncherDataReader
{
    public static string GetPath()
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(docs, "Mount and Blade II Bannerlord", "Configs", "LauncherData.xml");
    }

    /// <summary>
    /// Returns the IDs of the user's currently-enabled singleplayer modules in their saved order.
    /// Returns an empty list if the file is missing or malformed.
    /// </summary>
    public static List<string> ReadEnabledSingleplayerModules()
    {
        var result = new List<string>();
        var path = GetPath();
        if (!File.Exists(path)) return result;

        try
        {
            var doc = new XmlDocument();
            doc.Load(path);
            var nodes = doc.SelectNodes("/UserData/SingleplayerData/ModDatas/UserModData");
            if (nodes == null) return result;

            foreach (XmlNode node in nodes)
            {
                var id = node.SelectSingleNode("Id")?.InnerText;
                var sel = node.SelectSingleNode("IsSelected")?.InnerText;
                if (string.IsNullOrEmpty(id)) continue;
                if (string.Equals(sel, "true", StringComparison.OrdinalIgnoreCase))
                    result.Add(id);
            }
        }
        catch
        {
            // best-effort — return what we got, fall back to defaults at the call site
        }
        return result;
    }

    /// <summary>
    /// Atomically updates LauncherData.xml so that the given module ID is present and selected
    /// in the singleplayer mod list. Used to let the Steam launch path (which goes through the
    /// official TaleWorlds Launcher) see our mod with the box already ticked.
    /// </summary>
    public static void EnsureSingleplayerModuleEnabled(string moduleId, string version)
        => SetSingleplayerModule(moduleId, enabled: true, version: version);

    /// <summary>
    /// Sets IsSelected for the given module; doesn't add an entry if it doesn't exist.
    /// Used by the "Launch without our mod" path so the Steam route also sees us unchecked.
    /// </summary>
    public static void SetSingleplayerModuleEnabled(string moduleId, bool enabled)
        => SetSingleplayerModule(moduleId, enabled, version: null);

    private static void SetSingleplayerModule(string moduleId, bool enabled, string? version)
    {
        var path = GetPath();
        if (!File.Exists(path)) return;

        try
        {
            var doc = new XmlDocument { PreserveWhitespace = true };
            doc.Load(path);
            var modDatas = doc.SelectSingleNode("/UserData/SingleplayerData/ModDatas") as XmlElement;
            if (modDatas == null) return;

            XmlElement? ours = null;
            foreach (XmlNode node in modDatas.ChildNodes)
            {
                if (node is not XmlElement el) continue;
                var id = el.SelectSingleNode("Id")?.InnerText;
                if (string.Equals(id, moduleId, StringComparison.Ordinal)) { ours = el; break; }
            }

            if (ours == null)
            {
                if (!enabled) return; // nothing to do — module not registered, leaving as-is
                ours = doc.CreateElement("UserModData");
                AppendChild(doc, ours, "Id", moduleId);
                AppendChild(doc, ours, "LastKnownVersion", version ?? "v0.1.0");
                AppendChild(doc, ours, "IsSelected", "true");
                modDatas.AppendChild(ours);
            }
            else
            {
                var sel = ours.SelectSingleNode("IsSelected") as XmlElement;
                var value = enabled ? "true" : "false";
                if (sel == null) AppendChild(doc, ours, "IsSelected", value);
                else sel.InnerText = value;
            }

            var tmp = path + ".tmp";
            doc.Save(tmp);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }
        catch
        {
            // non-fatal — Steam path will just show the mod unchecked; user can tick it once
        }
    }

    private static void AppendChild(XmlDocument doc, XmlElement parent, string name, string value)
    {
        var el = doc.CreateElement(name);
        el.InnerText = value;
        parent.AppendChild(el);
    }
}

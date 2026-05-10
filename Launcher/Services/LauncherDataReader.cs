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
                ours = doc.CreateElement("UserModData");
                AppendChild(doc, ours, "Id", moduleId);
                AppendChild(doc, ours, "LastKnownVersion", version);
                AppendChild(doc, ours, "IsSelected", "true");
                modDatas.AppendChild(ours);
            }
            else
            {
                var sel = ours.SelectSingleNode("IsSelected") as XmlElement;
                if (sel == null) AppendChild(doc, ours, "IsSelected", "true");
                else sel.InnerText = "true";
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

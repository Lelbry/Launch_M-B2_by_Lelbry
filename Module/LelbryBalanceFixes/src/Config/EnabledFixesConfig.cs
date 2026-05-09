using System;
using System.Collections.Generic;
using System.IO;

namespace LelbryBalanceFixes
{
    internal static class EnabledFixesConfig
    {
        public const string FileName = "enabled.json";

        public static HashSet<string> Load(string moduleDir)
        {
            var path = Path.Combine(moduleDir, FileName);
            if (!File.Exists(path))
            {
                ModLog.Info($"{FileName} not found at {path} — no fixes will be applied.");
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                var raw = File.ReadAllText(path);
                var ids = MiniJson.ParseStringArray(raw);
                return new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                ModLog.Error($"Failed to read {FileName}: {ex.Message}");
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }
}

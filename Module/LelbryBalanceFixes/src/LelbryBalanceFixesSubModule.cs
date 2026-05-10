using System;
using System.IO;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace LelbryBalanceFixes
{
    public sealed class LelbryBalanceFixesSubModule : MBSubModuleBase
    {
        private const string HarmonyId = "lelbry.balance.fixes";

        private Harmony _harmony;
        private string _moduleDir;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            try
            {
                _moduleDir = ResolveModuleDir();
                LiveConfig.Init(_moduleDir);

                var enabled = EnabledFixesConfig.Load(_moduleDir);
                _harmony = new Harmony(HarmonyId);

                int applied = 0;
                foreach (var fix in BalanceFixesRegistry.Discover())
                {
                    if (!enabled.Contains(fix.Id)) continue;
                    try
                    {
                        fix.Apply(_harmony);
                        applied++;
                        ModLog.Info($"Applied fix '{fix.Id}'.");
                    }
                    catch (Exception ex)
                    {
                        ModLog.Error($"Failed to apply '{fix.Id}': {ex.Message}");
                    }
                }

                ModLog.Info($"Loaded. {applied} fix(es) active.");
            }
            catch (Exception ex)
            {
                ModLog.Error("OnSubModuleLoad failed: " + ex.Message);
            }
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            try
            {
                if (gameStarterObject is CampaignGameStarter starter && !string.IsNullOrEmpty(_moduleDir))
                {
                    starter.AddBehavior(new LiveStatusReporterBehavior(_moduleDir));
                    ModLog.Info("LiveStatusReporterBehavior registered.");
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("OnGameStart failed: " + ex.Message);
            }
        }

        private static string ResolveModuleDir()
        {
            var asmPath = typeof(LelbryBalanceFixesSubModule).Assembly.Location;
            var binDir = Path.GetDirectoryName(asmPath);
            if (string.IsNullOrEmpty(binDir)) return string.Empty;
            var winShipping = Path.GetDirectoryName(binDir);
            if (string.IsNullOrEmpty(winShipping)) return string.Empty;
            return Path.GetDirectoryName(winShipping) ?? string.Empty;
        }
    }
}

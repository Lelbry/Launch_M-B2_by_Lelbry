using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.GameComponents;

namespace LelbryBalanceFixes.Fixes
{
    [BalanceFix(
        id: "full_loot",
        title: "Полный лут с врагов",
        description: "Все экипированные предметы убитых врагов попадают в трофеи (шанс дропа = 100%). Включается/выключается в Live Tuning панели.")]
    public sealed class FullLootFix : IBalanceFix
    {
        public string Id => "full_loot";

        public void Apply(Harmony harmony)
        {
            var target = AccessTools.Method(
                typeof(DefaultBattleRewardModel),
                nameof(DefaultBattleRewardModel.GetLootedItemFromTroop));

            if (target == null)
            {
                ModLog.Error("DefaultBattleRewardModel.GetLootedItemFromTroop not found.");
                return;
            }

            var prefix = AccessTools.Method(typeof(FullLootFix), nameof(Prefix));
            harmony.Patch(target, prefix: new HarmonyMethod(prefix));
        }

        // The actual parameter in DefaultBattleRewardModel.GetLootedItemFromTroop is named
        // `targetValue` (verified by Bannerlord 1.3.15 metadata). Harmony binds Prefix params by
        // name, so this MUST match the game's parameter name exactly — not "lootAmount".
        public static void Prefix(ref float targetValue)
        {
            try
            {
                if (!LiveConfig.FullLootEnabled) return;
                if (targetValue < 1f) targetValue = 1f;
            }
            catch (Exception ex)
            {
                ModLog.Error("FullLootFix prefix: " + ex.Message);
            }
        }
    }
}

using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.GameComponents;

namespace LelbryBalanceFixes.Fixes
{
    [BalanceFix(
        id: "full_loot",
        title: "Полный лут с врагов",
        description: "Все экипированные предметы убитых врагов попадают в трофеи (шанс дропа = 100%).")]
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

        public static void Prefix(ref float lootAmount)
        {
            try
            {
                if (lootAmount < 1f) lootAmount = 1f;
            }
            catch (Exception ex)
            {
                ModLog.Error("FullLootFix prefix: " + ex.Message);
            }
        }
    }
}

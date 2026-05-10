using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.Core;

namespace LelbryBalanceFixes.Fixes
{
    [BalanceFix(
        id: "full_loot",
        title: "Полный лут с врагов",
        description: "Сильно увеличивает количество лута, выпадающего после битвы. Множитель и тумблер регулируются в Live Tuning. Дорогая экипировка падает в приоритете.")]
    public sealed class FullLootFix : IBalanceFix
    {
        public string Id => "full_loot";

        public void Apply(Harmony harmony)
        {
            // Bannerlord 1.3.x swaps BattleRewardModel based on which modules are active:
            //   - DefaultBattleRewardModel       (base)
            //   - StoryModeBattleRewardModel     (campaign-with-story)
            //   - NavalDLCBattleRewardModel      (Sandbox + Naval — Lelbry's case)
            // We discover every concrete subclass at apply-time and patch each one's
            // GetLootedItemFromTroop AND GetExpectedLootedItemValueFromCasualty.

            var perItemPostfix = AccessTools.Method(typeof(FullLootFix), nameof(PerItemPostfix));
            var expectedValuePostfix = AccessTools.Method(typeof(FullLootFix), nameof(ExpectedValuePostfix));
            var baseType = typeof(BattleRewardModel);
            int patched = 0;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch { continue; }
                foreach (var t in types)
                {
                    if (t == null || t.IsAbstract || t.IsInterface) continue;
                    if (!baseType.IsAssignableFrom(t)) continue;

                    PatchIfPresent(harmony, t, "GetLootedItemFromTroop", perItemPostfix, ref patched);
                    PatchIfPresent(harmony, t, "GetExpectedLootedItemValueFromCasualty", expectedValuePostfix, ref patched);
                }
            }

            if (patched == 0)
                ModLog.Error("FullLoot: no BattleRewardModel implementations found to patch.");
        }

        private static void PatchIfPresent(Harmony harmony, Type t, string methodName, System.Reflection.MethodInfo postfix, ref int patched)
        {
            var m = AccessTools.Method(t, methodName);
            if (m == null || m.DeclaringType != t) return;
            try
            {
                harmony.Patch(m, postfix: new HarmonyMethod(postfix));
                ModLog.Info("FullLoot: patched " + t.FullName + "." + methodName);
                patched++;
            }
            catch (Exception ex)
            {
                ModLog.Error("FullLoot: failed to patch " + t.FullName + "." + methodName + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Postfix on GetExpectedLootedItemValueFromCasualty — multiplies the expected loot
        /// value per casualty by LiveConfig.FullLootMultiplier. The game's loot loop uses this
        /// expected value as a target sum: it keeps drawing items until accumulated worth ≥ target.
        /// Bigger target → more iterations → more items per kill.
        /// </summary>
        public static void ExpectedValuePostfix(ref float __result)
        {
            try
            {
                if (!LiveConfig.FullLootEnabled) return;
                int mult = LiveConfig.FullLootMultiplier;
                if (mult <= 1) return;
                __result *= mult;
            }
            catch (Exception ex)
            {
                ModLog.Error("FullLootFix expected-value postfix: " + ex.Message);
            }
        }

        /// <summary>
        /// Postfix on GetLootedItemFromTroop — if the game returned empty, substitute a real
        /// piece. We pick the slot whose item value is CLOSEST to the targetValue the game
        /// asked for. Bannerlord's loot loop typically calls with descending targetValue (it
        /// "spends" the expected pool from biggest items first), so this naturally yields the
        /// most expensive armour/weapons in the early iterations and cheaper bits later.
        /// </summary>
        public static void PerItemPostfix(CharacterObject character, float targetValue, ref EquipmentElement __result)
        {
            try
            {
                if (!LiveConfig.FullLootEnabled) return;
                if (!__result.IsEmpty) return;
                if (character == null) return;

                var eq = character.RandomBattleEquipment ?? character.Equipment;
                if (eq == null) return;

                EquipmentElement bestMatch = default;
                int bestDiff = int.MaxValue;
                int targetInt = (int)targetValue;
                int target = targetInt > 0 ? targetInt : 1;

                for (int i = 0; i < 12; i++)
                {
                    var element = eq.GetEquipmentFromSlot((EquipmentIndex)i);
                    if (element.IsEmpty) continue;
                    int value = element.Item?.Value ?? 0;
                    int diff = Math.Abs(value - target);
                    if (diff < bestDiff)
                    {
                        bestDiff = diff;
                        bestMatch = element;
                    }
                }

                // Fallback — if no items, leave __result empty (game'll skip)
                if (bestDiff != int.MaxValue) __result = bestMatch;
            }
            catch (Exception ex)
            {
                ModLog.Error("FullLootFix per-item postfix: " + ex.Message);
            }
        }
    }
}

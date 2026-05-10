using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;

namespace LelbryBalanceFixes.Fixes
{
    [BalanceFix(
        id: "full_loot",
        title: "Полный лут с врагов",
        description: "Каждый убитый враг роняет до N предметов из своей экипировки (N = значение в Live Tuning). В первую очередь падает дорогая броня и оружие.")]
    public sealed class FullLootFix : IBalanceFix
    {
        public string Id => "full_loot";

        public void Apply(Harmony harmony)
        {
            // Two patches working together:
            //
            //  A) MapEvent.LootCasualtyCharacter — Prefix.
            //     This is the actual loot loop: for each casualty, the game calls it with
            //     a parameter `maxLootedItemsPerBodyForMainParty` capping how many items
            //     can fall off this body. Vanilla typically passes 1, so you get
            //     "8 items off 12 bandits". We bump that cap to LiveConfig.FullLootMultiplier
            //     so the loop iterates many more times per casualty.
            //
            //  B) BattleRewardModel.GetLootedItemFromTroop — Postfix on every concrete
            //     implementation (Default / StoryMode / NavalDLC + future).
            //     If the per-iteration roll returned empty, we substitute the slot whose
            //     item value is closest to the targetValue the game asked for, so the
            //     loop never wastes an iteration AND the expensive gear gets pulled
            //     first (Bannerlord's targetValue starts high, descends each iteration).

            // --- A: cap-bumping prefix on the loot loop itself ---
            var lootCasualtyMethod = AccessTools.Method(typeof(MapEvent), "LootCasualtyCharacter");
            if (lootCasualtyMethod == null)
            {
                ModLog.Error("FullLoot: MapEvent.LootCasualtyCharacter not found — main loot multiplier disabled.");
            }
            else
            {
                try
                {
                    var prefix = AccessTools.Method(typeof(FullLootFix), nameof(LootCasualtyPrefix));
                    harmony.Patch(lootCasualtyMethod, prefix: new HarmonyMethod(prefix));
                    ModLog.Info("FullLoot: patched MapEvent.LootCasualtyCharacter (prefix).");
                }
                catch (Exception ex)
                {
                    ModLog.Error("FullLoot: LootCasualtyCharacter patch failed: " + ex.Message);
                }
            }

            // --- B: per-item substitution across all BattleRewardModel implementations ---
            var perItemPostfix = AccessTools.Method(typeof(FullLootFix), nameof(PerItemPostfix));
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

                    var m = AccessTools.Method(t, "GetLootedItemFromTroop");
                    if (m == null || m.DeclaringType != t) continue;
                    try
                    {
                        harmony.Patch(m, postfix: new HarmonyMethod(perItemPostfix));
                        ModLog.Info("FullLoot: patched " + t.FullName + ".GetLootedItemFromTroop");
                        patched++;
                    }
                    catch (Exception ex)
                    {
                        ModLog.Error("FullLoot: failed to patch " + t.FullName + ": " + ex.Message);
                    }
                }
            }

            if (patched == 0)
                ModLog.Error("FullLoot: no BattleRewardModel implementations found to patch.");
        }

        /// <summary>
        /// Bumps the per-body item cap to LiveConfig.FullLootMultiplier. The cap is the
        /// upper bound on how many times the loot loop iterates per casualty, so this
        /// is the actual lever for "how much loot you get".
        /// </summary>
        public static void LootCasualtyPrefix(ref int maxLootedItemsPerBodyForMainParty)
        {
            try
            {
                if (!LiveConfig.FullLootEnabled) return;
                int mult = LiveConfig.FullLootMultiplier;
                if (mult <= maxLootedItemsPerBodyForMainParty) return; // never reduce below vanilla
                maxLootedItemsPerBodyForMainParty = mult;
            }
            catch (Exception ex)
            {
                ModLog.Error("FullLootFix loot-casualty prefix: " + ex.Message);
            }
        }

        /// <summary>
        /// If the game returned empty for this iteration, substitute a real piece.
        /// We pick the slot whose item value is CLOSEST to the targetValue Bannerlord asked
        /// for; the loop calls with descending targetValue (spending the expected pool
        /// biggest-first), so this naturally pulls the most expensive armour first.
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

                if (bestDiff != int.MaxValue) __result = bestMatch;
            }
            catch (Exception ex)
            {
                ModLog.Error("FullLootFix per-item postfix: " + ex.Message);
            }
        }
    }
}

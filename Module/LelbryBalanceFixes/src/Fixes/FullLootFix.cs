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
        description: "Сильно увеличивает количество предметов, выпадающих с убитых врагов после битвы. Включается/выключается в Live Tuning.")]
    public sealed class FullLootFix : IBalanceFix
    {
        // How much to multiply Bannerlord's "expected loot value per casualty" by.
        // The game's loot loop iterates GetLootedItemFromTroop until accumulated item value
        // reaches the expected value. Multiplying by ~10 gives roughly 10x more iterations,
        // which in practice gives near-full equipment off each kill.
        private const float ValueMultiplier = 10f;

        public string Id => "full_loot";

        public void Apply(Harmony harmony)
        {
            // Bannerlord 1.3.x swaps BattleRewardModel based on which modules are active:
            //   - DefaultBattleRewardModel       (base)
            //   - StoryModeBattleRewardModel     (campaign-with-story)
            //   - NavalDLCBattleRewardModel      (Sandbox + Naval — Lelbry's case)
            // We discover every concrete subclass at apply-time and patch each one's
            // GetLootedItemFromTroop AND GetExpectedLootedItemValueFromCasualty so future
            // model overrides from other mods get covered automatically.

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
        /// value per casualty by ValueMultiplier. The game's loot loop uses this expected
        /// value as a target sum: it keeps drawing items until accumulated worth ≥ target.
        /// Multiplying the target → more iterations → more items per kill.
        /// </summary>
        public static void ExpectedValuePostfix(ref float __result)
        {
            try
            {
                if (!LiveConfig.FullLootEnabled) return;
                __result *= ValueMultiplier;
            }
            catch (Exception ex)
            {
                ModLog.Error("FullLootFix expected-value postfix: " + ex.Message);
            }
        }

        /// <summary>
        /// Postfix on GetLootedItemFromTroop — if the per-iteration roll returned an empty
        /// slot, substitute a real piece from the troop's battle equipment so the iteration
        /// isn't wasted. Combined with the bumped expected value above, every kill yields
        /// most/all of the troop's gear.
        /// </summary>
        public static void PerItemPostfix(CharacterObject character, ref EquipmentElement __result)
        {
            try
            {
                if (!LiveConfig.FullLootEnabled) return;
                if (!__result.IsEmpty) return;
                if (character == null) return;

                var eq = character.RandomBattleEquipment ?? character.Equipment;
                if (eq == null) return;

                // Random slot pick to avoid always returning the same first non-empty slot
                // (which Bannerlord's loop might dedupe if it sees the exact same element).
                int start = UnityLikeRandom() % 12;
                for (int i = 0; i < 12; i++)
                {
                    int idx = (start + i) % 12;
                    var element = eq.GetEquipmentFromSlot((EquipmentIndex)idx);
                    if (!element.IsEmpty)
                    {
                        __result = element;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("FullLootFix per-item postfix: " + ex.Message);
            }
        }

        // System.Random is shared so it's OK to use a single instance.
        private static readonly Random Rng = new Random();
        private static int UnityLikeRandom()
        {
            lock (Rng) return Rng.Next(0, 12);
        }
    }
}

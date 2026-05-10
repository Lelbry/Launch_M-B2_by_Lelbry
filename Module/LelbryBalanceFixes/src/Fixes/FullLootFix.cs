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
        description: "Гарантирует, что с каждого убитого юнита падает хотя бы одна вещь из его экипировки. Включается/выключается в Live Tuning.")]
    public sealed class FullLootFix : IBalanceFix
    {
        public string Id => "full_loot";

        public void Apply(Harmony harmony)
        {
            // Bannerlord 1.3.x has multiple BattleRewardModel implementations registered by the
            // active modules (same story as PartySizeLimitModel):
            //   - DefaultBattleRewardModel       — base (TaleWorlds.CampaignSystem)
            //   - StoryModeBattleRewardModel     — when StoryMode is loaded (campaign)
            //   - NavalDLCBattleRewardModel      — when NavalDLC is loaded (Sandbox + Naval)
            // The active model swaps depending on game mode; patching just Default missed
            // Sandbox+Naval. So we discover every concrete subclass of BattleRewardModel at
            // patch time and apply our postfix to each. Future mods adding their own model
            // implementations get covered automatically.
            var postfix = AccessTools.Method(typeof(FullLootFix), nameof(Postfix));
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
                        harmony.Patch(m, postfix: new HarmonyMethod(postfix));
                        ModLog.Info("FullLoot: patched " + t.FullName);
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
        /// If the game decided this casualty drops nothing, substitute a real item from the
        /// troop's battle equipment so every kill yields at least one piece of gear.
        /// We pick the first non-empty slot — armour/weapons in the natural slot order.
        /// </summary>
        public static void Postfix(CharacterObject character, ref EquipmentElement __result)
        {
            try
            {
                if (!LiveConfig.FullLootEnabled) return;
                if (!__result.IsEmpty) return;        // game already returned a real item
                if (character == null) return;

                var eq = character.RandomBattleEquipment ?? character.Equipment;
                if (eq == null) return;

                // Standard EquipmentIndex range is 0..11 (weapons 0-3, head/body/leg/gloves/cape,
                // horse/harness). Iterating gives a deterministic non-empty pick if any slot
                // is filled.
                for (int i = 0; i < 12; i++)
                {
                    var element = eq.GetEquipmentFromSlot((EquipmentIndex)i);
                    if (!element.IsEmpty)
                    {
                        __result = element;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("FullLootFix postfix: " + ex.Message);
            }
        }
    }
}

using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace LelbryBalanceFixes.Fixes
{
    [BalanceFix(
        id: "party_size_boost",
        title: "Расширение лимита отряда",
        description: "Добавляет настраиваемый бонус к лимиту главного отряда. Размер регулируется в Live Tuning.")]
    public sealed class PartySizeBoostFix : IBalanceFix
    {
        private const int MinTotal = 5;
        public string Id => "party_size_boost";

        public void Apply(Harmony harmony)
        {
            // We patch PartyBase.PartySizeLimitExplainer (the ExplainedNumber getter) instead of
            // a specific *Model.GetPartyMemberSizeLimit override, because Bannerlord swaps the
            // active PartySizeLimitModel based on game mode + active modules:
            //   - StoryMode → StoryModePartySizeLimitModel (campaign with story)
            //   - NavalDLC active → NavalDLCPartySizeLimitModel (Sandbox-mode user)
            //   - else → DefaultPartySizeLimitModel
            // Patching only Default missed Sandbox + NavalDLC saves entirely. By patching the
            // consumer-side getter on PartyBase, we catch every code path through PartySizeLimit
            // regardless of which model is currently registered.
            var target = AccessTools.PropertyGetter(typeof(PartyBase), nameof(PartyBase.PartySizeLimitExplainer));
            if (target == null)
            {
                ModLog.Error("PartyBase.PartySizeLimitExplainer getter not found.");
                return;
            }

            var postfix = AccessTools.Method(typeof(PartySizeBoostFix), nameof(Postfix));
            harmony.Patch(target, postfix: new HarmonyMethod(postfix));
        }

        public static void Postfix(PartyBase __instance, ref ExplainedNumber __result)
        {
            try
            {
                if (__instance == null || !__instance.IsMobile) return;
                if (__instance.MobileParty == null || !__instance.MobileParty.IsMainParty) return;

                int bonus = LiveConfig.PartySizeBonus;
                if (bonus == 0) return;

                // min-clamp — never let the resulting limit drop below MinTotal
                float current = __result.ResultNumber;
                if (current + bonus < MinTotal)
                    bonus = (int)(MinTotal - current);

                __result.Add(bonus, BoostText, null);
            }
            catch (Exception ex)
            {
                ModLog.Error("PartySizeBoostFix postfix: " + ex.Message);
            }
        }

        private static readonly TextObject BoostText = new TextObject("Lelbry: Party size boost");
    }
}

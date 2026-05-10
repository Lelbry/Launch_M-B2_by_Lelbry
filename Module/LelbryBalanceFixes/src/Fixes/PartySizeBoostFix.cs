using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace LelbryBalanceFixes.Fixes
{
    [BalanceFix(
        id: "party_size_boost",
        title: "Расширение лимита отряда",
        description: "Добавляет настраиваемый бонус к максимальному размеру отряда игрока. Регулируется в Live Tuning панели лаунчера.")]
    public sealed class PartySizeBoostFix : IBalanceFix
    {
        private const int MinTotal = 5;
        public string Id => "party_size_boost";

        public void Apply(Harmony harmony)
        {
            var target = AccessTools.Method(
                typeof(DefaultPartySizeLimitModel),
                nameof(DefaultPartySizeLimitModel.GetPartyMemberSizeLimit));

            if (target == null)
            {
                ModLog.Error("DefaultPartySizeLimitModel.GetPartyMemberSizeLimit not found.");
                return;
            }

            var postfix = AccessTools.Method(typeof(PartySizeBoostFix), nameof(Postfix));
            harmony.Patch(target, postfix: new HarmonyMethod(postfix));
        }

        public static void Postfix(PartyBase party, ref ExplainedNumber __result)
        {
            try
            {
                if (party == null || !party.IsMobile) return;
                if (party.MobileParty == null || !party.MobileParty.IsMainParty) return;

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

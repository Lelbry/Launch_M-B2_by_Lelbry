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
        title: "Расширение лимита отряда (+50)",
        description: "Добавляет +50 к максимальному размеру отряда игрока.")]
    public sealed class PartySizeBoostFix : IBalanceFix
    {
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
                if (party != null && party.IsMobile && party.MobileParty != null && party.MobileParty.IsMainParty)
                {
                    __result.Add(50f, BoostText, null);
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("PartySizeBoostFix postfix: " + ex.Message);
            }
        }

        private static readonly TextObject BoostText = new TextObject("Lelbry: Party size boost");
    }
}

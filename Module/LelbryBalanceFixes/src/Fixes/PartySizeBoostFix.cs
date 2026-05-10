using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

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
            // Why this target: Bannerlord 1.3.x has multiple PartySizeLimitModel implementations
            // (Default / StoryMode / NavalDLC) that the campaign swaps in based on game mode and
            // active modules. Patching any one of them silently misses other game modes.
            //
            // Earlier we tried PartyBase.PartySizeLimitExplainer (ExplainedNumber). It works for
            // tooltips but the int property PartyBase.PartySizeLimit is computed by a *separate*
            // call to the model with includeDescriptions=false — it does NOT read from the
            // Explainer, so our Explainer postfix didn't propagate to the displayed number.
            //
            // Patching the int getter directly is the only single-spot fix that the UI actually
            // sees. We add the bonus to __result here; the explanation tooltip won't show our
            // contribution (small UX cost) but the limit number is consistent everywhere.
            var target = AccessTools.PropertyGetter(typeof(PartyBase), nameof(PartyBase.PartySizeLimit));
            if (target == null)
            {
                ModLog.Error("PartyBase.PartySizeLimit getter not found.");
                return;
            }

            var postfix = AccessTools.Method(typeof(PartySizeBoostFix), nameof(Postfix));
            harmony.Patch(target, postfix: new HarmonyMethod(postfix));
            ModLog.Info("PartySizeBoostFix: patched PartyBase.PartySizeLimit getter.");
        }

        public static void Postfix(PartyBase __instance, ref int __result)
        {
            try
            {
                if (__instance == null || !__instance.IsMobile) return;
                if (__instance.MobileParty == null || !__instance.MobileParty.IsMainParty) return;

                int bonus = LiveConfig.PartySizeBonus;
                if (bonus == 0) return;

                int newLimit = __result + bonus;
                if (newLimit < MinTotal) newLimit = MinTotal;
                __result = newLimit;
            }
            catch (Exception ex)
            {
                ModLog.Error("PartySizeBoostFix postfix: " + ex.Message);
            }
        }
    }
}

using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using SpireChangelog.Data;
using SpireChangelog.Helpers;

namespace SpireChangelog.Patches;

/// <summary>
/// Appends changelog tooltips to shop card hover tips.
/// </summary>
[HarmonyPatch(typeof(NMerchantCard), "CreateHoverTip")]
public static class MerchantCardHoverTipPatch
{
    static void Postfix(NMerchantCard __instance)
    {
        if (ChangelogDatabase.Instance == null) return;
        ChangelogDatabase.Instance.EnsureNameLookups();

        var cardNodeField = AccessTools.Field(typeof(NMerchantCard), "_cardNode");
        var cardNode = cardNodeField?.GetValue(__instance) as NCard;
        var card = cardNode?.Model;
        if (card == null) return;

        var changes = ChangelogDatabase.Instance.GetCardChanges(card.Id.Entry);
        if (changes.Count == 0) return;

        ChangelogUiHelper.AppendChangelogTips(__instance, changes);
    }
}

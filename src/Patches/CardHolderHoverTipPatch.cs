using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using SpireChangelog.Data;
using SpireChangelog.Helpers;

namespace SpireChangelog.Patches;

/// <summary>
/// Appends changelog tooltips to card hover tips on reward screens, shop, etc.
/// This shows patch history when hovering a card without needing to right-click inspect.
/// </summary>
[HarmonyPatch(typeof(NCardHolder), "CreateHoverTips")]
public static class CardHolderHoverTipPatch
{
    static void Postfix(NCardHolder __instance)
    {
        if (ChangelogDatabase.Instance == null) return;
        ChangelogDatabase.Instance.EnsureNameLookups();

        var card = __instance.CardNode?.Model;
        if (card == null) return;

        var changes = ChangelogDatabase.Instance.GetCardChanges(card.Id.Entry);
        if (changes.Count == 0) return;

        ChangelogUiHelper.AppendChangelogTips(__instance, changes);
    }
}

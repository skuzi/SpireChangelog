using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens;
using SpireChangelog.Data;
using SpireChangelog.Helpers;

namespace SpireChangelog.Patches;

[HarmonyPatch(typeof(NInspectCardScreen), "UpdateCardDisplay")]
public static class InspectCardPatch
{
    static void Postfix(NInspectCardScreen __instance)
    {
        ModEntry.TryRegisterConsoleCommands();

        if (ChangelogDatabase.Instance == null) return;
        ChangelogDatabase.Instance.EnsureNameLookups();

        var cardsField = AccessTools.Field(typeof(NInspectCardScreen), "_cards");
        var indexField = AccessTools.Field(typeof(NInspectCardScreen), "_index");
        if (cardsField == null || indexField == null) return;

        var cards = cardsField.GetValue(__instance) as List<CardModel>;
        var index = (int)(indexField.GetValue(__instance) ?? 0);
        if (cards == null || index < 0 || index >= cards.Count) return;

        var card = cards[index];
        var changes = ChangelogDatabase.Instance.GetCardChanges(card.Id.Entry);
        if (changes.Count == 0) return;

        ChangelogUiHelper.AppendChangelogTips(__instance, changes);
    }
}

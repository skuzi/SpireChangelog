using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using SpireChangelog.Data;

namespace SpireChangelog.Patches;

[HarmonyPatch(typeof(NCardLibrary), "_Ready")]
public static class CardLibraryPatch
{
    private static bool _filterActive;
    public static bool IsFilterActive => _filterActive;

    internal static void ApplyChangelogFilter(ref Func<CardModel, bool> filter)
    {
        if (!_filterActive || ChangelogDatabase.Instance == null) return;
        var orig = filter;
        var db = ChangelogDatabase.Instance;
        filter = card => orig(card) && db.GetCardChanges(card.Id.Entry).Count > 0;
    }

    static void Postfix(NCardLibrary __instance)
    {
        _filterActive = false;
        ModEntry.TryRegisterConsoleCommands();

        try
        {
            // Use Multiplayer Cards as template — same style
            var template = __instance.GetNodeOrNull<NLibraryStatTickbox>("%MultiplayerCards");
            if (template == null) return;

            var parent = template.GetParent();
            if (parent == null || parent.HasMeta("SpireChangelog")) return;
            parent.SetMeta("SpireChangelog", true);

            // Instantiate from the same scene the template came from
            var tickbox = (NLibraryStatTickbox)template.Duplicate();
            tickbox.UniqueNameInOwner = false;
            tickbox.Name = "RecentlyChanged";

            // Insert above Upgrades
            var upgrades = __instance.GetNodeOrNull("%Upgrades");
            parent.AddChild(tickbox);
            if (upgrades != null)
                parent.MoveChild(tickbox, upgrades.GetIndex());

            tickbox.SetLabel("Recently Changed");
            tickbox.IsTicked = false;

            // Replace duplicated signal with ours
            foreach (var c in tickbox.GetSignalConnectionList(NTickbox.SignalName.Toggled))
            {
                try { tickbox.Disconnect(NTickbox.SignalName.Toggled, c["callable"].AsCallable()); }
                catch { }
            }

            tickbox.Connect(NTickbox.SignalName.Toggled, Callable.From<NTickbox>(_ =>
            {
                _filterActive = tickbox.IsTicked;
                try
                {
                    var grid = AccessTools.Field(typeof(NCardLibrary), "_grid")?.GetValue(__instance);
                    var filter = AccessTools.Field(typeof(NCardLibrary), "_filter")?.GetValue(__instance);
                    var sort = AccessTools.Field(typeof(NCardLibrary), "_sortingPriority")?.GetValue(__instance);
                    if (grid != null && filter != null && sort != null)
                    {
                        AccessTools.Method(typeof(NCardLibraryGrid), "FilterCards",
                            new[] { typeof(Func<CardModel, bool>), typeof(List<SortingOrders>) })
                            ?.Invoke(grid, new[] { filter, sort });
                    }
                }
                catch { }
            }));
        }
        catch (Exception ex)
        {
            Log.Error($"[SpireChangelog] CardLibrary: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(NCardLibraryGrid), nameof(NCardLibraryGrid.FilterCards),
    new[] { typeof(Func<CardModel, bool>), typeof(List<SortingOrders>) })]
public static class CardLibraryFilterPatch2
{
    static void Prefix(ref Func<CardModel, bool> filter) => CardLibraryPatch.ApplyChangelogFilter(ref filter);
}

[HarmonyPatch(typeof(NCardLibraryGrid), nameof(NCardLibraryGrid.FilterCards),
    new[] { typeof(Func<CardModel, bool>) })]
public static class CardLibraryFilterPatch1
{
    static void Prefix(ref Func<CardModel, bool> filter) => CardLibraryPatch.ApplyChangelogFilter(ref filter);
}

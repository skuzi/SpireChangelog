using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.addons.mega_text;
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

        // The game creates hover tips with __instance as owner (line 289 in decompiled)
        var activeField = AccessTools.Field(typeof(NHoverTipSet), "_activeHoverTips");
        if (activeField?.GetValue(null) is not Dictionary<Control, NHoverTipSet> activeHoverTips)
            return;

        if (!activeHoverTips.TryGetValue(__instance, out var hoverTipSet))
            return;

        var containerField = AccessTools.Field(typeof(NHoverTipSet), "_textHoverTipContainer");
        var container = containerField?.GetValue(hoverTipSet) as Control;
        if (container == null) return;

        if (container.HasMeta("SpireChangelog_Added")) return;
        container.SetMeta("SpireChangelog_Added", true);

        try
        {
            string? energyPrefix = null;
            try { energyPrefix = EnergyIconHelper.GetPrefix(card); } catch { }

            var tipScene = PreloadManager.Cache.GetScene("res://scenes/ui/hover_tip.tscn");

            // One tooltip per patch version
            foreach (var change in changes)
            {
                var tipNode = tipScene.Instantiate<Control>(PackedScene.GenEditState.Disabled);
                tipNode.GetNode<MegaLabel>("%Title").SetTextAutoSize(change.Version);
                tipNode.GetNode<MegaRichTextLabel>("%Description")
                    .SetTextAutoSize(ChangelogUiHelper.ResolveEnergyIcons(change.ChangeText, energyPrefix));
                tipNode.GetNode<TextureRect>("%Icon").Visible = false;

                container.AddChild(tipNode);
                tipNode.ResetSize();
                container.Size = new Vector2(container.Size.X, container.Size.Y + tipNode.Size.Y + 5f);
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[SpireChangelog] Card tip error: {ex.Message}");
        }
    }
}

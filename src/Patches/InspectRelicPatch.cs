using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.InspectScreens;
using MegaCrit.Sts2.addons.mega_text;
using SpireChangelog.Data;
using SpireChangelog.Helpers;

namespace SpireChangelog.Patches;

[HarmonyPatch(typeof(NInspectRelicScreen), "UpdateRelicDisplay")]
public static class InspectRelicPatch
{
    static void Postfix(NInspectRelicScreen __instance)
    {
        if (ChangelogDatabase.Instance == null) return;
        ChangelogDatabase.Instance.EnsureNameLookups();

        var relicsField = AccessTools.Field(typeof(NInspectRelicScreen), "_relics");
        var indexField = AccessTools.Field(typeof(NInspectRelicScreen), "_index");
        if (relicsField == null || indexField == null) return;

        var relics = relicsField.GetValue(__instance) as IReadOnlyList<RelicModel>;
        var index = (int)(indexField.GetValue(__instance) ?? 0);
        if (relics == null || index < 0 || index >= relics.Count) return;

        var relic = relics[index];
        var changes = ChangelogDatabase.Instance.GetRelicChanges(relic.Id.Entry);
        if (changes.Count == 0) return;

        try
        {
            // Check if game already created a hover tip set for this screen
            var activeField = AccessTools.Field(typeof(NHoverTipSet), "_activeHoverTips");
            if (activeField?.GetValue(null) is Dictionary<Control, NHoverTipSet> active
                && active.TryGetValue(__instance, out var existingSet))
            {
                // Append to existing set (same approach as cards)
                var containerField = AccessTools.Field(typeof(NHoverTipSet), "_textHoverTipContainer");
                var container = containerField?.GetValue(existingSet) as Control;
                if (container != null && !container.HasMeta("SpireChangelog_Added"))
                {
                    container.SetMeta("SpireChangelog_Added", true);
                    var tipScene = PreloadManager.Cache.GetScene("res://scenes/ui/hover_tip.tscn");
                    foreach (var change in changes)
                    {
                        var tipNode = tipScene.Instantiate<Control>(PackedScene.GenEditState.Disabled);
                        tipNode.GetNode<MegaLabel>("%Title").SetTextAutoSize(change.Version);
                        tipNode.GetNode<MegaRichTextLabel>("%Description")
                            .SetTextAutoSize(ChangelogUiHelper.ResolveEnergyIcons(change.ChangeText));
                        tipNode.GetNode<TextureRect>("%Icon").Visible = false;
                        container.AddChild(tipNode);
                        tipNode.ResetSize();
                        container.Size = new Vector2(container.Size.X, container.Size.Y + tipNode.Size.Y + 5f);
                    }
                }
            }
            else
            {
                // No existing set — create with all patch tips
                var tips = changes.Select(c =>
                    (IHoverTip)ChangelogUiHelper.CreatePatchHoverTip(
                        c.Version,
                        ChangelogUiHelper.ResolveEnergyIcons(c.ChangeText)));
                NHoverTipSet.CreateAndShow(__instance, tips,
                    HoverTip.GetHoverTipAlignment(__instance));
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[SpireChangelog] Relic tip error: {ex.Message}");
        }
    }
}

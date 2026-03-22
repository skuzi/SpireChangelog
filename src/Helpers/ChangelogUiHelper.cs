using System;
using System.Collections.Generic;
using System.Text;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.addons.mega_text;
using SpireChangelog.Data;

namespace SpireChangelog.Helpers;

public static class ChangelogUiHelper
{
    /// <summary>
    /// Format changes as BBCode for MegaRichTextLabel display.
    /// Resolves [E] to the character's energy icon based on the model's pool.
    /// </summary>
    public static string FormatChanges(List<EntityChange> changes, string? energyPrefix = null)
    {
        var sb = new StringBuilder();
        foreach (var change in changes)
        {
            if (sb.Length > 0)
                sb.Append('\n');
            sb.Append($"[color=#FFD700][b][{change.Version}][/b][/color] {change.ChangeText}");
        }
        return ResolveEnergyIcons(sb.ToString(), energyPrefix);
    }

    /// <summary>
    /// Replace [E] with the actual energy icon [img] tag.
    /// Uses the provided prefix (from card/relic pool), falls back to RunManager, then colorless.
    /// </summary>
    public static string ResolveEnergyIcons(string text, string? energyPrefix = null)
    {
        if (!text.Contains("[E]")) return text;

        var prefix = energyPrefix;
        if (string.IsNullOrEmpty(prefix))
        {
            try
            {
                prefix = RunManager.Instance?.GetLocalCharacterEnergyIconPrefix();
            }
            catch { }
        }
        if (string.IsNullOrEmpty(prefix))
            prefix = "colorless";

        var img = $"[img]res://images/packed/sprite_fonts/{prefix}_energy_icon.png[/img]";
        return text.Replace("[E]", img);
    }

    /// <summary>
    /// Create a HoverTip with arbitrary title and description via reflection.
    /// </summary>
    public static HoverTip CreatePatchHoverTip(string title, string description)
    {
        var tip = default(HoverTip);

        var titleField = AccessTools.Field(typeof(HoverTip), "<Title>k__BackingField");
        var descField = AccessTools.Field(typeof(HoverTip), "<Description>k__BackingField");

        object boxed = tip;
        titleField?.SetValue(boxed, title);
        descField?.SetValue(boxed, description);
        tip = (HoverTip)boxed;

        tip.Id = "SpireChangelog_" + title;

        return tip;
    }

    /// <summary>
    /// Format changes for HoverTip description (plain text).
    /// </summary>
    public static string FormatChangesPlain(List<EntityChange> changes)
    {
        var sb = new StringBuilder();
        foreach (var change in changes)
        {
            if (sb.Length > 0)
                sb.Append('\n');
            sb.Append($"[{change.Version}] {change.ChangeText}");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Append changelog tip nodes to an existing NHoverTipSet's text container.
    /// Used by both the inspect screen patch and the card holder hover tip patch.
    /// </summary>
    public static void AppendChangelogTips(Control owner, List<EntityChange> changes, string? energyPrefix = null)
    {
        try
        {
            var activeField = AccessTools.Field(typeof(NHoverTipSet), "_activeHoverTips");
            if (activeField?.GetValue(null) is not Dictionary<Control, NHoverTipSet> activeHoverTips)
                return;

            if (!activeHoverTips.TryGetValue(owner, out var hoverTipSet))
                return;

            var containerField = AccessTools.Field(typeof(NHoverTipSet), "_textHoverTipContainer");
            var container = containerField?.GetValue(hoverTipSet) as Control;
            if (container == null) return;

            if (container.HasMeta("SpireChangelog_Added")) return;
            container.SetMeta("SpireChangelog_Added", true);

            var tipScene = PreloadManager.Cache.GetScene("res://scenes/ui/hover_tip.tscn");

            foreach (var change in changes)
            {
                var tipNode = tipScene.Instantiate<Control>(PackedScene.GenEditState.Disabled);
                tipNode.GetNode<MegaLabel>("%Title").SetTextAutoSize(change.Version);
                tipNode.GetNode<MegaRichTextLabel>("%Description")
                    .SetTextAutoSize(ResolveEnergyIcons(change.ChangeText, energyPrefix));
                tipNode.GetNode<TextureRect>("%Icon").Visible = false;

                container.AddChild(tipNode);
                tipNode.ResetSize();
                container.Size = new Vector2(container.Size.X, container.Size.Y + tipNode.Size.Y + 5f);
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[SpireChangelog] AppendChangelogTips error: {ex.Message}");
        }
    }
}

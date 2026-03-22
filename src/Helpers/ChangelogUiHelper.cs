using System.Collections.Generic;
using System.Text;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Runs;
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
}

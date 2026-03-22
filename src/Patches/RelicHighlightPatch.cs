using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Relics;
using SpireChangelog.Data;

namespace SpireChangelog.Patches;

[HarmonyPatch(typeof(NRelic), "_Ready")]
public static class RelicHighlightPatch
{
    private const string OverlayName = "SpireChangelogFrame";

    static void Postfix(NRelic __instance)
    {
        if (ChangelogDatabase.Instance == null) return;
        ChangelogDatabase.Instance.EnsureNameLookups();

        var model = __instance.Model;
        if (model == null) return;

        var changes = ChangelogDatabase.Instance.GetRelicChanges(model.Id.Entry);
        if (changes.Count == 0) return;

        // Guard against duplicate overlays on re-entry
        if (__instance.GetNodeOrNull(OverlayName) != null) return;

        var icon = __instance.Icon;
        if (icon == null) return;

        try
        {
            var overlay = new Panel();
            overlay.Name = OverlayName;
            overlay.MouseFilter = Control.MouseFilterEnum.Ignore;
            overlay.Position = icon.Position - new Vector2(4, 4);
            overlay.Size = icon.Size + new Vector2(8, 8);

            var style = new StyleBoxFlat();
            style.BgColor = new Color(0, 0, 0, 0); // transparent fill
            style.BorderColor = GetRarityColor(model.Rarity);
            style.SetBorderWidthAll(3);
            style.SetCornerRadiusAll(8);
            overlay.AddThemeStyleboxOverride("panel", style);

            __instance.AddChild(overlay);
        }
        catch (Exception ex)
        {
            Log.Warn($"[SpireChangelog] Relic frame: {ex.Message}");
        }
    }

    private static Color GetRarityColor(RelicRarity rarity) => rarity switch
    {
        RelicRarity.Common or RelicRarity.Starter or RelicRarity.None
            => new Color(0.75f, 0.7f, 0.6f, 0.8f),
        RelicRarity.Uncommon
            => new Color(0.4f, 0.7f, 1f, 1f),
        RelicRarity.Rare
            => new Color(1f, 0.8f, 0.2f, 1f),
        RelicRarity.Shop
            => new Color(0.3f, 0.6f, 1f, 1f),
        RelicRarity.Event
            => new Color(0.4f, 0.9f, 0.4f, 1f),
        RelicRarity.Ancient
            => new Color(1f, 0.3f, 0.3f, 1f),
        _ => new Color(1f, 0.85f, 0.2f, 1f),
    };
}

using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using SpireChangelog.Data;

namespace SpireChangelog.Patches;

/// <summary>
/// Adds an independent gold border glow on cards modified in recent patches.
/// Shown in card grids (library, pile viewers, reward/shop screens) but not
/// on cards flying around during combat animations.
/// </summary>
[HarmonyPatch(typeof(NCard), "UpdateVisuals")]
public static class CardHighlightPatch
{
    private const string OverlayName = "SpireChangelogGlow";

    /// <summary>
    /// Cards in a grid holder (pile viewer, library, reward screen) should glow.
    /// Cards loose in combat UI (flying between piles) should not.
    /// </summary>
    private static bool IsInCardGrid(NCard card)
    {
        var parent = card.GetParent();
        return parent is NGridCardHolder;
    }

    private static void RemoveOverlay(Node parent)
    {
        var existing = parent.GetNodeOrNull(OverlayName);
        if (existing != null)
        {
            parent.RemoveChild(existing);
            existing.Free();
        }
    }

    static void Postfix(NCard __instance, PileType pileType)
    {
        if (ChangelogDatabase.Instance == null) return;
        ChangelogDatabase.Instance.EnsureNameLookups();

        // Show glow for PileType.None (library, shop, rewards, inspect) always,
        // or for combat pile types only when displayed in a card grid (pile viewer)
        bool show = pileType == PileType.None || IsInCardGrid(__instance);
        if (!show)
        {
            RemoveOverlay(__instance);
            return;
        }

        var model = __instance.Model;
        if (model == null) return;

        var changes = ChangelogDatabase.Instance.GetCardChanges(model.Id.Entry);
        if (changes.Count == 0)
        {
            RemoveOverlay(__instance);
            return;
        }

        // Overlay already exists — nothing to do
        if (__instance.GetNodeOrNull(OverlayName) != null) return;

        // Create a gold border overlay using a StyleBoxFlat drawn as a Panel
        // This is independent of the game's CardHighlight system
        var frame = __instance.GetNodeOrNull<TextureRect>("%Frame");
        if (frame == null) return;

        var overlay = new Panel();
        overlay.Name = OverlayName;
        overlay.MouseFilter = Control.MouseFilterEnum.Ignore;
        overlay.Position = frame.Position - new Vector2(5, 5);
        overlay.Size = frame.Size + new Vector2(10, 10);

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0, 0, 0, 0); // transparent fill
        style.BorderColor = new Color(1f, 0.78f, 0f, 0.85f); // gold border
        style.SetBorderWidthAll(5);
        style.SetCornerRadiusAll(14);
        overlay.AddThemeStyleboxOverride("panel", style);

        __instance.AddChild(overlay);
        // Move behind card content so it renders as a background glow
        __instance.MoveChild(overlay, 0);
    }
}

using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Combat;
using SpireChangelog.Data;
using SpireChangelog.Helpers;

namespace SpireChangelog.Patches;

[HarmonyPatch(typeof(NCreature), "ShowHoverTips")]
public static class CreatureHoverTipPatch
{
    static void Prefix(NCreature __instance, ref IEnumerable<IHoverTip> hoverTips)
    {
        ModEntry.TryRegisterConsoleCommands();
        if (ChangelogDatabase.Instance == null) return;
        ChangelogDatabase.Instance.EnsureNameLookups();

        var entity = __instance.Entity;
        if (entity == null || !entity.IsMonster || entity.Monster == null) return;

        var monsterEntry = entity.Monster.Id.Entry;
        var changes = ChangelogDatabase.Instance.GetEnemyChanges(monsterEntry);
        if (changes.Count == 0) return;

        // One tip per patch version, appended after game's tips
        var patchTips = changes.Select(c =>
            (IHoverTip)ChangelogUiHelper.CreatePatchHoverTip(c.Version, c.ChangeText));
        hoverTips = hoverTips.Concat(patchTips);
    }
}

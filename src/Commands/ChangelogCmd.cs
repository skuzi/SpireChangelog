using System.Linq;
using System.Text;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using SpireChangelog.Data;

namespace SpireChangelog.Commands;

public class ChangelogCmd : AbstractConsoleCmd
{
    public override string CmdName => "changelog";
    public override string Args => "[card|relic|enemy] [name]";
    public override string Description => "Show parsed patch changes. Use 'changelog stats' for summary.";
    public override bool IsNetworked => false;
    public override bool DebugOnly => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        var db = ChangelogDatabase.Instance;
        if (db == null)
            return new CmdResult(false, "SpireChangelog database not initialized.");

        if (args.Length == 0 || args[0].ToLowerInvariant() == "stats")
        {
            return new CmdResult(true,
                $"SpireChangelog: {db.TotalChanges} changes across {db.PatchCount} patches.");
        }

        var category = args[0].ToLowerInvariant();

        // changelog list [card|relic|enemy] — show all changed entities
        if (category == "list")
        {
            var sb2 = new StringBuilder();
            var type = args.Length > 1 ? args[1].ToLowerInvariant() : "all";
            if (type is "all" or "card" or "cards")
            {
                var cards = db.GetAllCardKeys();
                if (cards.Count > 0)
                    sb2.AppendLine($"[gold]Cards ({cards.Count}):[/gold] {string.Join(", ", cards)}");
            }
            if (type is "all" or "relic" or "relics")
            {
                var relics = db.GetAllRelicKeys();
                if (relics.Count > 0)
                    sb2.AppendLine($"[gold]Relics ({relics.Count}):[/gold] {string.Join(", ", relics)}");
            }
            if (type is "all" or "enemy" or "enemies")
            {
                var enemies = db.GetAllEnemyKeys();
                if (enemies.Count > 0)
                    sb2.AppendLine($"[gold]Enemies ({enemies.Count}):[/gold] {string.Join(", ", enemies)}");
            }
            // Show multi-patch entries
            var multi = db.GetMultiPatchEntries();
            if (multi.Count > 0)
                sb2.AppendLine($"\n[gold]Multi-patch:[/gold] {string.Join(", ", multi)}");
            return new CmdResult(true, sb2.ToString().TrimEnd());
        }

        if (args.Length < 2)
        {
            return new CmdResult(false, "Usage: changelog <card|relic|enemy|list> [name]");
        }

        var name = string.Join(" ", args.Skip(1));
        var sb = new StringBuilder();

        var changes = category switch
        {
            "card" or "cards" => db.GetCardChanges(name.ToUpperInvariant().Replace(" ", "_"), 10),
            "relic" or "relics" => db.GetRelicChanges(name.ToUpperInvariant().Replace(" ", "_"), 10),
            "enemy" or "enemies" => db.GetEnemyChanges(name.ToUpperInvariant().Replace(" ", "_"), 10),
            _ => null
        };

        if (changes == null)
            return new CmdResult(false, $"Unknown category '{category}'. Use card, relic, or enemy.");

        if (changes.Count == 0)
            return new CmdResult(true, $"No changes found for '{name}'.");

        foreach (var change in changes)
        {
            sb.AppendLine($"[v {change.Version}] {change.ChangeText}");
        }

        return new CmdResult(true, sb.ToString().TrimEnd());
    }

    public override CompletionResult GetArgumentCompletions(Player? player, string[] args) => new();
}

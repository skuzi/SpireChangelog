using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Godot;
using MegaCrit.Sts2.Core.Logging;

namespace SpireChangelog.Data;

public static class PatchNotesParser
{
    private const string PatchNotesPath = "res://localization/eng/patch_notes";

    // Regex to strip BBCode tags
    private static readonly Regex BbcodeTagRegex = new(@"\[/?[^\]]+\]", RegexOptions.Compiled);

    // Extract bold text: [b]Name[/b]
    private static readonly Regex BoldTextRegex = new(@"\[b\](.*?)\[/b\]", RegexOptions.Compiled);

    private static readonly string[] VerbPrefixes =
        { "Reworked", "Nerfed", "Buffed", "Changed", "Tweaked", "Added", "Fixed", "Simplified", "Deprecated", "Implemented" };

    // Pre-compiled regexes for StripFormattingOnly
    private static readonly Regex FormattingTagRegex = new(@"\[/?[bius]\]", RegexOptions.Compiled);
    private static readonly Regex ColorTagRegex = new(@"\[/?(?:blue|red|green|gold|orange|purple|pink|aqua|color(?:=[^\]]*)?)\]", RegexOptions.Compiled);
    private static readonly Regex ImgTagRegex = new(@"\[img\][^\[]*\[/img\]", RegexOptions.Compiled);
    private static readonly Regex MultiSpaceRegex = new(@"  +", RegexOptions.Compiled);

    // Pre-compiled regexes for StripEntityPrefix
    private static readonly Regex RelicSuffixRegex = new(@"\s+relics?\s*:", RegexOptions.Compiled);
    private static readonly Regex CardSuffixRegex = new(@"\s+cards?\s*:", RegexOptions.Compiled);

    // Card section headers (character names and card categories)
    private static readonly string[] CardSectionHeaders =
        { "Ironclad", "Silent", "Regent", "Necrobinder", "Defect", "Watcher",
          "Colorless Cards", "Colorless", "General", "Status Cards", "Curse Cards" };

    // Enemy section headers
    private static readonly string[] EnemySectionHeaders =
        { "Enemies", "Monsters", "Encounters", "Enemy Changes", "Monster Changes" };

    // Relic section headers
    private static readonly string[] RelicSectionHeaders =
        { "Relics", "Relic Changes", "Potions & Relics", "Potions and Relics",
          "Relics & Potions", "Relics and Potions" };

    // Sections to skip (not card/relic/enemy)
    private static readonly string[] SkipSectionHeaders =
        { "Content", "Balance", "Bug Fixes", "Bugfixes", "Bug fixes", "Fixes",
          "QoL", "Quality of Life", "UI", "Visual", "Audio", "Misc", "Events",
          "Maps", "Shop", "Phobia", "Ascension", "New Features", "Features",
          "Performance", "Known Issues", "Other" };

    // Potion section headers (treated as relics for display)
    private static readonly string[] PotionSectionHeaders =
        { "Potions", "Potion Changes" };

    public static List<PatchVersion> ParseAllPatchNotes()
    {
        var results = new List<PatchVersion>();

        string[] files;
        try
        {
            files = DirAccess.GetFilesAt(PatchNotesPath);
        }
        catch (Exception ex)
        {
            Log.Error($"[SpireChangelog] Failed to read patch notes directory: {ex.Message}");
            return results;
        }

        if (files == null || files.Length == 0)
        {
            Log.Warn("[SpireChangelog] No patch note files found.");
            return results;
        }

        var sortedFiles = files.OrderByDescending(f => f).ToList();

        foreach (var fileName in sortedFiles)
        {
            var path = PatchNotesPath + "/" + fileName;
            try
            {
                var patch = ParseSinglePatchNote(path, fileName);
                if (patch != null)
                    results.Add(patch);
            }
            catch (Exception ex)
            {
                Log.Error($"[SpireChangelog] Failed to parse {fileName}: {ex.Message}");
            }
        }

        return results;
    }

    private static PatchVersion? ParseSinglePatchNote(string path, string fileName)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null) return null;

        var text = CallGetAsText(file);
        if (string.IsNullOrWhiteSpace(text)) return null;

        var baseName = fileName.Replace(".txt", "").Replace(".md", "");
        // Default version = formatted date like "Mar 19, 2026"
        var patch = new PatchVersion { Version = baseName };
        if (DateTime.TryParseExact(baseName, "yyyy_MM_d", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsedDate))
        {
            patch.Date = parsedDate.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture);
            patch.Version = parsedDate.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
        }

        // Override with version name if found in text (e.g. "v0.100.0")
        var versionMatch = Regex.Match(text, @"v\d+\.\d+(?:\.\d+)?(?:[a-z])?");
        if (versionMatch.Success)
            patch.Version = versionMatch.Value;

        // Parse the raw BBCode text line by line
        // Track current entity for sub-bullet collection:
        //   - Buffed Seapunk:        <- top-level bullet, entity = "Seapunk"
        //     - detail 1             <- sub-bullet, appended to Seapunk's changes
        //     - detail 2
        var lines = text.Split('\n');
        var currentSection = PatchSection.Unknown;
        string? currentEntityName = null;
        var currentEntityAliases = new List<string>();
        var currentEntityDetails = new List<string>();
        Dictionary<string, string>? currentDict = null;

        void FlushCurrentEntity()
        {
            if (currentEntityName != null && currentDict != null && currentEntityAliases.Count > 0)
            {
                // Each tooltip has its own title (the patch date), so just show the change text.
                // Sub-bullets become a bullet list, header gets prefix-stripped.
                string changeText;
                if (currentEntityDetails.Count > 1)
                {
                    var subs = currentEntityDetails.GetRange(1, currentEntityDetails.Count - 1);
                    changeText = "• " + string.Join("\n• ", subs);
                }
                else if (currentEntityDetails.Count == 1)
                {
                    var line = currentEntityDetails[0];
                    var stripped = StripEntityPrefix(line, currentEntityAliases);
                    changeText = string.IsNullOrWhiteSpace(stripped) ? line : stripped;
                }
                else
                {
                    changeText = "";
                }
                if (!string.IsNullOrWhiteSpace(changeText))
                {
                    // Store under ALL names (e.g. both "Prepared" and "Prepare")
                    foreach (var alias in currentEntityAliases)
                        currentDict.TryAdd(alias, changeText);
                }
            }
            currentEntityName = null;
            currentEntityAliases.Clear();
            currentEntityDetails.Clear();
            currentDict = null;
        }

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // Detect section headers
            var headerName = ExtractSectionHeader(line);
            if (headerName != null)
            {
                FlushCurrentEntity();
                currentSection = ClassifySection(headerName);
                continue;
            }

            // Only process bullet-point lines
            if (!line.StartsWith("-")) continue;
            var bulletContent = line.Substring(1).Trim();

            // Check indentation: sub-bullets have leading whitespace before the "-"
            bool isSubBullet = rawLine.Length > 0 && (rawLine[0] == ' ' || rawLine[0] == '\t');

            // Skip bug fix lines — not balance changes
            var plainBullet = StripBbcode(bulletContent).Trim();
            if (plainBullet.StartsWith("Fixed ", StringComparison.OrdinalIgnoreCase) ||
                plainBullet.StartsWith("Fix ", StringComparison.OrdinalIgnoreCase))
                continue;

            if (isSubBullet && currentEntityName != null)
            {
                // Sub-bullet: append details to current entity
                var detail = StripFormattingOnly(bulletContent).Trim();
                if (!string.IsNullOrWhiteSpace(detail))
                    currentEntityDetails.Add(detail);
                continue;
            }

            // Top-level bullet — flush previous entity
            FlushCurrentEntity();

            Dictionary<string, string>? sectionDict = currentSection switch
            {
                PatchSection.Cards => patch.Cards,
                PatchSection.Relics => patch.Relics,
                PatchSection.Enemies => patch.Enemies,
                _ => null
            };

            if (sectionDict == null) continue;

            var boldNames = ExtractBoldNames(bulletContent);
            if (boldNames.Count > 0)
            {
                // Use first name as primary, associate change with ALL names
                currentEntityName = boldNames[0];
                currentEntityAliases = boldNames;
                currentDict = sectionDict;
                var inlineText = StripFormattingOnly(bulletContent).Trim();
                if (!string.IsNullOrWhiteSpace(inlineText))
                    currentEntityDetails.Add(inlineText);
            }
            else
            {
                // Fallback: "Reworked Glow card: ..." without bold tags
                var parsed = ParseVerbPrefixLine(bulletContent);
                if (parsed != null)
                {
                    var name = parsed.Value.name;
                    // Strip " card" / " relic" suffix
                    if (name.EndsWith(" card", StringComparison.OrdinalIgnoreCase))
                        name = name.Substring(0, name.Length - 5).Trim();
                    if (name.EndsWith(" relic", StringComparison.OrdinalIgnoreCase))
                        name = name.Substring(0, name.Length - 6).Trim();
                    currentEntityName = name;
                    currentEntityAliases = new List<string> { name };
                    currentDict = sectionDict;
                    if (!string.IsNullOrWhiteSpace(parsed.Value.change))
                        currentEntityDetails.Add(parsed.Value.change);
                }
            }
        }
        FlushCurrentEntity();

        int total = patch.Cards.Count + patch.Relics.Count + patch.Enemies.Count;
        if (total > 0)
        {
            Log.Info($"[SpireChangelog] Parsed {fileName}: {patch.Cards.Count} cards, " +
                     $"{patch.Relics.Count} relics, {patch.Enemies.Count} enemies");
        }

        return patch;
    }

    /// <summary>
    /// Extract a section header name from lines like "[b]Ironclad:[/b]", "[b]Enemies:[/b]",
    /// or ALL CAPS lines like "BUG FIXES:"
    /// </summary>
    private static string? ExtractSectionHeader(string line)
    {
        var stripped = StripBbcode(line).Trim();

        // Don't treat bullet lines as headers
        if (stripped.StartsWith("-") || stripped.StartsWith("•")) return null;
        // Headers are short
        if (stripped.Length > 60) return null;

        // Pattern 1: [b]HeaderName:[/b] or [blue][b]CONTENT:[/b][/blue]
        var match = BoldTextRegex.Match(line);
        if (match.Success)
        {
            var boldText = match.Groups[1].Value.Trim().TrimEnd(':').Trim();
            return boldText;
        }

        // Pattern 2: ALL CAPS line ending with colon, e.g. "BUG FIXES:"
        var trimmed = stripped.TrimEnd(':').Trim();
        if (trimmed.Length >= 3 && trimmed.Length <= 40 &&
            trimmed == trimmed.ToUpperInvariant() &&
            trimmed.Any(char.IsLetter))
        {
            return trimmed;
        }

        return null;
    }

    private static PatchSection ClassifySection(string headerName)
    {
        var lower = headerName.ToLowerInvariant();

        if (CardSectionHeaders.Any(h => lower == h.ToLowerInvariant() ||
            lower.StartsWith(h.ToLowerInvariant())))
            return PatchSection.Cards;
        if (EnemySectionHeaders.Any(h => lower == h.ToLowerInvariant() ||
            lower.StartsWith(h.ToLowerInvariant())))
            return PatchSection.Enemies;
        if (RelicSectionHeaders.Any(h => lower == h.ToLowerInvariant() ||
            lower.StartsWith(h.ToLowerInvariant())))
            return PatchSection.Relics;
        if (PotionSectionHeaders.Any(h => lower == h.ToLowerInvariant() ||
            lower.StartsWith(h.ToLowerInvariant())))
            return PatchSection.Relics; // treat potions as relics
        if (SkipSectionHeaders.Any(h => lower == h.ToLowerInvariant() ||
            lower.StartsWith(h.ToLowerInvariant())))
            return PatchSection.Other;

        return PatchSection.Unknown;
    }

    /// <summary>
    /// Extract all [b]Name[/b] entries from a line.
    /// For "Reworked [b]Prepared[/b] card into [b]Prepare[/b]", returns both names.
    /// </summary>
    private static List<string> ExtractBoldNames(string line)
    {
        var result = new List<string>();
        foreach (Match match in BoldTextRegex.Matches(line))
        {
            var name = match.Groups[1].Value.Trim();
            if (!string.IsNullOrWhiteSpace(name) && name.Length <= 60)
                result.Add(name);
        }
        return result;
    }

    /// <summary>
    /// Parse lines like "Buffed EnemyName: change details" or "Nerfed EnemyName - change"
    /// Strips the verb prefix and returns (cleanName, fullChangeText).
    /// </summary>
    private static (string name, string change)? ParseVerbPrefixLine(string line)
    {
        var plain = StripBbcode(line).Trim();

        foreach (var verb in VerbPrefixes)
        {
            if (!plain.StartsWith(verb + " ", StringComparison.OrdinalIgnoreCase))
                continue;

            var rest = plain.Substring(verb.Length + 1).Trim();

            // Find where the name ends: at ":", "–", "—", ".", or end of line
            var separators = new[] { ':', '–', '—', '.' };
            var sepIdx = -1;
            foreach (var sep in separators)
            {
                var idx = rest.IndexOf(sep);
                if (idx > 0 && (sepIdx < 0 || idx < sepIdx))
                    sepIdx = idx;
            }

            string entityName;
            string changeText;
            if (sepIdx > 0)
            {
                entityName = rest.Substring(0, sepIdx).Trim();
                changeText = plain; // Keep full text including verb as the change description
            }
            else
            {
                entityName = rest.Trim();
                changeText = plain;
            }

            // Clean up entity name — remove parentheticals like "(formerly X)"
            var parenIdx = entityName.IndexOf('(');
            if (parenIdx > 0)
                entityName = entityName.Substring(0, parenIdx).Trim();

            if (entityName.Length >= 2 && entityName.Length <= 50)
                return (entityName, changeText);
        }

        return null;
    }

    /// <summary>
    /// Invokes FileAccess.GetAsText via reflection to handle Godot API differences.
    /// Godot 4.3+ changed the signature from GetAsText() to GetAsText(bool skipCr).
    /// Since the mod may run against different game builds, we try both signatures.
    /// </summary>
    private static string CallGetAsText(FileAccess file)
    {
        var type = typeof(FileAccess);
        var noArgMethod = type.GetMethod("GetAsText", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
        if (noArgMethod != null)
            return (string)noArgMethod.Invoke(file, null)!;

        var boolArgMethod = type.GetMethod("GetAsText", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(bool) }, null);
        if (boolArgMethod != null)
            return (string)boolArgMethod.Invoke(file, new object[] { true })!;

        throw new MissingMethodException("FileAccess.GetAsText not found");
    }

    /// <summary>
    /// Strip prefix from change text.
    /// Single entity: "Nerfed Borrowed Time card: now Exhausts" → "now Exhausts"
    /// Multi entity: "Changed Paper Krane and Tingsha relics: rarities swapped" → "Paper Krane and Tingsha: rarities swapped"
    /// </summary>
    private static string StripEntityPrefix(string line, List<string> aliases)
    {
        if (aliases.Count > 1)
        {
            // Multi-entity: strip only the leading verb
            foreach (var verb in VerbPrefixes)
            {
                if (line.StartsWith(verb + " ", StringComparison.OrdinalIgnoreCase))
                {
                    var rest = line.Substring(verb.Length + 1).Trim();
                    // Also strip "relics:" / "cards:" → just "Name and Name: change"
                    rest = RelicSuffixRegex.Replace(rest, ":");
                    rest = CardSuffixRegex.Replace(rest, ":");
                    return rest;
                }
            }
            return line;
        }

        // Single entity: strip verb + entity name + "card"/"relic" + ":"
        var entityName = aliases[0];
        var nameIdx = line.IndexOf(entityName, StringComparison.OrdinalIgnoreCase);
        if (nameIdx >= 0)
        {
            var afterName = nameIdx + entityName.Length;
            var rest = line.Substring(afterName).TrimStart();
            if (rest.StartsWith("card", StringComparison.OrdinalIgnoreCase))
                rest = rest.Substring(4).TrimStart();
            if (rest.StartsWith("relic", StringComparison.OrdinalIgnoreCase))
                rest = rest.Substring(5).TrimStart();
            rest = rest.TrimStart(':', '.', ' ', '-');
            if (!string.IsNullOrWhiteSpace(rest))
                return rest.Trim();
        }
        return line;
    }

    /// <summary>
    /// Strip formatting BBCode but keep game-specific tags like [E] that
    /// MegaRichTextLabel can render via its custom effects.
    /// </summary>
    public static string StripFormattingOnly(string text)
    {
        var result = text;
        result = FormattingTagRegex.Replace(result, "");
        result = ColorTagRegex.Replace(result, "");
        result = ImgTagRegex.Replace(result, "");
        // Keep [E] as-is in stored text — resolved to [img] at display time
        // by ChangelogUiHelper.ResolveEnergyIcons()
        result = MultiSpaceRegex.Replace(result, " ");
        return result;
    }

    /// <summary>
    /// Strip ALL BBCode tags. Used for section detection and entity name extraction only.
    /// </summary>
    public static string StripBbcode(string text)
    {
        var result = BbcodeTagRegex.Replace(text, "");
        result = MultiSpaceRegex.Replace(result, " ");
        return result;
    }
}

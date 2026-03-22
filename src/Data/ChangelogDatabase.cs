using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace SpireChangelog.Data;

public class ChangelogDatabase
{
    public static ChangelogDatabase? Instance { get; private set; }

    // Key = ModelId.Entry (e.g. "STRIKE"), Value = list of changes newest first
    private readonly Dictionary<string, List<EntityChange>> _cardChanges = new();
    private readonly Dictionary<string, List<EntityChange>> _relicChanges = new();
    private readonly Dictionary<string, List<EntityChange>> _enemyChanges = new();

    // Display name → ModelId.Entry lookup
    private readonly Dictionary<string, string> _cardNameToId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _relicNameToId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _enemyNameToId = new(StringComparer.OrdinalIgnoreCase);

    public int TotalChanges => _cardChanges.Values.Sum(v => v.Count) +
                               _relicChanges.Values.Sum(v => v.Count) +
                               _enemyChanges.Values.Sum(v => v.Count);
    public int PatchCount { get; private set; }

    private bool _nameLookupsBuilt;
    private DateTime _lastLookupAttempt = DateTime.MinValue;
    private static readonly TimeSpan LookupRetryInterval = TimeSpan.FromSeconds(5);

    public static void Initialize()
    {
        var db = new ChangelogDatabase();
        db.BuildNameLookups(); // may get 0 results if ModelDb isn't ready yet
        db.LoadPatchNotes();
        Instance = db;
    }

    /// <summary>
    /// Retry building name lookups if they were empty at init time.
    /// Called lazily from patches when game is fully loaded.
    /// </summary>
    public void EnsureNameLookups()
    {
        if (_nameLookupsBuilt) return;

        var now = DateTime.UtcNow;
        if (now - _lastLookupAttempt < LookupRetryInterval) return;
        _lastLookupAttempt = now;

        if (_cardNameToId.Count == 0 && _relicNameToId.Count == 0 && _enemyNameToId.Count == 0)
        {
            BuildNameLookups();
            if (_cardNameToId.Count > 0 || _relicNameToId.Count > 0 || _enemyNameToId.Count > 0)
            {
                _nameLookupsBuilt = true;
                // Re-process patch notes with proper name matching
                _cardChanges.Clear();
                _relicChanges.Clear();
                _enemyChanges.Clear();
                LoadPatchNotes();
            }
        }
    }

    private void BuildNameLookups()
    {
        // Use _contentById reflection to safely iterate all models.
        // ModelDb.AllCards/AllRelics throw due to CHARACTER pool lookups.
        try
        {
            var contentField = HarmonyLib.AccessTools.Field(typeof(ModelDb), "_contentById");
            if (contentField?.GetValue(null) is not Dictionary<ModelId, AbstractModel> content)
            {
                Log.Error("[SpireChangelog] Could not access ModelDb._contentById");
                return;
            }

            foreach (var (id, model) in content)
            {
                try
                {
                    if (model is CardModel card)
                    {
                        var title = card.Title;
                        if (!string.IsNullOrWhiteSpace(title) && !_cardNameToId.ContainsKey(title))
                            _cardNameToId[title] = id.Entry;
                    }
                    else if (model is RelicModel relic)
                    {
                        var title = relic.Title.GetFormattedText();
                        if (!string.IsNullOrWhiteSpace(title) && !_relicNameToId.ContainsKey(title))
                            _relicNameToId[title] = id.Entry;
                    }
                    else if (model is MonsterModel monster)
                    {
                        var title = monster.Title.GetFormattedText();
                        if (!string.IsNullOrWhiteSpace(title) && !_enemyNameToId.ContainsKey(title))
                            _enemyNameToId[title] = id.Entry;
                    }
                }
                catch { /* skip models that fail to resolve title */ }
            }

            Log.Info($"[SpireChangelog] Built name lookups: {_cardNameToId.Count} cards, " +
                     $"{_relicNameToId.Count} relics, {_enemyNameToId.Count} enemies");
        }
        catch (Exception ex)
        {
            Log.Error($"[SpireChangelog] Failed to build name lookups: {ex.Message}");
        }
    }

    /// <summary>
    /// Number of most-recent patch files to process. Increase for broader history
    /// at the cost of longer init time and larger tooltip text.
    /// </summary>
    internal const int MaxPatchesToShow = 3;

    private void LoadPatchNotes()
    {
        var allPatches = PatchNotesParser.ParseAllPatchNotes(); // newest first
        PatchCount = allPatches.Count;

        // Process the N most recent patch files by date
        foreach (var patch in allPatches.Take(MaxPatchesToShow))
        {
            ProcessSection(patch.Cards, _cardNameToId, _cardChanges, patch.Version);
            ProcessSection(patch.Relics, _relicNameToId, _relicChanges, patch.Version);
            ProcessSection(patch.Enemies, _enemyNameToId, _enemyChanges, patch.Version);
        }

        Log.Info($"[SpireChangelog] Database loaded: {TotalChanges} total changes from {Math.Min(MaxPatchesToShow, PatchCount)} recent patches ({PatchCount} total patches)");
    }

    private static void ProcessSection(
        Dictionary<string, string> parsedChanges,
        Dictionary<string, string> nameToId,
        Dictionary<string, List<EntityChange>> changeDb,
        string version)
    {
        foreach (var (displayName, changeText) in parsedChanges)
        {
            string? modelEntry = null;
            if (nameToId.TryGetValue(displayName, out var entry))
            {
                modelEntry = entry;
            }
            else
            {
                // Fuzzy: try trimming, removing "The", etc.
                var normalized = displayName.Trim();
                if (normalized.StartsWith("The ", StringComparison.OrdinalIgnoreCase))
                    normalized = normalized.Substring(4);

                foreach (var (name, id) in nameToId)
                {
                    if (name.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
                        name.Replace(" ", "").Equals(normalized.Replace(" ", ""), StringComparison.OrdinalIgnoreCase))
                    {
                        modelEntry = id;
                        break;
                    }
                }
            }

            if (modelEntry == null)
            {
                modelEntry = displayName.ToUpperInvariant().Replace(" ", "_");
            }

            if (!changeDb.ContainsKey(modelEntry))
            {
                changeDb[modelEntry] = new List<EntityChange>();
            }
            changeDb[modelEntry].Add(new EntityChange(version, changeText));
        }
    }

    public List<string> GetAllCardKeys() => _cardChanges.Keys.ToList();
    public List<string> GetAllRelicKeys() => _relicChanges.Keys.ToList();
    public List<string> GetAllEnemyKeys() => _enemyChanges.Keys.ToList();

    public List<string> GetMultiPatchEntries()
    {
        var result = new List<string>();
        foreach (var (key, changes) in _cardChanges)
            if (changes.Count > 1) result.Add($"card:{key}({changes.Count})");
        foreach (var (key, changes) in _relicChanges)
            if (changes.Count > 1) result.Add($"relic:{key}({changes.Count})");
        foreach (var (key, changes) in _enemyChanges)
            if (changes.Count > 1) result.Add($"enemy:{key}({changes.Count})");
        return result;
    }

    public List<EntityChange> GetCardChanges(string modelEntry, int maxCount = 3)
    {
        if (_cardChanges.TryGetValue(modelEntry, out var changes))
            return changes.Take(maxCount).ToList();
        return new List<EntityChange>();
    }

    public List<EntityChange> GetRelicChanges(string modelEntry, int maxCount = 3)
    {
        if (_relicChanges.TryGetValue(modelEntry, out var changes))
            return changes.Take(maxCount).ToList();
        return new List<EntityChange>();
    }

    public List<EntityChange> GetEnemyChanges(string modelEntry, int maxCount = 3)
    {
        if (_enemyChanges.TryGetValue(modelEntry, out var changes))
            return changes.Take(maxCount).ToList();
        return new List<EntityChange>();
    }
}

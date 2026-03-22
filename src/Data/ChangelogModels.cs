using System.Collections.Generic;

namespace SpireChangelog.Data;

/// <summary>
/// A single change to a card/relic/enemy in a specific patch.
/// </summary>
public record EntityChange(string Version, string ChangeText);

/// <summary>
/// All changes from a single patch note file.
/// </summary>
public class PatchVersion
{
    public string Version { get; set; } = "";
    public string Date { get; set; } = "";

    /// <summary>Key = display name (as it appears in patch notes), Value = change text.</summary>
    public Dictionary<string, string> Cards { get; set; } = new();
    public Dictionary<string, string> Relics { get; set; } = new();
    public Dictionary<string, string> Enemies { get; set; } = new();
}

/// <summary>
/// Which section of the patch notes we're currently parsing.
/// </summary>
public enum PatchSection
{
    Unknown,
    Cards,
    Relics,
    Enemies,
    Other
}

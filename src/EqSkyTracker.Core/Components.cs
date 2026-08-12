// Parses the turn-in "component" items named in a hint's `how_to_obtain`
// prose (e.g. "Sphinx Claw", "Wind Rune Izah") -- the raw drops a player farms
// in Plane of Sky and turns in to an NPC for a class-unlock reward, as opposed
// to the reward item itself.
//
// There's no structured component list in the bundled hint data, only this
// one consistently-formatted sentence shape ("Turn in X, Y plus Wind Rune Z to
// <NPC> to complete '<achievement>' (reward: <R>)."), so this is a regex parse
// of that specific shape rather than a general free-text parser. If a hint's
// text doesn't match the shape, parsing simply yields no components for it --
// callers should treat that as "unknown," not as an error.
using System.Text.RegularExpressions;

namespace EqSkyTracker.Core;

public static partial class Components
{
    [GeneratedRegex(@"^Turn in (.+) to .+ to complete '.+' \(reward: .+\)\.$")]
    private static partial Regex HowToObtainRegex();

    [GeneratedRegex(@"\s*\([^()]*\)\s*$")]
    private static partial Regex TagSuffixRegex();

    // Not just the numbered-island shape ("7-SotS") -- some components (e.g.
    // Efreeti-prefixed weapons) aren't tied to an island at all, and instead
    // carry a plain wiki-sourced note like "(Noble Dojorn/Overseer of Air)".
    [GeneratedRegex(@"\(([^()]+)\)")]
    private static partial Regex SourceTagRegex();

    /// <summary>
    /// Extract turn-in component item names (source-tag parentheticals
    /// stripped) from a hint's how_to_obtain text. Returns [] if the text
    /// doesn't match the expected sentence shape.
    /// </summary>
    public static List<string> ParseComponents(string howToObtain) =>
        [.. ParseComponentsWithTags(howToObtain).Select(c => c.Name)];

    /// <summary>
    /// True if a parsed component name is a Wind Rune. Wind Runes live in an
    /// alternate-currency window rather than bags/bank/keyring, so they never
    /// appear in an "/outputfile inventory" dump and can't be cross-referenced
    /// against a character's inventory -- callers that surface inventory-backed
    /// tracking (missing-items lists, farmed-item management) should exclude them.
    /// </summary>
    public static bool IsWindRune(string componentName) =>
        componentName.StartsWith("Wind Rune ", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Extract turn-in components from a hint's how_to_obtain text, pairing
    /// each item name with its own source-tag parenthetical -- an island tag
    /// (e.g. 'Sphinx Claw' -> "7-SotS") or, for components not tied to a
    /// specific island, a plain wiki-sourced note (e.g. 'Efreeti War Axe' ->
    /// "Noble Dojorn/Overseer of Air"). Components without any tag in the
    /// text (e.g. Wind Runes and NPC-purchased items) come back with a null
    /// tag -- that's expected, not an error. Returns [] if the text doesn't
    /// match the expected sentence shape.
    /// </summary>
    public static List<(string Name, string? Tag)> ParseComponentsWithTags(string howToObtain)
    {
        Match m = HowToObtainRegex().Match(howToObtain);
        if (!m.Success)
        {
            return [];
        }
        string itemsPart = m.Groups[1].Value;

        string listed;
        string? windRune;
        int splitAt = itemsPart.LastIndexOf(" plus ", StringComparison.Ordinal);
        if (splitAt >= 0)
        {
            listed = itemsPart[..splitAt];
            windRune = itemsPart[(splitAt + " plus ".Length)..];
        }
        else
        {
            listed = itemsPart;
            windRune = null;
        }

        var components = listed.Split(", ").Select(ParseNameAndTag).ToList();
        if (windRune is not null)
        {
            components.Add(ParseNameAndTag(windRune));
        }
        return components.Where(c => c.Name.Length > 0).ToList();
    }

    private static (string Name, string? Tag) ParseNameAndTag(string raw)
    {
        raw = raw.Trim();
        Match tagMatch = SourceTagRegex().Match(raw);
        string? tag = tagMatch.Success ? tagMatch.Groups[1].Value : null;
        string name = TagSuffixRegex().Replace(raw, "").Trim();
        return (name, tag);
    }
}

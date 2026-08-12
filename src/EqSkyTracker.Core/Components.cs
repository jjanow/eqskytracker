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

    [GeneratedRegex(@"\((\d+-[A-Za-z]+)\)")]
    private static partial Regex IslandTagRegex();

    /// <summary>
    /// Extract turn-in component item names (island-tag parentheticals
    /// stripped) from a hint's how_to_obtain text. Returns [] if the text
    /// doesn't match the expected sentence shape.
    /// </summary>
    public static List<string> ParseComponents(string howToObtain)
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

        var names = listed.Split(", ").Select(n => TagSuffixRegex().Replace(n, "").Trim()).ToList();
        if (windRune is not null)
        {
            names.Add(windRune.Trim());
        }
        return names.Where(n => n.Length > 0).ToList();
    }

    /// <summary>
    /// Pull the island-tag parentheticals (e.g. '7-SotS' from 'Sphinx Claw
    /// (7-SotS)') out of a hint's how_to_obtain text, in the order they appear.
    /// Not every component names a tag (e.g. Wind Runes never do), so this can
    /// return fewer tags than components -- that's expected, not an error.
    /// </summary>
    public static List<string> ExtractIslandTags(string howToObtain) =>
        IslandTagRegex().Matches(howToObtain).Select(m => m.Groups[1].Value).ToList();
}

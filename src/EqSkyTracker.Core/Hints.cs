// Optional 'how do I get this item' hints, loaded from a bundled JSON file.
//
// This is pure decoration on top of the achievement-derived completion data --
// if the file is missing or a given item isn't in it, the app still works
// correctly, it just won't have a tip for that item.
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EqSkyTracker.Core;

public class ItemHint
{
    public string? Npc { get; init; }
    public string? Zone { get; init; }
    public string? HowToObtain { get; init; }
    public required bool Found { get; init; }
}

public static class Hints
{
    public static readonly string DataFile =
        Path.Combine(AppContext.BaseDirectory, "data", "plane_of_sky_item_sources.json");

    public static Dictionary<string, ItemHint> LoadItemHints(string? path = null)
    {
        path ??= DataFile;
        var result = new Dictionary<string, ItemHint>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path))
        {
            return result;
        }

        JsonNode? raw;
        try
        {
            raw = JsonNode.Parse(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return result;
        }

        if (raw?["item_sources"] is not JsonObject itemSources)
        {
            return result;
        }

        foreach (KeyValuePair<string, JsonNode?> entry in itemSources)
        {
            if (entry.Value is not JsonObject info)
            {
                continue;
            }
            // `info["class"]` (wiki's class label, sometimes spelled differently
            // than the achievement export, e.g. "Shadow Knight" vs "Shadowknight")
            // is intentionally not surfaced here -- ItemStatus already carries the
            // achievement-derived class grouping, so keeping this one avoids two
            // sources of truth for the same fact.
            result[entry.Key] = new ItemHint
            {
                Npc = info["npc"]?.GetValue<string>(),
                Zone = info["zone_or_island"]?.GetValue<string>(),
                HowToObtain = info["how_to_obtain"]?.GetValue<string>(),
                Found = info["found"] is JsonValue foundValue && foundValue.GetValue<bool>(),
            };
        }
        return result;
    }
}

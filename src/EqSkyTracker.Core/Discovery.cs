// Locate character dump files and remember the last-used folder.
//
// EQ-style clients write "/outputfile achievements" and "/outputfile inventory"
// as `<Character>_<Server>-Achievements.txt` / `-Inventory.txt` directly into
// the game's install/working directory. There's no single blessed path across
// Windows/macOS/Linux (and this app was built against a Wine install, so even
// "typical" Windows guesses are speculative) -- so discovery is: look in a few
// common places, but always let the user point at the real folder, and remember
// whatever they picked.
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EqSkyTracker.Core;

public class Character
{
    public required string Name { get; init; }
    public string? AchievementsPath { get; set; }
    public string? InventoryPath { get; set; }
}

/// <summary>
/// The bits of the ambient environment discovery needs to consult (home
/// directory, OS, env vars, cwd, config storage location). Injectable so
/// tests can simulate a fresh install, a different OS, or an unwritable
/// install directory without touching real global state.
/// </summary>
public interface IDiscoveryEnvironment
{
    string HomeDirectory { get; }
    string ConfigDirectory { get; }
    bool IsWindows { get; }
    string? GetEnvironmentVariable(string name);
    string CurrentDirectory { get; }
}

public class DefaultDiscoveryEnvironment : IDiscoveryEnvironment
{
    public string HomeDirectory => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    // The app's own install/publish directory -- config.json lives alongside
    // the executable rather than under the user's home/profile, so the app
    // stays self-contained and portable (e.g. on a USB stick or a shared
    // machine). AppContext.BaseDirectory (not Assembly.Location, which is
    // empty under single-file publish) resolves correctly either way.
    public string ConfigDirectory => AppContext.BaseDirectory;

    public bool IsWindows => OperatingSystem.IsWindows();
    public string? GetEnvironmentVariable(string name) => Environment.GetEnvironmentVariable(name);
    public string CurrentDirectory => Directory.GetCurrentDirectory();
}

/// <summary>
/// Filesystem probes used by FindCharacters, injectable so tests can
/// simulate a directory listing or per-entry stat() failing with a
/// permission error without needing real restricted-permission files.
/// </summary>
public interface IPathProbe
{
    bool IsFile(string path);
    List<string> EnumerateEntries(string directory);
}

public class DefaultPathProbe : IPathProbe
{
    public bool IsFile(string path) => File.Exists(path);
    public List<string> EnumerateEntries(string directory) => [.. Directory.EnumerateFileSystemEntries(directory)];
}

public static class Discovery
{
    private static readonly IDiscoveryEnvironment DefaultEnv = new DefaultDiscoveryEnvironment();

    public static string ConfigDir(IDiscoveryEnvironment? env = null) => (env ?? DefaultEnv).ConfigDirectory;

    private static string ConfigFilePath(IDiscoveryEnvironment? env) => Path.Combine(ConfigDir(env), "config.json");

    private static JsonObject LoadConfig(IDiscoveryEnvironment? env)
    {
        try
        {
            string text = File.ReadAllText(ConfigFilePath(env));
            return JsonNode.Parse(text) as JsonObject ?? [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// Read-modify-write a single key so saving one setting (e.g. window
    /// geometry) never clobbers another (e.g. last_dir) already in the file.
    /// Best-effort: if the app's own directory isn't writable (e.g. installed
    /// system-wide into a read-only location), silently skip persisting rather
    /// than crashing -- remembering the folder/geometry is a convenience, not
    /// something the app depends on to function.
    /// </summary>
    private static void SaveConfigValue(string key, string value, IDiscoveryEnvironment? env)
    {
        try
        {
            string cdir = ConfigDir(env);
            Directory.CreateDirectory(cdir);
            JsonObject data = LoadConfig(env);
            data[key] = value;
            File.WriteAllText(ConfigFilePath(env), data.ToJsonString());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    public static string? LoadLastDir(IDiscoveryEnvironment? env = null)
    {
        string? value = LoadConfig(env)["last_dir"] is JsonValue v && v.TryGetValue(out string? s) ? s : null;
        return string.IsNullOrEmpty(value) ? null : value;
    }

    public static void SaveLastDir(string directory, IDiscoveryEnvironment? env = null) =>
        SaveConfigValue("last_dir", directory, env);

    /// <summary>Window geometry string (e.g. '900x600+120+80') from the previous run, if any.</summary>
    public static string? LoadWindowGeometry(IDiscoveryEnvironment? env = null) =>
        LoadConfig(env)["window_geometry"] is JsonValue v && v.TryGetValue(out string? s) ? s : null;

    public static void SaveWindowGeometry(string geometry, IDiscoveryEnvironment? env = null) =>
        SaveConfigValue("window_geometry", geometry, env);

    /// <summary>
    /// "Installed Games" holds one subfolder per installed title (e.g.
    /// "EverQuestLegends") -- the dump files live inside that subfolder, not
    /// in "Installed Games" itself. A single bounded listing, never recursive.
    /// </summary>
    private static List<string> InstalledGamesDirs(string installedGames)
    {
        var outList = new List<string> { installedGames };
        try
        {
            outList.AddRange(Directory.EnumerateDirectories(installedGames));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        return outList;
    }

    /// <summary>Best-effort guesses, checked in addition to whatever the user supplies.</summary>
    public static List<string> CandidateDirs(IDiscoveryEnvironment? envArg = null)
    {
        IDiscoveryEnvironment env = envArg ?? DefaultEnv;
        var candidates = new List<string>();

        string? last = LoadLastDir(env);
        if (last is not null)
        {
            candidates.Add(last);
        }

        string? envDir = env.GetEnvironmentVariable("EQSKYTRACKER_DIR");
        if (!string.IsNullOrEmpty(envDir))
        {
            candidates.Add(envDir);
        }

        string home = env.HomeDirectory;
        if (env.IsWindows)
        {
            string publicDir = env.GetEnvironmentVariable("PUBLIC") ?? @"C:\Users\Public";
            string installedGames = Path.Combine(publicDir, "Daybreak Game Company", "Installed Games");
            candidates.AddRange(InstalledGamesDirs(installedGames));
            candidates.Add(Path.Combine(home, "Documents", "EverQuest"));
        }
        else
        {
            // A handful of shallow, fixed guesses for common Wine/Proton-prefix
            // layouts. Deliberately not a recursive walk over $HOME -- on a
            // real machine that walks the whole home tree (game installs alone
            // can hold tens of thousands of asset files) and can take tens of
            // seconds, which would freeze the GUI before its first paint.
            //
            // Each Wine/Proton prefix is commonly its own directory rather than
            // one shared prefix (e.g. ~/Games/EQLegends, ~/Games/EQQuarm), so
            // check both a wine-roots parent directory itself *and* its
            // immediate subdirectories as candidate prefixes -- still just
            // bounded directory listings, never a walk.
            string[] wineRootParents =
            [
                Path.Combine(home, ".wine"),
                Path.Combine(home, "Games"),
                Path.Combine(home, ".local", "share", "wineprefixes"),
            ];
            var prefixes = new List<string>();
            foreach (string parent in wineRootParents)
            {
                prefixes.Add(parent);
                try
                {
                    prefixes.AddRange(Directory.EnumerateDirectories(parent));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }
            foreach (string prefix in prefixes)
            {
                string installedGames = Path.Combine(prefix, "drive_c", "users", "Public", "Daybreak Game Company", "Installed Games");
                candidates.AddRange(InstalledGamesDirs(installedGames));
            }
            candidates.Add(Path.Combine(home, "Documents", "EverQuest"));
        }

        candidates.Add(env.CurrentDirectory);

        var seen = new HashSet<string>();
        var unique = new List<string>();
        foreach (string c in candidates)
        {
            string resolved;
            try
            {
                resolved = Path.GetFullPath(c);
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException or PathTooLongException)
            {
                continue;
            }
            if (seen.Add(resolved) && Directory.Exists(resolved))
            {
                unique.Add(resolved);
            }
        }
        return unique;
    }

    /// <summary>
    /// Scan a directory (non-recursive) for <name>-Achievements.txt / -Inventory.txt pairs.
    ///
    /// Guarded against filesystem errors (e.g. permission denied): a candidate
    /// directory can exist but not be listable -- notably on macOS, where TCC
    /// can deny directory-listing access to folders like ~/Documents until the
    /// user grants it -- and a permission error here must not crash the app on
    /// startup, it should just mean "no characters found in this folder".
    /// </summary>
    public static List<Character> FindCharacters(string directory, IPathProbe? probeArg = null)
    {
        IPathProbe probe = probeArg ?? new DefaultPathProbe();
        var names = new Dictionary<string, Character>();
        if (!Directory.Exists(directory))
        {
            return [];
        }

        List<string> entries;
        try
        {
            entries = probe.EnumerateEntries(directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }

        const string achievementsSuffix = "-Achievements.txt";
        const string inventorySuffix = "-Inventory.txt";

        foreach (string path in entries)
        {
            bool isFile;
            try
            {
                isFile = probe.IsFile(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }
            if (!isFile)
            {
                continue;
            }

            string fileName = Path.GetFileName(path);
            string? name = null;
            bool isAchievements = false;
            if (fileName.EndsWith(achievementsSuffix, StringComparison.Ordinal))
            {
                name = fileName[..^achievementsSuffix.Length];
                isAchievements = true;
            }
            else if (fileName.EndsWith(inventorySuffix, StringComparison.Ordinal))
            {
                name = fileName[..^inventorySuffix.Length];
            }
            if (name is null)
            {
                continue;
            }

            if (!names.TryGetValue(name, out Character? character))
            {
                character = new Character { Name = name };
                names[name] = character;
            }
            if (isAchievements)
            {
                character.AchievementsPath = path;
            }
            else
            {
                character.InventoryPath = path;
            }
        }
        return [.. names.Values.OrderBy(c => c.Name, StringComparer.Ordinal)];
    }

    public static List<Character> FindAllCharacters(List<string> directories)
    {
        var seen = new Dictionary<string, Character>();
        foreach (string d in directories)
        {
            foreach (Character c in FindCharacters(d))
            {
                seen.TryAdd(c.Name, c);
            }
        }
        return [.. seen.Values.OrderBy(c => c.Name, StringComparer.Ordinal)];
    }
}

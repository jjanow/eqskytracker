using EqSkyTracker.Core;

namespace EqSkyTracker.Tests;

/// <summary>Test-only stand-in for the ambient environment, injected instead of monkeypatching statics.</summary>
public class FakeDiscoveryEnvironment : IDiscoveryEnvironment
{
    public required string HomeDirectory { get; init; }
    public required string ConfigDirectory { get; init; }
    public bool IsWindows { get; init; }
    public Dictionary<string, string> EnvVars { get; init; } = [];
    public required string CurrentDirectory { get; init; }

    public string? GetEnvironmentVariable(string name) => EnvVars.GetValueOrDefault(name);
}

public class ConfigPersistenceTests
{
    [Fact]
    public void SavingOneSettingDoesNotClobberAnother()
    {
        // Regression test: SaveLastDir used to overwrite the whole
        // config.json, so saving window geometry after picking a folder (or
        // vice versa) would silently erase the other setting.
        string tmp = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var env = new FakeDiscoveryEnvironment { HomeDirectory = tmp, ConfigDirectory = tmp, CurrentDirectory = tmp };
            Discovery.SaveLastDir("/some/dir", env);
            Discovery.SaveWindowGeometry("900x600+10+10", env);

            Assert.Equal("/some/dir", Discovery.LoadLastDir(env));
            Assert.Equal("900x600+10+10", Discovery.LoadWindowGeometry(env));
        }
        finally
        {
            Directory.Delete(tmp, recursive: true);
        }
    }

    [Fact]
    public void LoadWindowGeometryMissingReturnsNull()
    {
        string tmp = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var env = new FakeDiscoveryEnvironment { HomeDirectory = tmp, ConfigDirectory = tmp, CurrentDirectory = tmp };
            Assert.Null(Discovery.LoadWindowGeometry(env));
        }
        finally
        {
            Directory.Delete(tmp, recursive: true);
        }
    }

    [Fact]
    public void UnwritableConfigDirDoesNotRaise()
    {
        // Regression test: if the app is installed somewhere the current
        // user can't write to (e.g. a system-wide install on Windows/macOS/
        // Linux), saving a setting used to raise and crash whatever
        // triggered it (picking a folder, closing the window). Persisting
        // the setting is a convenience, not something the app should depend
        // on to keep running.
        //
        // POSIX permission bits don't apply on Windows, and root ignores
        // them entirely, so this regression only has a real filesystem
        // fixture to exercise it on a non-root POSIX system.
        if (OperatingSystem.IsWindows() || UnixIsRoot())
        {
            return;
        }

        string tmp = Directory.CreateTempSubdirectory().FullName;
        string unwritable = Path.Combine(tmp, "readonly");
        Directory.CreateDirectory(unwritable);
        File.SetUnixFileMode(unwritable, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            var env = new FakeDiscoveryEnvironment
            {
                HomeDirectory = tmp,
                ConfigDirectory = Path.Combine(unwritable, "nested"),
                CurrentDirectory = tmp,
            };
            Discovery.SaveLastDir("/some/dir", env); // must not throw
        }
        finally
        {
            File.SetUnixFileMode(unwritable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Directory.Delete(tmp, recursive: true);
        }
    }

    private static bool UnixIsRoot()
    {
        try
        {
            return Environment.UserName == "root";
        }
        catch
        {
            return false;
        }
    }
}

public class CandidateDirsTests
{
    [Fact]
    public void FindsPerGameWinePrefixLayoutWithoutRememberedDir()
    {
        // Regression test: on a fresh install (no config.json yet),
        // CandidateDirs() must still find dump files under a per-game
        // Wine/Proton prefix layout like
        // ~/Games/EQLegends/drive_c/users/Public/Daybreak Game Company/
        // Installed Games/EverQuestLegends/ -- not just a single shared
        // prefix directly under ~/Games.
        string fakeHome = Directory.CreateTempSubdirectory().FullName;
        try
        {
            string gameDir = Path.Combine(
                fakeHome, "Games", "EQLegends", "drive_c", "users", "Public",
                "Daybreak Game Company", "Installed Games", "EverQuestLegends");
            Directory.CreateDirectory(gameDir);
            File.WriteAllText(Path.Combine(gameDir, "Someone_server-Achievements.txt"), "");

            var env = new FakeDiscoveryEnvironment
            {
                HomeDirectory = fakeHome,
                ConfigDirectory = Directory.CreateTempSubdirectory().FullName, // no config.json here
                CurrentDirectory = fakeHome,
                IsWindows = false,
            };
            List<string> dirs = Discovery.CandidateDirs(env);

            Assert.Contains(Path.GetFullPath(gameDir), dirs);
            List<Character> characters = Discovery.FindCharacters(gameDir);
            Assert.Equal("Someone_server", characters[0].Name);
        }
        finally
        {
            Directory.Delete(fakeHome, recursive: true);
        }
    }
}

public class FindCharactersPermissionErrorTests
{
    private class ThrowingEnumerateProbe : IPathProbe
    {
        public bool IsFile(string path) => throw new NotSupportedException();
        public List<string> EnumerateEntries(string directory) => throw new UnauthorizedAccessException("denied");
    }

    private class ThrowingIsFileProbe : IPathProbe
    {
        private readonly IPathProbe _inner = new DefaultPathProbe();
        public bool IsFile(string path) => throw new UnauthorizedAccessException("denied");
        public List<string> EnumerateEntries(string directory) => _inner.EnumerateEntries(directory);
    }

    [Fact]
    public void UnreadableDirectoryReturnsEmptyInsteadOfRaising()
    {
        // Regression test: a candidate directory can exist but not be
        // listable (e.g. macOS TCC denying access to ~/Documents until the
        // user grants it). That must degrade to "no characters here", not
        // crash the app during startup's directory scan.
        string tmp = Directory.CreateTempSubdirectory().FullName;
        try
        {
            Assert.Empty(Discovery.FindCharacters(tmp, new ThrowingEnumerateProbe()));
        }
        finally
        {
            Directory.Delete(tmp, recursive: true);
        }
    }

    [Fact]
    public void UnreadableEntryIsSkippedNotRaised()
    {
        string tmp = Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.WriteAllText(Path.Combine(tmp, "Someone_server-Achievements.txt"), "");
            Assert.Empty(Discovery.FindCharacters(tmp, new ThrowingIsFileProbe()));
        }
        finally
        {
            Directory.Delete(tmp, recursive: true);
        }
    }
}

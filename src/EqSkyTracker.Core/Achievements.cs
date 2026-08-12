// Parser for EverQuest-style "/outputfile achievements" dumps.
//
// File format (tab-delimited, CRLF line endings):
//
//     Untapped Potential: Classes
//     C	Primary Class Unlock - Bard
//     C		Obtain Mask of Song.
//     C		Obtain Mantle of the Songweaver.
//     I		This achievement can be bypassed using a Primary Class Unlock Token.
//
// - A line with no leading "C"/"I" column and no tabs is a category header.
// - A line "C\t<name>" or "I\t<name>" is a top-level achievement, with C/I
//   reflecting whether the game itself considers it complete.
// - A line "C\t\t<text>" or "I\t\t<text>" is a sub-requirement of the most
//   recently seen achievement.
using System.Text.RegularExpressions;

namespace EqSkyTracker.Core;

public partial class Requirement
{
    public required string Text { get; init; }
    public required bool Complete { get; init; }

    /// <summary>The item name if this requirement is an "Obtain X." line, else null.</summary>
    public string? ItemName
    {
        get
        {
            Match m = ObtainRegex().Match(Text);
            return m.Success ? m.Groups[1].Value : null;
        }
    }

    [GeneratedRegex(@"^Obtain (.+?)\.?$")]
    private static partial Regex ObtainRegex();
}

public class Achievement
{
    public required string Name { get; init; }
    public required bool Complete { get; init; }
    public required string Category { get; init; }
    public List<Requirement> Requirements { get; } = [];

    public List<Requirement> ItemRequirements =>
        Requirements.Where(r => r.ItemName is not null).ToList();
}

public class ClassUnlock
{
    public required string ClassName { get; init; }
    public required bool Unlocked { get; init; }
    public required List<Requirement> Items { get; init; }

    public int ObtainedCount => Items.Count(i => i.Complete);
    public int TotalCount => Items.Count;
}

public static partial class Achievements
{
    public static List<Achievement> ParseAchievements(string path)
    {
        var achievements = new List<Achievement>();
        string category = "";
        Achievement? current = null;

        foreach (string line in DumpFile.ReadDumpLines(path))
        {
            if (line.Length == 0)
            {
                continue;
            }
            string[] parts = line.Split('\t');
            string statusFlag = parts[0];
            if (statusFlag != "C" && statusFlag != "I")
            {
                category = line.Trim();
                current = null;
                continue;
            }
            bool complete = statusFlag == "C";
            if (parts.Length == 2)
            {
                current = new Achievement { Name = parts[1].Trim(), Complete = complete, Category = category };
                achievements.Add(current);
            }
            else if (parts.Length >= 3 && current is not null)
            {
                string text = parts[^1].Trim();
                if (text.Length > 0)
                {
                    current.Requirements.Add(new Requirement { Text = text, Complete = complete });
                }
            }
        }
        return achievements;
    }

    [GeneratedRegex(@"^Primary Class Unlock - (.+)$")]
    private static partial Regex ClassUnlockRegex();

    /// <summary>Extract the 'Primary Class Unlock - X' achievements as ClassUnlock records.</summary>
    public static List<ClassUnlock> ClassUnlocks(List<Achievement> achievements)
    {
        var outList = new List<ClassUnlock>();
        foreach (Achievement ach in achievements)
        {
            Match m = ClassUnlockRegex().Match(ach.Name);
            if (!m.Success)
            {
                continue;
            }
            outList.Add(new ClassUnlock
            {
                ClassName = m.Groups[1].Value,
                Unlocked = ach.Complete,
                Items = ach.ItemRequirements,
            });
        }
        return outList;
    }
}

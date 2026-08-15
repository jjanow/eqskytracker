using EqSkyTracker.Core;

namespace EqSkyTracker.Cli;

public static class Program
{
    public static int Main(string[] args) => Run(args);

    private const string Usage = "usage: eqskytracker [-h] [--dir DIR] [--char CHAR] [--all] [--list-chars]";

    private static readonly string HelpText = Usage + """


        Track Plane of Sky class-unlock progress.

        options:
          -h, --help     show this help message and exit
          --dir DIR      Folder containing <Character>-Achievements.txt / -Inventory.txt dumps
          --char CHAR    Character name (e.g. Tholi_rivervale). Required if multiple
                         characters' dumps are in the same folder
          --all          Expand item checklists for classes with all rewards confirmed
                         too (they're always listed)
          --list-chars   List discovered characters and exit
        """;

    private static int Run(string[] args)
    {
        string? dir = null;
        string? charName = null;
        bool showAll = false;
        bool listChars = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-h":
                case "--help":
                    Console.WriteLine(HelpText);
                    return 0;
                case "--dir":
                    if (!TryTakeValue(args, ref i, "--dir", out dir))
                    {
                        return 2;
                    }
                    break;
                case "--char":
                    if (!TryTakeValue(args, ref i, "--char", out charName))
                    {
                        return 2;
                    }
                    break;
                case "--all":
                    showAll = true;
                    break;
                case "--list-chars":
                    listChars = true;
                    break;
                default:
                    Console.Error.WriteLine(Usage);
                    Console.Error.WriteLine($"eqskytracker: error: unrecognized arguments: {args[i]}");
                    return 2;
            }
        }

        if (dir is not null && !Directory.Exists(dir))
        {
            Console.Error.WriteLine($"--dir '{dir}' is not a directory.");
            return 1;
        }

        List<string> dirs = dir is not null ? [dir] : Discovery.CandidateDirs();
        List<Character> characters = Discovery.FindAllCharacters(dirs);

        if (listChars)
        {
            foreach (Character c in characters)
            {
                Console.WriteLine($"{c.Name} - {(c.AchievementsPath is not null ? "achievements" : "no achievements file")} + {(c.InventoryPath is not null ? "inventory" : "no inventory file")}");
            }
            return 0;
        }

        if (characters.Count == 0)
        {
            Console.Error.WriteLine("No character dump files found. Point me at the folder with --dir, " +
                                     "or run '/outputfile achievements' and '/outputfile inventory' in-game first.");
            return 1;
        }

        Character target;
        if (charName is not null)
        {
            Character? match = characters.FirstOrDefault(c => c.Name == charName);
            if (match is null)
            {
                Console.Error.WriteLine($"No dumps found for character '{charName}'. Known: {string.Join(", ", characters.Select(c => c.Name))}");
                return 1;
            }
            target = match;
        }
        else if (characters.Count == 1)
        {
            target = characters[0];
        }
        else
        {
            Console.WriteLine("Multiple characters found, pick one with --char:");
            foreach (Character c in characters)
            {
                Console.WriteLine($" - {c.Name}");
            }
            return 1;
        }

        if (target.AchievementsPath is null)
        {
            Console.Error.WriteLine($"{target.Name} has an inventory dump but no achievements dump -- " +
                                     "run '/outputfile achievements' in-game and try again.");
            return 1;
        }

        if (dir is not null)
        {
            Discovery.SaveLastDir(dir);
        }

        CharacterReport report = Report.BuildReport(target.AchievementsPath, target.InventoryPath);
        PrintReport(report, showAll);
        return 0;
    }

    private static bool TryTakeValue(string[] args, ref int i, string flag, out string? value)
    {
        if (i + 1 >= args.Length)
        {
            Console.Error.WriteLine(Usage);
            Console.Error.WriteLine($"eqskytracker: error: argument {flag}: expected one argument");
            value = null;
            return false;
        }
        i++;
        value = args[i];
        return true;
    }

    private static void PrintReport(CharacterReport report, bool showComplete)
    {
        Console.WriteLine();
        Console.WriteLine($"{report.CharacterName} -- Plane of Sky reward sets: {report.RewardCompleteCount}/{report.TotalClasses} complete " +
                           $"({report.UnlockedCount} classes unlocked)");
        Console.WriteLine();

        foreach (ClassReport cls in report.Classes.OrderBy(c => c.RewardComplete).ThenBy(c => c.ClassName, StringComparer.Ordinal))
        {
            if (cls.RewardComplete && !showComplete)
            {
                Console.WriteLine($"  [{"DONE",-8}] {cls.ClassName} ({cls.ObtainedCount}/{cls.TotalCount})");
                continue;
            }
            string mark = cls.RewardComplete ? "DONE" : cls.AutoCompleted ? "SHORTCUT" : "    ";
            Console.WriteLine($"  [{mark,-8}] {cls.ClassName} ({cls.ObtainedCount}/{cls.TotalCount})");
            if (cls.AutoCompleted && !cls.VerifiedFromInventory)
            {
                Console.WriteLine("            -> unlocked via shortcut; the achievement flags can't be trusted for this class." +
                                   " Add --dir pointing at a folder with an inventory dump to verify which rewards you actually have.");
            }
            foreach (ItemStatus item in cls.Items)
            {
                string box = item.Complete ? "x" : " ";
                string line = $"        [{box}] {item.Name}";
                if (!item.Complete)
                {
                    if (item.InInventory)
                    {
                        line += "  (already in your bags/bank!)";
                    }
                    if (item.Hint is { Found: true, HowToObtain: { } howToObtain })
                    {
                        line += $"\n            -> {howToObtain}";
                    }
                    if (item.Readiness is { } readiness)
                    {
                        if (readiness.ReadyToTurnIn)
                        {
                            line += "\n            -> READY TO TURN IN: all components are in your bags/bank/keyring.";
                        }
                        else if (readiness.AllTrackableComponentsPresent)
                        {
                            line += "\n            -> Components ready; still need to confirm the Wind Rune in your alternate-currency window.";
                        }
                        else
                        {
                            line += $"\n            -> {readiness.Have}/{readiness.Total} components in bags so far.";
                        }
                    }
                }
                Console.WriteLine(line);
            }
        }
        Console.WriteLine();

        if (report.FarmedItems.Count > 0)
        {
            List<FarmedItemStatus> needed = [.. report.FarmedItems.Where(f => !f.SafeToSell)];
            List<FarmedItemStatus> sellable = [.. report.FarmedItems.Where(f => f.SafeToSell)];
            Console.WriteLine("Plane of Sky turn-in components currently in your bags/bank/keyring:");
            if (needed.Count > 0)
            {
                Console.WriteLine("  Still needed -- keep these:");
                foreach (FarmedItemStatus f in needed)
                {
                    string where = string.Join(", ", f.Locations);
                    Console.WriteLine($"    [keep] {f.Name} x{f.Count} ({where}) -- needed for: {string.Join(", ", f.NeededFor)}");
                }
            }
            if (sellable.Count > 0)
            {
                Console.WriteLine("  Not needed for anything incomplete -- safe to sell/destroy:");
                foreach (FarmedItemStatus f in sellable)
                {
                    string where = string.Join(", ", f.Locations);
                    Console.WriteLine($"    [sell] {f.Name} x{f.Count} ({where})");
                }
            }
            Console.WriteLine();
        }
    }
}

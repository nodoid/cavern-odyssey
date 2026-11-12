using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SuperSqij;

public sealed class HighScoreEntry
{
    public string Name = HighScores.DefaultName;
    public int Score;
    public int Fragments;
    public int Doors;
    public int Fed;
    public Difficulty Difficulty;
    public float Seconds;
    public bool Victory;
    public DateTime When;
}

/// <summary>
/// Top-ten table kept in the user's application data folder, so scores survive quitting
/// and relaunching the game on every platform.
/// </summary>
public static class HighScores
{
    public const int MaxEntries = 10;

    /// <summary>The name every first-run entry starts with.</summary>
    public const string DefaultName = "Kip";

    private static string Folder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SuperSqij");

    public static string DefaultPath => Path.Combine(Folder, "highscores.txt");

    private static string NamePath => Path.Combine(Folder, "name.txt");

    /// <summary>The name attached to new scores. Defaults to KIP on a fresh install.</summary>
    public static string PlayerName
    {
        get
        {
            try
            {
                if (File.Exists(NamePath))
                {
                    string name = File.ReadAllText(NamePath).Trim();
                    if (name.Length > 0) return name;
                }
            }
            catch { }
            return DefaultName;
        }
        set
        {
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(NamePath, value.Trim());
            }
            catch { }
        }
    }

    /// <summary>Score multiplier each difficulty awards.</summary>
    public static int Multiplier(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => 1,
        Difficulty.Medium => 5,
        Difficulty.Hard => 10,
        _ => 1,
    };

    /// <summary>
    /// Loads the table. On a first run there is no file yet, so a starter table is
    /// written out (and saved) instead of showing an empty board.
    /// </summary>
    public static List<HighScoreEntry> Load()
    {
        if (!File.Exists(DefaultPath)) return Seed();

        var entries = new List<HighScoreEntry>();
        try
        {
            foreach (var line in File.ReadAllLines(DefaultPath))
            {
                var parts = line.Split('|');
                if (parts.Length < 7) continue;
                if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int score)) continue;
                var entry = new HighScoreEntry
                {
                    Name = parts.Length > 8 && parts[8].Trim().Length > 0 ? parts[8].Trim() : DefaultName,
                    Score = score,
                    Fragments = ParseInt(parts[1]),
                    Doors = ParseInt(parts[2]),
                    Fed = ParseInt(parts[3]),
                    Difficulty = ParseDifficulty(parts[4]),
                    Seconds = ParseFloat(parts[5]),
                    Victory = parts[6] == "1",
                };
                if (parts.Length > 7 &&
                    DateTime.TryParse(parts[7], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var when))
                    entry.When = when;
                entries.Add(entry);
            }
        }
        catch
        {
            // A corrupt or unreadable table must never stop the game from starting.
        }
        Sort(entries);
        if (entries.Count > MaxEntries) entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);
        return entries;
    }

    /// <summary>First-run table: one house score per difficulty so the board reads at a glance.</summary>
    private static List<HighScoreEntry> Seed()
    {
        var now = DateTime.Now;
        var entries = new List<HighScoreEntry>
        {
            new()
            {
                Name = DefaultName, Score = 42000, Fragments = World.FragmentTotal, Doors = World.DoorTotal,
                Fed = World.FruitTotal, Difficulty = Difficulty.Hard, Seconds = 512f, Victory = true,
                When = now.AddDays(-3),
            },
            new()
            {
                Name = DefaultName, Score = 14500, Fragments = 18, Doors = 15, Fed = 24,
                Difficulty = Difficulty.Medium, Seconds = 845f, Victory = false, When = now.AddDays(-2),
            },
            new()
            {
                Name = DefaultName, Score = 2600, Fragments = 9, Doors = 6, Fed = 9,
                Difficulty = Difficulty.Easy, Seconds = 431f, Victory = false, When = now.AddDays(-1),
            },
        };
        Save(entries);
        return entries;
    }

    /// <summary>True when a run of this score would make the top ten.</summary>
    public static bool WouldQualify(int score)
    {
        if (score <= 0) return false;
        var entries = Load();
        return entries.Count < MaxEntries || score > entries[^1].Score;
    }

    /// <summary>Adds the entry, saves the table, and returns its 1-based rank (0 if it missed the table).</summary>
    public static int Submit(HighScoreEntry entry)
    {
        var entries = Load();
        entries.Add(entry);
        Sort(entries);
        int rank = entries.IndexOf(entry) + 1;
        if (entries.Count > MaxEntries) entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);
        Save(entries);
        return rank <= MaxEntries ? rank : 0;
    }

    private static void Save(List<HighScoreEntry> entries)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllLines(DefaultPath, entries.Select(Format));
        }
        catch
        {
            // Scores are a nicety; failing to save should not break the run.
        }
    }

    /// <summary>Difficulty-weighted score for a finished run.</summary>
    public static int ScoreFor(World world, float seconds, bool victory)
    {
        int baseScore = world.FragmentsCollected * 500 +
                        world.DoorsOpened * 75 +
                        (World.FruitTotal - world.FruitCount) * 20 +
                        (victory ? 2500 : 0);
        if (victory) baseScore += Math.Max(0, 1200 - (int)seconds) * 2;
        return baseScore * Multiplier(world.Difficulty);
    }

    private static void Sort(List<HighScoreEntry> entries) =>
        entries.Sort((a, b) =>
        {
            int byScore = b.Score.CompareTo(a.Score);
            if (byScore != 0) return byScore;
            int byTime = a.Seconds.CompareTo(b.Seconds);
            if (byTime != 0) return byTime;
            return a.When.CompareTo(b.When);
        });

    private static string Format(HighScoreEntry e) => string.Join('|',
        e.Score.ToString(CultureInfo.InvariantCulture),
        e.Fragments.ToString(CultureInfo.InvariantCulture),
        e.Doors.ToString(CultureInfo.InvariantCulture),
        e.Fed.ToString(CultureInfo.InvariantCulture),
        ((int)e.Difficulty).ToString(CultureInfo.InvariantCulture),
        e.Seconds.ToString("0.##", CultureInfo.InvariantCulture),
        e.Victory ? "1" : "0",
        e.When.ToString("o", CultureInfo.InvariantCulture),
        string.IsNullOrWhiteSpace(e.Name) ? DefaultName : e.Name.Trim());

    private static int ParseInt(string s) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;

    private static float ParseFloat(string s) =>
        float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;

    private static Difficulty ParseDifficulty(string s) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) &&
        v >= (int)Difficulty.Easy && v <= (int)Difficulty.Hard
            ? (Difficulty)v
            : Difficulty.Medium;
}

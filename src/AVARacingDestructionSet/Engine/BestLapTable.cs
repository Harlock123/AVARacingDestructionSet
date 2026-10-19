namespace AVARacingDestructionSet.Engine;

/// <summary>A best lap: who, how long (in milliseconds), in what.</summary>
public record LapEntry(string Initials, int Millis, string Vehicle)
{
    public static string Format(double seconds) => seconds <= 0 ? "-:--.--" : $"{(int)(seconds / 60)}:{seconds % 60:00.00}";
    public string Time => Format(Millis / 1000.0);
}

/// <summary>
/// A track's ten best laps, kept in the user's application-data folder (one file for each track, by its name). A
/// fresh one starts with modest made-up times, the way arcade machines shipped their scores, so there is something to beat.
/// </summary>
public class BestLapTable
{
    public const int Size = 10;
    public const int InitialsLength = 3;

    public static readonly string DefaultFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AVARacingDestructionSet");

    private static readonly string[] DefaultInitials = ["RDS", "AVA", "EA.", "VRM", "OIL", "BAJ", "MOO", "JMP", "REV", "LAP"];
    private static readonly string[] DefaultVehicles = ["STOCK CAR", "INDY CAR", "BAJA BUG", "PICKUP", "DRAGSTER"];

    private readonly string _folder, _file;
    private readonly List<LapEntry> _entries = [];
    private string TablePath => Path.Combine(_folder, _file);

    public BestLapTable(string? folder = null, string? trackName = null)
    {
        _folder = folder ?? DefaultFolder;
        TrackName = trackName ?? "TRACK";
        _file = $"laps-{TrackFile.Slug(TrackName)}.txt";
    }

    public string TrackName { get; }
    public string Folder => _folder;
    public IReadOnlyList<LapEntry> Entries => _entries;
    public int Best => _entries.Count > 0 ? _entries[0].Millis : 0;

    /// <summary>The initials entered last time, offered as the starting point next time.</summary>
    public string LastInitials { get; private set; } = "AAA";

    /// <summary>A track's best laps; a new track's start with made-up times a little slower than <paramref name="par"/> seconds.</summary>
    public static BestLapTable Load(string? folder, string trackName, double par)
    {
        var table = new BestLapTable(folder, trackName);
        try
        {
            if (File.Exists(table.TablePath))
            {
                foreach (var line in File.ReadAllLines(table.TablePath))
                {
                    if (line.StartsWith("#last "))
                    {
                        table.LastInitials = Clean(line[6..]);
                        continue;
                    }
                    var parts = line.Split('|');
                    if (parts.Length == 3 && int.TryParse(parts[1], out int ms) && ms > 0)
                        table._entries.Add(new LapEntry(Clean(parts[0]), ms, parts[2].Trim().ToUpperInvariant()));
                }
            }
            else table.FillDefaults(par);
        }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException)
        {
            // An unreadable table just starts fresh.
            table._entries.Clear();
            table.FillDefaults(par);
        }
        table._entries.Sort((a, b) => a.Millis.CompareTo(b.Millis));
        if (table._entries.Count > Size) table._entries.RemoveRange(Size, table._entries.Count - Size);
        return table;
    }

    private void FillDefaults(double par)
    {
        for (int i = 0; i < Size; i++)
            _entries.Add(new LapEntry(DefaultInitials[i], (int)Math.Round((par * 1.12 + i * par * 0.03) * 1000), DefaultVehicles[i % DefaultVehicles.Length]));
    }

    public bool Qualifies(int millis) => millis > 0 && (_entries.Count < Size || millis < _entries[^1].Millis);

    /// <summary>Adds a lap and returns its place (0 = the best), or -1 if it didn't make the table.</summary>
    public int Insert(string initials, int millis, string vehicle)
    {
        if (!Qualifies(millis)) return -1;
        // A new time goes above an equal older one: it was set more recently.
        int index = _entries.FindIndex(e => millis <= e.Millis);
        if (index < 0) index = _entries.Count;
        _entries.Insert(index, new LapEntry(Clean(initials), millis, vehicle));
        if (_entries.Count > Size) _entries.RemoveRange(Size, _entries.Count - Size);
        LastInitials = Clean(initials);
        return index;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(_folder);
            var lines = new List<string> { $"#last {LastInitials}" };
            lines.AddRange(_entries.Select(e => $"{e.Initials}|{e.Millis}|{e.Vehicle}"));
            // Write then swap, so a crash mid-write can't leave a half-written table.
            string temp = TablePath + ".tmp";
            File.WriteAllLines(temp, lines);
            File.Move(temp, TablePath, overwrite: true);
        }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException)
        {
            // A read-only home folder just means the laps aren't remembered.
        }
    }

    private static string Clean(string initials)
    {
        var chars = initials.ToUpperInvariant().Where(c => InitialsEntry.Alphabet.Contains(c)).Take(InitialsLength).ToArray();
        return new string(chars).PadRight(InitialsLength, ' ');
    }
}

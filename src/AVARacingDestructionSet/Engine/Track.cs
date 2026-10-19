using System.Text.Json;
using System.Text.Json.Serialization;

namespace AVARacingDestructionSet.Engine;

/// <summary>The pieces in the box: roads that join at the middles of a square's edges.</summary>
public enum PieceKind { Straight, Curve, Banked, Cross, Bridge, Hill, Dip, Ramp, Start }

/// <summary>What the road is made of, the whole track over.</summary>
public enum Surface { Asphalt, Dirt, Ice }

/// <summary>Where the track is, which is how hard things fall and how hard tyres are pressed on the road.</summary>
public enum Planet { Moon, Mars, Venus, Earth, Jupiter }

/// <summary>How a road rises and falls across its square.</summary>
public enum Rise { Flat, Bridge, Hill, Dip, Ramp }

/// <summary>A road across a square: from one edge to another (0 north, 1 east, 2 south, 3 west), how it rises, and whether it's banked.</summary>
public readonly record struct Road(int A, int B, Rise Rise, bool Banked = false)
{
    public bool Straight => (A + 2) % 4 == B;
}

/// <summary>A piece on the grid: what it is, which square, and how many quarter turns clockwise it's been given.</summary>
public sealed class Piece
{
    public PieceKind Kind { get; set; }
    public int Col { get; set; }
    public int Row { get; set; }
    public int Rot { get; set; }

    public Piece() { }
    public Piece(PieceKind kind, int col, int row, int rot = 0) => (Kind, Col, Row, Rot) = (kind, col, row, rot);
    public Piece Copy() => (Piece)MemberwiseClone();
    public override string ToString() => $"{Kind} {Track.CellName(Col, Row)} r{Rot}";
}

public static class Pieces
{
    /// <summary>A square's side, in world units; the road's half width; the rises' heights.</summary>
    public const double Cell = 128, HalfWidth = 26, BridgeHeight = 30, HillHeight = 20, DipDepth = 14, RampHeight = 20, RampLip = 0.72;

    public static readonly PieceKind[] Box =
        [PieceKind.Straight, PieceKind.Curve, PieceKind.Banked, PieceKind.Cross, PieceKind.Bridge, PieceKind.Hill, PieceKind.Dip, PieceKind.Ramp, PieceKind.Start];

    public static string Name(PieceKind k) => k switch
    {
        PieceKind.Straight => "STRAIGHT", PieceKind.Curve => "CURVE", PieceKind.Banked => "BANKED TURN", PieceKind.Cross => "CROSSOVER",
        PieceKind.Bridge => "BRIDGE", PieceKind.Hill => "HILL", PieceKind.Dip => "DIP", PieceKind.Ramp => "JUMP RAMP", _ => "START LINE",
    };

    public static string Short(PieceKind k) => k switch
    {
        PieceKind.Banked => "BANKED", PieceKind.Cross => "CROSS", PieceKind.Ramp => "RAMP", PieceKind.Start => "START", _ => Name(k),
    };

    public static string Describe(PieceKind k) => k switch
    {
        PieceKind.Straight => "A PLAIN STRAIGHT. FLAT OUT.",
        PieceKind.Curve => "A QUARTER TURN. BRAKE BEFORE IT OR SLIDE OFF.",
        PieceKind.Banked => "A TURN TILTED INWARD: IT GRIPS HARDER, SO TAKE IT FASTER.",
        PieceKind.Cross => "TWO ROADS ACROSS EACH OTHER, FLAT. MIND THE OTHER CAR.",
        PieceKind.Bridge => "ONE ROAD OVER THE OTHER, ON A HUMPBACK BRIDGE. HOP IT AT SPEED.",
        PieceKind.Hill => "A HILL: FAST OVER THE TOP AND YOU'RE IN THE AIR. LOW GRAVITY: HIGHER.",
        PieceKind.Dip => "A DIP IN THE ROAD. LAND HARD IN IT ON JUPITER AND IT HURTS.",
        PieceKind.Ramp => "A JUMP. IT FACES ONE WAY. ON THE MOON YOU'LL FLY HALF THE TRACK.",
        _ => "THE START AND FINISH. IT POINTS THE WAY THE RACE GOES.",
    };

    /// <summary>How many different ways round a piece can be put down.</summary>
    public static int Turns(PieceKind k) => k switch
    {
        PieceKind.Cross => 1,
        PieceKind.Straight or PieceKind.Hill or PieceKind.Dip or PieceKind.Bridge => 2,
        _ => 4,
    };

    /// <summary>A ramp or the start line points one way: the way across it, as a heading (0 north).</summary>
    public static bool OneWay(PieceKind k) => k is PieceKind.Ramp or PieceKind.Start;

    /// <summary>The roads across a piece as put down: unturned, a straight runs north to south and a curve north to east; a one-way piece runs south to north.</summary>
    public static Road[] Roads(Piece p)
    {
        int r = ((p.Rot % 4) + 4) % 4;
        int E(int e) => (e + r) % 4;
        return p.Kind switch
        {
            PieceKind.Straight => [new(E(0), E(2), Rise.Flat)],
            PieceKind.Curve => [new(E(0), E(1), Rise.Flat)],
            PieceKind.Banked => [new(E(0), E(1), Rise.Flat, Banked: true)],
            PieceKind.Cross => [new(0, 2, Rise.Flat), new(1, 3, Rise.Flat)],
            // The bridge: the road that runs north to south (turned: east to west) goes over.
            PieceKind.Bridge => [new(E(0), E(2), Rise.Bridge), new(E(1), E(3), Rise.Flat)],
            PieceKind.Hill => [new(E(0), E(2), Rise.Hill)],
            PieceKind.Dip => [new(E(0), E(2), Rise.Dip)],
            PieceKind.Ramp => [new(E(2), E(0), Rise.Ramp)],
            _ => [new(E(2), E(0), Rise.Flat)],
        };
    }

    /// <summary>The height of a road a fraction s of the way across, going the way it was laid (from A to B).</summary>
    public static double Height(Rise rise, double s) => rise switch
    {
        Rise.Bridge => BridgeHeight * (1 - Math.Cos(2 * Math.PI * s)) / 2,
        Rise.Hill => HillHeight * (1 - Math.Cos(2 * Math.PI * s)) / 2,
        Rise.Dip => -DipDepth * (1 - Math.Cos(2 * Math.PI * s)) / 2,
        Rise.Ramp => s < RampLip ? RampHeight * s / RampLip : 0,
        _ => 0,
    };

    /// <summary>The middle of a square's edge, from the square's top-left corner.</summary>
    public static Vec Mid(int edge) => edge switch
    {
        0 => new Vec(Cell / 2, 0), 1 => new Vec(Cell, Cell / 2), 2 => new Vec(Cell / 2, Cell), _ => new Vec(0, Cell / 2),
    };

    /// <summary>A step across an edge, in squares.</summary>
    public static (int DC, int DR) Step(int edge) => edge switch { 0 => (0, -1), 1 => (1, 0), 2 => (0, 1), _ => (-1, 0) };

    /// <summary>Points along a road from edge <paramref name="from"/> to its other end, a fraction <paramref name="s"/> of the way, from the square's corner.</summary>
    public static Vec Along(Road road, int from, double s)
    {
        int to = from == road.A ? road.B : road.A;
        if (road.Straight) return Mid(from) + (Mid(to) - Mid(from)) * s;
        // A quarter circle round the corner the two edges share.
        var corner = new Vec(from == 1 || to == 1 ? Cell : 0, from == 2 || to == 2 ? Cell : 0);
        double a0 = (Mid(from) - corner).Angle, a1 = (Mid(to) - corner).Angle;
        double a = a0 + Field.AngleDiff(a0, a1) * s;
        return corner + Vec.FromAngle(a) * (Cell / 2);
    }

    /// <summary>How long a road is, edge to edge.</summary>
    public static double Length(Road road) => road.Straight ? Cell : Math.PI / 2 * Cell / 2;
}

/// <summary>What's wrong with a track, if anything, and which square to look at.</summary>
public sealed record Check(bool Ok, string Why, int Col = -1, int Row = -1)
{
    public static Check Good(string why) => new(true, why);
    public static Check Bad(string why, int col = -1, int row = -1) => new(false, why, col, row);
}

/// <summary>A track: pieces on a grid of squares, what the road is made of, and the planet it's on.</summary>
public sealed class Track
{
    public const int Cols = 10, Rows = 8;
    public const int NameLength = 20;

    public string Name { get; set; } = "NEW TRACK";
    public Surface Surface { get; set; } = Surface.Asphalt;
    public Planet Planet { get; set; } = Planet.Earth;
    public List<Piece> Pieces { get; set; } = [];

    public static bool OnGrid(int col, int row) => col is >= 0 and < Cols && row is >= 0 and < Rows;

    /// <summary>A square by its column letter and row number, as the editor shows them: "D5".</summary>
    public static string CellName(int col, int row) => $"{(char)('A' + col)}{row + 1}";

    public Piece? At(int col, int row) => Pieces.FirstOrDefault(p => p.Col == col && p.Row == row);

    public Track Copy() => new() { Name = Name, Surface = Surface, Planet = Planet, Pieces = Pieces.Select(p => p.Copy()).ToList() };

    /// <summary>Puts a piece down, replacing whatever was in its square.</summary>
    public void Put(Piece p)
    {
        Pieces.RemoveAll(q => q.Col == p.Col && q.Row == p.Row);
        Pieces.Add(p);
    }

    public bool Remove(int col, int row) => Pieces.RemoveAll(q => q.Col == col && q.Row == row) > 0;

    /// <summary>Puts right a track read from a file: on the grid, one piece a square, turns in range, a sensible name.</summary>
    public void Tidy()
    {
        Name = CleanName(Name);
        Pieces ??= [];
        var seen = new HashSet<(int, int)>();
        var kept = new List<Piece>();
        for (int i = Pieces.Count - 1; i >= 0; i--)
        {
            var p = Pieces[i];
            if (p == null || !OnGrid(p.Col, p.Row) || !Enum.IsDefined(p.Kind) || !seen.Add((p.Col, p.Row))) continue;
            p.Rot = ((p.Rot % 4) + 4) % 4;
            kept.Insert(0, p);
        }
        Pieces = kept;
        if (!Enum.IsDefined(Surface)) Surface = Surface.Asphalt;
        if (!Enum.IsDefined(Planet)) Planet = Planet.Earth;
    }

    public static string CleanName(string? name)
    {
        var kept = new string((name ?? "").ToUpperInvariant().Where(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' or ' ' or '-' or '!' or '.' or '\'').ToArray());
        var s = string.Join(' ', kept.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        s = s[..Math.Min(s.Length, NameLength)].Trim();
        return s.Length == 0 ? "NEW TRACK" : s;
    }

    /// <summary>Whether the track is one closed loop, and if not, why not.</summary>
    public Check Check() => Course.Trace(this).Check;

    public static string Describe(Surface s) => s switch { Surface.Asphalt => "ASPHALT", Surface.Dirt => "DIRT", _ => "ICE" };
    public static string Describe(Planet p) => p.ToString().ToUpperInvariant();

    /// <summary>How hard things fall, as a fraction of the Earth's.</summary>
    public static double Gravity(Planet p) => p switch
    {
        Planet.Moon => 0.166, Planet.Mars => 0.38, Planet.Venus => 0.9, Planet.Jupiter => 2.53, _ => 1.0,
    };
}

/// <summary>Tracks to and from their JSON files.</summary>
public static class TrackFile
{
    public const string Format = "AVARacingDestructionSet track";
    public const int Version = 1;
    public const string Extension = ".rds.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private sealed class Envelope
    {
        public string Format { get; set; } = TrackFile.Format;
        public int Version { get; set; } = TrackFile.Version;
        public Track? Track { get; set; }
    }

    public static string ToJson(Track t) => JsonSerializer.Serialize(new Envelope { Track = t }, Options);

    public static Track? FromJson(string json)
    {
        try
        {
            var e = JsonSerializer.Deserialize<Envelope>(json, Options);
            if (e?.Format != Format || e.Version > Version || e.Track is not { } t) return null;
            t.Tidy();
            return t;
        }
        catch (Exception x) when (x is JsonException or NotSupportedException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }

    public static void Save(Track t, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        // Write then swap, so a crash mid-write can't leave half a track.
        string temp = path + ".tmp";
        File.WriteAllText(temp, ToJson(t));
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>A saved track, or null if the file isn't one.</summary>
    public static Track? Load(string path)
    {
        try
        {
            return FromJson(File.ReadAllText(path));
        }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>A file name from a track's name: "OXBOW OVAL" is oxbow-oval.rds.json.</summary>
    public static string Slug(string name)
    {
        var s = new string(name.ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        while (s.Contains("--")) s = s.Replace("--", "-");
        return s.Length == 0 ? "untitled" : s;
    }

    /// <summary>The tracks saved in a folder, by name.</summary>
    public static List<(string Name, string Path)> List(string folder)
    {
        var list = new List<(string, string)>();
        try
        {
            if (!Directory.Exists(folder)) return list;
            foreach (var f in Directory.GetFiles(folder, "*" + Extension).Order())
                if (Load(f) is { } t) list.Add((t.Name, f));
        }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException)
        {
        }
        return list;
    }
}

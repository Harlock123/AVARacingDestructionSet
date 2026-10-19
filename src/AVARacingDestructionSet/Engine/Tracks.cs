namespace AVARacingDestructionSet.Engine;

/// <summary>
/// The ten tracks that come in the box, and the blank one. Each is written as a drive round it, square by square
/// from the start line: S straight, L and R curves (l and r banked), H hill, D dip, J jump ramp, and where the road
/// crosses one already laid, X for a flat crossover, B to go over it on a bridge or U to go under. A number repeats.
/// </summary>
public static class Tracks
{
    private sealed record Design(string Name, Surface Surface, Planet Planet, int Col, int Row, int Heading, string Drive, string About);

    private static readonly Design[] Designs =
    [
        new("OXBOW OVAL", Surface.Asphalt, Planet.Earth, 4, 1, 1, "F S3 r S4 r S2 H S H S r S4 r S2", "FOUR BANKED TURNS AND TWO HILLS: FLAT OUT ALL THE WAY ROUND."),
        new("FIGURE EIGHT FAIR", Surface.Asphalt, Planet.Earth, 2, 4, 1, "F S3 S2 r S2 r S2 r S2 X S2 L S3 L S2 L", "THE COUNTY FAIR'S FIGURE EIGHT: THE CROSSING IS WHERE IT HAPPENS."),
        new("KNOTTED RIBBON", Surface.Asphalt, Planet.Earth, 2, 3, 1, "F S S S S r S l r r S3 r S2 B S L S2 L S L", "A FIGURE EIGHT THAT GOES OVER ITSELF ON A BRIDGE, WITH AN ESSES."),
        new("PARCO VELOCE", Surface.Asphalt, Planet.Earth, 3, 1, 1, "F S4 r S4 r S R L S L R S2 r S4 r S", "LONG STRAIGHTS, A CHICANE AND TWO BANKED BENDS: TOP SPEED COUNTS."),
        new("MUDSLIDE GULCH", Surface.Dirt, Planet.Earth, 2, 1, 1, "F S H S r D l S r S H r S D S r l S S r S H S r", "DIRT, HILLS AND DIPS DOWN A WINDING GULCH. KNOBBY TYRES."),
        new("FROSTBITE RING", Surface.Ice, Planet.Earth, 3, 6, 1, "F S3 l S4 l S l S r S3 l S2 l S", "SHEET ICE. BRAKE EARLY, STEER GENTLY, AND PRAY FOR STUDS."),
        new("CRATER RIM", Surface.Dirt, Planet.Moon, 2, 1, 1, "F J S5 r S2 H S r S J S5 r S4 r", "ROUND THE RIM OF A CRATER ON THE MOON. THE JUMPS GO ON FOR EVER."),
        new("RED PLANET RALLY", Surface.Dirt, Planet.Mars, 3, 2, 1, "F S J S2 r S H r S3 r S2 B l S l l", "MARTIAN DUST, A BRIDGE AND A JUMP OFF THE END OF IT."),
        new("JUPITER GRIND", Surface.Asphalt, Planet.Jupiter, 2, 6, 1, "F S2 H S2 L S H S L S L S R S R S2 L S2 L S H S2 L", "TWO AND A HALF TIMES EARTH'S GRAVITY: GRIP FOR DAYS, BUT EVERY BUMP HURTS."),
        new("SPAGHETTI JUNCTION", Surface.Asphalt, Planet.Earth, 4, 4, 1, "F S S S r S r S r S B S l H S l S S S r S r S r S X", "OVER, UNDER AND ACROSS: TWO CROSSINGS AND A BRIDGE IN A KNOT."),
    ];

    public static readonly string[] Names = [.. Designs.Select(d => d.Name)];

    public static string About(string name) => Designs.FirstOrDefault(d => d.Name == name)?.About ?? "";

    public static bool IsBuiltIn(string name) => Names.Contains(name);

    public static Track Make(string name)
    {
        var d = Designs.First(x => x.Name == name);
        var t = Walk(d.Col, d.Row, d.Heading, d.Drive);
        t.Name = d.Name;
        t.Surface = d.Surface;
        t.Planet = d.Planet;
        return t;
    }

    public static Track Blank() => new() { Name = "NEW TRACK" };

    /// <summary>Lays a track by driving it: from a square, heading one way (0 north, 1 east...), piece by piece.</summary>
    public static Track Walk(int col, int row, int heading, string drive)
    {
        var t = new Track();
        int h = heading;
        foreach (char ch in Expand(drive))
        {
            var here = t.At(col, row);
            switch (ch)
            {
                case 'X' or 'B' or 'U':
                    if (here is not { Kind: PieceKind.Straight } || here.Rot % 2 == h % 2)
                        throw new ArgumentException($"nothing to cross at {Track.CellName(col, row)}");
                    // A bridge's turn-0 road (north-south) goes over: B puts this road over, U the one already there.
                    int over = ch == 'U' ? here.Rot % 2 : h % 2;
                    t.Put(new Piece(ch == 'X' ? PieceKind.Cross : PieceKind.Bridge, col, row, ch == 'X' ? 0 : over));
                    break;
                default:
                    if (here != null) throw new ArgumentException($"{Track.CellName(col, row)} is laid twice");
                    t.Put(Lay(ch, col, row, h));
                    break;
            }
            if (ch is 'L' or 'l') h = (h + 3) % 4;
            if (ch is 'R' or 'r') h = (h + 1) % 4;
            var (dc, dr) = Pieces.Step(h);
            col += dc;
            row += dr;
        }
        return t;
    }

    /// <summary>The piece for one step of a drive, going in at heading h.</summary>
    private static Piece Lay(char ch, int col, int row, int h)
    {
        // A curve turned r joins edges r and r+1: going right from heading h it joins the edge behind (h+2) and h+1.
        int right = (h + 1) % 4, left = (h + 2) % 4;
        return ch switch
        {
            'S' => new Piece(PieceKind.Straight, col, row, h % 2),
            'H' => new Piece(PieceKind.Hill, col, row, h % 2),
            'D' => new Piece(PieceKind.Dip, col, row, h % 2),
            'J' => new Piece(PieceKind.Ramp, col, row, h),
            'F' => new Piece(PieceKind.Start, col, row, h),
            'R' => new Piece(PieceKind.Curve, col, row, right),
            'r' => new Piece(PieceKind.Banked, col, row, right),
            'L' => new Piece(PieceKind.Curve, col, row, left),
            'l' => new Piece(PieceKind.Banked, col, row, left),
            _ => throw new ArgumentException($"what is '{ch}'?"),
        };
    }

    private static IEnumerable<char> Expand(string drive)
    {
        for (int i = 0; i < drive.Length; i++)
        {
            char c = drive[i];
            if (c == ' ') continue;
            int j = i + 1;
            while (j < drive.Length && char.IsAsciiDigit(drive[j])) j++;
            int n = j > i + 1 ? int.Parse(drive[(i + 1)..j]) : 1;
            for (int k = 0; k < n; k++) yield return c;
            i = j - 1;
        }
    }
}

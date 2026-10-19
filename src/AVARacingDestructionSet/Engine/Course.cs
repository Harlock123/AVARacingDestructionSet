namespace AVARacingDestructionSet.Engine;

/// <summary>One square's road, as the loop goes across it: which piece, which of its roads, and the edge it comes in at.</summary>
public readonly record struct Visit(int Piece, Road Road, int From)
{
    public int To => From == Road.A ? Road.B : Road.A;
}

/// <summary>
/// A track laid out for racing: the loop from the start line round to it again, sampled every few units, with the
/// road's height and whether it's banked at each sample. Everything about where a car is goes by its nearest
/// sample: how far round it is, which way the road runs, how high it is, how sharply it bends.
/// </summary>
/// <summary>A rise in the road a car can leave the ground from: where it starts, what it is, and how far the road runs on straight from there (room to land).</summary>
public readonly record struct Hump(int Start, Rise Rise, double Runout);

public sealed class Course
{
    public const double Spacing = 4;

    public Track Track { get; }
    public Visit[] Visits { get; }
    public Vec[] Points { get; }
    /// <summary>The road's height at each sample (a car on it stands this high).</summary>
    public double[] Z { get; }
    /// <summary>How far round the loop each sample is, from the start line.</summary>
    public double[] Along { get; }
    public double Length { get; }
    /// <summary>The piece each sample is on (an index into the track's pieces).</summary>
    public int[] Piece { get; }
    public bool[] Banked { get; }
    public Rise[] Rises { get; }
    /// <summary>How far across its square each sample is, the way the loop goes (0 to 1): the road's height there follows from it.</summary>
    public double[] Across { get; }
    /// <summary>How sharply the road bends at each sample (1 / its radius; 0 straight).</summary>
    public double[] Bend { get; }
    /// <summary>The ramps, hills, dips and bridges round the loop, where a car can leave the ground.</summary>
    public Hump[] Humps { get; }
    private readonly Dictionary<double, double[]> _jumpSpeeds = [];

    private Course(Track t, List<Visit> visits)
    {
        Track = t;
        Visits = [.. visits];
        var pts = new List<Vec>();
        var z = new List<double>();
        var piece = new List<int>();
        var banked = new List<bool>();
        var rises = new List<Rise>();
        var across = new List<double>();
        var humps = new List<(int Start, Rise Rise)>();
        int line = 0;
        for (int v = 0; v < visits.Count; v++)
        {
            var (pi, road, from) = (visits[v].Piece, visits[v].Road, visits[v].From);
            var p = t.Pieces[pi];
            var origin = new Vec(p.Col * Pieces.Cell, p.Row * Pieces.Cell);
            int n = (int)Math.Round(Pieces.Length(road) / Spacing);
            if (road.Rise != Rise.Flat) humps.Add((pts.Count, road.Rise));
            for (int k = 0; k < n; k++)
            {
                double s = k / (double)n;
                // The start line is halfway across the first square: the loop starts there.
                if (v == 0 && k == n / 2) line = pts.Count;
                pts.Add(origin + Pieces.Along(road, from, s));
                z.Add(Pieces.Height(road.Rise, from == road.A ? s : 1 - s));
                piece.Add(pi);
                banked.Add(road.Banked);
                rises.Add(road.Rise);
                across.Add(s);
            }
        }
        int m = pts.Count;
        int Rot(int i) => ((i - line) % m + m) % m;
        Points = new Vec[m];
        Z = new double[m];
        Piece = new int[m];
        Banked = new bool[m];
        Rises = new Rise[m];
        Across = new double[m];
        for (int i = 0; i < m; i++)
        {
            Points[Rot(i)] = pts[i];
            Z[Rot(i)] = z[i];
            Piece[Rot(i)] = piece[i];
            Banked[Rot(i)] = banked[i];
            Rises[Rot(i)] = rises[i];
            Across[Rot(i)] = across[i];
        }
        Along = new double[m];
        double run = 0;
        for (int i = 1; i < m; i++)
        {
            run += (Points[i] - Points[i - 1]).Length;
            Along[i] = run;
        }
        Length = run + (Points[0] - Points[^1]).Length;
        Bend = new double[m];
        for (int i = 0; i < m; i++)
        {
            double turn = Field.AngleDiff(Dir(i - 2).Angle, Dir(i + 2).Angle);
            Bend[i] = Math.Abs(turn) / (4 * Spacing);
        }
        Humps = [.. humps.Select(h => new Hump(Rot(h.Start), h.Rise, RunFrom(Rot(h.Start)))).OrderBy(h => h.Start)];
    }

    /// <summary>The loop laid out for racing, or null if the track isn't one closed loop.</summary>
    public static Course? Build(Track t)
    {
        var (check, visits) = Trace(t);
        return check.Ok ? new Course(t, visits) : null;
    }

    /// <summary>
    /// Follows the road from the start line, square to square, until it comes back to the line (a closed loop) or
    /// goes wrong: off the grid, into an empty square, into a piece whose road doesn't meet it, the wrong way over a
    /// ramp. Then every piece must have been used, every road of it.
    /// </summary>
    public static (Check Check, List<Visit> Visits) Trace(Track t)
    {
        var visits = new List<Visit>();
        if (t.Pieces.Count == 0) return (Check.Bad("THE GRID IS EMPTY: DRAG PIECES ONTO IT FROM THE BOX"), visits);
        var starts = t.Pieces.Where(p => p.Kind == PieceKind.Start).ToList();
        if (starts.Count == 0) return (Check.Bad("THE TRACK NEEDS A START LINE"), visits);
        if (starts.Count > 1)
            return (Check.Bad($"ONE START LINE ONLY: TAKE AWAY THE ONE AT {Track.CellName(starts[1].Col, starts[1].Row)}", starts[1].Col, starts[1].Row), visits);
        var start = starts[0];
        int startIndex = t.Pieces.IndexOf(start);
        var first = Pieces.Roads(start)[0];
        visits.Add(new Visit(startIndex, first, first.A));
        var used = new HashSet<(int, int)> { (startIndex, 0) };
        int exit = first.B, col = start.Col, row = start.Row;
        double length = Pieces.Length(first);
        for (int guard = 0; ; guard++)
        {
            if (guard > Track.Cols * Track.Rows * 2) return (Check.Bad("THE ROAD GOES ROUND IN CIRCLES"), visits);
            var (dc, dr) = Pieces.Step(exit);
            int nc = col + dc, nr = row + dr;
            if (!Track.OnGrid(nc, nr))
                return (Check.Bad($"THE ROAD RUNS OFF THE EDGE OF THE GRID AT {Track.CellName(col, row)}", col, row), visits);
            var p = t.At(nc, nr);
            if (p == null) return (Check.Bad($"THE ROAD STOPS: {Track.CellName(nc, nr)} IS EMPTY", nc, nr), visits);
            int enter = (exit + 2) % 4, pi = t.Pieces.IndexOf(p);
            var roads = Pieces.Roads(p);
            int ri = Array.FindIndex(roads, r => r.A == enter || r.B == enter);
            if (ri < 0) return (Check.Bad($"BAD JOIN AT {Track.CellName(nc, nr)}: THE {Pieces.Name(p.Kind)} DOESN'T MEET THE ROAD", nc, nr), visits);
            var road = roads[ri];
            if (Pieces.OneWay(p.Kind) && road.A != enter)
            {
                if (p == start) return (Check.Bad("THE ROAD COMES BACK TO THE START LINE THE WRONG WAY", nc, nr), visits);
                return (Check.Bad($"THE {Pieces.Name(p.Kind)} AT {Track.CellName(nc, nr)} FACES THE WRONG WAY: TURN IT ROUND", nc, nr), visits);
            }
            if (p == start) break;
            if (!used.Add((pi, ri))) return (Check.Bad($"THE ROAD GOES ROUND IN CIRCLES AT {Track.CellName(nc, nr)}", nc, nr), visits);
            visits.Add(new Visit(pi, road, enter));
            length += Pieces.Length(road);
            exit = road.A == enter ? road.B : road.A;
            (col, row) = (nc, nr);
        }
        for (int i = 0; i < t.Pieces.Count; i++)
        {
            var p = t.Pieces[i];
            int n = Pieces.Roads(p).Length;
            int got = Enumerable.Range(0, n).Count(r => used.Contains((i, r)));
            if (got == n) continue;
            string at = Track.CellName(p.Col, p.Row);
            return (got > 0
                ? Check.Bad($"THE LOOP USES ONLY ONE ROAD OF THE {Pieces.Name(p.Kind)} AT {at}", p.Col, p.Row)
                : Check.Bad($"THE {Pieces.Name(p.Kind)} AT {at} ISN'T PART OF THE LOOP", p.Col, p.Row), visits);
        }
        return (Check.Good($"A CLOSED LOOP: {visits.Count} SQUARES, {length / 1000:0.00} KM A LAP"), visits);
    }

    private int Wrap(int i) => ((i % Points.Length) + Points.Length) % Points.Length;

    /// <summary>Which way the road runs at a sample.</summary>
    public Vec Dir(int i) => (Points[Wrap(i + 1)] - Points[Wrap(i - 1)]).Unit;

    /// <summary>To the right of the way the road runs.</summary>
    public Vec Right(int i) => Dir(i).Right;

    /// <summary>The sample nearest to a distance round the loop.</summary>
    public int IndexAt(double along)
    {
        along = ((along % Length) + Length) % Length;
        int lo = 0, hi = Points.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (Along[mid] <= along) lo = mid; else hi = mid - 1;
        }
        return lo;
    }

    public Vec At(double along) => Points[IndexAt(along)];

    /// <summary>
    /// The nearest sample to a point: looked for near a hint first (where the car was), so a car on one road of a
    /// crossover keeps to it; everywhere if that's no good, preferring a road at the car's height (over or under a bridge).
    /// </summary>
    public int Nearest(Vec p, int hint = -1, double z = 0)
    {
        int best = -1;
        double least = double.MaxValue;
        if (hint >= 0)
            for (int k = -40; k <= 40; k++)
            {
                int i = Wrap(hint + k);
                double d = (Points[i] - p).LengthSq;
                if (d < least) { least = d; best = i; }
            }
        if (best >= 0 && least < Pieces.HalfWidth * Pieces.HalfWidth * 6) return best;
        least = double.MaxValue;
        for (int i = 0; i < Points.Length; i++)
        {
            double dz = z - Z[i];
            double d = (Points[i] - p).LengthSq + dz * dz * 2;
            if (d < least) { least = d; best = i; }
        }
        return best;
    }

    /// <summary>How far a point is off the middle of the road at a sample, to the right (negative: left).</summary>
    public double Off(Vec p, int i) => (p - Points[i]).Dot(Right(i));

    /// <summary>The road's height under a point near a sample. Rises are only ever on straight roads, so it's worked out exactly from how far across the square the point is.</summary>
    public double Height(Vec p, int i) =>
        Rises[i] == Rise.Flat ? 0 : Pieces.Height(Rises[i], Math.Clamp(Across[i] + (p - Points[i]).Dot(Dir(i)) / Pieces.Cell, 0, 1));

    /// <summary>How sharply the road bends over a stretch ahead (the tightest point).</summary>
    public double BendAhead(double from, double ahead)
    {
        double most = 0;
        for (double s = 0; s <= ahead; s += Spacing * 2) most = Math.Max(most, Bend[IndexAt(from + s)]);
        return most;
    }

    /// <summary>The road's height a distance round the loop.</summary>
    public double HeightAlong(double along)
    {
        int i = IndexAt(along);
        double past = ((along % Length) + Length) % Length - Along[i];
        return Rises[i] == Rise.Flat ? 0 : Pieces.Height(Rises[i], Math.Clamp(Across[i] + past / Pieces.Cell, 0, 1));
    }

    /// <summary>
    /// Where a car going straight over a hump at a speed comes down, as a distance from the hump's start (0: it
    /// never leaves the ground). The same rule as the race: the car follows the road unless the road falls away
    /// faster than gravity can pull the car down.
    /// </summary>
    public double FlightLength(Hump h, double speed, double gravity)
    {
        const double dt = Field.Step;
        double g = Sim.Fall * gravity, start = Along[h.Start], z = Z[h.Start], vz = 0;
        bool flying = false;
        for (double x = 0; x < 2000; x += speed * dt)
        {
            double ground = HeightAlong(start + x + speed * dt);
            if (flying)
            {
                vz -= g * dt;
                z += vz * dt;
                if (z <= ground) return x;
                continue;
            }
            double ballistic = Sim.Ballistic(z, vz, g);
            if (ground < ballistic - Sim.LaunchSlack)
            {
                flying = true;
                vz -= g * dt;
                z = ballistic;
            }
            else
            {
                vz = (ground - z) / dt;
                z = ground;
                if (x > Pieces.Cell * 1.5) return 0;
            }
        }
        return 2000;
    }

    /// <summary>For each hump, the fastest a car can go over it and still come down on the straight road beyond.</summary>
    public double[] JumpSpeeds(double gravity)
    {
        if (_jumpSpeeds.TryGetValue(gravity, out var known)) return known;
        var speeds = new double[Humps.Length];
        for (int k = 0; k < Humps.Length; k++)
        {
            var h = Humps[k];
            double room = h.Runout - 24, lo = 40, hi = 700;
            if (FlightLength(h, hi, gravity) <= room) { speeds[k] = hi; continue; }
            if (FlightLength(h, lo, gravity) > room) { speeds[k] = lo; continue; }
            for (int i = 0; i < 24; i++)
            {
                double mid = (lo + hi) / 2;
                if (FlightLength(h, mid, gravity) <= room) lo = mid; else hi = mid;
            }
            speeds[k] = lo;
        }
        return _jumpSpeeds[gravity] = speeds;
    }

    /// <summary>How far the road runs on in a straight line from a sample, within a road's width.</summary>
    private double RunFrom(int i)
    {
        var at = Points[i];
        var d = Dir(i);
        for (double s = Spacing; s < 1400; s += Spacing)
        {
            var p = Points[IndexAt(Along[i] + s)];
            double off = Math.Abs((p - at).Dot(d.Right));
            if (off > Pieces.HalfWidth * 0.6) return s;
        }
        return 1400;
    }
}

using Avalonia;
using Avalonia.Media;
using AVARacingDestructionSet.Engine;
using Effect = AVARacingDestructionSet.Engine.Effect;

namespace AVARacingDestructionSet.Views;

/// <summary>
/// The world in the original's 3/4 view: the ground seen from above and in front, so that a point at (x, y) a
/// height z up is drawn at (x, y·<see cref="Squash"/> − z). Things further down the screen are nearer; things
/// higher up are drawn further up it. Everything here is drawn in those units, under a transform for where it's going.
/// </summary>
public sealed partial class GameCanvas
{
    /// <summary>How much the ground is foreshortened, seen at a slant; and how far left a thing leans for its height (seen a little from the right).</summary>
    public const double Squash = 0.75, Lean = 0.3;

    /// <summary>A point in the world, a height up, as drawn.</summary>
    public static Point P(Vec v, double z = 0) => new(v.X - z * Lean, v.Y * Squash - z);
    public static Point P(double x, double y, double z = 0) => new(x - z * Lean, y * Squash - z);

    /// <summary>Whether an upright face pointing a way (on the ground) is turned towards the viewer, who looks from above, in front and a little to the right.</summary>
    private static bool Facing(Vec outward) => outward.X * Lean + outward.Y / Squash > 0.02;

    // ───────────────────────────── the ground ─────────────────────────────

    private static (uint Base, uint Light, uint Dark) Ground(Planet p) => p switch
    {
        Planet.Moon => (0xFF8C8C94, 0xFFA8A8B2, 0xFF6C6C74),
        Planet.Mars => (0xFFB05E3C, 0xFFC87A50, 0xFF8A4228),
        Planet.Venus => (0xFFB8A044, 0xFFD0BA5A, 0xFF8E7628),
        Planet.Jupiter => (0xFFC88A58, 0xFFE0AA78, 0xFF9C6440),
        _ => (0xFF5CA44E, 0xFF78C062, 0xFF46863C),
    };

    /// <summary>The ground over the whole grid and a fringe beyond it, with the planet's scenery wherever the road isn't.</summary>
    private static void DrawGround(DrawingContext dc, Track t, Rect visible, bool grid)
    {
        var (bas, light, dark) = Ground(t.Planet);
        double w = Track.Cols * Pieces.Cell, h = Track.Rows * Pieces.Cell;
        dc.FillRectangle(Brush(Shade(bas, 0.55)), visible.Inflate(4));
        dc.FillRectangle(Brush(bas), new Rect(P(0, 0), P(w, h)));
        // Jupiter's bands.
        if (t.Planet == Planet.Jupiter)
            for (int i = 0; i < 9; i++)
                dc.FillRectangle(Brush(i % 2 == 0 ? 0xFFBE7E4E : 0xFFD49A68), new Rect(P(0, i * h / 9), P(w, i * h / 9 + h / 18)));
        // The ground's speckle.
        for (int i = 0; i < 1400; i++)
        {
            uint q = Hash(i, 3, (int)t.Planet);
            double x = q % (uint)w, y = (q >> 11) % (uint)h;
            var at = P(x, y);
            if (!visible.Contains(at)) continue;
            dc.FillRectangle(Brush(q % 3 == 0 ? light : dark), new Rect(at.X, at.Y, 3 + q % 3, 2));
        }
        // A tree, a crater, a rock: a few a square, away from the road.
        var items = new List<(Vec At, uint Q)>();
        for (int c = 0; c < Track.Cols; c++)
        for (int r = 0; r < Track.Rows; r++)
        {
            var piece = t.At(c, r);
            for (int k = 0; k < 3; k++)
            {
                uint q = Hash(c * 31 + r, k, 77 + (int)t.Planet);
                if (q % 5 < (t.Planet == Planet.Earth ? 3u : 2u)) continue;
                var at = new Vec(c * Pieces.Cell + 14 + q % 100, r * Pieces.Cell + 14 + (q >> 8) % 100);
                if (piece != null && NearRoad(piece, at, Pieces.HalfWidth + 16)) continue;
                if (!visible.Inflate(40).Contains(P(at))) continue;
                items.Add((at, q));
            }
        }
        if (grid)
        {
            var line = Pen(0x30000000, 1.2);
            for (int c = 0; c <= Track.Cols; c++) dc.DrawLine(line, P(c * Pieces.Cell, 0), P(c * Pieces.Cell, h));
            for (int r = 0; r <= Track.Rows; r++) dc.DrawLine(line, P(0, r * Pieces.Cell), P(w, r * Pieces.Cell));
        }
        foreach (var (at, q) in items.Where(i => t.Planet is Planet.Moon or Planet.Jupiter)) DrawScenery(dc, t.Planet, at, q);
        _scenery = items.Where(i => t.Planet is not (Planet.Moon or Planet.Jupiter)).ToList();
    }

    /// <summary>Scenery that stands up (trees, rocks), drawn after the roads of its row so the roads don't cover it.</summary>
    private static List<(Vec At, uint Q)> _scenery = [];

    private static void DrawStanding(DrawingContext dc, Planet planet, int row)
    {
        foreach (var (at, q) in _scenery)
            if ((int)(at.Y / Pieces.Cell) == row) DrawScenery(dc, planet, at, q);
    }

    private static bool NearRoad(Piece p, Vec at, double reach)
    {
        var origin = new Vec(p.Col * Pieces.Cell, p.Row * Pieces.Cell);
        foreach (var road in Pieces.Roads(p))
            for (int k = 0; k <= 8; k++)
                if ((origin + Pieces.Along(road, road.A, k / 8.0) - at).Length < reach) return true;
        return false;
    }

    private static void DrawScenery(DrawingContext dc, Planet planet, Vec at, uint q)
    {
        double s = 0.8 + q % 7 / 10.0;
        switch (planet)
        {
            case Planet.Earth:
            {
                // A tree: its shadow, a trunk, and the round top in three greens.
                dc.DrawEllipse(Brush(0x40000000), null, P(at.X + 6, at.Y + 4), 13 * s, 7 * s);
                Poly(dc, 0xFF6B4A2A, null, 0, P(at.X - 2, at.Y, 12 * s), P(at.X + 2, at.Y, 12 * s), P(at.X + 2, at.Y), P(at.X - 2, at.Y));
                dc.DrawEllipse(Brush(0xFF2E6B2A), null, P(at.X, at.Y, 18 * s), 13 * s, 11 * s);
                dc.DrawEllipse(Brush(0xFF3E8A36), null, P(at.X - 2, at.Y, 21 * s), 9 * s, 8 * s);
                dc.DrawEllipse(Brush(0xFF62AE4E), null, P(at.X - 4, at.Y, 24 * s), 4 * s, 3.5 * s);
                break;
            }
            case Planet.Moon:
            {
                // A crater: a dark bowl with a lit rim.
                double r = 10 + q % 14;
                dc.DrawEllipse(Brush(0xFFB4B4BE), null, P(at.X - 1, at.Y - 1), r + 2, (r + 2) * Squash);
                dc.DrawEllipse(Brush(0xFF6A6A72), null, P(at), r, r * Squash);
                dc.DrawEllipse(Brush(0xFF7C7C84), null, P(at.X + r * 0.25, at.Y + r * 0.25), r * 0.6, r * 0.6 * Squash);
                break;
            }
            case Planet.Jupiter:
                dc.DrawEllipse(Brush(0x60E8C8A0), null, P(at), 16 * s, 7 * s);
                dc.DrawEllipse(Brush(0x50A0502A), null, P(at.X + 3, at.Y + 1), 8 * s, 3.5 * s);
                break;
            default:
            {
                // A rock, lit from the top left.
                uint rock = planet == Planet.Mars ? 0xFF7A3A28u : 0xFF7A6024u;
                dc.DrawEllipse(Brush(0x40000000), null, P(at.X + 4, at.Y + 3), 11 * s, 5 * s);
                Poly(dc, rock, null, 0, P(at.X - 9 * s, at.Y), P(at.X - 5 * s, at.Y, 9 * s), P(at.X + 3 * s, at.Y, 11 * s), P(at.X + 9 * s, at.Y, 4 * s), P(at.X + 8 * s, at.Y + 2));
                Poly(dc, Shade(rock, 1.35), null, 0, P(at.X - 5 * s, at.Y, 9 * s), P(at.X + 3 * s, at.Y, 11 * s), P(at.X + 1 * s, at.Y, 6 * s));
                if (planet == Planet.Venus && q % 3 == 0) dc.DrawEllipse(Brush(0xC0F0782A), null, P(at.X + 14, at.Y + 8), 6, 3);
                break;
            }
        }
    }

    // ───────────────────────────── the pieces ─────────────────────────────

    private static (uint Road, uint Edge, uint Side) RoadColours(Track t) => t.Surface switch
    {
        Surface.Dirt => t.Planet switch
        {
            // Dirt is whatever the planet's ground is made of, packed down.
            Planet.Moon => (0xFFB4B4BC, 0xFF7C7C86, 0xFF5C5C64),
            Planet.Mars => (0xFFD08050, 0xFF8A4228, 0xFF6A3018),
            Planet.Venus => (0xFFD4B860, 0xFF8E7628, 0xFF6A5418),
            Planet.Jupiter => (0xFFDCA878, 0xFF9C6440, 0xFF744828),
            _ => (0xFF94683C, 0xFF6E4A28, 0xFF5A3C20),
        },
        Surface.Ice => (0xFFDCEEFA, 0xFF88C8E8, 0xFF6A9AB8),
        _ => (0xFF56565C, 0xFFE4E4E4, 0xFF34343A),
    };

    /// <summary>A road's edges and height, a step at a time across its square, the way it was laid (or from the given end).</summary>
    private static (Vec L, Vec R, Vec C, double Z)[] Edges(Piece p, Road road, int from, double half, int n)
    {
        var origin = new Vec(p.Col * Pieces.Cell, p.Row * Pieces.Cell);
        var pts = new (Vec, Vec, Vec, double)[n + 1];
        for (int k = 0; k <= n; k++)
        {
            double s = k / (double)n;
            var c = origin + Pieces.Along(road, from, s);
            var d = (Pieces.Along(road, from, Math.Min(1, s + 0.01)) - Pieces.Along(road, from, Math.Max(0, s - 0.01))).Unit;
            double z = Pieces.Height(road.Rise, from == road.A ? s : 1 - s);
            pts[k] = (c - d.Right * half, c + d.Right * half, c, z);
        }
        return pts;
    }

    /// <summary>
    /// A piece of track: hills and dips shape the ground of their square first; then each road, with the sides of
    /// any of it that's raised, kerbs on the bends, the chequers of the start line. A bridge's deck is left for
    /// <see cref="DrawDeck"/>, after the cars that pass under it.
    /// </summary>
    private static void DrawPiece(DrawingContext dc, Piece p, Track t)
    {
        var (road, edge, side) = RoadColours(t);
        var (bas, light, dark) = Ground(t.Planet);
        var roads = Pieces.Roads(p);
        if (p.Kind is PieceKind.Hill or PieceKind.Dip) DrawRidge(dc, p, roads[0], bas, light, dark);
        foreach (var r in roads)
        {
            if (r.Rise == Rise.Bridge) continue;
            if (r.Rise == Rise.Ramp)
            {
                DrawRamp(dc, p, r, road, edge);
                continue;
            }
            var e = Edges(p, r, r.A, Pieces.HalfWidth, r.Straight ? 8 : 12);
            if (!r.Straight) DrawKerbs(dc, e, t.Surface, r.Banked);
            Strip(dc, e, road);
            Lines(dc, e, t.Surface, edge, r.Banked);
        }
        if (p.Kind == PieceKind.Start) DrawStartLine(dc, p, roads[0]);
        if (p.Kind == PieceKind.Bridge)
        {
            // The road under the bridge's deck gets the bridge's shadow.
            var under = Edges(p, roads[0], roads[0].A, Pieces.HalfWidth + 6, 16)[5..12];
            Poly(dc, 0x50000000, null, 0, [.. under.Select(x => P(x.L)), .. under.Reverse().Select(x => P(x.R))]);
        }
    }

    /// <summary>A road's surface as a strip between its edges.</summary>
    private static void Strip(DrawingContext dc, (Vec L, Vec R, Vec C, double Z)[] e, uint fill) =>
        Poly(dc, fill, null, 0, [.. e.Select(x => P(x.L, x.Z)), .. e.Reverse().Select(x => P(x.R, x.Z))]);

    /// <summary>The painted lines (asphalt), the ruts (dirt) or the snowbanks (ice) along a road's edges.</summary>
    private static void Lines(DrawingContext dc, (Vec L, Vec R, Vec C, double Z)[] e, Surface s, uint edge, bool banked)
    {
        var inset = e.Select(x => (L: x.L + (x.C - x.L).Unit * 3, R: x.R + (x.C - x.R).Unit * 3, x.C, x.Z)).ToArray();
        switch (s)
        {
            case Surface.Asphalt:
                Polyline(dc, Pen(edge, 1.6), [.. inset.Select(x => P(x.L, x.Z))]);
                Polyline(dc, Pen(edge, 1.6), [.. inset.Select(x => P(x.R, x.Z))]);
                if (!banked) Polyline(dc, Dash(0xC0F0D040, 1.6), [.. e.Select(x => P(x.C, x.Z))]);
                break;
            case Surface.Dirt:
                var rut1 = e.Select(x => P(x.C + (x.L - x.C) * 0.45, x.Z)).ToArray();
                var rut2 = e.Select(x => P(x.C + (x.R - x.C) * 0.45, x.Z)).ToArray();
                Polyline(dc, Pen(Alpha(edge, 0.5), 3), rut1);
                Polyline(dc, Pen(Alpha(edge, 0.5), 3), rut2);
                Polyline(dc, Pen(edge, 2.5), [.. e.Select(x => P(x.L, x.Z))]);
                Polyline(dc, Pen(edge, 2.5), [.. e.Select(x => P(x.R, x.Z))]);
                break;
            default:
                Polyline(dc, Pen(0xFFFFFFFF, 4), [.. e.Select(x => P(x.L, x.Z))]);
                Polyline(dc, Pen(0xFFFFFFFF, 4), [.. e.Select(x => P(x.R, x.Z))]);
                Polyline(dc, Pen(0x6088C8E8, 1.2), [.. e.Select(x => P(x.C + (x.L - x.C) * 0.3, x.Z))]);
                break;
        }
    }

    /// <summary>Red and white kerbs round the outside and inside of a bend (or a banked turn's raised wall).</summary>
    private static void DrawKerbs(DrawingContext dc, (Vec L, Vec R, Vec C, double Z)[] e, Surface s, bool banked)
    {
        if (s != Surface.Asphalt && !banked) return;
        for (int k = 0; k + 1 < e.Length; k++)
        {
            uint col = k % 2 == 0 ? 0xFFE03A2A : 0xFFF2F2F2;
            foreach (bool left in new[] { true, false })
            {
                var (a, b) = left ? (e[k].L, e[k + 1].L) : (e[k].R, e[k + 1].R);
                var (oa, ob) = (a + (a - e[k].C).Unit * 5, b + (b - e[k + 1].C).Unit * 5);
                if (banked)
                {
                    // The banking: the outside of the turn rises in a wall.
                    bool outside = (e[e.Length / 2].C - e[0].C).Dot(a - e[k].C) < 0 == left;
                    double hgt = outside ? 6 : 0;
                    Poly(dc, outside ? Shade(col, 0.8) : col, null, 0, P(a), P(b), P(ob, hgt), P(oa, hgt));
                }
                else Poly(dc, col, null, 0, P(a), P(b), P(ob), P(oa));
            }
        }
    }

    private static void DrawStartLine(DrawingContext dc, Piece p, Road road)
    {
        var e = Edges(p, road, road.A, Pieces.HalfWidth, 16);
        var (a, b) = (e[7], e[9]);
        for (int i = 0; i < 8; i++)
        for (int k = 0; k < 2; k++)
        {
            Vec At((Vec L, Vec R, Vec C, double Z) x, double f) => x.L + (x.R - x.L) * f;
            var m = e[8];
            var row0 = k == 0 ? a : m;
            var row1 = k == 0 ? m : b;
            Poly(dc, (i + k) % 2 == 0 ? 0xFFF4F4F4 : 0xFF1A1A1A, null, 0,
                P(At(row0, i / 8.0)), P(At(row0, (i + 1) / 8.0)), P(At(row1, (i + 1) / 8.0)), P(At(row1, i / 8.0)));
        }
    }

    /// <summary>The start line's gantry over the road: two posts and a chequered beam. Drawn with the decks, over the cars.</summary>
    private static void DrawGantry(DrawingContext dc, Piece p, Road road)
    {
        var e = Edges(p, road, road.A, Pieces.HalfWidth + 6, 16);
        var (l, r, d) = (e[8].L, e[8].R, (e[9].C - e[7].C).Unit);
        const double top = 34;
        foreach (var post in new[] { l, r }) Solid(dc, post - d * 1.5 - d.Right * 1.5, post + d * 1.5 + d.Right * 1.5, d, 0, top, 0xFF4A4A56);
        for (int i = 0; i < 6; i++)
        {
            var a = l + (r - l) * (i / 6.0);
            var b = l + (r - l) * ((i + 1) / 6.0);
            Solid(dc, a - d * 2.5, b + d * 2.5, d, top, top + 8, i % 2 == 0 ? 0xFFF2F2F2 : 0xFF1A1A1A);
        }
    }

    /// <summary>
    /// An upright box between two opposite corners of its footprint (the footprint's sides run along and across
    /// <paramref name="along"/>), from one height to another: the faces turned to the viewer, then the top.
    /// </summary>
    private static void Solid(DrawingContext dc, Vec a, Vec b, Vec along, double z0, double z1, uint colour)
    {
        var across = along.Right;
        double la = (b - a).Dot(along), lc = (b - a).Dot(across);
        var k = new[] { a, a + along * la, a + along * la + across * lc, a + across * lc };
        var mid = (a + b) / 2;
        for (int i = 0; i < 4; i++)
        {
            var (p0, p1) = (k[i], k[(i + 1) % 4]);
            var outward = ((p0 + p1) / 2 - mid).Unit;
            if (!Facing(outward)) continue;
            Poly(dc, Shade(colour, outward.Y > 0.7 ? 0.7 : 0.82), null, 0, P(p0, z1), P(p1, z1), P(p1, z0), P(p0, z0));
        }
        Poly(dc, colour, null, 0, [.. k.Select(c => P(c, z1))]);
    }

    private static StreamGeometry Quad(Point a, Point b, Point c, Point d)
    {
        var g = new StreamGeometry();
        using var ctx = g.Open();
        ctx.BeginFigure(a, true);
        ctx.LineTo(b);
        ctx.LineTo(c);
        ctx.LineTo(d);
        ctx.EndFigure(true);
        return g;
    }

    /// <summary>
    /// A hill or dip shapes the ground of its square: a mound (or hollow) as high (or deep) as the road across it,
    /// flat under the road and falling away to the square's edges, drawn as a mesh from the back to the front and lit
    /// from the top left.
    /// </summary>
    private static void DrawRidge(DrawingContext dc, Piece p, Road road, uint bas, uint light, uint dark)
    {
        var origin = new Vec(p.Col * Pieces.Cell, p.Row * Pieces.Cell);
        bool northSouth = road.A % 2 == 0;
        const int n = 16;
        double step = Pieces.Cell / n, flat = Pieces.HalfWidth + 6, half = Pieces.Cell / 2;
        double Z(double u, double v)
        {
            double along = northSouth ? v : u, across = Math.Abs((northSouth ? u : v) - half);
            double taper = across <= flat ? 1 : 0.5 * (1 + Math.Cos(Math.PI * (across - flat) / (half - flat)));
            return Pieces.Height(road.Rise, along / Pieces.Cell) * taper;
        }
        var light3 = new[] { -0.45, -0.55, 0.7 };
        for (int j = 0; j < n; j++)
        for (int i = 0; i < n; i++)
        {
            double u0 = i * step, u1 = u0 + step, v0 = j * step, v1 = v0 + step;
            double z00 = Z(u0, v0), z10 = Z(u1, v0), z11 = Z(u1, v1), z01 = Z(u0, v1);
            // The slope, for the light.
            double dx = (z10 + z11 - z00 - z01) / (2 * step), dy = (z01 + z11 - z00 - z10) / (2 * step);
            double len = Math.Sqrt(dx * dx + dy * dy + 1);
            double lit = (-dx * light3[0] - dy * light3[1] + light3[2]) / len / 0.7;
            uint col = Math.Abs(dx) + Math.Abs(dy) < 0.02 ? bas : lit > 1.02 ? light : lit < 0.95 ? (lit < 0.75 ? Shade(dark, 0.9) : dark) : bas;
            Poly(dc, col, null, 0, P(origin.X + u0, origin.Y + v0, z00), P(origin.X + u1, origin.Y + v0, z10), P(origin.X + u1, origin.Y + v1, z11), P(origin.X + u0, origin.Y + v1, z01));
        }
    }

    /// <summary>A jump ramp: a yellow and black wedge rising to its lip, with its raised sides and the face under the lip.</summary>
    private static void DrawRamp(DrawingContext dc, Piece p, Road road, uint fill, uint edge)
    {
        // The road on past the lip, at ground level.
        var all = Edges(p, road, road.A, Pieces.HalfWidth, 20);
        int lip = (int)Math.Ceiling(Pieces.RampLip * 20);
        Strip(dc, all[lip..], fill);
        var wedge = all[..(lip + 1)].Select((x, k) => k == lip ? (x.L, x.R, x.C, Z: Pieces.RampHeight) : x).ToArray();
        // Its sides, where they face the viewer, and the face under the lip.
        var dir = (wedge[^1].C - wedge[0].C).Unit;
        foreach (bool left in new[] { true, false })
        {
            var e = wedge.Select(x => left ? x.L : x.R).ToArray();
            var outward = left ? -dir.Right : dir.Right;
            if (!Facing(outward)) continue;
            for (int k = 0; k + 1 < e.Length; k++)
                Poly(dc, 0xFF6A5418, null, 0, P(e[k], wedge[k].Z), P(e[k + 1], wedge[k + 1].Z), P(e[k + 1]), P(e[k]));
        }
        if (Facing(dir)) Poly(dc, 0xFF4A3A10, null, 0, P(wedge[^1].L, Pieces.RampHeight), P(wedge[^1].R, Pieces.RampHeight), P(wedge[^1].R), P(wedge[^1].L));
        Strip(dc, wedge, 0xFFE8C030);
        for (int k = 1; k < wedge.Length - 1; k += 3)
        {
            var (a, b) = (wedge[k], wedge[Math.Min(k + 1, wedge.Length - 1)]);
            Poly(dc, 0xFF1A1A1A, null, 0, P(a.L, a.Z), P(a.C + (b.C - a.C) * 1.6, (a.Z + b.Z) / 2 + 1), P(a.R, a.Z), P(a.R + (b.R - a.R) * 0.5, (a.Z + b.Z) / 2), P(a.C + (b.C - a.C) * 0.6, a.Z), P(a.L + (b.L - a.L) * 0.5, (a.Z + b.Z) / 2));
        }
        Polyline(dc, Pen(0xFF1A1A1A, 1.4), [.. wedge.Select(x => P(x.L, x.Z))]);
        Polyline(dc, Pen(0xFF1A1A1A, 1.4), [.. wedge.Select(x => P(x.R, x.Z))]);
        dc.DrawLine(Pen(0xFFF4F4F4, 2.5), P(wedge[^1].L, Pieces.RampHeight), P(wedge[^1].R, Pieces.RampHeight));
    }

    /// <summary>A bridge's deck: its pillars, its sides where they face the viewer, the road on top, and the railings.</summary>
    private static void DrawDeck(DrawingContext dc, Piece p, Track t)
    {
        var road = Pieces.Roads(p)[0];
        var (fill, edge, _) = RoadColours(t);
        var e = Edges(p, road, road.A, Pieces.HalfWidth + 3, 16);
        var dir = (e[^1].C - e[0].C).Unit;
        // Pillars under the middle, either side of the road below.
        foreach (int k in new[] { 4, 12 })
            foreach (var x in new[] { e[k].L, e[k].R })
            {
                Solid(dc, x - dir * 3 - dir.Right * 3, x + dir * 3 + dir.Right * 3, dir, 0, e[k].Z - 1, 0xFF8A8A94);
            }
        foreach (bool left in new[] { true, false })
        {
            var outward = left ? -dir.Right : dir.Right;
            if (!Facing(outward)) continue;
            for (int k = 0; k + 1 < e.Length; k++)
            {
                var (a, b) = left ? (e[k].L, e[k + 1].L) : (e[k].R, e[k + 1].R);
                Poly(dc, 0xFF6A6A74, null, 0, P(a, e[k].Z), P(b, e[k + 1].Z), P(b, Math.Max(0, e[k + 1].Z - 7)), P(a, Math.Max(0, e[k].Z - 7)));
            }
        }
        Strip(dc, e, 0xFFA8A8B0);
        Strip(dc, Edges(p, road, road.A, Pieces.HalfWidth - 2, 16), fill);
        Polyline(dc, Dash(0xC0F0D040, 1.6), [.. e.Select(x => P(x.C, x.Z))]);
        // Railings: posts and a top rail on both sides.
        foreach (bool left in new[] { true, false })
        {
            var rail = e.Select(x => P(left ? x.L : x.R, x.Z + 6)).ToArray();
            for (int k = 0; k < e.Length; k += 2) dc.DrawLine(Pen(0xFFF2F2F2, 1.2), P(left ? e[k].L : e[k].R, e[k].Z), rail[k]);
            Polyline(dc, Pen(0xFFF2F2F2, 2), rail);
        }
    }

    /// <summary>A whole track: the ground, then the pieces row by row from the top (so nearer pieces cover further ones), each row's standing scenery after its roads.</summary>
    private static void DrawTrack(DrawingContext dc, Track t, Rect visible, bool grid = false)
    {
        DrawGround(dc, t, visible, grid);
        for (int r = 0; r < Track.Rows; r++)
        {
            foreach (var p in t.Pieces.Where(p => p.Row == r).OrderBy(p => p.Col))
                if (visible.Inflate(80).Intersects(new Rect(P(p.Col * Pieces.Cell, p.Row * Pieces.Cell, 40), P((p.Col + 1) * Pieces.Cell, (p.Row + 1) * Pieces.Cell, -20))))
                    DrawPiece(dc, p, t);
            DrawStanding(dc, t.Planet, r);
        }
    }

    /// <summary>What goes over the cars: bridge decks and the start line's gantry.</summary>
    private static void DrawOverhead(DrawingContext dc, Track t)
    {
        foreach (var p in t.Pieces.OrderBy(p => p.Row))
        {
            if (p.Kind == PieceKind.Bridge) DrawDeck(dc, p, t);
            if (p.Kind == PieceKind.Start) DrawGantry(dc, p, Pieces.Roads(p)[0]);
        }
    }

    // ───────────────────────────── vehicles ─────────────────────────────

    private enum Role { Body, Cabin, Tyre, Metal, Dark, White, Gold, Rider, Load, Lamp }

    /// <summary>A box of a vehicle: from its back to its front (along), its left to its right, its bottom to its top, all in world units from its middle.</summary>
    private readonly record struct Box(double A0, double A1, double S0, double S1, double Z0, double Z1, Role Role);

    private static Box[] Wheels(double a0, double a1, double s0, double s1, double z1, params double[] axles) =>
        [.. axles.SelectMany(ax => new[] { new Box(ax + a0, ax + a1, -s1, -s0, 0, z1, Role.Tyre), new Box(ax + a0, ax + a1, s0, s1, 0, z1, Role.Tyre) })];

    private static readonly Dictionary<VehicleKind, Box[]> Shapes = new()
    {
        [VehicleKind.StockCar] =
        [
            .. Wheels(-3, 3, 4, 7, 5, 8, -8),
            new(-13, 13, -6.5, 6.5, 2.5, 6.5, Role.Body), new(12, 13.6, -6, 6, 2, 4, Role.Metal), new(-13.6, -12, -6, 6, 2, 4, Role.Metal),
            new(-6, 4, -5.5, 5.5, 6.5, 10, Role.Cabin), new(-3, 1, -2.2, 2.2, 10, 10.4, Role.White),
            new(12.6, 13.4, -5.5, -3, 4, 5.6, Role.Lamp), new(12.6, 13.4, 3, 5.5, 4, 5.6, Role.Lamp),
        ],
        [VehicleKind.BajaBug] =
        [
            .. Wheels(-3.5, 3.5, 4.5, 8.5, 7, 7.5, -7.5),
            new(-10, 9, -5, 5, 4, 8, Role.Body), new(9, 11.5, -4, 4, 4, 6.5, Role.Body), new(-6, 2, -4.5, 4.5, 8, 11.5, Role.Cabin),
            new(-12, -10, -3, 3, 5, 10, Role.Tyre), new(1, 2, -4.6, 4.6, 11.5, 12.6, Role.Lamp),
        ],
        [VehicleKind.Dragster] =
        [
            .. Wheels(-4, 4, 3, 7.5, 9.5, -12), .. Wheels(-1.6, 1.6, 2.8, 4.4, 3.4, 14.5),
            new(-12, 17, -1.6, 1.6, 2, 4, Role.Body), new(-9, -3, -3, 3, 3, 8, Role.Metal), new(-7, -5, -1.6, 1.6, 8, 10.5, Role.Metal),
            new(-14, -10, -2, 2, 3, 7, Role.Cabin), new(-16.2, -15.6, -0.5, 0.5, 4, 10.5, Role.Dark), new(-18, -15, -6.5, 6.5, 10.5, 11.6, Role.Body),
        ],
        [VehicleKind.IndyCar] =
        [
            .. Wheels(-2.5, 2.5, 4, 7.2, 5, 10.5), .. Wheels(-3, 3, 4, 7.8, 6, -9),
            new(4, 15, -1.6, 1.6, 2, 4, Role.Body), new(13, 16, -6.5, 6.5, 1, 2.2, Role.White), new(-11, 4, -4, 4, 2, 5.4, Role.Body),
            new(-4, 1.5, -1.8, 1.8, 5.4, 7.6, Role.Cabin), new(-13.6, -13, -0.5, 0.5, 4, 7.6, Role.Dark), new(-15, -12, -6.5, 6.5, 7.6, 9, Role.White),
        ],
        [VehicleKind.Pickup] =
        [
            .. Wheels(-3, 3, 4, 7.5, 6, 8.5, -8),
            new(-13, 13.6, -7, 7, 3, 6.5, Role.Body), new(1, 8, -6.5, 6.5, 6.5, 11, Role.Cabin), new(8, 13.6, -6.5, 6.5, 6.5, 8, Role.Body),
            new(-12.5, 1, -6, 6, 6, 6.5, Role.Load), new(-13, 1, -7, -6, 6.5, 9, Role.Body), new(-13, 1, 6, 7, 6.5, 9, Role.Body), new(-13, -12, -7, 7, 6.5, 9, Role.Body),
            new(13.2, 14, -6, -3.5, 4.5, 6, Role.Lamp), new(13.2, 14, 3.5, 6, 4.5, 6, Role.Lamp),
        ],
        [VehicleKind.Motorcycle] =
        [
            new(4, 9, -1, 1, 0, 7, Role.Tyre), new(-9, -4, -1, 1, 0, 7, Role.Tyre),
            new(-6.5, 7, -1.6, 1.6, 4, 7.5, Role.Metal), new(1, 5, -2.5, 2.5, 6, 8.5, Role.Body), new(3.5, 4.5, -3.8, 3.8, 9, 10, Role.Metal),
            new(-3, 1, -2.6, 2.6, 7.5, 13, Role.Rider), new(-1.6, 1.6, -2.1, 2.1, 13, 16, Role.Body),
        ],
        [VehicleKind.DumpTruck] =
        [
            .. Wheels(-3.5, 3.5, 5, 8.6, 8, 12.5, -5, -12.5),
            new(-17, 17, -7, 7, 4, 7, Role.Dark), new(9, 17, -7.5, 7.5, 7, 15, Role.Cabin), new(-17, 8, -8.5, 8.5, 7, 16, Role.Body),
            new(-16, 7, -7.5, 7.5, 16, 16.4, Role.Load), new(16.4, 17.2, -6, 6, 5, 7, Role.Lamp),
        ],
        [VehicleKind.MoonBuggy] =
        [
            .. Wheels(-3.5, 3.5, 4, 8, 8, 7.5, -7.5),
            new(-9, 10, -4, 4, 4, 6, Role.Metal), new(-4, 1, -3, 3, 6, 9, Role.Body), new(-5, -4, -4, 4, 6, 13, Role.Metal),
            new(-8.3, -7.7, 3, 3.6, 6, 19, Role.Metal), new(-8, -3.5, 3, 3.3, 15.5, 19, Role.Body), new(8, 10, -3, 3, 6, 7, Role.Lamp),
        ],
        [VehicleKind.LunarRover] =
        [
            .. Wheels(-2.5, 2.5, 5, 8, 6, 8.5, 0, -9.5),
            new(-13, 13, -6, 6, 4.5, 7, Role.Gold), new(-7, 5, -4, 4, 7, 10, Role.Body), new(7, 8, -0.5, 0.5, 7, 13, Role.Metal),
            new(5, 10, -3, 3, 13, 14, Role.White), new(10, 11, 3, 4, 7, 11.5, Role.Metal), new(-12, -9, -5, 5, 7, 8, Role.Dark),
        ],
    };

    private static uint RoleColour(Role r, uint body) => r switch
    {
        Role.Body => body, Role.Cabin => 0xFF2C3C5C, Role.Tyre => 0xFF1C1C1E, Role.Metal => 0xFFA8A8B4, Role.Dark => 0xFF3A3A40,
        Role.White => 0xFFF4F4F4, Role.Gold => 0xFFD8A838, Role.Rider => 0xFF2A2A30, Role.Load => 0xFF8A8270, _ => 0xFFFFE860,
    };

    /// <summary>
    /// A vehicle in 3/4 view: each of its boxes turned to its heading, lowest first and furthest first, with the
    /// top lit and the sides facing the viewer shaded (a cabin's roof is the body's colour over its glass).
    /// </summary>
    public static void DrawVehicle(DrawingContext dc, VehicleKind kind, Vec at, double z, double heading, uint colour, double alpha = 1)
    {
        var f = Vec.FromAngle(heading);
        var r = f.Right;
        var boxes = Shapes[kind].OrderBy(b => b.Z0).ThenBy(b => (at + f * ((b.A0 + b.A1) / 2) + r * ((b.S0 + b.S1) / 2)).Y).ToArray();
        using var _ = alpha < 1 ? dc.PushOpacity(alpha) : default(DrawingContext.PushedState?);
        foreach (var b in boxes)
        {
            Vec C(double a, double s) => at + f * a + r * s;
            var corners = new[] { C(b.A0, b.S0), C(b.A1, b.S0), C(b.A1, b.S1), C(b.A0, b.S1) };
            uint col = RoleColour(b.Role, colour);
            uint top = b.Role == Role.Cabin ? Shade(colour, 1.1) : Shade(col, 1.12);
            uint sideCol = b.Role == Role.Cabin ? 0xFF22304C : Shade(col, 0.68);
            for (int k = 0; k < 4; k++)
            {
                var (p0, p1) = (corners[k], corners[(k + 1) % 4]);
                var mid = (p0 + p1) / 2;
                var outward = (mid - (corners[0] + corners[2]) / 2).Unit;
                if (!Facing(outward)) continue;
                uint face = outward.Y > 0.7 ? sideCol : outward.X > 0.7 ? Shade(sideCol, 0.82) : Shade(sideCol, 0.9);
                Poly(dc, face, 0x60000000, 0.5, P(p0, z + b.Z1), P(p1, z + b.Z1), P(p1, z + b.Z0), P(p0, z + b.Z0));
                if (b.Role == Role.Cabin)
                {
                    // The windows: a band of glass with the body colour under it.
                    Poly(dc, Shade(colour, 0.7), null, 0, P(p0, z + b.Z0 + (b.Z1 - b.Z0) * 0.3), P(p1, z + b.Z0 + (b.Z1 - b.Z0) * 0.3), P(p1, z + b.Z0), P(p0, z + b.Z0));
                    Poly(dc, 0x50A8D0FF, null, 0, P(p0, z + b.Z1 - 0.6), P(p0 + (p1 - p0) * 0.35, z + b.Z1 - 0.6), P(p0 + (p1 - p0) * 0.2, z + b.Z0 + (b.Z1 - b.Z0) * 0.35), P(p0, z + b.Z0 + (b.Z1 - b.Z0) * 0.35));
                }
            }
            Poly(dc, top, 0x70000000, 0.5, [.. corners.Select(c => P(c, z + b.Z1))]);
            if (b.Role == Role.White && kind == VehicleKind.StockCar)
                dc.DrawEllipse(null, Pen(0xFF1A1A1A, 1), P(at + f * ((b.A0 + b.A1) / 2), z + b.Z1), 1.4, 1.1);
        }
    }

    /// <summary>A car's shadow, on the ground under it however high it's flown (fainter and wider the higher it is).</summary>
    private static void DrawShadow(DrawingContext dc, Car c, double ground)
    {
        var body = c.Body;
        var f = c.Forward;
        var r = f.Right;
        double lift = Math.Clamp((c.Z - ground) / 150, 0, 0.5);
        var pts = Enumerable.Range(0, 16).Select(k =>
        {
            double a = k * Math.PI / 8;
            return P(c.Pos + f * (Math.Cos(a) * (body.Length / 2 + 2) * (1 + lift)) + r * (Math.Sin(a) * (body.Width / 2 + 2) * (1 + lift)), ground);
        }).ToArray();
        Poly(dc, Alpha(0xFF000000, 0.45 - lift * 0.25), null, 0, pts);
    }

    // ───────────────────────────── on the road ─────────────────────────────

    private void DrawDrop(DrawingContext dc, Drop d)
    {
        if (d.Kind == DropKind.Oil)
        {
            double fade = Math.Clamp((Sim.OilSeconds - d.Age) / 5, 0.3, 1);
            using var _ = dc.PushOpacity(fade);
            uint q = (uint)(d.Pos.X * 7 + d.Pos.Y * 13);
            dc.DrawEllipse(Brush(0xFF101014), null, P(d.Pos, d.Z), 19, 12);
            dc.DrawEllipse(Brush(0xFF101014), null, P(d.Pos.X + 10 - q % 7, d.Pos.Y + 7, d.Z), 9, 6);
            dc.DrawEllipse(Brush(0xFF101014), null, P(d.Pos.X - 11, d.Pos.Y - 4 + q % 5, d.Z), 7, 5);
            dc.DrawEllipse(Brush(0x707A5AE8), null, P(d.Pos.X - 5, d.Pos.Y - 4, d.Z), 8, 2.5);
            dc.DrawEllipse(Brush(0x604AE8C0), null, P(d.Pos.X + 4, d.Pos.Y + 2, d.Z), 5, 1.6);
            return;
        }
        // A land mine: a dark dome with studs, its lamp blinking once it's armed.
        bool armed = d.Age > Sim.MineArm;
        var c = P(d.Pos, d.Z);
        dc.DrawEllipse(Brush(0x50000000), null, new Point(c.X + 2, c.Y + 1.5), 9, 5);
        dc.DrawEllipse(Brush(0xFF3A3A30), Pen(0xFF1A1A14, 1), c, 8, 5.5);
        dc.DrawEllipse(Brush(0xFF5A5A48), null, new Point(c.X - 1.5, c.Y - 1.5), 5, 3);
        for (int k = 0; k < 6; k++)
        {
            double a = k * Math.PI / 3;
            dc.DrawEllipse(Brush(0xFF8A8A70), null, new Point(c.X + Math.Cos(a) * 6, c.Y + Math.Sin(a) * 4), 1.2, 1);
        }
        bool lamp = armed ? Blink(4) : Blink(10);
        dc.DrawEllipse(Brush(lamp ? 0xFFFF3A2Au : 0xFF6A1A14u), null, new Point(c.X, c.Y - 3), 2.2, 2);
        if (lamp && armed) dc.DrawEllipse(Brush(0x50FF3A2A), null, new Point(c.X, c.Y - 3), 6, 5);
    }

    private static void DrawEffect(DrawingContext dc, Effect e)
    {
        double t = e.Age / e.Life;
        switch (e.Kind)
        {
            case EffectKind.Smoke:
            {
                double r = (4 + 9 * t) * e.Size;
                dc.DrawEllipse(Brush(Alpha(e.Colour, 0.55 * (1 - t))), null, P(e.Pos, e.Z + 4 + t * 14), r, r * 0.85);
                break;
            }
            case EffectKind.Dust:
            {
                double r = (5 + 12 * t) * e.Size;
                dc.DrawEllipse(Brush(Alpha(e.Colour, 0.6 * (1 - t))), null, P(e.Pos, e.Z + 2 + t * 6), r, r * 0.6);
                break;
            }
            case EffectKind.Boom:
            {
                double r = (10 + 34 * Math.Sqrt(t)) * e.Size;
                var c = P(e.Pos, e.Z + 8 + t * 20);
                dc.DrawEllipse(Brush(Alpha(0xFF2A2A2A, 0.6 * (1 - t))), null, new Point(c.X, c.Y - t * 18), r * 1.1, r * 0.9);
                dc.DrawEllipse(Brush(Alpha(0xFFE84A1A, 1 - t)), null, c, r * 0.85, r * 0.75);
                dc.DrawEllipse(Brush(Alpha(0xFFFF9A2A, 1 - t)), null, c, r * 0.6, r * 0.52);
                dc.DrawEllipse(Brush(Alpha(0xFFFFF0A0, 1 - t * 1.4)), null, c, r * 0.32, r * 0.28);
                if (t < 0.15) dc.DrawEllipse(Brush(Alpha(0xFFFFFFFF, 0.8 - t * 5)), null, c, r * 1.8, r * 1.4);
                break;
            }
            case EffectKind.Splat:
                for (int k = 0; k < 6; k++)
                {
                    double a = k * 1.05 + 0.3, d = 6 + 16 * t;
                    dc.DrawEllipse(Brush(Alpha(0xFF101014, 1 - t)), null, P(e.Pos + Vec.FromAngle(a) * d, e.Z + 8 * Math.Sin(t * Math.PI)), 2.5, 2);
                }
                break;
            case EffectKind.Spark:
                for (int k = 0; k < 7; k++)
                {
                    double a = k * 0.9 + e.Pos.X, d = 4 + 18 * t * e.Size;
                    var c = P(e.Pos, e.Z);
                    dc.DrawLine(Pen(Alpha(k % 2 == 0 ? 0xFFFFF060 : 0xFFFFA020, 1 - t), 1.6), new Point(c.X + Math.Cos(a) * d * 0.5, c.Y + Math.Sin(a) * d * 0.4), new Point(c.X + Math.Cos(a) * d, c.Y + Math.Sin(a) * d * 0.8));
                }
                break;
            case EffectKind.Debris:
            {
                var c = P(e.Pos, e.Z);
                dc.DrawEllipse(Brush(Alpha(0xFF000000, 0.3)), null, P(e.Pos), 2.5 * e.Size, 1.5 * e.Size);
                double s = 3 * e.Size, a = e.Age * 12;
                Poly(dc, Alpha(e.Colour, Math.Min(1, (1 - t) * 3)), null, 0,
                    new Point(c.X + Math.Cos(a) * s, c.Y + Math.Sin(a) * s), new Point(c.X - Math.Sin(a) * s, c.Y + Math.Cos(a) * s * 0.6), new Point(c.X - Math.Cos(a) * s, c.Y - Math.Sin(a) * s));
                break;
            }
            case EffectKind.Word:
                break;
        }
    }

    /// <summary>
    /// The race as the world sees it: the track; what's been dropped on the road; the shadows; the cars that are
    /// low enough to pass under a bridge, nearest last; the bridges and the gantry; then anything higher (on a
    /// deck, or flying); the smoke and explosions over all.
    /// </summary>
    private void DrawRace(DrawingContext dc, World w, Rect visible)
    {
        DrawTrack(dc, w.Track, visible);
        foreach (var d in w.Drops) DrawDrop(dc, d);
        foreach (var e in w.Effects.Where(e => e.Kind is EffectKind.Dust)) DrawEffect(dc, e);
        var cars = w.Cars.Where(c => !c.Out).OrderBy(c => c.Pos.Y).ToList();
        foreach (var c in cars) DrawShadow(dc, c, c.OnRoad ? w.Course.Height(c.Pos, c.Index) : 0);
        bool High(Car c) => c.Z > 12;
        foreach (var c in cars.Where(c => !High(c))) DrawCar(dc, c);
        DrawOverhead(dc, w.Track);
        foreach (var c in cars.Where(High)) DrawCar(dc, c);
        foreach (var e in w.Effects.Where(e => e.Kind is not EffectKind.Dust)) DrawEffect(dc, e);
    }

    public static uint CarColour(int id) => id == 0 ? 0xFFE0402E : 0xFF3E74E0;

    private void DrawCar(DrawingContext dc, Car c)
    {
        if (c.Towed > 0)
        {
            // Gone with the tow truck: a flashing marker where it'll come back.
            if (Blink(4)) dc.DrawEllipse(null, Pen(Alpha(CarColour(c.Id), 0.8), 2), P(Engine.World.Course.Points[c.LastRoad], Engine.World.Course.Z[c.LastRoad]), 16, 10);
            return;
        }
        if (c.Wrecked > 0)
        {
            // A burnt-out shell, burning.
            DrawVehicle(dc, c.Build.Vehicle, c.Pos, c.Z, c.Heading, 0xFF3A3430);
            var top = P(c.Pos, c.Z + 10);
            dc.DrawEllipse(Brush(Alpha(0xFFFF8A2A, 0.6 + 0.3 * Math.Sin(_clock * 21))), null, top, 6, 7);
            dc.DrawEllipse(Brush(Alpha(0xFFFFE070, 0.7 + 0.3 * Math.Sin(_clock * 17))), null, new Point(top.X, top.Y + 1), 3, 4);
            return;
        }
        double alpha = c.Ghost > 0 && Blink(10) ? 0.4 : 1;
        DrawVehicle(dc, c.Build.Vehicle, c.Pos, c.Z, c.Heading, CarColour(c.Id), alpha);
    }
}

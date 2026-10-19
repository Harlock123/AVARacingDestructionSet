using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AVARacingDestructionSet.Engine;

namespace AVARacingDestructionSet.Views;

public sealed partial class GameCanvas
{
    private static readonly Dictionary<uint, IBrush> BrushCache = [];
    private static readonly Dictionary<(uint, double), Pen> PenCache = [];
    private static readonly Dictionary<(uint, double), Pen> DashCache = [];
    private static readonly Typeface Retro = new(new FontFamily("avares://AVARacingDestructionSet/Assets/Fonts#VT323"));

    public static IBrush Brush(uint argb)
    {
        if (!BrushCache.TryGetValue(argb, out var b))
            BrushCache[argb] = b = new SolidColorBrush(Color.FromUInt32(argb)).ToImmutable();
        return b;
    }

    private static Pen Pen(uint argb, double width = 1)
    {
        if (!PenCache.TryGetValue((argb, width), out var p))
            PenCache[(argb, width)] = p = new Pen(Brush(argb), width, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        return p;
    }

    private static Pen Dash(uint argb, double width)
    {
        if (!DashCache.TryGetValue((argb, width), out var p))
            DashCache[(argb, width)] = p = new Pen(Brush(argb), width, new DashStyle([3, 4], 0), PenLineCap.Flat);
        return p;
    }

    // The home computer's sixteen colours, a little brighter: the screen's blue and its light blue border, and the rest.
    private const uint Blue = 0xFF40318D, LightBlue = 0xFF7869C4, Ink = 0xFFC8C0F8, White = 0xFFF8F8F8, Yellow = 0xFFF2E05A, Red = 0xFFE05A48;
    private const uint Cyan = 0xFF6ACBD4, Green = 0xFF6CC860, Orange = 0xFFE09040, Purple = 0xFFA458B8, Dim = 0xFF9A90D8, PanelBack = 0xFF2E2470;
    /// <summary>The raster bars' colours, top to bottom.</summary>
    private static readonly uint[] Bars = [0xFF883932, 0xFFB86962, 0xFFE09040, 0xFFF2E05A, 0xFF94E089, 0xFF6ACBD4, 0xFF7869C4, 0xFFA458B8];

    private static void Text(DrawingContext dc, string text, Point at, double size, uint colour, bool center = false, bool right = false, bool shadow = true)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, Brush(colour));
        double x = center ? at.X - ft.Width / 2 : right ? at.X - ft.Width : at.X;
        if (shadow) dc.DrawText(new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, Brush(0xC0000000)), new Point(x + size / 14, at.Y - ft.Height / 2 + size / 14));
        dc.DrawText(ft, new Point(x, at.Y - ft.Height / 2));
    }

    private static double TextWidth(string text, double size) =>
        new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, Brushes.White).Width;

    /// <summary>Text cut into lines no wider than a width, at a size.</summary>
    private static List<string> Wrap(string text, double width, double size)
    {
        var lines = new List<string>();
        var line = "";
        foreach (var word in text.Split(' '))
        {
            string next = line.Length == 0 ? word : line + " " + word;
            if (TextWidth(next, size) > width && line.Length > 0)
            {
                lines.Add(line);
                line = word;
            }
            else line = next;
        }
        if (line.Length > 0) lines.Add(line);
        return lines;
    }

    private static void Poly(DrawingContext dc, uint fill, uint? edge, double width, params Point[] pts)
    {
        if (pts.Length < 2) return;
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(pts[0], true);
            foreach (var p in pts.Skip(1)) c.LineTo(p);
            c.EndFigure(true);
        }
        dc.DrawGeometry(fill == 0 ? null : Brush(fill), edge is { } e ? Pen(e, width) : null, g);
    }

    private static void Polyline(DrawingContext dc, Pen pen, IReadOnlyList<Point> pts)
    {
        if (pts.Count < 2) return;
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(pts[0], false);
            for (int i = 1; i < pts.Count; i++) c.LineTo(pts[i]);
            c.EndFigure(false);
        }
        dc.DrawGeometry(null, pen, g);
    }

    private static uint Hash(int x, int y, int salt)
    {
        uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(salt * 83492791);
        h ^= h >> 13;
        h *= 0x5bd1e995;
        h ^= h >> 15;
        return h;
    }

    private static uint Alpha(uint colour, double a) => (uint)(Math.Clamp(a, 0, 1) * 255) << 24 | (colour & 0x00FFFFFF);

    /// <summary>A colour made darker (k &lt; 1) or lighter (k &gt; 1).</summary>
    private static uint Shade(uint c, double k)
    {
        uint Ch(int shift) => (uint)Math.Clamp((((c >> shift) & 0xFF) * k), 0, 255) << shift;
        return (c & 0xFF000000) | Ch(16) | Ch(8) | Ch(0);
    }

    private void Hit(Rect r, Action a) => _hits.Add((r, a));

    /// <summary>A button: its face, its label, and what it does when clicked.</summary>
    private void Button(DrawingContext dc, Rect r, string label, Action act, bool on = false, double size = 20, bool enabled = true, uint? accent = null)
    {
        bool hover = r.Contains(_mouse) && enabled;
        uint face = on ? 0xFF6A5AC8 : !enabled ? 0xFF2A2260 : hover ? 0xFF5A4AB0 : 0xFF3A2E8A;
        dc.FillRectangle(Brush(face), r, 3);
        dc.DrawRectangle(null, Pen(on ? Yellow : accent ?? (enabled ? LightBlue : 0xFF3A3478), on ? 2 : 1.4), r, 3, 3);
        double s = size;
        while (TextWidth(label, s) > r.Width - 8 && s > 10) s -= 0.5;
        Text(dc, label, r.Center, s, on ? Yellow : enabled ? accent ?? White : 0xFF6A62A8, center: true);
        if (enabled) Hit(r, act);
    }

    private static void Panel(DrawingContext dc, Rect r, uint back = PanelBack)
    {
        dc.FillRectangle(Brush(back), r, 4);
        dc.DrawRectangle(null, Pen(LightBlue, 1.5), r, 4, 4);
    }

    private bool Blink(double rate = 3) => (int)(_clock * rate) % 2 == 0;

    // ───────────────────────────── the screen ─────────────────────────────

    private void DrawScreen(DrawingContext dc)
    {
        _hits.Clear();
        var g = Engine;
        dc.FillRectangle(Brush(LightBlue), new Rect(0, 0, Field.Width, Field.Height));
        dc.FillRectangle(Brush(Blue), new Rect(4, 4, Field.Width - 8, Field.Height - 8));
        switch (g.State)
        {
            case GameState.Title: DrawTitle(dc); break;
            case GameState.Attract or GameState.Racing or GameState.Paused or GameState.Results: DrawRaceScreen(dc); break;
            case GameState.Editing: DrawEditor(dc); break;
            case GameState.Garage: DrawGarage(dc); break;
            case GameState.BestLaps: DrawBestLaps(dc); break;
            case GameState.EnterInitials: DrawInitials(dc); break;
        }
        if (g.State == GameState.Paused)
        {
            dc.FillRectangle(Brush(0xA0000000), new Rect(0, 0, Field.Width, Field.Height));
            Text(dc, "PAUSED", new Point(Field.Width / 2, 350), 90, Yellow, center: true);
            Text(dc, "ANY KEY TO CARRY ON  -  ESC TO LEAVE THE RACE", new Point(Field.Width / 2, 420), 28, White, center: true);
        }
        if (g.State == GameState.Attract)
        {
            dc.FillRectangle(Brush(0xC0000000), new Rect(0, 378, Field.Width, 12));
            if (Blink(1.5)) Text(dc, "DEMONSTRATION  -  PRESS ENTER OR CLICK TO BUILD A TRACK", new Point(Field.Width / 2, 384), 20, Yellow, center: true);
        }
        if (Sound.IsMuted && g.State != GameState.Racing) Text(dc, "SOUND OFF (M)", new Point(Field.Width - 14, Field.Height - 16), 16, Dim, right: true);
    }

    /// <summary>The bar along the top: the program's name, and the three stages, the one on now lit.</summary>
    private void DrawHeader(DrawingContext dc, string right)
    {
        var g = Engine;
        dc.FillRectangle(Brush(0xFF241C60), new Rect(4, 4, Field.Width - 8, 42));
        for (int i = 0; i < Bars.Length; i++) dc.FillRectangle(Brush(Bars[i]), new Rect(4, 44 + i * 0.5, Field.Width - 8, 1));
        Text(dc, "RACING DESTRUCTION SET", new Point(16, 25), 32, White);
        string[] steps = ["1 TRACK", "2 VEHICLES", "3 RACE!"];
        for (int i = 0; i < 3; i++)
        {
            var r = new Rect(392 + i * 118, 9, 112, 30);
            bool on = i == 0 && g.State == GameState.Editing || i == 1 && g.State == GameState.Garage;
            int step = i;
            Button(dc, r, steps[i], () =>
            {
                if (step == 0) g.OpenEditor();
                else if (step == 1) g.OpenGarage();
                else g.StartRace();
            }, on: on, size: 22, accent: i == 2 ? Green : null);
        }
        double size = 24;
        while (TextWidth(right, size) > 250 && size > 14) size -= 1;
        Text(dc, right, new Point(Field.Width - 16, 25), size, Yellow, right: true);
    }

    // ───────────────────────────── the title ─────────────────────────────

    private void DrawTitle(DrawingContext dc)
    {
        double cx = Field.Width / 2;
        // Raster bars rolling behind the name, as the home computers' title screens had them.
        for (int i = 0; i < 24; i++)
        {
            double y = 70 + i * 6 + Math.Sin(_clock * 2 + i * 0.35) * 10;
            dc.FillRectangle(Brush(Alpha(Bars[(i + (int)(_clock * 6)) % Bars.Length], 0.55)), new Rect(4, y, Field.Width - 8, 4));
        }
        Text(dc, "A  V  A", new Point(cx, 60), 30, Ink, center: true);
        string[] words = ["RACING", "DESTRUCTION", "SET"];
        double[] ys = [118, 182, 240];
        for (int w = 0; w < 3; w++)
        {
            string word = words[w];
            double size = w == 1 ? 86 : 76, step = size * 0.52;
            for (int i = 0; i < word.Length; i++)
            {
                double x = cx - (word.Length - 1) * step / 2 + i * step, y = ys[w] + Math.Sin(_clock * 3 + i * 0.5 + w) * 4;
                Text(dc, word[i].ToString(), new Point(x + 4, y + 4), size, 0xFF101010, center: true, shadow: false);
                Text(dc, word[i].ToString(), new Point(x, y), size, w == 1 ? Bars[(i + w * 2) % Bars.Length] : White, center: true, shadow: false);
            }
        }
        Text(dc, "IN THE STYLE OF RICK KOENIG'S 1985 CLASSIC", new Point(cx, 286), 24, Yellow, center: true);
        // A little track below, with two cars going round it.
        DrawTrackPreview(dc, Tracks.Make("SPAGHETTI JUNCTION"), new Rect(150, 306, 724, 302));
        string[] lines =
        [
            "BUILD A TRACK FROM A BOX OF PIECES: CURVES, BANKED TURNS, CROSSOVERS, BRIDGES,",
            "HILLS, DIPS AND JUMPS. PUT IT ON THE MOON. BUILD A CAR, BOLT ON ARMOUR AND MINES,",
            "AND RACE A FRIEND OR THE COMPUTER - OR TRY TO WRECK THEM.",
        ];
        for (int i = 0; i < lines.Length; i++) Text(dc, lines[i], new Point(cx, 632 + i * 24), 21, Ink, center: true);
        if (Blink(2)) Text(dc, "PRESS ENTER OR CLICK TO BUILD A TRACK", new Point(cx, 712), 34, Yellow, center: true);
        Text(dc, "MUSIC: IN THE HALL OF THE MOUNTAIN KING, EDVARD GRIEG (1875)", new Point(cx, 738), 16, Dim, center: true);
        Text(dc, "AN INDEPENDENT TRIBUTE - NOT AFFILIATED WITH ELECTRONIC ARTS", new Point(cx, 754), 14, 0xFF7A70C0, center: true);
    }

    /// <summary>A track small in a window, the squares it uses filling it, with two cars going round.</summary>
    private void DrawTrackPreview(DrawingContext dc, Track t, Rect view)
    {
        Panel(dc, view.Inflate(4), 0xFF000000);
        if (t.Pieces.Count == 0) return;
        var course = Course.Build(t);
        double x0 = (t.Pieces.Min(p => p.Col) - 0.6) * Pieces.Cell, x1 = (t.Pieces.Max(p => p.Col) + 1.6) * Pieces.Cell;
        double y0 = (t.Pieces.Min(p => p.Row) - 0.5) * Pieces.Cell * Squash - 30, y1 = (t.Pieces.Max(p => p.Row) + 1.4) * Pieces.Cell * Squash;
        double k = Math.Min(view.Width / (x1 - x0), view.Height / (y1 - y0));
        double ox = view.Center.X - (x0 + x1) / 2 * k, oy = view.Center.Y - (y0 + y1) / 2 * k;
        using (dc.PushClip(view))
        using (dc.PushTransform(Matrix.CreateScale(k, k) * Matrix.CreateTranslation(ox, oy)))
        {
            DrawTrack(dc, t, new Rect((view.X - ox) / k, (view.Y - oy) / k, view.Width / k, view.Height / k));
            if (course == null)
            {
                DrawOverhead(dc, t);
                return;
            }
            var cars = new List<(VehicleKind V, Vec At, double Z, double H, uint C)>();
            for (int i = 0; i < 2; i++)
            {
                double along = (_clock * (230 - i * 14) + i * 140) % course.Length;
                int j = course.IndexAt(along);
                cars.Add((i == 0 ? VehicleKind.StockCar : VehicleKind.Pickup, course.Points[j] + course.Right(j) * (i == 0 ? -8 : 8), course.Z[j], course.Dir(j).Angle, CarColour(i)));
            }
            foreach (var c in cars.Where(c => c.Z < 12).OrderBy(c => c.At.Y))
            {
                dc.DrawEllipse(Brush(0x50000000), null, P(c.At), 16, 10);
                DrawVehicle(dc, c.V, c.At, c.Z, c.H, c.C);
            }
            DrawOverhead(dc, t);
            foreach (var c in cars.Where(c => c.Z >= 12)) DrawVehicle(dc, c.V, c.At, c.Z, c.H, c.C);
        }
    }

    // ───────────────────────────── the best laps ─────────────────────────────

    private void DrawBestLaps(DrawingContext dc)
    {
        var g = Engine;
        for (int i = 0; i < Bars.Length; i++) dc.FillRectangle(Brush(Bars[i]), new Rect(4, 40 + i * 5, Field.Width - 8, 4));
        Text(dc, "BEST LAPS", new Point(Field.Width / 2, 120), 80, Yellow, center: true);
        var r = new Rect(40, 190, 560, 500);
        Panel(dc, r);
        Text(dc, g.BestLaps.TrackName, new Point(r.Center.X, r.Y + 30), 34, Cyan, center: true);
        Text(dc, "WHO   TIME      IN", new Point(r.X + 104, r.Y + 70), 24, Dim);
        var entries = g.BestLaps.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            bool mine = i == g.NewEntryIndex && Blink(4);
            uint c = mine ? Red : i == 0 ? Yellow : White;
            double y = r.Y + 108 + i * 38;
            Text(dc, $"{i + 1,2}.", new Point(r.X + 30, y), 30, Dim);
            Text(dc, e.Initials, new Point(r.X + 100, y), 32, c);
            Text(dc, e.Time, new Point(r.X + 180, y), 32, c);
            Text(dc, e.Vehicle, new Point(r.X + 320, y), 28, mine ? Red : Ink);
        }
        var track = g.World.Track.Name == g.BestLaps.TrackName ? g.World.Track : Tracks.IsBuiltIn(g.BestLaps.TrackName) ? Tracks.Make(g.BestLaps.TrackName) : null;
        if (track != null)
        {
            DrawTrackPreview(dc, track, new Rect(624, 196, 356, 300));
            Text(dc, $"{Track.Describe(track.Surface)}  -  {Track.Describe(track.Planet)} {Track.Gravity(track.Planet):0.##}G", new Point(802, 524), 22, Ink, center: true);
            if (Tracks.IsBuiltIn(track.Name))
            {
                double y = 560;
                foreach (var line in Wrap(Tracks.About(track.Name), 340, 20)) { Text(dc, line, new Point(802, y), 20, Dim, center: true); y += 22; }
            }
        }
        Text(dc, "ENTER: CARRY ON", new Point(Field.Width / 2, 724), 28, White, center: true);
    }

    private void DrawInitials(DrawingContext dc)
    {
        var g = Engine;
        double cx = Field.Width / 2;
        for (int i = 0; i < Bars.Length; i++) dc.FillRectangle(Brush(Bars[i]), new Rect(4, 40 + i * 5, Field.Width - 8, 4));
        Text(dc, "A NEW BEST LAP!", new Point(cx, 170), 76, Yellow, center: true);
        Text(dc, g.BestLaps.TrackName, new Point(cx, 232), 34, Cyan, center: true);
        if (g.EntryPlayer >= 0)
        {
            var car = g.World.Cars[g.EntryPlayer];
            Text(dc, $"PLAYER {g.EntryPlayer + 1}  -  {car.Body.Name}", new Point(cx, 286), 30, g.EntryPlayer == 0 ? Shade(CarColour(0), 1.3) : Shade(CarColour(1), 1.4), center: true);
            Text(dc, LapEntry.Format(car.BestLap), new Point(cx, 350), 70, White, center: true);
        }
        Text(dc, "ENTER YOUR INITIALS", new Point(cx, 430), 32, Dim, center: true);
        if (g.Initials is { } entry)
        {
            string s = entry.Initials;
            for (int i = 0; i < s.Length; i++)
            {
                bool cur = i == entry.Cursor && Blink(3);
                Text(dc, s[i].ToString(), new Point(cx - 70 + i * 70, 512), 90, cur ? Red : Yellow, center: true);
                dc.FillRectangle(Brush(i == entry.Cursor ? Red : Dim), new Rect(cx - 94 + i * 70, 560, 48, 4));
            }
        }
        Text(dc, "TYPE, OR UP/DOWN AND SPACE; ENTER WHEN DONE", new Point(cx, 640), 26, White, center: true);
    }
}

using Avalonia;
using Avalonia.Media;
using AVARacingDestructionSet.Engine;

namespace AVARacingDestructionSet.Views;

public sealed partial class GameCanvas
{
    public static readonly Rect BoxRect = new(782, 72, 232, 240);
    public static readonly Rect NameRect = new(120, 690, 300, 30);
    public static readonly Rect StatusRect = new(12, 536, 762, 32);
    public static readonly Rect EditorPanel = new(12, 604, 762, 156);
    public static readonly Rect NextRect = new(786, 684, 226, 74);
    private static readonly Tool[] ToolOrder = [Tool.Hand, Tool.Turn, Tool.Erase];
    private List<(string Name, string Path)> _saved = [];
    private double _savedAt = -10;

    /// <summary>Where each piece sits in the parts box.</summary>
    public static Rect BoxTile(int i) => new(786 + i % 3 * 76, 76 + i / 3 * 78, 72, 74);
    public static Rect ToolRect(int i) => new(786 + i * 76, 318, 72, 44);
    public static readonly Rect UndoRect = new(786, 368, 110, 30), ClearRect = new(902, 368, 110, 30);
    public static Rect TabRect(int i) => new(12 + i * 134, 574, 130, 28);
    public static Rect SurfaceRect(Surface s) => new(120 + (int)s * 120, 614, 112, 30);
    public static Rect PlanetRect(Planet p) => new(120 + (int)p * 130, 652, 124, 30);
    public static Rect BuiltInRect(int i) => new(24 + i % 4 * 186, 614 + i / 4 * 46, 180, 38);

    private static string ToolName(Tool t) => t switch { Tool.Hand => "HAND", Tool.Turn => "TURN", _ => "ERASE" };
    private static string TabName(Tab t) => t switch { Tab.Terrain => "TERRAIN", Tab.Tracks => "TRACKS", _ => "YOUR TRACKS" };

    private static string About(Planet p) => p switch
    {
        Planet.Moon => "THE MOON: A SIXTH OF EARTH'S GRAVITY. LITTLE GRIP, AND THE JUMPS GO ON FOR EVER.",
        Planet.Mars => "MARS: TWO FIFTHS OF EARTH'S GRAVITY. LIGHT ON THE CORNERS, LONG IN THE AIR.",
        Planet.Venus => "VENUS: NEARLY EARTH'S GRAVITY, IN A YELLOW HAZE.",
        Planet.Jupiter => "JUPITER: TWO AND A HALF TIMES EARTH'S. GRIP FOR DAYS, SHORT JUMPS, HARD LANDINGS.",
        _ => "EARTH: THE GRAVITY WE'RE USED TO.",
    };

    private static string About(Surface s) => s switch
    {
        Surface.Dirt => "DIRT: LOOSE AND SLOWER. KNOBBY TYRES GRIP IT BEST.",
        Surface.Ice => "ICE: HARDLY ANY GRIP. STUDDED TYRES, OR SLIDE.",
        _ => "ASPHALT: THE MOST GRIP, FOR SLICKS ABOVE ALL.",
    };

    // ───────────────────────────── the construction set ─────────────────────────────

    private void DrawEditor(DrawingContext dc)
    {
        var g = Engine;
        var ed = g.Editor;
        DrawHeader(dc, ed.Track.Name);
        DrawGrid(dc);
        // How the track stands: a closed loop, or what's wrong.
        var check = ed.Check;
        Panel(dc, StatusRect, check.Ok ? 0xFF1E4A28 : 0xFF5A1E2A);
        string status = ed.Message ?? (check.Ok ? "READY TO RACE - " + check.Why : check.Why);
        double size = 22;
        while (TextWidth(status, size) > StatusRect.Width - 50 && size > 12) size -= 0.5;
        Text(dc, ed.Message != null ? "*" : check.Ok ? "OK" : "!!", new Point(StatusRect.X + 20, StatusRect.Center.Y), 22, check.Ok ? Green : Red, center: true);
        Text(dc, status, new Point(StatusRect.X + 40, StatusRect.Center.Y), size, ed.Message != null ? Yellow : check.Ok ? 0xFFB8F0B0 : 0xFFFFC0B8);
        // The tabs and the panel under them.
        for (int i = 0; i < 3; i++)
        {
            var t = (Tab)i;
            var r = TabRect(i);
            bool on = ed.Tab == t;
            dc.FillRectangle(Brush(on ? PanelBack : 0xFF241C60), new Rect(r.X, r.Y, r.Width, r.Height + (on ? 4 : 0)), 3);
            dc.DrawRectangle(null, Pen(on ? Yellow : LightBlue, 1.4), r, 3, 3);
            Text(dc, TabName(t), r.Center, 21, on ? Yellow : Dim, center: true);
            Hit(r, () => { ed.Tab = t; ed.Heard.Add(Sfx.Click); });
        }
        Panel(dc, EditorPanel);
        switch (ed.Tab)
        {
            case Tab.Terrain: DrawTerrainTab(dc); break;
            case Tab.Tracks: DrawTracksTab(dc); break;
            default: DrawYoursTab(dc); break;
        }
        DrawPartsBox(dc);
        Button(dc, NextRect, "NEXT: VEHICLES  >", g.OpenGarage, size: 28, accent: Green);
        // A piece from the box, in the hand over the panels.
        if (ed.Carried is { } held && ed.Hover.Col < 0 && _mouse.X >= 0)
        {
            using (dc.PushOpacity(0.85))
                DrawPieceIcon(dc, held.Kind, held.Rot, new Rect(_mouse.X - 36, _mouse.Y - 30, 72, 60));
        }
    }

    /// <summary>The grid in 3/4 view, its squares' letters and numbers, the piece under the mouse and the one in the hand.</summary>
    private void DrawGrid(DrawingContext dc)
    {
        var ed = Engine.Editor;
        var t = ed.Track;
        var frame = new Rect(12, 52, 762, 480);
        Panel(dc, frame, 0xFF000000);
        for (int c = 0; c < Track.Cols; c++) Text(dc, ((char)('A' + c)).ToString(), new Point(CellCentre(c, 0).X, 66), 18, Dim, center: true, shadow: false);
        for (int r = 0; r < Track.Rows; r++) Text(dc, (r + 1).ToString(), new Point(23, CellCentre(0, r).Y), 18, Dim, center: true, shadow: false);
        var check = ed.Check;
        using (dc.PushClip(new Rect(GridRect.X, frame.Y + 24, GridRect.Width, frame.Bottom - frame.Y - 28)))
        using (dc.PushTransform(Matrix.CreateScale(GridScale, GridScale) * Matrix.CreateTranslation(GridOrigin.X, GridOrigin.Y)))
        {
            var visible = new Rect(0, -60, Track.Cols * Pieces.Cell, Track.Rows * Pieces.Cell * Squash + 60);
            DrawTrack(dc, t, visible, grid: true);
            DrawOverhead(dc, t);
            // Which way the race goes: chevrons along the loop.
            if (ed.Course is { } course)
                for (double s = 40; s < course.Length; s += 96)
                {
                    int i = course.IndexAt(s);
                    var at = course.Points[i];
                    var d = course.Dir(i);
                    double z = course.Z[i] + (course.Rises[i] == Rise.Bridge ? 0 : 0);
                    Polyline(dc, Pen(0xC0FFE860, 3.5), [P(at - d * 6 - d.Right * 7, z), P(at + d * 4, z), P(at - d * 6 + d.Right * 7, z)]);
                }
            // The square that's wrong.
            if (!check.Ok && check.Col >= 0 && Blink(2.5)) CellOutline(dc, check.Col, check.Row, Red, 5);
            if (ed.Hover.Col >= 0)
            {
                var (hc, hr) = ed.Hover;
                if (ed.Carried is { } held)
                {
                    var ghost = new Piece(held.Kind, hc, hr, held.Rot);
                    using (dc.PushOpacity(0.7))
                    {
                        CellFill(dc, hc, hr, Ground(t.Planet).Base);
                        DrawPiece(dc, ghost, t);
                        if (ghost.Kind == PieceKind.Bridge) DrawDeck(dc, ghost, t);
                        if (ghost.Kind == PieceKind.Start) DrawGantry(dc, ghost, Pieces.Roads(ghost)[0]);
                    }
                }
                CellOutline(dc, hc, hr, ed.Tool == Tool.Erase && ed.Carried == null ? Red : Yellow, 3.5);
            }
            if (ed.Selected.Col >= 0 && ed.Selected != ed.Hover && t.At(ed.Selected.Col, ed.Selected.Row) != null)
                CellOutline(dc, ed.Selected.Col, ed.Selected.Row, Cyan, 2.5);
        }
    }

    private static Point[] CellCorners(int c, int r) =>
        [P(c * Pieces.Cell, r * Pieces.Cell), P((c + 1) * Pieces.Cell, r * Pieces.Cell), P((c + 1) * Pieces.Cell, (r + 1) * Pieces.Cell), P(c * Pieces.Cell, (r + 1) * Pieces.Cell)];

    private static void CellOutline(DrawingContext dc, int c, int r, uint colour, double width)
    {
        var k = CellCorners(c, r);
        Polyline(dc, Pen(colour, width), [k[0], k[1], k[2], k[3], k[0]]);
    }

    private static void CellFill(DrawingContext dc, int c, int r, uint colour) => Poly(dc, colour, null, 0, CellCorners(c, r));

    /// <summary>A piece on its own, filling a rectangle on the screen, on a patch of the track's ground.</summary>
    private void DrawPieceIcon(DrawingContext dc, PieceKind kind, int rot, Rect r)
    {
        var t = Engine.Editor.Track;
        var p = new Piece(kind, 0, 0, rot);
        double k = r.Width / Pieces.Cell * 0.86;
        double h = Pieces.Cell * Squash * k;
        using (dc.PushClip(r))
        using (dc.PushTransform(Matrix.CreateScale(k, k) * Matrix.CreateTranslation(r.Center.X - Pieces.Cell * k / 2, r.Y + (r.Height - h) / 2 + 4)))
        {
            CellFill(dc, 0, 0, Ground(t.Planet).Base);
            DrawPiece(dc, p, t);
            if (kind == PieceKind.Bridge) DrawDeck(dc, p, t);
            if (kind == PieceKind.Start) DrawGantry(dc, p, Pieces.Roads(p)[0]);
        }
    }

    private void DrawPartsBox(DrawingContext dc)
    {
        var ed = Engine.Editor;
        Text(dc, "THE PARTS BOX", new Point(BoxRect.Center.X, 62), 22, White, center: true);
        for (int i = 0; i < Pieces.Box.Length; i++)
        {
            var k = Pieces.Box[i];
            var r = BoxTile(i);
            bool hover = r.Contains(_mouse);
            bool held = ed.Carried is { } c && ed.CarriedIsNew && c.Kind == k;
            dc.FillRectangle(Brush(held ? 0xFF6A5AC8 : hover ? 0xFF4A3CA0 : 0xFF241C60), r, 3);
            dc.DrawRectangle(null, Pen(held ? Yellow : hover ? White : LightBlue, held ? 2 : 1.2), r, 3, 3);
            DrawPieceIcon(dc, k, held ? ed.Carried!.Rot : 0, new Rect(r.X + 2, r.Y + 2, r.Width - 4, r.Height - 20));
            Text(dc, Pieces.Short(k), new Point(r.Center.X, r.Bottom - 10), 16, held || hover ? Yellow : Ink, center: true, shadow: false);
            int tile = i;
            Hit(r, () =>
            {
                ed.TakeFromBox(k);
                _boxTile = tile;
            });
        }
        for (int i = 0; i < ToolOrder.Length; i++)
        {
            var t = ToolOrder[i];
            var r = ToolRect(i);
            bool on = ed.Tool == t && !(ed.Carried is { } c && ed.CarriedIsNew);
            Button(dc, r, ToolName(t), () => ed.SetTool(t), on: on, size: 22);
            Text(dc, KeyFor(t), new Point(r.X + 7, r.Y + 9), 13, Dim, shadow: false);
        }
        Button(dc, UndoRect, "UNDO", () => ed.Undo(), size: 20, enabled: ed.CanUndo);
        Button(dc, ClearRect, "CLEAR", ed.Clear, size: 20, enabled: ed.Track.Pieces.Count > 0, accent: Red);
        // About the piece in hand (or last handled), and what to do.
        var info = new Rect(786, 406, 226, 270);
        Panel(dc, info);
        var shown = ed.Carried?.Kind ?? ed.SelectedPiece?.Kind ?? ed.Shown;
        double y = info.Y + 18;
        if (shown is { } kind)
        {
            Text(dc, Pieces.Name(kind), new Point(info.Center.X, y), 26, Yellow, center: true);
            DrawPieceIcon(dc, kind, ed.Carried?.Rot ?? ed.SelectedPiece?.Rot ?? 0, new Rect(info.Center.X - 60, y + 14, 120, 96));
            y += 124;
            foreach (var line in Wrap(Pieces.Describe(kind), info.Width - 20, 19)) { Text(dc, line, new Point(info.X + 10, y), 19, Ink); y += 20; }
        }
        else
        {
            Text(dc, "HOW TO BUILD", new Point(info.Center.X, y), 26, Yellow, center: true);
            y += 34;
            foreach (var line in new[] { "MAKE ONE CLOSED LOOP:", "- A START LINE, POINTING", "  THE WAY THE RACE GOES", "- EVERY ROAD JOINED TO", "  THE NEXT, EDGE TO EDGE", "- EVERY PIECE ON THE LOOP", "THE BAR UNDER THE GRID", "SAYS WHAT'S WRONG." })
            {
                Text(dc, line, new Point(info.X + 12, y), 18, Ink);
                y += 19;
            }
        }
        y = Math.Max(y + 6, info.Bottom - 80);
        foreach (var line in Wrap(Hint(), info.Width - 20, 17)) { Text(dc, line, new Point(info.X + 10, y), 17, Cyan); y += 18; }
    }

    private string Hint()
    {
        var ed = Engine.Editor;
        if (ed.Carried is { } c && c.Kind is var k && ed.CarriedIsNew && _boxTile < 0) return "CLICK SQUARES TO PUT THEM DOWN. RIGHT-CLICK OR R TURNS IT; ESC PUTS IT BACK.";
        if (ed.Carried != null) return "LET GO ON A SQUARE TO PUT IT THERE, OR OVER THE BOX TO THROW IT AWAY. RIGHT-CLICK TURNS IT.";
        return ed.Tool switch
        {
            Tool.Turn => "CLICK A PIECE TO TURN IT A QUARTER.",
            Tool.Erase => "CLICK A PIECE TO TAKE IT AWAY.",
            _ => "DRAG PIECES FROM THE BOX ONTO THE GRID. DRAG THEM ABOUT; RIGHT-CLICK TURNS ONE.",
        };
    }

    private void DrawTerrainTab(DrawingContext dc)
    {
        var g = Engine;
        var ed = g.Editor;
        double x = EditorPanel.X + 12;
        Text(dc, "SURFACE", new Point(x, SurfaceRect(0).Center.Y), 21, Dim);
        foreach (var s in Enum.GetValues<Surface>())
            Button(dc, SurfaceRect(s), Track.Describe(s), () => ed.SetSurface(s), on: ed.Track.Surface == s, size: 21);
        string about = About(ed.Track.Surface);
        double aboutSize = 17, aboutX = SurfaceRect(Surface.Ice).Right + 12;
        while (TextWidth(about, aboutSize) > EditorPanel.Right - aboutX - 8 && aboutSize > 11) aboutSize -= 0.5;
        Text(dc, about, new Point(aboutX, SurfaceRect(0).Center.Y), aboutSize, Ink);
        Text(dc, "GRAVITY", new Point(x, PlanetRect(0).Center.Y), 21, Dim);
        foreach (var p in Enum.GetValues<Planet>())
            Button(dc, PlanetRect(p), $"{Track.Describe(p)} {Track.Gravity(p):0.##}G", () => ed.SetPlanet(p), on: ed.Track.Planet == p, size: 20);
        Text(dc, "NAME", new Point(x, NameRect.Center.Y), 21, Dim);
        bool typing = g.Renaming != null;
        dc.FillRectangle(Brush(typing ? 0xFF101040 : 0xFF241C60), NameRect, 3);
        dc.DrawRectangle(null, Pen(typing ? Yellow : LightBlue, 1.4), NameRect, 3, 3);
        string name = typing ? g.Renaming! + (Blink(3) ? "_" : " ") : ed.Track.Name;
        Text(dc, name, new Point(NameRect.X + 10, NameRect.Center.Y), 22, typing ? Yellow : White);
        if (!typing) Hit(NameRect, g.BeginRename);
        Text(dc, typing ? "TYPE A NAME, ENTER WHEN DONE" : "CLICK TO RENAME", new Point(NameRect.Right + 12, NameRect.Center.Y), 16, Dim);
        double y = EditorPanel.Bottom - 16;
        Text(dc, About(ed.Track.Planet), new Point(x, y), 18, Cyan);
    }

    private void DrawTracksTab(DrawingContext dc)
    {
        var g = Engine;
        var names = Tracks.Names.Append("BLANK GRID").ToArray();
        for (int i = 0; i < names.Length; i++)
        {
            string n = names[i];
            Button(dc, BuiltInRect(i), n, () => { if (n == "BLANK GRID") g.OpenBlank(); else g.OpenBuiltIn(n); }, on: g.Editor.Track.Name == n, size: 19);
        }
        Text(dc, "TEN TRACKS IN THE BOX: OPEN ONE TO RACE OR CHANGE", new Point(BuiltInRect(11).X + 4, BuiltInRect(11).Center.Y - 8), 15, Dim);
        Text(dc, "(UNDO BRINGS BACK WHAT WAS THERE)", new Point(BuiltInRect(11).X + 4, BuiltInRect(11).Center.Y + 8), 15, Dim);
    }

    private void DrawYoursTab(DrawingContext dc)
    {
        var g = Engine;
        if (_clock - _savedAt > 2 || _savedAt > _clock)
        {
            _saved = g.SavedTracks();
            _savedAt = _clock;
        }
        double x = EditorPanel.X + 12, y = EditorPanel.Y + 12;
        Button(dc, new Rect(x, y, 200, 34), "SAVE THIS TRACK", () => { g.SaveTrack(); _savedAt = -10; }, size: 21, accent: Green);
        Text(dc, $"AS {TrackFile.Slug(g.Editor.Track.Name).ToUpperInvariant()}{TrackFile.Extension.ToUpperInvariant()}", new Point(x + 212, y + 17), 17, Dim);
        if (_saved.Count == 0) Text(dc, "NO TRACKS SAVED YET", new Point(x, y + 64), 20, Ink);
        for (int i = 0; i < Math.Min(_saved.Count, 9); i++)
        {
            var (name, path) = _saved[i];
            Button(dc, new Rect(x + i % 3 * 248, y + 46 + i / 3 * 34, 240, 30), name, () => g.OpenFile(path), on: g.Editor.Track.Name == name, size: 19);
        }
    }
}

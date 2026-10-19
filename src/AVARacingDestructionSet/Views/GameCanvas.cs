using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using AVARacingDestructionSet.Engine;

namespace AVARacingDestructionSet.Views;

/// <summary>
/// The screen: a fixed 1024×768 picture scaled to fit the window. The construction set (the grid on the left, the
/// parts box on the right, the terrain and the tracks below), the garage (the vehicles, their parts, the race), and
/// the race split in two, a half for each car. Runs the engine from a 60 Hz timer, and turns the mouse and the
/// keys into the engine's calls.
/// </summary>
public sealed partial class GameCanvas : Control
{
    public const double ScreenWidth = 1024;
    public const double ScreenHeight = 768;

    private readonly DispatcherTimer? _timer;
    private DateTime _last = DateTime.Now;
    private bool _padPausePrev, _padBackPrev, _padUpPrev, _padDownPrev, _padLeftPrev, _padRightPrev, _padAcceptPrev, _padDonePrev;
    private double _clock;
    private readonly List<(Rect R, Action Do)> _hits = [];
    private Point _mouse = new(-1, -1);
    /// <summary>A piece being dragged from the box (let go on its own tile, it stays in the hand to be stamped down square after square).</summary>
    private int _boxTile = -1;
    /// <summary>Whose vehicle the garage is showing.</summary>
    public int GaragePlayer { get; set; }

    public GameEngine Engine { get; }
    public SoundManager Sound { get; }
    public InputHandler Input { get; } = new();

    public GameCanvas(bool silent = false) : this(new GameEngine(), new SoundManager(silent), startTimer: true) { }

    /// <summary>For tests: an engine of your own, and no timer (call <see cref="Tick"/>).</summary>
    public GameCanvas(GameEngine engine, SoundManager sound, bool startTimer)
    {
        Engine = engine;
        Sound = sound;
        Engine.Sound += Sound.Play;
        Focusable = true;
        ClipToBounds = true;
        if (startTimer)
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _timer.Tick += (_, _) => Tick();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _timer?.Start();
        Focus();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer?.Stop();
        Sound.Dispose();
        Input.Dispose();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        Input.Clear();
    }

    /// <summary>One frame: read the controls, run the game, redraw.</summary>
    public void Tick()
    {
        var now = DateTime.Now;
        double dt = Math.Min(0.1, (now - _last).TotalSeconds);
        _last = now;
        _clock += dt;
        Input.Update();
        HandlePad();
        bool start = Input.Start && Engine.Renaming == null && Engine.State != GameState.Racing;
        Engine.Update(dt, Input.Player(0), Input.Player(1), start);
        Sound.Engines(Engine.State == GameState.Paused ? null : Engine.EngineNoise());
        Cursor = Engine.State == GameState.Racing ? new Cursor(StandardCursorType.None) : null;
        InvalidateVisual();
    }

    /// <summary>For tests: moves the drawing clock (blinking lamps, the turntable) without running the game.</summary>
    public void Advance(double dt)
    {
        _clock += dt;
        InvalidateVisual();
    }

    private void HandlePad()
    {
        bool Edge(bool now, ref bool prev) { bool e = now && !prev; prev = now; return e; }
        if (Edge(Input.PadPause, ref _padPausePrev))
        {
            if (Engine.State == GameState.Racing) Engine.Pause();
            else if (Engine.State == GameState.Paused) Engine.Resume();
        }
        if (Edge(Input.PadBack, ref _padBackPrev))
        {
            if (Engine.State == GameState.Paused) Engine.QuitRace();
            else if (Engine.State == GameState.Racing) Engine.Pause();
            else if (Engine.State == GameState.Garage) Engine.OpenEditor();
        }
        if (Engine.State == GameState.EnterInitials && Engine.Initials is { } entry)
        {
            if (Edge(Input.PadUp, ref _padUpPrev)) entry.Cycle(1);
            if (Edge(Input.PadDown, ref _padDownPrev)) entry.Cycle(-1);
            if (Edge(Input.PadLeft, ref _padLeftPrev)) entry.Back();
            if (Edge(Input.PadRight, ref _padRightPrev)) entry.Move(1);
            if (Edge(Input.PadAccept, ref _padAcceptPrev)) entry.Advance();
            if (Edge(Input.PadDone, ref _padDonePrev)) entry.Finish();
        }
        if (Engine.State == GameState.Garage)
        {
            if (Edge(Input.PadLeft, ref _padLeftPrev)) StepVehicle(-1);
            if (Edge(Input.PadRight, ref _padRightPrev)) StepVehicle(1);
        }
    }

    // ───────────────────────────── keys ─────────────────────────────

    private static readonly Dictionary<Key, Tool> ToolKeys = new() { [Key.H] = Tool.Hand, [Key.T] = Tool.Turn, [Key.E] = Tool.Erase };

    public static string KeyFor(Tool t) => ToolKeys.FirstOrDefault(kv => kv.Value == t).Key.ToString();

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.Handled = true;
        var g = Engine;
        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (e.Key == Key.S && ctrl)
        {
            SaveScreenshot(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), $"AVARacingDestructionSet_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png"));
            return;
        }
        // While entering initials every key belongs to the entry; while renaming the track, to the name.
        if (g.State == GameState.EnterInitials && g.Initials is { } entry)
        {
            InitialsKey(entry, e.Key);
            return;
        }
        if (g.Renaming is { } name)
        {
            RenameKey(name, e.Key);
            return;
        }
        if (e.Key == Key.M)
        {
            Sound.IsMuted = !Sound.IsMuted;
            return;
        }
        switch (g.State)
        {
            case GameState.Title or GameState.Attract:
                if (e.Key == Key.Escape)
                {
                    if (g.State == GameState.Attract) g.ReturnToTitle();
                    else (TopLevel.GetTopLevel(this) as Window)?.Close();
                    return;
                }
                break;
            case GameState.Editing:
                if (EditorKey(e.Key, ctrl, e.KeyModifiers.HasFlag(KeyModifiers.Shift))) return;
                break;
            case GameState.Garage:
                if (e.Key == Key.Escape) { g.OpenEditor(); return; }
                if (e.Key is Key.Left or Key.Right && !ctrl) { StepVehicle(e.Key == Key.Left ? -1 : 1); return; }
                if (e.Key == Key.Tab) { GaragePlayer = 1 - GaragePlayer; g.Choose(() => { }); return; }
                break;
            case GameState.Racing:
                if (e.Key is Key.Escape or Key.P or Key.Pause) { g.Pause(); return; }
                break;
            case GameState.Paused:
                if (e.Key == Key.Escape) g.QuitRace();
                else g.Resume();
                return;
            case GameState.Results or GameState.BestLaps:
                if (e.Key == Key.Escape) { g.OpenGarage(); return; }
                break;
        }
        Input.OnKeyDown(e.Key, e.PhysicalKey);
    }

    /// <summary>The construction set's keys; true if the key was one of them.</summary>
    private bool EditorKey(Key key, bool ctrl, bool shift)
    {
        var ed = Engine.Editor;
        if (ctrl && key == Key.Z) { ed.Undo(); return true; }
        if (ctrl) return false;
        if (ToolKeys.TryGetValue(key, out var tool)) { ed.SetTool(tool); return true; }
        switch (key)
        {
            case Key.Escape:
                if (ed.Carried != null) { ed.Cancel(); _boxTile = -1; }
                else Engine.ReturnToTitle();
                return true;
            case Key.Delete or Key.Back:
                ed.DeleteSelected();
                return true;
            case Key.R:
                ed.RotateSelected();
                return true;
            case Key.Tab:
                ed.Tab = (Tab)(((int)ed.Tab + (shift ? 2 : 1)) % 3);
                ed.Heard.Add(Sfx.Click);
                return true;
        }
        return false;
    }

    private void RenameKey(string name, Key key)
    {
        switch (key)
        {
            case Key.Enter: Engine.FinishRename(); return;
            case Key.Escape: Engine.Renaming = null; return;
            case Key.Back: if (name.Length > 0) Engine.Renaming = name[..^1]; return;
        }
        char? c = key switch
        {
            >= Key.A and <= Key.Z => (char)('A' + (key - Key.A)),
            >= Key.D0 and <= Key.D9 => (char)('0' + (key - Key.D0)),
            Key.Space => ' ',
            Key.OemMinus => '-',
            _ => null,
        };
        if (c is { } ch && name.Length < Track.NameLength) Engine.Renaming = name + ch;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        Input.OnKeyUp(e.Key, e.PhysicalKey);
        e.Handled = true;
    }

    private static void InitialsKey(InitialsEntry entry, Key key)
    {
        switch (key)
        {
            case Key.Up: entry.Cycle(1); break;
            case Key.Down: entry.Cycle(-1); break;
            case Key.Left or Key.Back: entry.Back(); break;
            case Key.Right: entry.Move(1); break;
            case Key.Space: entry.Advance(); break;
            case Key.Enter: entry.Finish(); break;
            case Key.OemPeriod or Key.Decimal: entry.Type('.'); break;
            case >= Key.A and <= Key.Z: entry.Type((char)('A' + (key - Key.A))); break;
            case >= Key.D0 and <= Key.D9: entry.Type((char)('0' + (key - Key.D0))); break;
            case >= Key.NumPad0 and <= Key.NumPad9: entry.Type((char)('0' + (key - Key.NumPad0))); break;
        }
    }

    private void StepVehicle(int by)
    {
        var b = Engine.Builds[GaragePlayer];
        int i = Array.IndexOf(Vehicles.All, b.Vehicle);
        Engine.Choose(() => b.Vehicle = Vehicles.All[(i + by + Vehicles.All.Length) % Vehicles.All.Length]);
    }

    // ───────────────────────────── the mouse ─────────────────────────────

    /// <summary>A point in the window to the fixed screen.</summary>
    private Point ToScreen(Point p)
    {
        var (s, ox, oy) = Fit();
        return s <= 0 ? new Point(-1, -1) : new Point((p.X - ox) / s, (p.Y - oy) / s);
    }

    /// <summary>Where the editor's grid is on the screen, and how big: a square is <see cref="GridScale"/> × 128 across.</summary>
    public static readonly Point GridOrigin = new(34, 84);
    public const double GridScale = 74.0 / 128;
    public static readonly Rect GridRect = new(GridOrigin.X, GridOrigin.Y, Track.Cols * 74, Track.Rows * 74 * Squash);

    /// <summary>The square of the grid under a point on the screen (-1, -1 off it).</summary>
    public static (int Col, int Row) ToCell(Point at)
    {
        double x = (at.X - GridOrigin.X) / GridScale, y = (at.Y - GridOrigin.Y) / GridScale / Squash;
        int c = (int)Math.Floor(x / Pieces.Cell), r = (int)Math.Floor(y / Pieces.Cell);
        return Track.OnGrid(c, r) ? (c, r) : (-1, -1);
    }

    /// <summary>The middle of a square of the grid on the screen.</summary>
    public static Point CellCentre(int col, int row) =>
        new(GridOrigin.X + (col + 0.5) * Pieces.Cell * GridScale, GridOrigin.Y + (row + 0.5) * Pieces.Cell * GridScale * Squash);

    /// <summary>The button (or other place that answers a click) under a point, topmost first.</summary>
    private Action? HitAt(Point at)
    {
        for (int i = _hits.Count - 1; i >= 0; i--)
            if (_hits[i].R.Contains(at)) return _hits[i].Do;
        return null;
    }

    /// <summary>The left (or right) button down at a point on the screen. For tests too.</summary>
    public void Press(Point at, bool right = false)
    {
        var g = Engine;
        _mouse = at;
        switch (g.State)
        {
            case GameState.Title or GameState.Attract:
                g.OpenEditor();
                return;
            case GameState.Paused:
                g.Resume();
                return;
            case GameState.Editing or GameState.Garage or GameState.BestLaps or GameState.Results:
                break;
            default:
                return;
        }
        if (g.Renaming != null && !NameRect.Contains(at)) g.FinishRename();
        var ed = g.Editor;
        var (col, row) = ToCell(at);
        if (right)
        {
            if (g.State == GameState.Editing) ed.RightClick(col, row);
            return;
        }
        if (HitAt(at) is { } act) { act(); return; }
        if (g.State == GameState.Editing && col >= 0)
        {
            ed.Drag(col, row);
            ed.Press(col, row);
        }
    }

    /// <summary>The mouse moved to a point on the screen (a button held or not).</summary>
    public void PointTo(Point at)
    {
        _mouse = at;
        if (Engine.State != GameState.Editing) return;
        var (col, row) = ToCell(at);
        Engine.Editor.Drag(col, row);
    }

    /// <summary>The left button up at a point on the screen.</summary>
    public void Lift(Point at)
    {
        _mouse = at;
        int tile = _boxTile;
        _boxTile = -1;
        if (Engine.State != GameState.Editing) return;
        var ed = Engine.Editor;
        if (ed.Carried == null) return;
        var (col, row) = ToCell(at);
        bool overBox = BoxRect.Contains(at) || col < 0;
        // Let go on the tile it came from, a piece stays in the hand, to be clicked down square after square.
        if (tile >= 0 && BoxTile(tile).Contains(at)) return;
        if (tile >= 0 || !ed.CarriedIsNew) ed.Release(col, row, overBox);
    }

    /// <summary>A click (down and up) at a point on the screen.</summary>
    public void Click(Point at, bool right = false)
    {
        Press(at, right);
        if (!right) Lift(at);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        PointTo(ToScreen(e.GetPosition(this)));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        Focus();
        var props = e.GetCurrentPoint(this).Properties;
        Press(ToScreen(e.GetPosition(this)), right: props.IsRightButtonPressed);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton == MouseButton.Left) Lift(ToScreen(e.GetPosition(this)));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _mouse = new Point(-1, -1);
        if (Engine.State == GameState.Editing) Engine.Editor.Drag(-1, -1);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (Engine.State == GameState.Editing) Engine.Editor.RightClick(Engine.Editor.Hover.Col, Engine.Editor.Hover.Row);
        else if (Engine.State == GameState.Garage) StepVehicle(e.Delta.Y > 0 ? -1 : 1);
    }

    /// <summary>Saves what's on screen as a PNG.</summary>
    public void SaveScreenshot(string path)
    {
        var size = new PixelSize((int)Math.Max(1, Bounds.Width), (int)Math.Max(1, Bounds.Height));
        using var bitmap = new RenderTargetBitmap(size);
        bitmap.Render(this);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        bitmap.Save(path, PngBitmapEncoderOptions.Default);
    }

    /// <summary>How the fixed screen fits the window: scale and letterbox offsets.</summary>
    private (double Scale, double X, double Y) Fit()
    {
        double s = Math.Min(Bounds.Width / ScreenWidth, Bounds.Height / ScreenHeight);
        return (s, (Bounds.Width - ScreenWidth * s) / 2, (Bounds.Height - ScreenHeight * s) / 2);
    }

    public override void Render(DrawingContext dc)
    {
        dc.FillRectangle(Brushes.Black, new Rect(Bounds.Size));
        var (s, ox, oy) = Fit();
        if (s <= 0) return;
        using (dc.PushTransform(Matrix.CreateScale(s, s) * Matrix.CreateTranslation(ox, oy)))
        using (dc.PushClip(new Rect(0, 0, ScreenWidth, ScreenHeight)))
            DrawScreen(dc);
    }
}

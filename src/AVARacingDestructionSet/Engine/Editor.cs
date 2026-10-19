namespace AVARacingDestructionSet.Engine;

/// <summary>The tools: the hand picks pieces up and moves them (and back into the box to throw them away); the turn tool turns a piece a quarter; the eraser takes one away.</summary>
public enum Tool { Hand, Turn, Erase }

/// <summary>The panel under the grid: the terrain (surface and planet), the tracks in the box, and the user's own.</summary>
public enum Tab { Terrain, Tracks, Yours }

/// <summary>
/// The construction set with no UI: a track and everything done to it. The mouse comes in as presses, moves and
/// releases on squares of the grid (column, row; off the grid is -1); each change is kept so it can be undone.
/// </summary>
public sealed class Editor
{
    public const int UndoDepth = 60;

    private readonly List<Track> _undo = [];
    private Check? _check;
    private Course? _course;
    private (int Col, int Row) _liftedFrom = (-1, -1);

    public Editor(Track? track = null) => Track = track ?? Tracks.Blank();

    public Track Track { get; private set; }
    public Tool Tool { get; private set; } = Tool.Hand;
    public Tab Tab { get; set; } = Tab.Terrain;
    /// <summary>A piece in the hand, following the mouse: fresh from the box, or lifted off the grid.</summary>
    public Piece? Carried { get; private set; }
    public bool CarriedIsNew { get; private set; }
    /// <summary>The square under the mouse (-1: off the grid).</summary>
    public (int Col, int Row) Hover { get; private set; } = (-1, -1);
    /// <summary>The square last picked from or put down on (-1: none).</summary>
    public (int Col, int Row) Selected { get; private set; } = (-1, -1);
    /// <summary>The kind of piece last handled, for the description beside the box.</summary>
    public PieceKind? Shown { get; private set; }
    public bool CanUndo => _undo.Count > 0;
    /// <summary>Changed since it was last kept on disk (the engine keeps the working track there).</summary>
    public bool Changed { get; set; }
    public List<Sfx> Heard { get; } = [];
    public string? Message { get; private set; }
    public double MessageTime { get; private set; }
    /// <summary>Whether the track is a closed loop, and if not why not: worked out again after every change.</summary>
    public Check Check => _check ??= Track.Check();
    /// <summary>The track laid out for racing (a copy, so changes after don't touch a race), or null if it isn't a closed loop.</summary>
    public Course? Course => Check.Ok ? _course ??= Engine.Course.Build(Track.Copy()) : null;
    public Piece? SelectedPiece => Track.At(Selected.Col, Selected.Row);

    public void Tick(double dt)
    {
        if (MessageTime > 0 && (MessageTime -= dt) <= 0) Message = null;
    }

    public void Say(string text, double seconds = 2.5)
    {
        Message = text;
        MessageTime = seconds;
    }

    private void Touch()
    {
        Changed = true;
        Invalidate();
    }

    private void Invalidate()
    {
        _check = null;
        _course = null;
    }

    /// <summary>A whole new track to work on (from the box, a file, a blank): what was there can be had back with undo.</summary>
    public void Open(Track t)
    {
        Remember();
        Track = t.Copy();
        Selected = (-1, -1);
        Cancel();
        Touch();
    }

    public void SetTool(Tool t)
    {
        Cancel();
        Tool = t;
        Heard.Add(Sfx.Click);
    }

    /// <summary>Drops whatever is in the hand: a piece from the box goes back; a lifted one goes back where it was.</summary>
    public void Cancel()
    {
        if (Carried != null && !CarriedIsNew && Track.OnGrid(_liftedFrom.Col, _liftedFrom.Row))
        {
            Carried.Col = _liftedFrom.Col;
            Carried.Row = _liftedFrom.Row;
            Track.Put(Carried);
            _undo.RemoveAt(_undo.Count - 1);
            Invalidate();
        }
        Carried = null;
    }

    private void Remember()
    {
        _undo.Add(Track.Copy());
        if (_undo.Count > UndoDepth) _undo.RemoveAt(0);
    }

    public bool Undo()
    {
        Cancel();
        if (_undo.Count == 0) { Heard.Add(Sfx.Refused); return false; }
        Track = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        Selected = (-1, -1);
        Touch();
        Heard.Add(Sfx.Undo);
        return true;
    }

    // ───────────────────────────── pieces ─────────────────────────────

    /// <summary>A new piece straight onto a square, replacing whatever was there.</summary>
    public Piece? Place(PieceKind kind, int col, int row, int rot = 0)
    {
        if (!Track.OnGrid(col, row)) { Heard.Add(Sfx.Refused); return null; }
        Remember();
        var p = new Piece(kind, col, row, rot % Pieces.Turns(kind));
        Track.Put(p);
        Selected = (col, row);
        Shown = kind;
        Touch();
        Heard.Add(Sfx.Place);
        return p;
    }

    /// <summary>A piece taken from the box into the hand, to be put down where the mouse lets go.</summary>
    public void TakeFromBox(PieceKind kind)
    {
        Cancel();
        Tool = Tool.Hand;
        Carried = new Piece(kind, -1, -1);
        CarriedIsNew = true;
        Shown = kind;
        Heard.Add(Sfx.Pick);
    }

    /// <summary>Turns a piece a quarter clockwise (a straight has two ways round, a crossover only one).</summary>
    public bool Rotate(int col, int row)
    {
        var p = Track.At(col, row);
        if (p == null || Pieces.Turns(p.Kind) == 1)
        {
            if (p != null) Say($"THE {Pieces.Name(p.Kind)} IS THE SAME ANY WAY ROUND");
            Heard.Add(Sfx.Refused);
            return false;
        }
        Remember();
        p = Track.At(col, row)!;
        p.Rot = (p.Rot + 1) % Pieces.Turns(p.Kind);
        Selected = (col, row);
        Shown = p.Kind;
        Touch();
        Heard.Add(Sfx.Turn);
        return true;
    }

    /// <summary>Turns the piece in the hand.</summary>
    public void TurnCarried()
    {
        if (Carried is not { } p) return;
        p.Rot = (p.Rot + 1) % Pieces.Turns(p.Kind);
        Heard.Add(Pieces.Turns(p.Kind) == 1 ? Sfx.Refused : Sfx.Turn);
    }

    public bool Delete(int col, int row)
    {
        if (Track.At(col, row) == null) { Heard.Add(Sfx.Refused); return false; }
        Remember();
        Track.Remove(col, row);
        if (Selected == (col, row)) Selected = (-1, -1);
        Touch();
        Heard.Add(Sfx.Delete);
        return true;
    }

    public void RotateSelected()
    {
        if (Carried != null) TurnCarried();
        else Rotate(Selected.Col, Selected.Row);
    }

    public void DeleteSelected()
    {
        if (Carried != null) { Carried = null; Heard.Add(Sfx.Delete); return; }
        Delete(Selected.Col, Selected.Row);
    }

    /// <summary>Everything off the grid (undo brings it back).</summary>
    public void Clear()
    {
        if (Track.Pieces.Count == 0) { Heard.Add(Sfx.Refused); return; }
        Remember();
        Cancel();
        Track.Pieces.Clear();
        Selected = (-1, -1);
        Touch();
        Heard.Add(Sfx.Delete);
    }

    // ───────────────────────────── the mouse ─────────────────────────────

    /// <summary>The left button goes down on a square. With a piece from the box in the hand, it's put down there, and another like it stays in the hand.</summary>
    public void Press(int col, int row)
    {
        if (!Track.OnGrid(col, row)) return;
        if (Carried is { } held && CarriedIsNew)
        {
            Place(held.Kind, col, row, held.Rot);
            return;
        }
        switch (Tool)
        {
            case Tool.Hand:
            {
                Selected = (col, row);
                if (Track.At(col, row) is not { } p) return;
                Remember();
                Track.Remove(col, row);
                Invalidate();
                Carried = p;
                CarriedIsNew = false;
                _liftedFrom = (col, row);
                Shown = p.Kind;
                Heard.Add(Sfx.Pick);
                return;
            }
            case Tool.Turn:
                Rotate(col, row);
                return;
            case Tool.Erase:
                Delete(col, row);
                return;
        }
    }

    /// <summary>The mouse moves (a button held or not) over a square, or off the grid (-1).</summary>
    public void Drag(int col, int row) => Hover = Track.OnGrid(col, row) ? (col, row) : (-1, -1);

    /// <summary>The left button comes up: over a square, or off the grid or over the box (where a piece in the hand is thrown away).</summary>
    public void Release(int col, int row, bool overBox = false)
    {
        if (Carried is not { } p) return;
        Carried = null;
        bool onGrid = Track.OnGrid(col, row) && !overBox;
        if (CarriedIsNew)
        {
            if (onGrid) Place(p.Kind, col, row, p.Rot);
            else Heard.Add(Sfx.Click);
            return;
        }
        if (!onGrid)
        {
            // Back in the box: it's gone. (The press already kept the track as it was, for undo.)
            if (Selected == _liftedFrom) Selected = (-1, -1);
            Touch();
            Heard.Add(Sfx.Delete);
            return;
        }
        if ((col, row) == _liftedFrom)
        {
            // Picked up and put back: nothing to undo.
            Track.Put(p);
            _undo.RemoveAt(_undo.Count - 1);
            Invalidate();
            return;
        }
        p.Col = col;
        p.Row = row;
        Track.Put(p);
        Selected = (col, row);
        Touch();
        Heard.Add(Sfx.Place);
    }

    /// <summary>The right button: turns the piece in the hand, or the one under the mouse.</summary>
    public void RightClick(int col, int row)
    {
        if (Carried != null) TurnCarried();
        else Rotate(col, row);
    }

    // ───────────────────────────── the terrain ─────────────────────────────

    public void SetSurface(Surface s)
    {
        if (Track.Surface == s) return;
        Remember();
        Track.Surface = s;
        Touch();
        Heard.Add(Sfx.Click);
    }

    public void SetPlanet(Planet p)
    {
        if (Track.Planet == p) return;
        Remember();
        Track.Planet = p;
        Touch();
        Heard.Add(Sfx.Click);
    }

    public void Rename(string name)
    {
        name = Track.CleanName(name);
        if (name == Track.Name) return;
        Remember();
        Track.Name = name;
        Touch();
    }
}

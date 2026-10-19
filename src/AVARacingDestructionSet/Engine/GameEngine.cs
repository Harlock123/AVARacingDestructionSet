namespace AVARacingDestructionSet.Engine;

public enum GameState
{
    /// <summary>The title, with cars going round a track behind it.</summary>
    Title,
    /// <summary>The demonstration: two computer drivers racing the tracks in the box.</summary>
    Attract,
    /// <summary>The construction set: the track editor.</summary>
    Editing,
    /// <summary>The vehicle builder, and how the race is to be run.</summary>
    Garage,
    Racing,
    Paused,
    /// <summary>The race over: who won, the times.</summary>
    Results,
    EnterInitials,
    /// <summary>The track's ten best laps.</summary>
    BestLaps,
}

/// <summary>Sounds the engine asks for; the host decides how they sound.</summary>
public enum Sfx
{
    Beep, Go, Skid, Bump, Crash, Explosion, Oil, Mine, Empty, Jump, Land, Tow, Respawn, Lap, FinalLap, Flag, Win, Lose,
    Click, Pick, Place, Delete, Turn, Undo, Refused, Saved, Record,
    TitleTune, MusicStop,
}

/// <summary>
/// The whole program with no UI: the title and its demonstration, the construction set, the garage, a race on the
/// track built, the best laps. Call <see cref="Update"/> with the elapsed time and both players' controls; it runs
/// in fixed 60 Hz steps, so it plays the same at any frame rate. The mouse comes in through the <see cref="Editor"/>
/// and the garage's choices.
/// </summary>
public sealed class GameEngine
{
    public const double TitleIdleSeconds = 18;
    public const double AttractSeconds = 55;
    public const double BestLapsSeconds = 10;
    /// <summary>How long the results stand before the program moves on by itself.</summary>
    public const double ResultsSeconds = 12;

    private double _accumulator, _idle, _saveWait;
    private bool _startPrev, _tunePending = true;
    private int _attract, _seed = 1;
    private readonly Queue<int> _pending = new();
    /// <summary>Where the best laps lead: back to the garage after a race, to the title after the demonstration.</summary>
    private GameState _after = GameState.Garage;

    public GameEngine(string? folder = null)
    {
        Folder = folder ?? BestLapTable.DefaultFolder;
        Editor = new Editor(TrackFile.Load(WorkingPath) ?? Tracks.Make(Tracks.Names[0]));
        Editor.Changed = false;
        Demo(0);
        State = GameState.Title;
    }

    public event Action<Sfx>? Sound;

    public GameState State { get; private set; } = GameState.Title;
    public double StateTime { get; private set; }
    public bool Silent { get; set; }
    /// <summary>The user's folder: best laps, the track being worked on, and the saved tracks.</summary>
    public string Folder { get; }
    public string TracksFolder => Path.Combine(Folder, "tracks");
    public string WorkingPath => Path.Combine(Folder, "working" + TrackFile.Extension);
    public Editor Editor { get; }
    public World World { get; private set; } = new();
    public Pilot[] Pilots { get; } = [new(), new()];
    /// <summary>The two players' vehicles, as built in the garage (the second is the computer's, if it's driving).</summary>
    public Build[] Builds { get; } =
    [
        new() { Vehicle = VehicleKind.StockCar, Engine = EngineKind.Eight, Weapon = WeaponKind.Oil },
        new() { Vehicle = VehicleKind.Pickup, Engine = EngineKind.Eight, Weapon = WeaponKind.Mines },
    ];
    /// <summary>A friend at the second car's controls (otherwise the computer drives it).</summary>
    public bool Friend { get; set; }
    public RaceRules Rules { get; set; } = new();
    /// <summary>The best laps of the track being raced (or last raced).</summary>
    public BestLapTable BestLaps { get; private set; } = new();
    public InitialsEntry? Initials { get; private set; }
    /// <summary>The player whose initials are being entered.</summary>
    public int EntryPlayer { get; private set; } = -1;
    public int NewEntryIndex { get; private set; } = -1;
    /// <summary>The name of the track being typed in (null: not renaming).</summary>
    public string? Renaming { get; set; }
    public bool[] Humans => [true, Friend];

    /// <summary>The track in the construction set laid out for racing, or null if it isn't a closed loop.</summary>
    public Course? Course => Editor.Course;

    private void Play(Sfx s)
    {
        if (!Silent) Sound?.Invoke(s);
    }

    public void Update(double dt, Controls p1, Controls p2, bool start)
    {
        _accumulator += Math.Min(dt, 0.25);
        while (_accumulator >= Field.Step)
        {
            _accumulator -= Field.Step;
            Tick(p1, p2, start);
        }
    }

    public void Tick(Controls p1, Controls p2 = default, bool start = false)
    {
        double dt = Field.Step;
        StateTime += dt;
        bool startPressed = start && !_startPrev;
        _startPrev = start;
        Editor.Tick(dt);
        switch (State)
        {
            case GameState.Title:
                if (_tunePending) { _tunePending = false; Play(Sfx.TitleTune); }
                _idle += dt;
                if (startPressed) OpenEditor();
                else if (_idle > TitleIdleSeconds) StartAttract();
                break;
            case GameState.Attract:
                Sim.Step(World, [Pilots[0].Decide(World, 0), Pilots[1].Decide(World, 1)]);
                Hear();
                if (startPressed) OpenEditor();
                else if (World.Over && World.PhaseTime > 4 || StateTime > AttractSeconds) ShowBestLaps(-1, GameState.Title);
                break;
            case GameState.Editing:
                if (startPressed) OpenGarage();
                AutoSave(dt);
                break;
            case GameState.Garage:
                if (startPressed) StartRace();
                break;
            case GameState.Racing:
                Sim.Step(World, [p1, Friend ? p2 : Pilots[1].Decide(World, 1)]);
                Hear();
                if (World.Over && World.PhaseTime > 2.5)
                {
                    State = GameState.Results;
                    StateTime = 0;
                }
                break;
            case GameState.Results:
                Sim.Step(World, new Controls[World.Players]);
                Hear();
                if (startPressed || StateTime > ResultsSeconds) NextInitials();
                break;
            case GameState.EnterInitials:
                if (Initials is { IsDone: true } done)
                {
                    var car = World.Cars[EntryPlayer];
                    int place = BestLaps.Insert(done.Initials, Millis(car.BestLap), car.Body.Name);
                    BestLaps.Save();
                    Initials = null;
                    NewEntryIndex = place;
                    NextInitials(place);
                }
                break;
            case GameState.BestLaps:
                if (startPressed || StateTime > BestLapsSeconds)
                {
                    if (_after == GameState.Garage) OpenGarage();
                    else ReturnToTitle();
                }
                break;
        }
        foreach (var s in Editor.Heard) Play(s);
        Editor.Heard.Clear();
    }

    private static int Millis(double seconds) => (int)Math.Round(seconds * 1000);

    /// <summary>The sounds of the race.</summary>
    private void Hear()
    {
        foreach (var s in World.Heard) Play(s);
        World.Heard.Clear();
    }

    /// <summary>The track being built is kept on disk as it changes, so nothing is lost when the program closes.</summary>
    private void AutoSave(double dt)
    {
        _saveWait -= dt;
        var e = Editor;
        if (!e.Changed || _saveWait > 0 || e.Carried != null) return;
        e.Changed = false;
        _saveWait = 1;
        try
        {
            TrackFile.Save(e.Track, WorkingPath);
        }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException)
        {
            // A read-only home folder just means the track isn't kept.
        }
    }

    /// <summary>
    /// A lap a little quicker than a fresh table's made-up times: the time the course takes at a steady 300, or
    /// slower where the grip is less (in low gravity, on dirt or ice).
    /// </summary>
    public static double Par(Course c)
    {
        double grip = Math.Sqrt(Math.Min(1, Sim.GravityGrip(Track.Gravity(c.Track.Planet), false)));
        double surface = c.Track.Surface switch { Surface.Ice => 0.85, Surface.Dirt => 0.92, _ => 1 };
        return c.Length / (300 * grip * surface);
    }

    /// <summary>Each human driver whose best lap made the track's table, in turn, enters initials; then the table.</summary>
    private void NextInitials(int place = -1)
    {
        if (State == GameState.Results)
        {
            _pending.Clear();
            for (int p = 0; p < World.Players; p++)
                if (World.Cars[p].Human) _pending.Enqueue(p);
            NewEntryIndex = -1;
        }
        while (_pending.Count > 0)
        {
            int p = _pending.Dequeue();
            if (!BestLaps.Qualifies(Millis(World.Cars[p].BestLap))) continue;
            EntryPlayer = p;
            State = GameState.EnterInitials;
            StateTime = 0;
            Initials = new InitialsEntry(BestLaps.LastInitials);
            Play(Sfx.Record);
            return;
        }
        EntryPlayer = -1;
        ShowBestLaps(place, GameState.Garage);
    }

    private void ShowBestLaps(int place, GameState after)
    {
        if (State == GameState.Attract) BestLaps = BestLapTable.Load(Folder, World.Track.Name, Par(World.Course));
        State = GameState.BestLaps;
        StateTime = 0;
        NewEntryIndex = place;
        _after = after;
    }

    /// <summary>Two computer drivers on one of the tracks in the box, a plain race and a destruction race in turn.</summary>
    private void Demo(int which)
    {
        var track = Tracks.Make(Tracks.Names[which % Tracks.Names.Length]);
        var course = Engine.Course.Build(track)!;
        bool destruction = which % 2 == 1;
        World = new World();
        Sim.Start(World, track, course, [Vehicles.ForComputer(track, destruction, which), Vehicles.ForComputer(track, destruction, which + 1)], [false, false],
            new RaceRules(destruction ? Mode.Destruction : Mode.Racing, 3), ++_seed);
    }

    private void StartAttract()
    {
        Demo(_attract++);
        State = GameState.Attract;
        StateTime = 0;
        Play(Sfx.MusicStop);
    }

    public void ReturnToTitle()
    {
        State = GameState.Title;
        StateTime = 0;
        _idle = 0;
        _tunePending = true;
        Renaming = null;
        Demo(0);
    }

    public void OpenEditor()
    {
        if (State is GameState.Title or GameState.Attract) Play(Sfx.MusicStop);
        State = GameState.Editing;
        StateTime = 0;
        Renaming = null;
    }

    public void OpenGarage()
    {
        if (State is GameState.Title or GameState.Attract) Play(Sfx.MusicStop);
        Editor.Cancel();
        Renaming = null;
        State = GameState.Garage;
        StateTime = 0;
    }

    /// <summary>A race on the track in the construction set, if it's a closed loop (if not, back to the construction set to say why).</summary>
    public bool StartRace()
    {
        Editor.Cancel();
        Renaming = null;
        if (Course is not { } course)
        {
            OpenEditor();
            Editor.Say("THE TRACK ISN'T READY: " + Editor.Check.Why, 4);
            Play(Sfx.Refused);
            return false;
        }
        World = new World();
        Sim.Start(World, course.Track, course, Builds, Humans, Rules, ++_seed);
        BestLaps = BestLapTable.Load(Folder, course.Track.Name, Par(course));
        NewEntryIndex = -1;
        State = GameState.Racing;
        StateTime = 0;
        Play(Sfx.MusicStop);
        return true;
    }

    public void Pause()
    {
        if (State == GameState.Racing) State = GameState.Paused;
    }

    public void Resume()
    {
        if (State == GameState.Paused) State = GameState.Racing;
    }

    /// <summary>Out of the race, back to the garage.</summary>
    public void QuitRace()
    {
        if (State is GameState.Racing or GameState.Paused or GameState.Results) OpenGarage();
    }

    /// <summary>A garage choice: the change, and the click.</summary>
    public void Choose(Action change)
    {
        change();
        Play(Sfx.Click);
    }

    /// <summary>How each car's engine sounds now: its vehicle and how hard it's revving (1 is idle), or null with no race on.</summary>
    public (VehicleKind Vehicle, double Revs)[]? EngineNoise()
    {
        if (State is not (GameState.Racing or GameState.Results or GameState.Attract) || World.Cars.Length == 0) return null;
        return [.. World.Cars.Select(c => (c.Build.Vehicle, c.Active ? 0.8 + 1.4 * Math.Min(1, Math.Abs(c.Speed) / Math.Max(1, c.Stats.Top)) + (c.Airborne ? 0.3 : 0) : 0.0))];
    }

    // ───────────────────────────── tracks on disk ─────────────────────────────

    public void OpenBuiltIn(string name)
    {
        Editor.Open(Tracks.Make(name));
        Editor.Say($"{name}: {Tracks.About(name)}", 4);
    }

    public void OpenBlank()
    {
        Editor.Open(Tracks.Blank());
        Editor.Say("A BLANK GRID: DRAG PIECES ON FROM THE BOX");
    }

    /// <summary>Saves the track into the tracks folder, under its name; returns the file, or null if it couldn't be written.</summary>
    public string? SaveTrack()
    {
        string path = Path.Combine(TracksFolder, TrackFile.Slug(Editor.Track.Name) + TrackFile.Extension);
        try
        {
            TrackFile.Save(Editor.Track, path);
            Editor.Say($"SAVED AS {Path.GetFileName(path).ToUpperInvariant()}");
            Play(Sfx.Saved);
            return path;
        }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException)
        {
            Editor.Say("COULDN'T SAVE THE TRACK");
            Play(Sfx.Refused);
            return null;
        }
    }

    public bool OpenFile(string path)
    {
        if (TrackFile.Load(path) is not { } t)
        {
            Editor.Say("THAT ISN'T A TRACK");
            Play(Sfx.Refused);
            return false;
        }
        Editor.Open(t);
        Editor.Say($"{t.Name} LOADED");
        return true;
    }

    public List<(string Name, string Path)> SavedTracks() => TrackFile.List(TracksFolder);

    public void BeginRename() => Renaming = Editor.Track.Name;

    public void FinishRename()
    {
        if (Renaming is { } n) Editor.Rename(n);
        Renaming = null;
    }

    // ───────────────────────────── for tests ─────────────────────────────

    public void Run(int steps, Controls p1 = default, Controls p2 = default)
    {
        for (int i = 0; i < steps; i++) Tick(p1, p2);
    }

    public void Idle(int steps) => Run(steps);
}

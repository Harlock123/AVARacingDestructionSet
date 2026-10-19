using AVARacingDestructionSet.Engine;

namespace AVARacingDestructionSet.Tests;

/// <summary>Shared helpers: temp folders, engines, races, driving.</summary>
public static class Lab
{
    public static string TempFolder() => Path.Combine(Path.GetTempPath(), "avards-tests-" + Guid.NewGuid().ToString("N"));

    /// <summary>A program on its own temp folder, quiet.</summary>
    public static GameEngine Engine() => new(TempFolder()) { Silent = true };

    /// <summary>A world ready to race on a track: the cars on the grid, past the lights unless asked not to be.</summary>
    public static World Race(Track t, Build? a = null, Build? b = null, RaceRules? rules = null, bool go = true, int seed = 3)
    {
        var course = Course.Build(t) ?? throw new InvalidOperationException(t.Check().Why);
        var w = new World();
        Sim.Start(w, t, course, [a ?? new Build(), b ?? new Build()], [true, true], rules ?? new RaceRules(), seed);
        if (go)
            while (w.Phase == RacePhase.Countdown) Sim.Step(w, new Controls[2]);
        w.Heard.Clear();
        return w;
    }

    public static World Race(string builtIn, Build? a = null, Build? b = null, RaceRules? rules = null, bool go = true) =>
        Race(Tracks.Make(builtIn), a, b, rules, go);

    public static void Step(World w, int steps, Controls a = default, Controls b = default)
    {
        for (int i = 0; i < steps; i++) Sim.Step(w, [a, b]);
    }

    /// <summary>Puts a car somewhere round the course, in the middle of the road (or off it), pointing along it, still.</summary>
    public static void Put(World w, int car, double along, double off = 0, double headingOff = 0)
    {
        var c = w.Cars[car];
        int i = w.Course.IndexAt(along);
        c.Index = c.LastRoad = i;
        c.Pos = w.Course.Points[i] + w.Course.Right(i) * off;
        c.Heading = w.Course.Dir(i).Angle + headingOff;
        c.Vel = default;
        c.Z = w.Course.Z[i];
        c.VZ = 0;
        c.Airborne = false;
        c.Progress = w.Course.Along[i];
    }

    /// <summary>Parks a car out of the way, far down the road from the other.</summary>
    public static void Park(World w, int car, double along) => Put(w, car, along, 0);

    /// <summary>A plain rectangle of a track from B2 (the start) round to C2.</summary>
    public const string Rectangle = "F S5 R S5 R S6 R S5 R";

    /// <summary>A straight track: a long rectangle, with something to try in the middle of the top straight.</summary>
    public static Track Strip(PieceKind middle = PieceKind.Straight, Surface surface = Surface.Asphalt, Planet planet = Planet.Earth, int rot = 1)
    {
        var t = Tracks.Walk(2, 1, 1, Rectangle);
        t.Put(new Piece(middle, 5, 1, middle == PieceKind.Cross ? 0 : rot));
        if (middle == PieceKind.Cross) throw new ArgumentException("a crossover needs a second road");
        t.Surface = surface;
        t.Planet = planet;
        t.Name = "STRIP";
        return t;
    }
}

namespace AVARacingDestructionSet.Engine;

/// <summary>A driver's controls: steering (-1 left, 0, 1 right), the gas, the brake, and the weapon.</summary>
public readonly record struct Controls(int Steer = 0, bool Gas = false, bool Brake = false, bool Fire = false);

/// <summary>A plain race, or one where the cars carry weapons and can be wrecked.</summary>
public enum Mode { Racing, Destruction }

/// <summary>What happens to a wrecked car: it comes back after a while, or it's out (and the other car has won).</summary>
public enum WreckRule { Respawn, Out }

/// <summary>How the race is to be run.</summary>
public sealed record RaceRules(Mode Mode = Mode.Racing, int Laps = 3, WreckRule Wrecks = WreckRule.Respawn)
{
    public const int MaxLaps = 9;
    public bool Destruction => Mode == Mode.Destruction;
}

/// <summary>Where a race has got to: the lights counting down, the race, everyone home.</summary>
public enum RacePhase { Countdown, Racing, Finished }

public enum DropKind { Oil, Mine }

/// <summary>Something a car has left on the road: an oil slick or a land mine.</summary>
public sealed class Drop
{
    public DropKind Kind;
    public Vec Pos;
    public double Z;
    public int Owner;
    public double Age;
    public Drop Copy() => (Drop)MemberwiseClone();
}

public enum EffectKind { Smoke, Dust, Boom, Splat, Spark, Debris, Word }

/// <summary>Something to see for a moment: tyre smoke, dust, an explosion, oil splashing, sparks, a word.</summary>
public sealed class Effect
{
    public EffectKind Kind;
    public Vec Pos, Vel;
    public double Z, VZ, Age, Life = 0.8, Size = 1;
    public string Text = "";
    public uint Colour = 0xFFFFE14A;
    public bool Done => Age >= Life;
    public Effect Copy() => (Effect)MemberwiseClone();
}

/// <summary>A vehicle in the race, as built in the garage, and everything about how it's getting on.</summary>
public sealed class Car
{
    public int Id;
    public bool Human;
    public Build Build = new();
    public Stats Stats;
    public Vec Pos, Vel;
    /// <summary>How high it is (the road's height under it when it's on the ground) and how fast it's going up.</summary>
    public double Z, VZ;
    /// <summary>Which way it points: 0 up the screen (north), turning clockwise.</summary>
    public double Heading;
    public bool Airborne;
    public double AirTime;
    /// <summary>Its nearest sample on the course; the last one it was on the road at; how far it has gone in all (behind the line at the start).</summary>
    public int Index, LastRoad;
    public double Progress;
    public bool OnRoad = true;
    /// <summary>The laps it has done, the clock when this one began, and the times.</summary>
    public int Laps;
    public double LapStart;
    public List<double> LapTimes = [];
    public double Damage;
    /// <summary>Wrecked and burning (counting down to coming back); out of the race; just back, and passing through things.</summary>
    public double Wrecked, Ghost;
    public bool Out;
    public int Wrecks;
    /// <summary>Slithering on oil; spinning (and which way); being towed back to the road; how hard it's sliding sideways.</summary>
    public double Slick, Spin, SpinDir = 1, Towed, Skid;
    public int Ammo;
    public double Cooldown;
    public bool FirePrev;
    public bool Finished;
    public int Place;
    public double FinishTime;

    public Vec Forward => Vec.FromAngle(Heading);
    public double Speed => Vel.Dot(Forward);
    public Vehicles.Body Body => Vehicles.Of(Build.Vehicle);
    /// <summary>How near another car's middle can come to this one's.</summary>
    public double Radius => (Body.Length + Body.Width) / 4;
    public bool Active => !Out && Wrecked <= 0 && Towed <= 0;
    public double BestLap => LapTimes.Count == 0 ? 0 : LapTimes.Min();

    public Car Copy()
    {
        var c = (Car)MemberwiseClone();
        c.Build = Build.Copy();
        c.LapTimes = [.. LapTimes];
        return c;
    }
}

/// <summary>
/// Everything in a race that moves or changes, kept together so it can be copied whole: the track and its course
/// (which never change, and are shared by copies), the two cars, what they've dropped on the road, the clock.
/// </summary>
public sealed class World
{
    public const int Players = 2;

    public Track Track = new();
    public Course Course = null!;
    public RaceRules Rules = new();
    public Car[] Cars = [];
    public List<Drop> Drops = [];
    public List<Effect> Effects = [];
    public RacePhase Phase;
    public double PhaseTime;
    /// <summary>The race's clock, from the green light.</summary>
    public double Clock;
    /// <summary>Once one car is home, how long the other has left.</summary>
    public double Late = -1;
    public int Finishers;
    /// <summary>The car that won (-1: not yet).</summary>
    public int Winner = -1;
    public Dice Dice = new(1);
    public List<Sfx> Heard = [];
    /// <summary>A word over each car's half of the screen, and for how much longer.</summary>
    public string?[] Messages = new string?[Players];
    public double[] MessageTimes = new double[Players];

    public double Gravity => Track.Gravity(Track.Planet);
    public bool Over => Phase == RacePhase.Finished;

    public void Say(int car, string text, double seconds = 1.6)
    {
        Messages[car] = text;
        MessageTimes[car] = seconds;
    }

    public World Copy() => new()
    {
        Track = Track, Course = Course, Rules = Rules, Cars = [.. Cars.Select(c => c.Copy())], Drops = [.. Drops.Select(d => d.Copy())],
        Effects = [.. Effects.Select(e => e.Copy())], Phase = Phase, PhaseTime = PhaseTime, Clock = Clock, Late = Late, Finishers = Finishers,
        Winner = Winner, Dice = Dice, Heard = [.. Heard], Messages = [.. Messages], MessageTimes = [.. MessageTimes],
    };
}

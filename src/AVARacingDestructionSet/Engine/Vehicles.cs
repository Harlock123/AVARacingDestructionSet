namespace AVARacingDestructionSet.Engine;

public enum VehicleKind { StockCar, BajaBug, Dragster, IndyCar, Pickup, Motorcycle, DumpTruck, MoonBuggy, LunarRover }
public enum EngineKind { Four, Six, Eight, Blown }
public enum TyreKind { Street, Slicks, Knobby, Studded }
public enum WeightKind { Light, Medium, Heavy }
public enum ArmourKind { None, Light, Heavy }
public enum WeaponKind { None, Oil, Mines }

/// <summary>A vehicle as built in the garage: the body, the engine, the tyres, the ballast, the armour and the weapon.</summary>
public sealed class Build
{
    public VehicleKind Vehicle { get; set; } = VehicleKind.StockCar;
    public EngineKind Engine { get; set; } = EngineKind.Six;
    public TyreKind Tyres { get; set; } = TyreKind.Street;
    public WeightKind Weight { get; set; } = WeightKind.Medium;
    public ArmourKind Armour { get; set; } = ArmourKind.None;
    public WeaponKind Weapon { get; set; } = WeaponKind.Oil;

    public Build Copy() => (Build)MemberwiseClone();
}

/// <summary>What a built vehicle can do, on a given surface.</summary>
public readonly record struct Stats(double Top, double Accel, double Grip, double Mass, double Toughness, double Protection, bool LowGravity)
{
    /// <summary>How much damage a knock of a given size does it, after its toughness and armour.</summary>
    public double Hurt(double raw) => raw / Toughness * (1 - Protection);
}

/// <summary>The bodies, and how a build's parts change what it can do.</summary>
public static class Vehicles
{
    /// <summary>A body: how fast, how quick off the mark, how grippy, how heavy and how tough, as built with a V6, street tyres and no armour.</summary>
    public sealed record Body(string Name, double Top, double Accel, double Grip, double Mass, double Toughness, double Length, double Width, double Height, double DirtBonus, bool LowGravity, string About);

    public static readonly VehicleKind[] All = Enum.GetValues<VehicleKind>();

    private static readonly Dictionary<VehicleKind, Body> Bodies = new()
    {
        [VehicleKind.StockCar] = new("STOCK CAR", 300, 240, 1.0, 1.0, 1.0, 26, 13, 9, 1.0, false, "THE ALL-ROUNDER: A BIG SEDAN WITH A ROLL CAGE."),
        [VehicleKind.BajaBug] = new("BAJA BUG", 272, 250, 0.95, 0.8, 0.9, 22, 13, 10, 1.25, false, "A BEETLE ON STILTS. HAPPY IN THE DIRT."),
        [VehicleKind.Dragster] = new("DRAGSTER", 390, 330, 0.74, 0.9, 0.6, 34, 11, 7, 0.85, false, "FASTEST IN A STRAIGHT LINE, AND ONLY THERE."),
        [VehicleKind.IndyCar] = new("INDY CAR", 368, 290, 1.25, 0.7, 0.5, 28, 12, 6, 0.7, false, "WINGS AND SLICK MANNERS. FRAGILE."),
        [VehicleKind.Pickup] = new("PICKUP", 282, 215, 0.92, 1.3, 1.4, 27, 14, 11, 1.12, false, "A FARM TRUCK WITH A BIG ENGINE. TAKES A KNOCK."),
        [VehicleKind.Motorcycle] = new("MOTORCYCLE", 330, 320, 1.08, 0.45, 0.35, 18, 6, 10, 1.0, false, "QUICK AND NIMBLE. ONE MINE AND IT'S OVER."),
        [VehicleKind.DumpTruck] = new("DUMP TRUCK", 222, 150, 0.86, 2.2, 2.4, 34, 17, 16, 1.1, false, "SLOW, HUGE AND ALMOST UNBREAKABLE. RAM THINGS."),
        [VehicleKind.MoonBuggy] = new("MOON BUGGY", 262, 240, 1.0, 0.6, 0.8, 22, 14, 8, 1.15, true, "BUILT FOR LOW GRAVITY: GRIPS ON THE MOON."),
        [VehicleKind.LunarRover] = new("LUNAR ROVER", 232, 222, 1.1, 0.7, 1.1, 26, 15, 9, 1.1, true, "SIX WHEELS AND A DISH. STEADY ANYWHERE."),
    };

    public static Body Of(VehicleKind k) => Bodies[k];

    public static string Name(EngineKind e) => e switch { EngineKind.Four => "4 CYL", EngineKind.Six => "V6", EngineKind.Eight => "V8", _ => "BLOWN V8" };
    public static string Name(TyreKind t) => t switch { TyreKind.Street => "STREET", TyreKind.Slicks => "SLICKS", TyreKind.Knobby => "KNOBBY", _ => "STUDDED" };
    public static string Name(WeightKind w) => w switch { WeightKind.Light => "LIGHT", WeightKind.Medium => "MEDIUM", _ => "HEAVY" };
    public static string Name(ArmourKind a) => a switch { ArmourKind.None => "NONE", ArmourKind.Light => "LIGHT", _ => "HEAVY" };
    public static string Name(WeaponKind w) => w switch { WeaponKind.None => "NONE", WeaponKind.Oil => "OIL", _ => "MINES" };

    /// <summary>An engine's power (a V6 is 1) and its weight.</summary>
    public static (double Power, double Mass) Of(EngineKind e) => e switch
    {
        EngineKind.Four => (0.82, 0), EngineKind.Six => (1.0, 0.1), EngineKind.Eight => (1.18, 0.2), _ => (1.38, 0.3),
    };

    /// <summary>How well a set of tyres grips each surface.</summary>
    public static double Grip(TyreKind t, Surface s) => (t, s) switch
    {
        (TyreKind.Street, Surface.Asphalt) => 1.0, (TyreKind.Street, Surface.Dirt) => 0.78, (TyreKind.Street, _) => 0.42,
        (TyreKind.Slicks, Surface.Asphalt) => 1.22, (TyreKind.Slicks, Surface.Dirt) => 0.6, (TyreKind.Slicks, _) => 0.3,
        (TyreKind.Knobby, Surface.Asphalt) => 0.86, (TyreKind.Knobby, Surface.Dirt) => 1.08, (TyreKind.Knobby, _) => 0.5,
        (_, Surface.Asphalt) => 0.9, (_, Surface.Dirt) => 0.86, _ => 0.86,
    };

    /// <summary>The ballast: how heavy, and how much tougher.</summary>
    public static (double Mass, double Toughness) Of(WeightKind w) => w switch
    {
        WeightKind.Light => (0.84, 0.8), WeightKind.Medium => (1.0, 1.0), _ => (1.22, 1.25),
    };

    /// <summary>The armour: how much of a knock it keeps out, and its weight.</summary>
    public static (double Protection, double Mass) Of(ArmourKind a) => a switch
    {
        ArmourKind.None => (0, 0), ArmourKind.Light => (0.35, 0.15), _ => (0.6, 0.35),
    };

    /// <summary>How many of a weapon a vehicle carries.</summary>
    public static int Ammo(WeaponKind w) => w switch { WeaponKind.Oil => 6, WeaponKind.Mines => 4, _ => 0 };

    /// <summary>
    /// What a build can do on a surface. Weight is the body's times the ballast, plus the engine's and the armour's.
    /// More power is more top speed (as its square root) and more acceleration; more weight is less acceleration
    /// and more toughness. The tyres and the body decide the grip.
    /// </summary>
    public static Stats StatsOf(Build b, Surface surface)
    {
        var body = Of(b.Vehicle);
        var (power, engineMass) = Of(b.Engine);
        var (ballast, tough) = Of(b.Weight);
        var (protection, armourMass) = Of(b.Armour);
        double mass = body.Mass * ballast + engineMass + armourMass;
        double reference = body.Mass + 0.1;
        double top = body.Top * Math.Sqrt(power) * (surface == Surface.Dirt ? 0.95 : 1);
        double accel = body.Accel * power * Math.Pow(reference / mass, 0.8);
        double grip = body.Grip * Grip(b.Tyres, surface) * (surface == Surface.Dirt ? body.DirtBonus : 1) * (b.Weight == WeightKind.Heavy ? 1.04 : b.Weight == WeightKind.Light ? 0.97 : 1);
        double toughness = body.Toughness * tough * Math.Sqrt(mass / reference);
        return new Stats(top, accel, grip, mass, toughness, protection, body.LowGravity);
    }

    /// <summary>The builds the computer drives: a sensible choice for the track and the race.</summary>
    public static Build ForComputer(Track t, bool destruction, int pick)
    {
        VehicleKind[] choices = t.Planet is Planet.Moon or Planet.Mars ? [VehicleKind.MoonBuggy, VehicleKind.LunarRover, VehicleKind.BajaBug]
            : t.Surface == Surface.Dirt ? [VehicleKind.BajaBug, VehicleKind.Pickup, VehicleKind.StockCar]
            : destruction ? [VehicleKind.Pickup, VehicleKind.StockCar, VehicleKind.DumpTruck]
            : [VehicleKind.IndyCar, VehicleKind.StockCar, VehicleKind.Dragster];
        return new Build
        {
            Vehicle = choices[((pick % choices.Length) + choices.Length) % choices.Length],
            Engine = EngineKind.Eight,
            Tyres = t.Surface switch { Surface.Ice => TyreKind.Studded, Surface.Dirt => TyreKind.Knobby, _ => TyreKind.Street },
            Armour = destruction && pick % 3 == 2 ? ArmourKind.Light : ArmourKind.None,
            Weapon = destruction ? (pick % 2 == 0 ? WeaponKind.Mines : WeaponKind.Oil) : WeaponKind.None,
        };
    }
}

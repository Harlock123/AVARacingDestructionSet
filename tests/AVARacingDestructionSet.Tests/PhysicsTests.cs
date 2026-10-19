using AVARacingDestructionSet.Engine;

namespace AVARacingDestructionSet.Tests;

/// <summary>Driving, grip on each surface, gravity on each planet, and flying off ramps and hills.</summary>
public class PhysicsTests
{
    private static readonly Controls Gas = new(0, true), Brake = new(0, false, true);

    private static Car Car(double top = 300, double accel = 240) => new() { Heading = Math.PI / 2, Stats = new Stats(top, accel, 1, 1, 1, 0, false) };

    [Fact]
    public void The_gas_takes_a_car_up_to_its_top_speed_and_off_it_the_car_rolls_to_a_stop()
    {
        var c = Car();
        for (int i = 0; i < 60 * 6; i++) Sim.Drive(c, Gas, Field.Step, 900, 240, 300, false);
        Assert.InRange(c.Speed, 290, 300.001);
        for (int i = 0; i < 60 * 5; i++) Sim.Drive(c, default, Field.Step, 900, 240, 300, false);
        Assert.Equal(0, c.Speed, 3);
    }

    [Fact]
    public void The_brake_stops_it_far_quicker_and_the_rough_drags_it_down()
    {
        var a = Car();
        var b = Car();
        var rough = Car();
        foreach (var c in new[] { a, b, rough }) c.Vel = new Vec(280, 0);
        for (int i = 0; i < 30; i++)
        {
            Sim.Drive(a, Brake, Field.Step, 900, 240, 300, false);
            Sim.Drive(b, default, Field.Step, 900, 240, 300, false);
            Sim.Drive(rough, Gas, Field.Step, 900, 240, 300, true);
        }
        Assert.True(a.Speed < 0.5 * b.Speed);
        Assert.True(rough.Speed < 260);
    }

    [Fact]
    public void Steering_turns_it_more_once_it_is_moving()
    {
        var still = Car();
        var going = Car();
        going.Vel = new Vec(200, 0);
        for (int i = 0; i < 20; i++)
        {
            Sim.Drive(still, new Controls(1), Field.Step, 900, 240, 300, false);
            Sim.Drive(going, new Controls(1, true), Field.Step, 900, 240, 300, false);
        }
        Assert.True(going.Heading - Math.PI / 2 > (still.Heading - Math.PI / 2) * 3);
        // Right is clockwise: heading east and steering right, it turns towards the south (down the screen).
        Assert.True(going.Vel.Y > 0);
    }

    [Fact]
    public void Grip_holds_a_car_in_a_bend_until_it_runs_out_and_then_it_slides()
    {
        // Round and round a circle of radius 64 at a steady speed: as much sideways pull as v²/r.
        double Slide(double speed, double lateral)
        {
            var c = Car(speed, 2000);
            c.Vel = new Vec(speed, 0);
            double most = 0;
            for (int i = 0; i < 60 * 2; i++)
            {
                c.Heading += speed / 64 * Field.Step;
                Sim.Drive(c, new Controls(0, true), Field.Step, lateral, 2000, speed, false);
                most = Math.Max(most, c.Skid);
            }
            return most;
        }
        Assert.Equal(0, Slide(200, Sim.GripScale), 6);
        Assert.True(Slide(280, Sim.GripScale) > 20);
        Assert.Equal(0, Slide(280, Sim.GripScale * Sim.BankedGrip), 6);
    }

    [Fact]
    public void Each_surface_grips_less_than_asphalt_and_ice_least_unless_the_tyres_are_made_for_it()
    {
        var b = new Build();
        double On(Surface s, TyreKind t) => Vehicles.StatsOf(new Build { Tyres = t }, s).Grip;
        Assert.True(On(Surface.Asphalt, TyreKind.Street) > On(Surface.Dirt, TyreKind.Street));
        Assert.True(On(Surface.Dirt, TyreKind.Street) > On(Surface.Ice, TyreKind.Street) * 1.5);
        Assert.True(On(Surface.Asphalt, TyreKind.Slicks) > On(Surface.Asphalt, TyreKind.Street));
        Assert.True(On(Surface.Dirt, TyreKind.Knobby) > On(Surface.Dirt, TyreKind.Street));
        Assert.True(On(Surface.Ice, TyreKind.Studded) > On(Surface.Ice, TyreKind.Street) * 1.8);
        Assert.True(On(Surface.Ice, TyreKind.Slicks) < On(Surface.Ice, TyreKind.Street));
        // Dirt is slower in a straight line too.
        Assert.True(Vehicles.StatsOf(b, Surface.Dirt).Top < Vehicles.StatsOf(b, Surface.Asphalt).Top);
    }

    [Fact]
    public void Low_gravity_means_less_grip_and_less_go_and_high_gravity_more_grip_and_less_go()
    {
        double moon = Track.Gravity(Planet.Moon), mars = Track.Gravity(Planet.Mars), jupiter = Track.Gravity(Planet.Jupiter);
        Assert.True(Sim.GravityGrip(moon, false) < Sim.GravityGrip(mars, false));
        Assert.True(Sim.GravityGrip(mars, false) < Sim.GravityGrip(1, false));
        Assert.True(Sim.GravityGrip(jupiter, false) > 1.5);
        Assert.Equal(1, Sim.GravityGrip(1, false));
        Assert.True(Sim.GravityAccel(moon, false) < 1 && Sim.GravityAccel(jupiter, false) < 1);
        Assert.Equal(1, Sim.GravityAccel(1, false));
        // Vehicles built for low gravity grip better there.
        Assert.True(Sim.GravityGrip(moon, true) > Sim.GravityGrip(moon, false) * 1.4);
        // And it shows on the track: the same car in the same bend is pulled round harder on Jupiter than on the Moon.
        double Pull(Planet p)
        {
            var w = Lab.Race(Lab.Strip(planet: p));
            return Sim.Lateral(w, w.Cars[0], 0);
        }
        Assert.True(Pull(Planet.Moon) < Pull(Planet.Mars) && Pull(Planet.Mars) < Pull(Planet.Earth) && Pull(Planet.Earth) < Pull(Planet.Jupiter));
    }

    [Fact]
    public void Off_the_road_the_grip_halves_and_too_far_off_the_tow_truck_brings_the_car_back()
    {
        var w = Lab.Race(Lab.Strip());
        var c = w.Cars[0];
        Assert.Equal(Sim.Lateral(w, c, 0, true) * Sim.RoughGrip, Sim.Lateral(w, c, 0, false), 6);
        Lab.Park(w, 1, w.Course.Length * 0.5);
        Lab.Put(w, 0, 300, 0, Math.PI / 2);
        c.Vel = c.Forward * 250;
        int towed = -1;
        for (int t = 0; t < 120 && towed < 0; t++)
        {
            Lab.Step(w, 1, new Controls(0, true));
            if (c.Towed > 0) towed = t;
        }
        Assert.True(towed > 0);
        Assert.Contains(Sfx.Tow, w.Heard);
        Lab.Step(w, (int)(Sim.TowSeconds * 60) + 2);
        Assert.Equal(0, c.Towed);
        Assert.True(Math.Abs(w.Course.Off(c.Pos, c.Index)) < 1);
        Assert.Equal(w.Course.Dir(c.Index).Angle, c.Heading, 6);
        Assert.Equal(new Vec(0, 0), c.Vel);
    }

    /// <summary>A car at a steady speed straight at a ramp: how long it's in the air and how far it flies.</summary>
    private static (double Air, double Flight, double Landing) Jump(Planet planet, double speed)
    {
        var w = Lab.Race(Lab.Strip(PieceKind.Ramp, planet: planet));
        Lab.Park(w, 1, w.Course.Length * 0.6);
        var c = w.Cars[0];
        var ramp = w.Course.Humps.Single();
        Lab.Put(w, 0, w.Course.Along[ramp.Start] - 60);
        c.Vel = c.Forward * speed;
        double air = 0, took = 0, landing = 0;
        bool was = false;
        for (int t = 0; t < 60 * 8; t++)
        {
            // Hold the speed (the gas, and a top speed just there), and keep it straight.
            c.Stats = c.Stats with { Top = speed };
            Lab.Step(w, 1, new Controls(0, true));
            if (c.Airborne && !was) took = c.Progress;
            if (!c.Airborne && was) { landing = c.Progress; break; }
            if (c.Airborne) { air += Field.Step; landing = Math.Max(landing, -c.VZ); }
            was = c.Airborne;
        }
        return (air, c.Progress - took, landing);
    }

    [Fact]
    public void A_ramp_throws_a_car_into_the_air_and_the_lower_the_gravity_the_longer_it_flies()
    {
        var earth = Jump(Planet.Earth, 240);
        var moon = Jump(Planet.Moon, 240);
        var mars = Jump(Planet.Mars, 240);
        var jupiter = Jump(Planet.Jupiter, 240);
        Assert.InRange(earth.Air, 0.25, 0.7);
        Assert.True(moon.Air > mars.Air && mars.Air > earth.Air && earth.Air > jupiter.Air);
        // Flight time goes about as one over the gravity (the lip's height counts too, so a little less than that).
        Assert.InRange(moon.Air / earth.Air, 3.5, 6.5);
        Assert.True(moon.Flight > 3 * earth.Flight);
        // Faster, further.
        Assert.True(Jump(Planet.Earth, 300).Flight > earth.Flight);
    }

    [Fact]
    public void The_course_works_out_where_a_jump_comes_down_as_the_race_does()
    {
        var w = Lab.Race(Lab.Strip(PieceKind.Ramp, planet: Planet.Mars));
        var ramp = w.Course.Humps.Single();
        var (_, flight, _) = Jump(Planet.Mars, 220);
        double predicted = w.Course.FlightLength(ramp, 220, w.Gravity);
        // From the ramp's start, against from where it took off: the lip is most of a square on.
        Assert.InRange(predicted - Pieces.Cell * Pieces.RampLip, flight - 25, flight + 25);
        // The fastest it can leave the ramp and land on the straight beyond.
        double safe = w.Course.JumpSpeeds(w.Gravity)[0];
        Assert.True(w.Course.FlightLength(ramp, safe, w.Gravity) <= ramp.Runout);
        Assert.True(w.Course.FlightLength(ramp, safe + 30, w.Gravity) > ramp.Runout - 24);
    }

    [Fact]
    public void Over_a_hill_fast_a_car_leaves_the_ground_slowly_it_doesnt_and_on_the_moon_it_always_does()
    {
        double Air(Planet p, double speed)
        {
            var w = Lab.Race(Lab.Strip(PieceKind.Hill, planet: p));
            Lab.Park(w, 1, w.Course.Length * 0.6);
            var c = w.Cars[0];
            Lab.Put(w, 0, w.Course.Along[w.Course.Humps.Single().Start] - 40);
            c.Vel = c.Forward * speed;
            double air = 0;
            for (int t = 0; t < 120; t++)
            {
                c.Stats = c.Stats with { Top = speed };
                Lab.Step(w, 1, new Controls(0, true));
                if (c.Airborne) air += Field.Step;
            }
            return air;
        }
        Assert.Equal(0, Air(Planet.Earth, 90));
        Assert.True(Air(Planet.Earth, 290) > 0.15);
        Assert.True(Air(Planet.Moon, 90) > 0.2);
        Assert.Equal(0, Air(Planet.Jupiter, 160));
    }

    [Fact]
    public void A_hard_landing_hurts_in_a_destruction_race_but_not_in_a_plain_one()
    {
        foreach (var mode in new[] { Mode.Racing, Mode.Destruction })
        {
            var w = Lab.Race(Lab.Strip(planet: Planet.Jupiter), rules: new RaceRules(mode));
            var c = w.Cars[0];
            c.Airborne = true;
            c.Z = 60;
            c.VZ = -350;
            Lab.Step(w, 12);
            Assert.False(c.Airborne);
            Assert.Contains(Sfx.Land, w.Heard);
            if (mode == Mode.Destruction) Assert.True(c.Damage > 10); else Assert.Equal(0, c.Damage);
        }
    }

    [Fact]
    public void A_car_driven_off_a_bridge_falls_to_the_ground()
    {
        var t = Tracks.Make("KNOTTED RIBBON");
        var w = Lab.Race(t);
        int bridge = t.Pieces.FindIndex(p => p.Kind == PieceKind.Bridge);
        int i = Enumerable.Range(0, w.Course.Points.Length).First(k => w.Course.Piece[k] == bridge && w.Course.Z[k] > 25);
        var c = w.Cars[0];
        Lab.Put(w, 0, w.Course.Along[i], 0, Math.PI / 2);
        Lab.Park(w, 1, w.Course.Along[i] + 900);
        c.Vel = c.Forward * 120;
        bool flew = false;
        for (int k = 0; k < 60; k++)
        {
            Lab.Step(w, 1, new Controls(0, true));
            flew |= c.Airborne;
        }
        Assert.True(flew);
        Assert.False(c.Airborne);
        Assert.Equal(0, c.Z, 3);
    }
}

using AVARacingDestructionSet.Engine;

namespace AVARacingDestructionSet.Tests;

/// <summary>The garage: what the body, engine, tyres, weight and armour do to a vehicle.</summary>
public class VehicleTests
{
    private static Stats Of(Build b, Surface s = Surface.Asphalt) => Vehicles.StatsOf(b, s);

    [Fact]
    public void Nine_vehicles_each_its_own_and_each_with_a_name_and_a_shape()
    {
        Assert.Equal(9, Vehicles.All.Length);
        var stats = Vehicles.All.Select(v => Of(new Build { Vehicle = v })).ToList();
        Assert.Equal(9, stats.Distinct().Count());
        foreach (var v in Vehicles.All)
        {
            var body = Vehicles.Of(v);
            Assert.NotEmpty(body.Name);
            Assert.NotEmpty(body.About);
            Assert.True(body.Length > body.Width);
        }
        // The dragster is fastest, the dump truck toughest, the motorcycle frailest, the Indy car grippiest.
        Assert.Equal(VehicleKind.Dragster, Vehicles.All.MaxBy(v => Of(new Build { Vehicle = v }).Top));
        Assert.Equal(VehicleKind.DumpTruck, Vehicles.All.MaxBy(v => Of(new Build { Vehicle = v }).Toughness));
        Assert.Equal(VehicleKind.Motorcycle, Vehicles.All.MinBy(v => Of(new Build { Vehicle = v }).Toughness));
        Assert.Equal(VehicleKind.IndyCar, Vehicles.All.MaxBy(v => Of(new Build { Vehicle = v }).Grip));
        Assert.True(Of(new Build { Vehicle = VehicleKind.MoonBuggy }).LowGravity);
        Assert.False(Of(new Build()).LowGravity);
    }

    [Fact]
    public void A_bigger_engine_is_more_top_speed_and_acceleration_and_a_little_more_weight()
    {
        Stats? was = null;
        foreach (var e in Enum.GetValues<EngineKind>())
        {
            var s = Of(new Build { Engine = e });
            if (was is { } w)
            {
                Assert.True(s.Top > w.Top);
                Assert.True(s.Accel > w.Accel);
                Assert.True(s.Mass > w.Mass);
            }
            was = s;
        }
    }

    [Fact]
    public void Ballast_trades_acceleration_for_toughness()
    {
        var light = Of(new Build { Weight = WeightKind.Light });
        var heavy = Of(new Build { Weight = WeightKind.Heavy });
        Assert.True(light.Accel > heavy.Accel);
        Assert.True(light.Toughness < heavy.Toughness);
        Assert.True(light.Mass < heavy.Mass);
        Assert.Equal(light.Top, heavy.Top);
    }

    [Fact]
    public void Armour_keeps_out_part_of_every_knock_but_weighs_the_car_down()
    {
        var none = Of(new Build());
        var light = Of(new Build { Armour = ArmourKind.Light });
        var heavy = Of(new Build { Armour = ArmourKind.Heavy });
        Assert.True(none.Hurt(40) > light.Hurt(40) && light.Hurt(40) > heavy.Hurt(40));
        Assert.Equal(40 / none.Toughness, none.Hurt(40), 9);
        Assert.True(heavy.Accel < none.Accel);
        Assert.True(heavy.Mass > light.Mass && light.Mass > none.Mass);
    }

    [Fact]
    public void Weapons_come_in_their_own_numbers()
    {
        Assert.Equal(0, Vehicles.Ammo(WeaponKind.None));
        Assert.True(Vehicles.Ammo(WeaponKind.Oil) > Vehicles.Ammo(WeaponKind.Mines));
        Assert.True(Vehicles.Ammo(WeaponKind.Mines) > 0);
    }

    [Fact]
    public void The_build_shows_in_the_race_a_bigger_engine_pulls_away_down_a_straight()
    {
        var w = Lab.Race(Lab.Strip(), new Build { Engine = EngineKind.Blown }, new Build { Engine = EngineKind.Four });
        for (int t = 0; t < 60 * 2; t++) Sim.Step(w, [Pilot.Drive(w, w.Cars[0], -13), Pilot.Drive(w, w.Cars[1], 13)]);
        Assert.True(w.Cars[0].Progress > w.Cars[1].Progress + 30);
    }

    [Fact]
    public void A_build_copies_and_the_computers_choose_for_the_track()
    {
        var b = new Build { Vehicle = VehicleKind.Dragster, Tyres = TyreKind.Slicks };
        var c = b.Copy();
        c.Vehicle = VehicleKind.Pickup;
        Assert.Equal(VehicleKind.Dragster, b.Vehicle);
        Assert.Equal(TyreKind.Studded, Vehicles.ForComputer(Tracks.Make("FROSTBITE RING"), false, 0).Tyres);
        Assert.True(Of(Vehicles.ForComputer(Tracks.Make("CRATER RIM"), false, 0)).LowGravity);
        Assert.Equal(WeaponKind.None, Vehicles.ForComputer(Tracks.Make("OXBOW OVAL"), false, 0).Weapon);
        Assert.NotEqual(WeaponKind.None, Vehicles.ForComputer(Tracks.Make("OXBOW OVAL"), true, 0).Weapon);
    }
}

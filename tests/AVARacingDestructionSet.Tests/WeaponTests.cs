using AVARacingDestructionSet.Engine;

namespace AVARacingDestructionSet.Tests;

/// <summary>Destruction: oil, mines, ramming, damage, armour, wrecks and what comes of them.</summary>
public class WeaponTests
{
    private static readonly RaceRules Destruction = new(Mode.Destruction, 3);
    private static readonly Controls Fire = new(0, false, false, true);

    private static World War(WeaponKind a = WeaponKind.Oil, WeaponKind b = WeaponKind.Mines, RaceRules? rules = null, ArmourKind armour = ArmourKind.None) =>
        Lab.Race(Lab.Strip(), new Build { Weapon = a }, new Build { Weapon = b, Armour = armour }, rules ?? Destruction);

    [Fact]
    public void The_weapon_button_drops_one_oil_slick_behind_the_car_each_press_until_they_run_out()
    {
        var w = War();
        var c = w.Cars[0];
        Lab.Park(w, 1, 900);
        Lab.Put(w, 0, 300);
        int ammo = c.Ammo;
        Assert.Equal(Vehicles.Ammo(WeaponKind.Oil), ammo);
        Lab.Step(w, 1, a: Fire);
        Assert.Single(w.Drops);
        var d = w.Drops[0];
        Assert.Equal(DropKind.Oil, d.Kind);
        // Behind it.
        Assert.True((d.Pos - c.Pos).Dot(c.Forward) < -c.Body.Length / 2);
        Assert.Contains(Sfx.Oil, w.Heard);
        // Holding it down drops no more; let go and press again, once the cooldown's over.
        Lab.Step(w, 10, a: Fire);
        Assert.Single(w.Drops);
        for (int k = 0; k < 20; k++)
        {
            Lab.Step(w, 1);
            Lab.Step(w, (int)(Sim.Cooldown * 60) + 1, a: Fire);
        }
        Assert.Equal(0, c.Ammo);
        Assert.Equal(ammo, w.Drops.Count);
        w.Heard.Clear();
        Lab.Step(w, 1);
        Lab.Step(w, 1, a: Fire);
        Assert.Equal(ammo, w.Drops.Count);
        Assert.Contains(Sfx.Empty, w.Heard);
    }

    [Fact]
    public void A_plain_race_has_no_weapons()
    {
        var w = War(rules: new RaceRules(Mode.Racing));
        Assert.All(w.Cars, c => Assert.Equal(0, c.Ammo));
        Lab.Step(w, 1, Fire, Fire);
        Assert.Empty(w.Drops);
    }

    [Fact]
    public void Oil_takes_the_grip_away_and_spins_the_car_that_drives_into_it()
    {
        var w = War();
        var c = w.Cars[1];
        Lab.Park(w, 0, 1200);
        Lab.Put(w, 1, 300);
        w.Drops.Add(new Drop { Kind = DropKind.Oil, Pos = w.Course.At(360), Owner = 0 });
        c.Vel = c.Forward * 200;
        double heading = c.Heading;
        for (int t = 0; t < 30 && c.Slick <= 0; t++) Lab.Step(w, 1, b: new Controls(0, true));
        Assert.True(c.Slick > 0);
        Assert.True(Sim.Lateral(w, c, c.Index) < Sim.GripScale * 0.2);
        Lab.Step(w, 10, b: new Controls(0, true));
        Assert.True(Math.Abs(Field.AngleDiff(heading, c.Heading)) > 0.5);
        Assert.Equal(0, c.Damage);
    }

    [Fact]
    public void A_mine_blows_up_under_the_other_car_throwing_it_in_the_air_and_damaging_it()
    {
        var w = War(WeaponKind.Mines);
        var c = w.Cars[1];
        Lab.Park(w, 0, 1200);
        Lab.Put(w, 1, 300);
        w.Drops.Add(new Drop { Kind = DropKind.Mine, Pos = w.Course.At(340), Owner = 0 });
        c.Vel = c.Forward * 150;
        for (int t = 0; t < 30 && w.Drops.Count > 0; t++) Lab.Step(w, 1, b: new Controls(0, true));
        Assert.Empty(w.Drops);
        Assert.True(c.Airborne);
        Assert.Contains(Sfx.Explosion, w.Heard);
        Assert.Contains(w.Effects, e => e.Kind == EffectKind.Boom);
        Assert.Equal(c.Stats.Hurt(Sim.MineDamage), c.Damage, 6);
        Assert.Equal("MINE!", w.Messages[1]);
    }

    [Fact]
    public void A_mine_waits_a_moment_before_it_can_catch_the_car_that_laid_it()
    {
        var w = War(WeaponKind.Mines);
        var c = w.Cars[0];
        Lab.Park(w, 1, 1200);
        Lab.Put(w, 0, 300);
        // Laid, and backed straight over: nothing, while it's arming.
        Lab.Step(w, 1, Fire);
        var mine = w.Drops.Single();
        c.Pos = mine.Pos;
        Lab.Step(w, 2);
        Assert.Single(w.Drops);
        Assert.Equal(0, c.Damage);
        Lab.Step(w, (int)(Sim.MineArm * 60) + 2);
        Assert.Empty(w.Drops);
        Assert.True(c.Damage > 0);
    }

    [Fact]
    public void Armour_and_toughness_take_the_sting_out_of_a_mine()
    {
        double Damage(Build b)
        {
            var w = Lab.Race(Lab.Strip(), new Build(), b, Destruction);
            var c = w.Cars[1];
            Lab.Park(w, 0, 1200);
            Lab.Put(w, 1, 300);
            w.Drops.Add(new Drop { Kind = DropKind.Mine, Pos = c.Pos, Owner = 0 });
            Lab.Step(w, 2);
            return c.Damage;
        }
        double plain = Damage(new Build()), armoured = Damage(new Build { Armour = ArmourKind.Heavy }), truck = Damage(new Build { Vehicle = VehicleKind.DumpTruck });
        Assert.True(plain > 0);
        Assert.True(armoured < plain * 0.5);
        Assert.True(truck < plain * 0.5);
        Assert.Equal(100, Damage(new Build { Vehicle = VehicleKind.Motorcycle }));
    }

    [Fact]
    public void Ramming_hurts_in_a_destruction_race_the_lighter_car_more_and_not_at_all_in_a_plain_one()
    {
        foreach (var mode in new[] { Mode.Racing, Mode.Destruction })
        {
            var w = Lab.Race(Lab.Strip(), new Build { Vehicle = VehicleKind.DumpTruck }, new Build { Vehicle = VehicleKind.StockCar }, new RaceRules(mode));
            var (truck, car) = (w.Cars[0], w.Cars[1]);
            Lab.Put(w, 1, 400);
            Lab.Put(w, 0, 340);
            truck.Vel = truck.Forward * 260;
            Lab.Step(w, 20);
            Assert.True(car.Speed > 60, "pushed along");
            if (mode == Mode.Racing)
            {
                Assert.Equal(0, car.Damage);
                Assert.Equal(0, truck.Damage);
            }
            else
            {
                Assert.True(car.Damage > 5);
                Assert.True(car.Damage > truck.Damage * 3);
            }
            Assert.Contains(w.Heard, s => s is Sfx.Crash or Sfx.Bump);
        }
    }

    [Fact]
    public void Cars_at_different_heights_pass_through_each_other_one_over_a_bridge_and_one_under()
    {
        var t = Tracks.Make("KNOTTED RIBBON");
        var w = Lab.Race(t, rules: Destruction);
        int bridge = t.Pieces.FindIndex(p => p.Kind == PieceKind.Bridge);
        var p = t.Pieces[bridge];
        var middle = new Vec((p.Col + 0.5) * Pieces.Cell, (p.Row + 0.5) * Pieces.Cell);
        int over = Enumerable.Range(0, w.Course.Points.Length).Single(i => w.Course.Piece[i] == bridge && w.Course.Z[i] > 25 && (w.Course.Points[i] - middle).Length < 3);
        int under = Enumerable.Range(0, w.Course.Points.Length).Single(i => w.Course.Piece[i] == bridge && w.Course.Z[i] == 0 && (w.Course.Points[i] - middle).Length < 3);
        Lab.Put(w, 0, w.Course.Along[over]);
        Lab.Put(w, 1, w.Course.Along[under]);
        Assert.True((w.Cars[0].Pos - w.Cars[1].Pos).Length < 1);
        Lab.Step(w, 1);
        Assert.True((w.Cars[0].Pos - w.Cars[1].Pos).Length < 5);
        Assert.Equal(0, w.Cars[0].Damage + w.Cars[1].Damage);
    }

    [Fact]
    public void A_wrecked_car_burns_then_comes_back_on_the_road_mended_and_passing_through_things_for_a_moment()
    {
        var w = War();
        var c = w.Cars[1];
        Lab.Park(w, 0, 1200);
        Lab.Put(w, 1, 300);
        c.Damage = 95;
        Sim.Hurt(w, c, 30);
        Assert.Equal(1, c.Wrecks);
        Assert.True(c.Wrecked > 0);
        Assert.Contains(Sfx.Explosion, w.Heard);
        Assert.Contains(w.Effects, e => e.Kind == EffectKind.Debris);
        Assert.Equal("WRECKED!", w.Messages[1]);
        var at = c.Pos;
        Lab.Step(w, (int)(Sim.WreckSeconds * 60) - 5, b: new Controls(0, true));
        Assert.Equal(at, c.Pos);
        Lab.Step(w, 10);
        Assert.Equal(0, c.Wrecked);
        Assert.Equal(0, c.Damage);
        Assert.True(c.Ghost > 0);
        Assert.True(Math.Abs(w.Course.Off(c.Pos, c.Index)) < 1);
        // Just back, nothing hurts it.
        Sim.Hurt(w, c, 50);
        Assert.Equal(0, c.Damage);
    }

    [Fact]
    public void When_wrecked_cars_are_out_a_wreck_ends_the_race_and_the_other_car_wins()
    {
        var w = War(rules: new RaceRules(Mode.Destruction, 3, WreckRule.Out));
        Sim.Hurt(w, w.Cars[0], 1000);
        Assert.True(w.Cars[0].Out);
        Assert.True(w.Over);
        Assert.Equal(1, w.Winner);
        Assert.Equal(w.Cars[1], Sim.Standings(w)[0]);
    }
}

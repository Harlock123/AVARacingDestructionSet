using AVARacingDestructionSet.Engine;

namespace AVARacingDestructionSet.Tests;

/// <summary>The computer driver: it finishes every track in the box, and does far better than no one.</summary>
public class PilotTests
{
    public static IEnumerable<TheoryDataRow<string>> TrackNames() => Tracks.Names.Select(n => new TheoryDataRow<string>(n));

    [Theory]
    [MemberData(nameof(TrackNames))]
    public void The_computer_drives_both_cars_home_on_every_track_in_the_box_without_a_tow(string name)
    {
        var track = Tracks.Make(name);
        var w = Lab.Race(track, Vehicles.ForComputer(track, false, 0), Vehicles.ForComputer(track, false, 1), new RaceRules(Mode.Racing, 2));
        var pilots = new[] { new Pilot(), new Pilot() };
        bool towed = false;
        for (int t = 0; t < 60 * 120 && !w.Over; t++)
        {
            Sim.Step(w, [pilots[0].Decide(w, 0), pilots[1].Decide(w, 1)]);
            towed |= w.Cars.Any(c => c.Towed > 0);
        }
        Assert.True(w.Over, $"{name}: the race never ended");
        Assert.All(w.Cars, c => Assert.True(c.Finished, $"{name}: car {c.Id} didn't finish"));
        Assert.False(towed, $"{name}: towed");
        // Quicker than the made-up best laps a new track starts with.
        Assert.True(w.Cars.Min(c => c.BestLap) < GameEngine.Par(w.Course) * 1.12, name);
    }

    [Theory]
    [InlineData("OXBOW OVAL")]
    [InlineData("CRATER RIM")]
    [InlineData("SPAGHETTI JUNCTION")]
    public void In_a_destruction_race_the_computers_still_get_home(string name)
    {
        var track = Tracks.Make(name);
        var w = Lab.Race(track, Vehicles.ForComputer(track, true, 0), Vehicles.ForComputer(track, true, 1), new RaceRules(Mode.Destruction, 2));
        var pilots = new[] { new Pilot(), new Pilot() };
        for (int t = 0; t < 60 * 150 && !w.Over; t++) Sim.Step(w, [pilots[0].Decide(w, 0), pilots[1].Decide(w, 1)]);
        Assert.True(w.Over);
        Assert.Contains(w.Cars, c => c.Finished);
    }

    [Fact]
    public void Holding_the_gas_down_and_hardly_steering_never_finishes_a_lap()
    {
        var track = Tracks.Make("FIGURE EIGHT FAIR");
        var w = Lab.Race(track, new Build(), Vehicles.ForComputer(track, false, 1), new RaceRules(Mode.Racing, 2));
        var pilot = new Pilot();
        for (int t = 0; t < 60 * 60 && !w.Over; t++) Sim.Step(w, [new Controls(t % 200 < 25 ? 1 : 0, true), pilot.Decide(w, 1)]);
        Assert.Empty(w.Cars[0].LapTimes);
        Assert.True(w.Cars[1].Finished);
    }

    [Fact]
    public void It_slows_for_a_jump_on_the_moon_so_as_to_land_on_the_road()
    {
        var earth = Lab.Race(Lab.Strip(PieceKind.Ramp));
        var moon = Lab.Race(Lab.Strip(PieceKind.Ramp, planet: Planet.Moon));
        double Before(World w) => Pilot.SafeSpeed(w, w.Cars[0], w.Course.Along[w.Course.Humps[0].Start] - 10, 200);
        Assert.True(Before(moon) < Before(earth) * 0.8);
        Assert.True(moon.Course.JumpSpeeds(moon.Gravity)[0] < earth.Course.JumpSpeeds(earth.Gravity)[0]);
    }

    [Fact]
    public void It_brakes_for_bends_more_on_ice_than_on_asphalt()
    {
        var asphalt = Lab.Race(Lab.Strip());
        var ice = Lab.Race(Lab.Strip(surface: Surface.Ice));
        // Just before the first bend.
        double at = 5.5 * Pieces.Cell;
        Assert.True(Pilot.SafeSpeed(ice, ice.Cars[0], at, 250) < Pilot.SafeSpeed(asphalt, asphalt.Cars[0], at, 250) * 0.8);
    }

    [Fact]
    public void In_a_destruction_race_it_drops_its_weapon_on_a_car_close_behind()
    {
        var w = Lab.Race(Lab.Strip(), new Build { Weapon = WeaponKind.Mines }, new Build(), new RaceRules(Mode.Destruction));
        Lab.Put(w, 0, 600);
        Lab.Put(w, 1, 520);
        var c = new Pilot().Decide(w, 0);
        Assert.True(c.Fire);
        Lab.Put(w, 1, 1500);
        Assert.False(new Pilot().Decide(w, 0).Fire);
    }
}

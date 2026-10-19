using AVARacingDestructionSet.Engine;

namespace AVARacingDestructionSet.Tests;

/// <summary>The race: the lights, the grid, laps and lap times, the finish, the standings, two players, and a world copied.</summary>
public class RaceTests
{
    [Fact]
    public void The_cars_wait_side_by_side_behind_the_line_for_the_lights()
    {
        var w = Lab.Race("OXBOW OVAL", go: false);
        Assert.Equal(RacePhase.Countdown, w.Phase);
        var (a, b) = (w.Cars[0], w.Cars[1]);
        Assert.Equal(-Sim.GridBack, a.Progress);
        Assert.Equal(2 * Sim.GridSide, (a.Pos - b.Pos).Length, 3);
        var at = a.Pos;
        var heard = new List<Sfx>();
        for (int t = 0; t < (int)(Sim.CountdownSeconds * 60) - 2; t++)
        {
            Sim.Step(w, [new Controls(0, true), new Controls(0, true)]);
            heard.AddRange(w.Heard);
            w.Heard.Clear();
        }
        Assert.Equal(RacePhase.Countdown, w.Phase);
        Assert.Equal(at, a.Pos);
        Assert.Equal(3, heard.Count(s => s == Sfx.Beep));
        Lab.Step(w, 4, new Controls(0, true));
        Assert.Equal(RacePhase.Racing, w.Phase);
        Assert.Contains(Sfx.Go, w.Heard);
        Lab.Step(w, 20, new Controls(0, true));
        Assert.NotEqual(at, a.Pos);
    }

    /// <summary>Carries a car round the course by hand, a stretch at a time, until a distance.</summary>
    private static void Carry(World w, Car c, double to)
    {
        for (double s = c.Progress; s < to && !c.Finished; s += 30)
        {
            int i = w.Course.IndexAt(s);
            c.Pos = w.Course.Points[i];
            Sim.Step(w, [default, default]);
        }
    }

    [Fact]
    public void Laps_count_as_the_car_goes_round_with_a_time_for_each_and_the_last_brings_it_home_first()
    {
        var w = Lab.Race("OXBOW OVAL", rules: new RaceRules(Mode.Racing, 3));
        var c = w.Cars[0];
        Assert.Equal(1, Sim.LapOf(w, c));
        Carry(w, c, w.Course.Length + 40);
        Assert.Equal(2, Sim.LapOf(w, c));
        Assert.Single(c.LapTimes);
        Assert.Equal(w.Clock, c.LapTimes[0], 1);
        Assert.Contains(Sfx.Lap, w.Heard);
        Carry(w, c, 2 * w.Course.Length + 40);
        Assert.Equal("FINAL LAP!", w.Messages[0]);
        Carry(w, c, 3 * w.Course.Length + 40);
        Assert.True(c.Finished);
        Assert.Equal(1, c.Place);
        Assert.Equal(3, c.LapTimes.Count);
        Assert.Equal(c.FinishTime, c.LapTimes.Sum(), 6);
        Assert.Equal(c.LapTimes.Min(), c.BestLap);
        Assert.Equal(0, w.Winner);
        Assert.Contains(Sfx.Flag, w.Heard);
        // The other car has a while left to finish, then the race is over.
        Assert.True(w.Late > 0);
        Assert.False(w.Over);
        Lab.Step(w, (int)(Sim.LateSeconds * 60) + 2);
        Assert.True(w.Over);
        Assert.False(w.Cars[1].Finished);
        Assert.Equal([c, w.Cars[1]], Sim.Standings(w));
    }

    [Fact]
    public void A_short_cut_across_the_rough_doesnt_count_as_progress()
    {
        var w = Lab.Race("OXBOW OVAL");
        var c = w.Cars[0];
        Carry(w, c, 300);
        double was = c.Progress;
        // Suddenly half a lap on.
        Lab.Put(w, 0, w.Course.Along[c.Index] + w.Course.Length * 0.4);
        c.Progress = was;
        Lab.Step(w, 2);
        Assert.InRange(c.Progress, was - 10, was + 10);
    }

    [Fact]
    public void Each_player_drives_their_own_car_with_their_own_controls()
    {
        var w = Lab.Race("PARCO VELOCE");
        var (a, b) = (w.Cars[0], w.Cars[1]);
        Lab.Step(w, 60, new Controls(0, true), default);
        Assert.True(a.Speed > 100);
        Assert.Equal(0, b.Speed, 3);
        double heading = b.Heading;
        Lab.Step(w, 30, default, new Controls(1, true));
        Assert.True(Field.AngleDiff(heading, b.Heading) > 0.3);
        Assert.True(a.Human && b.Human);
    }

    [Fact]
    public void A_copied_world_goes_on_exactly_as_the_original_and_changing_one_leaves_the_other_alone()
    {
        var w = Lab.Race("SPAGHETTI JUNCTION", new Build { Weapon = WeaponKind.Mines }, new Build { Weapon = WeaponKind.Oil }, new RaceRules(Mode.Destruction, 3));
        var pilots = new[] { new Pilot(), new Pilot() };
        for (int t = 0; t < 60 * 6; t++) Sim.Step(w, [pilots[0].Decide(w, 0), pilots[1].Decide(w, 1)]);
        var copy = w.Copy();
        Assert.NotSame(w.Cars[0], copy.Cars[0]);
        Assert.NotSame(w.Cars[0].LapTimes, copy.Cars[0].LapTimes);
        Assert.NotSame(w.Cars[0].Build, copy.Cars[0].Build);
        Assert.Same(w.Course, copy.Course);
        var pa = new[] { new Pilot(), new Pilot() };
        var pb = new[] { new Pilot(), new Pilot() };
        for (int t = 0; t < 60 * 8; t++)
        {
            Sim.Step(w, [pa[0].Decide(w, 0), pa[1].Decide(w, 1)]);
            Sim.Step(copy, [pb[0].Decide(copy, 0), pb[1].Decide(copy, 1)]);
        }
        for (int i = 0; i < 2; i++)
        {
            Assert.Equal(w.Cars[i].Pos, copy.Cars[i].Pos);
            Assert.Equal(w.Cars[i].Progress, copy.Cars[i].Progress);
            Assert.Equal(w.Cars[i].Damage, copy.Cars[i].Damage);
        }
        Assert.Equal(w.Drops.Count, copy.Drops.Count);
        Assert.Equal(w.Clock, copy.Clock);
        copy.Cars[0].Pos = new Vec(1, 1);
        copy.Cars[0].LapTimes.Add(99);
        copy.Drops.Clear();
        Assert.NotEqual(new Vec(1, 1), w.Cars[0].Pos);
        Assert.DoesNotContain(99, w.Cars[0].LapTimes);
    }

    [Fact]
    public void Lap_times_print_as_minutes_seconds_and_hundredths()
    {
        Assert.Equal("0:09.87", LapEntry.Format(9.87));
        Assert.Equal("1:05.20", LapEntry.Format(65.2));
        Assert.Equal("-:--.--", LapEntry.Format(0));
    }
}

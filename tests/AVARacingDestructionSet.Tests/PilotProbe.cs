using AVARacingDestructionSet.Engine;

namespace AVARacingDestructionSet.Tests;

/// <summary>
/// How the computer drivers get on, track by track (set RDS_PROBE to run it, for tuning: "1" for plain races,
/// "destruction" for destruction races, "lazy" for a car that holds the gas down and steers now and then against the computer).
/// </summary>
public sealed class PilotProbe(ITestOutputHelper output)
{
    [Fact]
    public void How_the_computer_drives()
    {
        string? mode = Environment.GetEnvironmentVariable("RDS_PROBE");
        if (mode is null) return;
        bool destruction = mode == "destruction";
        foreach (var name in Tracks.Names)
        {
            var track = Tracks.Make(name);
            var course = Course.Build(track)!;
            var w = new World();
            var pilots = new[] { new Pilot(), new Pilot() };
            Sim.Start(w, track, course, [Vehicles.ForComputer(track, destruction, 0), Vehicles.ForComputer(track, destruction, 1)], [false, false],
                new RaceRules(destruction ? Mode.Destruction : Mode.Racing, 3), 7);
            int tows = 0, air = 0;
            var towed = new bool[2];
            double maxAir = 0;
            int t = 0;
            for (; t < 60 * 300 && !w.Over; t++)
            {
                var c0 = mode == "lazy" ? new Controls(t % 200 < 25 ? 1 : 0, true) : pilots[0].Decide(w, 0);
                Sim.Step(w, [c0, pilots[1].Decide(w, 1)]);
                for (int i = 0; i < 2; i++)
                {
                    if (w.Cars[i].Towed > 0 && !towed[i])
                    {
                        tows++;
                        var c = w.Cars[i];
                        var p = track.Pieces[course.Piece[c.LastRoad]];
                        output.WriteLine($"   tow car {i} at {c.Pos} last road on {p} speed {c.Vel.Length:0}");
                    }
                    towed[i] = w.Cars[i].Towed > 0;
                    if (w.Cars[i].Airborne) { air++; maxAir = Math.Max(maxAir, w.Cars[i].AirTime); }
                }
            }
            string Laps(Car c) => string.Join(" ", c.LapTimes.Select(x => x.ToString("0.0")));
            output.WriteLine($"{name,-20} {track.Planet,-7} {track.Surface,-7} len {course.Length,5:0} par {GameEngine.Par(course),5:0.0}s  after {t / 60.0,5:0.0}s winner {w.Winner}  tows {tows} air {air / 60.0:0.0}s (longest {maxAir:0.0}s) wrecks {w.Cars[0].Wrecks}/{w.Cars[1].Wrecks} damage {w.Cars[0].Damage:0}/{w.Cars[1].Damage:0} ammo {w.Cars[0].Ammo}/{w.Cars[1].Ammo}");
            output.WriteLine($"     {w.Cars[0].Body.Name}: {Laps(w.Cars[0])}   {w.Cars[1].Body.Name}: {Laps(w.Cars[1])}");
        }
    }
}

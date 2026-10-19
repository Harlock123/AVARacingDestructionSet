using AVARacingDestructionSet.Engine;

namespace AVARacingDestructionSet.Tests;

/// <summary>The program: the title and its demonstration, building, the garage, racing, the results and the best laps.</summary>
public class GameTests
{
    [Fact]
    public void Enter_goes_from_the_title_to_the_construction_set_to_the_garage_to_the_race()
    {
        var g = Lab.Engine();
        Assert.Equal(GameState.Title, g.State);
        g.Tick(default, default, start: true);
        Assert.Equal(GameState.Editing, g.State);
        g.Tick(default);
        g.Tick(default, default, start: true);
        Assert.Equal(GameState.Garage, g.State);
        g.Tick(default);
        g.Tick(default, default, start: true);
        Assert.Equal(GameState.Racing, g.State);
        Assert.Equal(g.Editor.Track.Name, g.World.Track.Name);
        Assert.Equal(RacePhase.Countdown, g.World.Phase);
    }

    [Fact]
    public void A_track_that_isnt_a_closed_loop_cant_be_raced_and_the_construction_set_says_why()
    {
        var g = Lab.Engine();
        g.OpenEditor();
        g.Editor.Track.Remove(5, 1);
        g.Editor.Open(g.Editor.Track);
        g.OpenGarage();
        Assert.False(g.StartRace());
        Assert.Equal(GameState.Editing, g.State);
        Assert.StartsWith("THE TRACK ISN'T READY: THE ROAD STOPS", g.Editor.Message);
    }

    [Fact]
    public void The_race_uses_the_builds_from_the_garage_and_the_computer_drives_the_second_car_unless_a_friend_does()
    {
        var g = Lab.Engine();
        g.Builds[0].Vehicle = VehicleKind.IndyCar;
        g.Builds[1].Vehicle = VehicleKind.DumpTruck;
        g.Rules = new RaceRules(Mode.Destruction, 4);
        g.StartRace();
        Assert.Equal((VehicleKind.IndyCar, VehicleKind.DumpTruck), (g.World.Cars[0].Build.Vehicle, g.World.Cars[1].Build.Vehicle));
        Assert.Equal(4, g.World.Rules.Laps);
        Assert.False(g.World.Cars[1].Human);
        g.Run(60 * 6);
        Assert.True(g.World.Cars[1].Progress > 200);
        Assert.True(g.World.Cars[0].Progress < 0);
        g.Friend = true;
        g.StartRace();
        Assert.True(g.World.Cars[1].Human);
        g.Run(60 * 6);
        Assert.True(g.World.Cars[1].Progress < 0);
        // Building changes nothing in a race already run.
        g.Builds[0].Vehicle = VehicleKind.Motorcycle;
        Assert.Equal(VehicleKind.IndyCar, g.World.Cars[0].Build.Vehicle);
    }

    /// <summary>A short race, both cars driven by the computer, to the results.</summary>
    private static GameEngine Raced(string track = "FROSTBITE RING", int laps = 1)
    {
        var g = Lab.Engine();
        g.OpenBuiltIn(track);
        g.Rules = new RaceRules(Mode.Racing, laps);
        Assert.True(g.StartRace());
        for (int t = 0; t < 60 * 90 && g.State == GameState.Racing; t++) g.Tick(g.Pilots[0].Decide(g.World, 0));
        Assert.Equal(GameState.Results, g.State);
        return g;
    }

    [Fact]
    public void A_record_lap_asks_for_initials_and_goes_on_the_tracks_best_laps_which_are_kept()
    {
        var g = Raced();
        var car = g.World.Cars[0];
        Assert.True(car.BestLap > 0);
        g.Tick(default, default, start: true);
        Assert.Equal(GameState.EnterInitials, g.State);
        Assert.Equal(0, g.EntryPlayer);
        foreach (char ch in "VRM") g.Initials!.Type(ch);
        g.Tick(default);
        Assert.Equal(GameState.BestLaps, g.State);
        var entry = g.BestLaps.Entries[g.NewEntryIndex];
        Assert.Equal(("VRM", (int)Math.Round(car.BestLap * 1000), car.Body.Name), (entry.Initials, entry.Millis, entry.Vehicle));
        // Kept for next time, under the track's name.
        var again = BestLapTable.Load(g.Folder, "FROSTBITE RING", 99);
        Assert.Contains(again.Entries, e => e.Initials == "VRM");
        Assert.Equal("VRM", again.LastInitials);
        g.Run((int)(GameEngine.BestLapsSeconds * 60) + 2);
        Assert.Equal(GameState.Garage, g.State);
    }

    [Fact]
    public void Without_a_record_the_results_lead_straight_to_the_best_laps_and_back_to_the_garage()
    {
        var g = Lab.Engine();
        g.OpenBuiltIn("FROSTBITE RING");
        // A table already full of impossible laps.
        var table = BestLapTable.Load(g.Folder, "FROSTBITE RING", 1);
        for (int i = 0; i < BestLapTable.Size; i++) table.Insert("ACE", 1000 + i, "INDY CAR");
        table.Save();
        g.Rules = new RaceRules(Mode.Racing, 1);
        g.StartRace();
        for (int t = 0; t < 60 * 90 && g.State == GameState.Racing; t++) g.Tick(g.Pilots[0].Decide(g.World, 0));
        g.Run((int)(GameEngine.ResultsSeconds * 60) + 2);
        Assert.Equal(GameState.BestLaps, g.State);
        Assert.Equal(-1, g.NewEntryIndex);
        g.Tick(default, default, start: true);
        Assert.Equal(GameState.Garage, g.State);
    }

    [Fact]
    public void A_fresh_tracks_best_laps_are_made_up_a_little_slower_than_par_and_a_lap_must_beat_the_last_to_count()
    {
        var t = BestLapTable.Load(Lab.TempFolder(), "NEW ONE", 10);
        Assert.Equal(BestLapTable.Size, t.Entries.Count);
        Assert.True(t.Entries[0].Millis > 10000);
        Assert.True(t.Entries.Zip(t.Entries.Skip(1)).All(p => p.First.Millis <= p.Second.Millis));
        Assert.False(t.Qualifies(t.Entries[^1].Millis + 1));
        Assert.False(t.Qualifies(0));
        Assert.True(t.Qualifies(t.Entries[^1].Millis - 1));
        Assert.Equal(0, t.Insert("AVA", 5000, "PICKUP"));
        Assert.Equal(BestLapTable.Size, t.Entries.Count);
        Assert.Equal(-1, t.Insert("AVA", 99999, "PICKUP"));
    }

    [Fact]
    public void The_title_runs_a_demonstration_of_two_computer_drivers_then_the_best_laps_and_the_demonstrations_vary()
    {
        var g = Lab.Engine();
        var seen = new List<(string, Mode)>();
        for (int round = 0; round < 2; round++)
        {
            g.Idle((int)(GameEngine.TitleIdleSeconds * 60) + 2);
            Assert.Equal(GameState.Attract, g.State);
            Assert.All(g.World.Cars, c => Assert.False(c.Human));
            seen.Add((g.World.Track.Name, g.World.Rules.Mode));
            g.Idle(60 * 8);
            Assert.True(g.World.Cars.Max(c => c.Progress) > 300, string.Join(", ", g.World.Cars.Select(c => $"{c.Progress:0} {c.Speed:0} {g.World.Phase}")));
            for (int i = 0; i < (int)(GameEngine.AttractSeconds * 60) + 2 && g.State == GameState.Attract; i++) g.Idle(1);
            Assert.Equal(GameState.BestLaps, g.State);
            Assert.Equal(g.World.Track.Name, g.BestLaps.TrackName);
            g.Idle((int)(GameEngine.BestLapsSeconds * 60) + 2);
            Assert.Equal(GameState.Title, g.State);
        }
        Assert.Equal([(Tracks.Names[0], Mode.Racing), (Tracks.Names[1], Mode.Destruction)], seen);
    }

    [Fact]
    public void A_race_pauses_and_resumes_and_can_be_left_for_the_garage()
    {
        var g = Lab.Engine();
        g.StartRace();
        g.Run(60 * 4, new Controls(0, true));
        g.Pause();
        Assert.Equal(GameState.Paused, g.State);
        var at = g.World.Cars[0].Pos;
        g.Run(60, new Controls(0, true));
        Assert.Equal(at, g.World.Cars[0].Pos);
        g.Resume();
        g.Run(10, new Controls(0, true));
        Assert.NotEqual(at, g.World.Cars[0].Pos);
        g.Pause();
        g.QuitRace();
        Assert.Equal(GameState.Garage, g.State);
    }

    [Fact]
    public void The_track_being_built_is_kept_on_disk_and_comes_back_next_time()
    {
        string folder = Lab.TempFolder();
        var g = new GameEngine(folder) { Silent = true };
        g.OpenEditor();
        g.OpenBuiltIn("MUDSLIDE GULCH");
        g.Editor.SetPlanet(Planet.Venus);
        g.Run(120);
        Assert.True(File.Exists(g.WorkingPath));
        var again = new GameEngine(folder) { Silent = true };
        Assert.Equal(("MUDSLIDE GULCH", Planet.Venus), (again.Editor.Track.Name, again.Editor.Track.Planet));
        // A first run starts on the first track in the box.
        Assert.Equal(Tracks.Names[0], Lab.Engine().Editor.Track.Name);
    }

    [Fact]
    public void Garage_choices_click_and_the_engines_rev_with_the_cars()
    {
        var g = new GameEngine(Lab.TempFolder());
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.Choose(() => g.Builds[0].Tyres = TyreKind.Studded);
        Assert.Equal(TyreKind.Studded, g.Builds[0].Tyres);
        Assert.Contains(Sfx.Click, heard);
        Assert.Null(g.EngineNoise());
        g.StartRace();
        var idle = g.EngineNoise()!;
        Assert.Equal(g.Builds[0].Vehicle, idle[0].Vehicle);
        for (int t = 0; t < 60 * 6; t++) g.Tick(g.Pilots[0].Decide(g.World, 0));
        Assert.True(g.EngineNoise()![0].Revs > idle[0].Revs + 0.5);
    }
}

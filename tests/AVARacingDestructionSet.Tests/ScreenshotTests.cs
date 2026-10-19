using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AVARacingDestructionSet.Engine;
using AVARacingDestructionSet.Views;
using Controls = AVARacingDestructionSet.Engine.Controls;

namespace AVARacingDestructionSet.Tests;

/// <summary>
/// Draws the program in a real (headless) window at the moments worth showing, driving it with the mouse where a
/// person would. Set RDS_SHOTS to a folder to save them as PNGs: the README's screenshots come from here.
/// </summary>
public sealed class ScreenshotTests
{
    private static (Window W, GameCanvas Canvas, GameEngine G) Open()
    {
        var g = Lab.Engine();
        var canvas = new GameCanvas(g, new SoundManager(silent: true), startTimer: false);
        var w = new Window { Width = 1024, Height = 768, Content = canvas };
        w.Show();
        Pump();
        return (w, canvas, g);
    }

    private static void Pump()
    {
        for (int i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Draws a frame, so the buttons are where the clicks will look for them.</summary>
    private static void Draw(Window w, GameCanvas c)
    {
        c.InvalidateVisual();
        Pump();
        Assert.NotNull(w.CaptureRenderedFrame());
    }

    private static void Shot(Window w, GameCanvas c, string name)
    {
        c.InvalidateVisual();
        Pump();
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var dir = Environment.GetEnvironmentVariable("RDS_SHOTS");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        frame.Save(Path.Combine(dir, $"AVARacingDestructionSet-{name}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    /// <summary>The construction set open on a track from the box.</summary>
    private static (Window W, GameCanvas Canvas, GameEngine G) Building(string track)
    {
        var (w, c, g) = Open();
        g.Tick(default, default, start: true);
        Assert.Equal(GameState.Editing, g.State);
        g.OpenBuiltIn(track);
        g.Run(200);
        Draw(w, c);
        return (w, c, g);
    }

    /// <summary>A race on a track from the box, both cars driven by the computer, until a moment comes.</summary>
    private static (Window W, GameCanvas Canvas, GameEngine G) Racing(string track, RaceRules rules, Func<World, bool> moment, int maxSeconds = 200, Action<GameEngine>? setUp = null)
    {
        var (w, c, g) = Building(track);
        g.Rules = rules;
        setUp?.Invoke(g);
        c.Click(GameCanvas.NextRect.Center);
        Assert.Equal(GameState.Garage, g.State);
        Draw(w, c);
        c.Click(GameCanvas.RaceRect.Center);
        Assert.Equal(GameState.Racing, g.State);
        bool found = false;
        for (int t = 0; t < 60 * maxSeconds && g.State == GameState.Racing; t++)
        {
            if (moment(g.World)) { found = true; break; }
            g.Tick(g.Pilots[0].Decide(g.World, 0));
            if (t % 3 == 0) c.Advance(0.05);
        }
        Assert.True(found, $"the moment never came: {g.State} {g.World.Phase} clock {g.World.Clock:0.0} drops {g.World.Drops.Count} winner {g.World.Winner} {g.Editor.Message}");
        return (w, c, g);
    }

    [AvaloniaFact]
    public void The_title()
    {
        var (w, c, g) = Open();
        g.Idle(40);
        c.Advance(2.3);
        Assert.Equal(GameState.Title, g.State);
        Shot(w, c, "title");
    }

    [AvaloniaFact]
    public void The_construction_set_with_a_ramp_on_its_way_from_the_box()
    {
        var (w, c, g) = Building("KNOTTED RIBBON");
        var ed = g.Editor;
        // Turn a piece with the right button, then drag a ramp out of the box towards an empty square.
        int ramp = Array.IndexOf(Pieces.Box, PieceKind.Ramp);
        c.Press(GameCanvas.BoxTile(ramp).Center);
        Assert.NotNull(ed.Carried);
        c.PointTo(GameCanvas.CellCentre(7, 1));
        c.Click(GameCanvas.CellCentre(7, 1), right: true);
        c.PointTo(GameCanvas.CellCentre(7, 1) + new Point(4, 2));
        c.Advance(0.4);
        Assert.Equal((7, 1), ed.Hover);
        Shot(w, c, "construction-set");
        c.Lift(GameCanvas.CellCentre(7, 1));
        Assert.Equal(PieceKind.Ramp, ed.Track.At(7, 1)?.Kind);
        Assert.Equal(1, ed.Track.At(7, 1)?.Rot);
    }

    [AvaloniaFact]
    public void The_vehicle_builder()
    {
        var (w, c, g) = Building("MUDSLIDE GULCH");
        c.Click(GameCanvas.NextRect.Center);
        Draw(w, c);
        c.Click(GameCanvas.VehicleTile(Array.IndexOf(Vehicles.All, VehicleKind.BajaBug)).Center);
        c.Click(GameCanvas.OptionRect(1, (int)TyreKind.Knobby, 4).Center);
        c.Click(GameCanvas.OptionRect(3, (int)ArmourKind.Light, 3).Center);
        c.Click(GameCanvas.OptionRect(4, (int)WeaponKind.Mines, 3).Center);
        c.Click(GameCanvas.SetupButton(0, 1).Center);
        Draw(w, c);
        c.Click(GameCanvas.SetupButton(3, 1).Center);
        var b = g.Builds[0];
        Assert.Equal((VehicleKind.BajaBug, TyreKind.Knobby, ArmourKind.Light, WeaponKind.Mines), (b.Vehicle, b.Tyres, b.Armour, b.Weapon));
        Assert.Equal((Mode.Destruction, WreckRule.Out), (g.Rules.Mode, g.Rules.Wrecks));
        c.PointTo(GameCanvas.VehicleTile(Array.IndexOf(Vehicles.All, VehicleKind.DumpTruck)).Center);
        c.Advance(2.2);
        Shot(w, c, "vehicle-builder");
    }

    [AvaloniaFact]
    public void A_split_screen_race()
    {
        // One car over the bridge, the other not far behind.
        var (w, c, g) = Racing("KNOTTED RIBBON", new RaceRules(Mode.Racing, 3), x => x.Clock > 5 && x.Cars.Any(k => k.Z > 22)
            && Math.Abs(x.Cars[0].Progress - x.Cars[1].Progress) is > 50 and < 240);
        Shot(w, c, "split-screen");
    }

    [AvaloniaFact]
    public void A_jump_on_the_moon()
    {
        var (w, c, g) = Racing("CRATER RIM", new RaceRules(Mode.Racing, 3), x => x.Cars.Any(k => k.Airborne && k.AirTime > 0.7 && k.Z > 42 && k.VZ < 10), setUp: e =>
        {
            e.Builds[0].Vehicle = VehicleKind.MoonBuggy;
            e.Builds[1].Vehicle = VehicleKind.LunarRover;
        });
        Shot(w, c, "moon-jump");
    }

    [AvaloniaFact]
    public void Destruction()
    {
        var (w, c, g) = Racing("FIGURE EIGHT FAIR", new RaceRules(Mode.Destruction, 5), x => x.Clock > 1.6 && x.Cars[0].Speed > 60 && x.Cars[1].Speed > 60
            && Math.Abs(x.Cars[0].Progress - x.Cars[1].Progress) is > 30 and < 300, 240, e =>
        {
            e.Builds[0] = new Build { Vehicle = VehicleKind.StockCar, Engine = EngineKind.Eight, Weapon = WeaponKind.Oil };
            e.Builds[1] = new Build { Vehicle = VehicleKind.Pickup, Engine = EngineKind.Eight, Weapon = WeaponKind.Mines };
        });
        var x = g.World;
        // The car in front drops what it has; the one behind gets a mine it didn't see.
        var (front, back) = x.Cars[0].Progress > x.Cars[1].Progress ? (x.Cars[0], x.Cars[1]) : (x.Cars[1], x.Cars[0]);
        for (int k = 0; k < 2; k++)
        {
            int i = x.Course.IndexAt(x.Course.Along[back.Index] + 50 + k * 34);
            double off = k == 0 ? x.Course.Off(back.Pos, back.Index) : -10;
            x.Drops.Add(new Drop { Kind = k == 0 ? DropKind.Mine : DropKind.Oil, Pos = x.Course.Points[i] + x.Course.Right(i) * off, Owner = front.Id, Age = 3 });
        }
        int j = x.Course.IndexAt(x.Course.Along[front.Index] - 30);
        x.Drops.Add(new Drop { Kind = DropKind.Oil, Pos = x.Course.Points[j] + x.Course.Right(j) * 12, Owner = front.Id, Age = 1 });
        for (int t = 0; t < 120 && !x.Effects.Any(e => e.Kind == EffectKind.Boom && e.Age > 0.12); t++)
        {
            g.Tick(new Controls(0, true));
            if (t % 3 == 0) c.Advance(0.05);
        }
        Assert.Contains(x.Effects, e => e.Kind == EffectKind.Boom);
        Assert.True(back.Damage > 0);
        Shot(w, c, "destruction");
    }

    [AvaloniaFact]
    public void The_best_laps()
    {
        var (w, c, g) = Racing("PARCO VELOCE", new RaceRules(Mode.Racing, 2), x => x.Over, 120);
        for (int t = 0; t < 60 * 10 && g.State == GameState.Racing; t++) g.Tick(default);
        Assert.Equal(GameState.Results, g.State);
        c.Advance(0.6);
        Draw(w, c);
        g.Tick(default, default, start: true);
        Assert.Equal(GameState.EnterInitials, g.State);
        foreach (char ch in "AVA") g.Initials!.Type(ch);
        g.Tick(default);
        Assert.Equal(GameState.BestLaps, g.State);
        Assert.Equal(0, g.NewEntryIndex);
        c.Advance(0.1);
        Shot(w, c, "best-laps");
    }
}

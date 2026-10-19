using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using AVARacingDestructionSet.Engine;
using AVARacingDestructionSet.Views;
using Controls = AVARacingDestructionSet.Engine.Controls;

namespace AVARacingDestructionSet.Tests;

/// <summary>The real window, driven by the real mouse and keys (headless): what a person's clicks and key presses do.</summary>
public sealed class UiTests
{
    private static (Window W, GameCanvas Canvas, GameEngine G) Open()
    {
        var g = Lab.Engine();
        var canvas = new GameCanvas(g, new SoundManager(silent: true), startTimer: false);
        var w = new Window { Width = 1024, Height = 768, Content = canvas };
        w.Show();
        canvas.Focus();
        Draw(w, canvas);
        return (w, canvas, g);
    }

    private static void Draw(Window w, GameCanvas c)
    {
        c.InvalidateVisual();
        for (int i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs();
        Assert.NotNull(w.CaptureRenderedFrame());
    }

    private static void Key(Window w, Key k, RawInputModifiers mods = RawInputModifiers.None)
    {
        w.KeyPress(k, mods, PhysicalKey.None, null);
        w.KeyRelease(k, mods, PhysicalKey.None, null);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(Window w, Point at, MouseButton button = MouseButton.Left)
    {
        w.MouseMove(at);
        w.MouseDown(at, button);
        w.MouseUp(at, button);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Enter held through a step of the program (the engine sees it as start).</summary>
    private static void Enter(Window w, GameCanvas c, GameEngine g)
    {
        w.KeyPress(Avalonia.Input.Key.Enter, RawInputModifiers.None, PhysicalKey.None, null);
        Dispatcher.UIThread.RunJobs();
        g.Tick(c.Input.Player(0), c.Input.Player(1), c.Input.Start);
        w.KeyRelease(Avalonia.Input.Key.Enter, RawInputModifiers.None, PhysicalKey.None, null);
        Dispatcher.UIThread.RunJobs();
        g.Tick(c.Input.Player(0), c.Input.Player(1), c.Input.Start);
    }

    /// <summary>The construction set, open on a blank grid.</summary>
    private static (Window W, GameCanvas Canvas, GameEngine G) Building()
    {
        var (w, c, g) = Open();
        Enter(w, c, g);
        Assert.Equal(GameState.Editing, g.State);
        g.OpenBlank();
        Draw(w, c);
        return (w, c, g);
    }

    [AvaloniaFact]
    public void A_piece_dragged_from_the_box_with_the_mouse_lands_on_the_grid_and_dragged_back_is_gone()
    {
        var (w, c, g) = Building();
        var tile = GameCanvas.BoxTile(Array.IndexOf(Pieces.Box, PieceKind.Bridge)).Center;
        var drop = GameCanvas.CellCentre(3, 4);
        w.MouseMove(tile);
        w.MouseDown(tile, MouseButton.Left);
        w.MouseMove(new Point(500, 300));
        Draw(w, c);
        w.MouseMove(drop);
        Draw(w, c);
        w.MouseUp(drop, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(PieceKind.Bridge, g.Editor.Track.At(3, 4)?.Kind);
        // Picked up with the hand and dropped on the box: gone.
        w.MouseMove(drop);
        w.MouseDown(drop, MouseButton.Left);
        w.MouseMove(GameCanvas.BoxRect.Center);
        w.MouseUp(GameCanvas.BoxRect.Center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(g.Editor.Track.Pieces);
    }

    [AvaloniaFact]
    public void A_piece_clicked_in_the_box_is_clicked_down_square_after_square_and_right_click_turns_it()
    {
        var (w, c, g) = Building();
        Click(w, GameCanvas.BoxTile(Array.IndexOf(Pieces.Box, PieceKind.Straight)).Center);
        Assert.NotNull(g.Editor.Carried);
        Click(w, GameCanvas.CellCentre(2, 2), MouseButton.Right);
        for (int col = 2; col <= 6; col++) Click(w, GameCanvas.CellCentre(col, 2));
        Assert.Equal(5, g.Editor.Track.Pieces.Count);
        Assert.All(g.Editor.Track.Pieces, p => Assert.Equal(1, p.Rot));
        Key(w, Avalonia.Input.Key.Escape);
        Assert.Null(g.Editor.Carried);
        // Right-click a piece on the grid to turn it.
        Click(w, GameCanvas.CellCentre(4, 2), MouseButton.Right);
        Assert.Equal(0, g.Editor.Track.At(4, 2)!.Rot);
        Draw(w, c);
    }

    [AvaloniaFact]
    public void The_tool_keys_and_buttons_turn_and_erase_and_ctrl_z_undoes()
    {
        var (w, c, g) = Building();
        g.OpenBuiltIn("OXBOW OVAL");
        Draw(w, c);
        Key(w, Avalonia.Input.Key.T);
        Assert.Equal(Tool.Turn, g.Editor.Tool);
        Click(w, GameCanvas.CellCentre(5, 1));
        Assert.Equal(0, g.Editor.Track.At(5, 1)!.Rot);
        Key(w, Avalonia.Input.Key.Z, RawInputModifiers.Control);
        Assert.Equal(1, g.Editor.Track.At(5, 1)!.Rot);
        Draw(w, c);
        Click(w, GameCanvas.ToolRect(2).Center);
        Assert.Equal(Tool.Erase, g.Editor.Tool);
        Click(w, GameCanvas.CellCentre(5, 1));
        Assert.Null(g.Editor.Track.At(5, 1));
        Draw(w, c);
        Click(w, GameCanvas.UndoRect.Center);
        Assert.NotNull(g.Editor.Track.At(5, 1));
        Key(w, Avalonia.Input.Key.H);
        Click(w, GameCanvas.CellCentre(3, 1));
        Key(w, Avalonia.Input.Key.R);
        Assert.Equal(0, g.Editor.Track.At(3, 1)!.Rot);
        Key(w, Avalonia.Input.Key.Delete);
        Assert.Null(g.Editor.Track.At(3, 1));
    }

    [AvaloniaFact]
    public void The_terrain_panel_sets_the_surface_and_gravity_and_the_name_is_typed()
    {
        var (w, c, g) = Building();
        Click(w, GameCanvas.SurfaceRect(Surface.Dirt).Center);
        Click(w, GameCanvas.PlanetRect(Planet.Moon).Center);
        Assert.Equal((Surface.Dirt, Planet.Moon), (g.Editor.Track.Surface, g.Editor.Track.Planet));
        Draw(w, c);
        Click(w, GameCanvas.NameRect.Center);
        Assert.NotNull(g.Renaming);
        for (int i = 0; i < 12; i++) Key(w, Avalonia.Input.Key.Back);
        foreach (var k in new[] { Avalonia.Input.Key.L, Avalonia.Input.Key.U, Avalonia.Input.Key.N, Avalonia.Input.Key.A, Avalonia.Input.Key.Space, Avalonia.Input.Key.D9 })
            Key(w, k);
        Draw(w, c);
        Key(w, Avalonia.Input.Key.Enter);
        Assert.Null(g.Renaming);
        Assert.Equal("LUNA 9", g.Editor.Track.Name);
    }

    [AvaloniaFact]
    public void The_tracks_tab_opens_the_tracks_in_the_box_and_your_tracks_saves_and_opens_your_own()
    {
        var (w, c, g) = Building();
        Key(w, Avalonia.Input.Key.Tab);
        Assert.Equal(Tab.Tracks, g.Editor.Tab);
        Draw(w, c);
        Click(w, GameCanvas.BuiltInRect(Array.IndexOf(Tracks.Names, "JUPITER GRIND")).Center);
        Assert.Equal("JUPITER GRIND", g.Editor.Track.Name);
        Click(w, GameCanvas.TabRect(2).Center);
        Assert.Equal(Tab.Yours, g.Editor.Tab);
        Draw(w, c);
        Click(w, new Point(GameCanvas.EditorPanel.X + 112, GameCanvas.EditorPanel.Y + 29));
        Assert.Single(g.SavedTracks());
        g.OpenBlank();
        c.Advance(3);
        Draw(w, c);
        Click(w, new Point(GameCanvas.EditorPanel.X + 132, GameCanvas.EditorPanel.Y + 73));
        Assert.Equal("JUPITER GRIND", g.Editor.Track.Name);
    }

    [AvaloniaFact]
    public void The_garage_builds_both_vehicles_and_sets_up_the_race_with_the_mouse_and_the_keys()
    {
        var (w, c, g) = Building();
        g.OpenBuiltIn("OXBOW OVAL");
        Click(w, GameCanvas.NextRect.Center);
        Assert.Equal(GameState.Garage, g.State);
        Draw(w, c);
        Click(w, GameCanvas.VehicleTile(Array.IndexOf(Vehicles.All, VehicleKind.Dragster)).Center);
        Click(w, GameCanvas.OptionRect(0, (int)EngineKind.Blown, 4).Center);
        Click(w, GameCanvas.OptionRect(2, (int)WeightKind.Light, 3).Center);
        Assert.Equal((VehicleKind.Dragster, EngineKind.Blown, WeightKind.Light), (g.Builds[0].Vehicle, g.Builds[0].Engine, g.Builds[0].Weight));
        Key(w, Avalonia.Input.Key.Right);
        Assert.Equal(VehicleKind.IndyCar, g.Builds[0].Vehicle);
        Click(w, GameCanvas.PlayerTab(1).Center);
        Assert.Equal(1, c.GaragePlayer);
        Draw(w, c);
        Click(w, GameCanvas.VehicleTile(Array.IndexOf(Vehicles.All, VehicleKind.LunarRover)).Center);
        Assert.Equal(VehicleKind.LunarRover, g.Builds[1].Vehicle);
        Assert.Equal(VehicleKind.IndyCar, g.Builds[0].Vehicle);
        Click(w, GameCanvas.SetupButton(1, 3, 4).Center);
        Click(w, GameCanvas.SetupButton(1, 3, 4).Center);
        Click(w, GameCanvas.SetupButton(2, 1).Center);
        Click(w, GameCanvas.SetupButton(0, 1).Center);
        Draw(w, c);
        Assert.Equal((5, true, Mode.Destruction), (g.Rules.Laps, g.Friend, g.Rules.Mode));
        Key(w, Avalonia.Input.Key.Escape);
        Assert.Equal(GameState.Editing, g.State);
    }

    [AvaloniaFact]
    public void Two_players_race_with_their_own_keys_and_pause_and_leave_with_p_and_escape()
    {
        var (w, c, g) = Building();
        g.OpenBuiltIn("PARCO VELOCE");
        g.Friend = true;
        g.OpenGarage();
        Draw(w, c);
        Click(w, GameCanvas.RaceRect.Center);
        Assert.Equal(GameState.Racing, g.State);
        g.Run((int)(Sim.CountdownSeconds * 60) + 2);
        w.KeyPress(Avalonia.Input.Key.Up, RawInputModifiers.None, PhysicalKey.ArrowUp, null);
        w.KeyPress(Avalonia.Input.Key.W, RawInputModifiers.None, PhysicalKey.W, null);
        Dispatcher.UIThread.RunJobs();
        for (int t = 0; t < 40; t++) g.Tick(c.Input.Player(0), c.Input.Player(1));
        Assert.True(g.World.Cars[0].Speed > 50 && g.World.Cars[1].Speed > 50);
        w.KeyRelease(Avalonia.Input.Key.W, RawInputModifiers.None, PhysicalKey.W, null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(new Controls(0, true), c.Input.Player(0));
        Assert.Equal(default, c.Input.Player(1));
        Draw(w, c);
        Key(w, Avalonia.Input.Key.P);
        Assert.Equal(GameState.Paused, g.State);
        Draw(w, c);
        Key(w, Avalonia.Input.Key.Space);
        Assert.Equal(GameState.Racing, g.State);
        Key(w, Avalonia.Input.Key.Escape);
        Key(w, Avalonia.Input.Key.Escape);
        Assert.Equal(GameState.Garage, g.State);
        Key(w, Avalonia.Input.Key.M);
        Assert.True(c.Sound.IsMuted);
        Draw(w, c);
    }

    [AvaloniaFact]
    public void Every_screen_draws_the_title_the_demonstration_the_results_the_initials_and_the_best_laps()
    {
        var (w, c, g) = Open();
        Draw(w, c);
        g.Idle((int)(GameEngine.TitleIdleSeconds * 60) + 2);
        Assert.Equal(GameState.Attract, g.State);
        g.Idle(60 * 5);
        Draw(w, c);
        // A click on the demonstration goes to the construction set.
        Click(w, new Point(500, 300));
        Assert.Equal(GameState.Editing, g.State);
        g.OpenBuiltIn("FROSTBITE RING");
        g.Rules = new RaceRules(Mode.Destruction, 1);
        g.StartRace();
        for (int t = 0; t < 60 * 90 && g.State == GameState.Racing; t++) g.Tick(g.Pilots[0].Decide(g.World, 0));
        Assert.Equal(GameState.Results, g.State);
        Draw(w, c);
        Enter(w, c, g);
        Assert.Equal(GameState.EnterInitials, g.State);
        Draw(w, c);
        foreach (var k in new[] { Avalonia.Input.Key.A, Avalonia.Input.Key.B }) Key(w, k);
        Key(w, Avalonia.Input.Key.Enter);
        g.Tick(default);
        Assert.Equal(GameState.BestLaps, g.State);
        Assert.StartsWith("AB", g.BestLaps.Entries[g.NewEntryIndex].Initials);
        Draw(w, c);
        Enter(w, c, g);
        Assert.Equal(GameState.Garage, g.State);
    }
}

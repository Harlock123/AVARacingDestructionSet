using Avalonia.Input;
using AVARacingDestructionSet.Engine;

namespace AVARacingDestructionSet.Tests;

/// <summary>The keys, each player's own, and that they mean what the engine takes them to mean.</summary>
public sealed class InputTests
{
    [Fact]
    public void Player_one_drives_with_the_arrows_and_drops_with_right_ctrl_or_enter()
    {
        using var input = new InputHandler();
        input.OnKeyDown(Key.Left);
        input.OnKeyDown(Key.Up);
        Assert.Equal(new Controls(-1, true, false, false), input.Player(0));
        Assert.Equal(default, input.Player(1));
        input.OnKeyUp(Key.Left);
        input.OnKeyUp(Key.Up);
        input.OnKeyDown(Key.Right);
        input.OnKeyDown(Key.Down);
        input.OnKeyDown(Key.RightCtrl);
        Assert.Equal(new Controls(1, false, true, true), input.Player(0));
        input.OnKeyUp(Key.RightCtrl);
        input.OnKeyDown(Key.Enter);
        Assert.True(input.Player(0).Fire);
        Assert.True(input.Start);
    }

    [Fact]
    public void Player_two_drives_with_w_a_s_d_and_drops_with_left_shift_or_space()
    {
        using var input = new InputHandler();
        input.OnKeyDown(Key.A);
        input.OnKeyDown(Key.W);
        input.OnKeyDown(Key.Space);
        Assert.Equal(new Controls(-1, true, false, true), input.Player(1));
        Assert.Equal(default, input.Player(0));
        input.OnKeyUp(Key.A);
        input.OnKeyUp(Key.W);
        input.OnKeyUp(Key.Space);
        input.OnKeyDown(Key.D);
        input.OnKeyDown(Key.S);
        Assert.Equal(new Controls(1, false, true, false), input.Player(1));
        input.Clear();
        Assert.Equal(default, input.Player(1));
    }

    [Fact]
    public void Up_through_the_keys_drives_the_car_forward_the_way_it_points_and_down_brakes_it()
    {
        using var input = new InputHandler();
        var w = Lab.Race("PARCO VELOCE");
        var c = w.Cars[0];
        var start = c.Pos;
        input.OnKeyDown(Key.Up);
        for (int t = 0; t < 60; t++) Sim.Step(w, [input.Player(0), input.Player(1)]);
        Assert.True(c.Speed > 100);
        Assert.True((c.Pos - start).Dot(c.Forward) > 50);
        input.OnKeyUp(Key.Up);
        input.OnKeyDown(Key.Down);
        for (int t = 0; t < 30; t++) Sim.Step(w, [input.Player(0), input.Player(1)]);
        Assert.True(c.Speed < 20);
        // The second player's W is its gas the same way.
        input.Clear();
        input.OnKeyDown(Key.W);
        var b = w.Cars[1];
        for (int t = 0; t < 30; t++) Sim.Step(w, [input.Player(0), input.Player(1)]);
        Assert.True(b.Speed > 50);
    }

    [Fact]
    public void Right_turns_the_car_clockwise_on_the_screen()
    {
        using var input = new InputHandler();
        var w = Lab.Race("PARCO VELOCE");
        var c = w.Cars[0];
        input.OnKeyDown(Key.Up);
        for (int t = 0; t < 40; t++) Sim.Step(w, [input.Player(0), default]);
        double heading = c.Heading;
        input.OnKeyDown(Key.Right);
        for (int t = 0; t < 10; t++) Sim.Step(w, [input.Player(0), default]);
        Assert.True(Field.AngleDiff(heading, c.Heading) > 0.1);
    }
}

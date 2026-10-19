using AVARacingDestructionSet.Engine;

namespace AVARacingDestructionSet.Tests;

/// <summary>The construction set: pieces from the box, moved, turned, thrown away and stamped down; the terrain; undo.</summary>
public class EditorTests
{
    private static Editor Blank() => new(new Track());

    [Fact]
    public void A_piece_dragged_from_the_box_lands_where_the_mouse_lets_go_and_let_go_off_the_grid_is_nothing()
    {
        var ed = Blank();
        ed.TakeFromBox(PieceKind.Banked);
        Assert.True(ed.CarriedIsNew);
        ed.Drag(4, 5);
        Assert.Equal((4, 5), ed.Hover);
        ed.Release(4, 5);
        var p = ed.Track.At(4, 5);
        Assert.Equal(PieceKind.Banked, p?.Kind);
        Assert.Null(ed.Carried);
        Assert.Equal((4, 5), ed.Selected);
        Assert.Contains(Sfx.Place, ed.Heard);
        ed.TakeFromBox(PieceKind.Hill);
        ed.Release(-1, -1, overBox: true);
        Assert.Single(ed.Track.Pieces);
    }

    [Fact]
    public void A_piece_in_the_hand_turns_with_the_right_button_and_goes_down_turned()
    {
        var ed = Blank();
        ed.TakeFromBox(PieceKind.Ramp);
        ed.RightClick(-1, -1);
        ed.RightClick(-1, -1);
        Assert.Equal(2, ed.Carried!.Rot);
        ed.Release(2, 2);
        Assert.Equal(2, ed.Track.At(2, 2)!.Rot);
        // A crossover is the same any way round.
        ed.TakeFromBox(PieceKind.Cross);
        ed.TurnCarried();
        Assert.Equal(0, ed.Carried!.Rot);
        Assert.Contains(Sfx.Refused, ed.Heard);
    }

    [Fact]
    public void Clicked_from_the_box_a_piece_stays_in_the_hand_to_be_stamped_down_square_after_square()
    {
        var ed = Blank();
        ed.TakeFromBox(PieceKind.Straight);
        ed.TurnCarried();
        for (int c = 1; c <= 5; c++) ed.Press(c, 3);
        Assert.Equal(5, ed.Track.Pieces.Count);
        Assert.All(ed.Track.Pieces, p => Assert.Equal((PieceKind.Straight, 1, 3), (p.Kind, p.Rot, p.Row)));
        Assert.NotNull(ed.Carried);
        ed.Cancel();
        Assert.Null(ed.Carried);
        // Each one can be undone.
        ed.Undo();
        Assert.Equal(4, ed.Track.Pieces.Count);
    }

    [Fact]
    public void The_hand_picks_a_piece_up_and_moves_it_puts_it_back_where_it_was_or_throws_it_in_the_box()
    {
        var ed = new Editor(Tracks.Make("OXBOW OVAL"));
        int n = ed.Track.Pieces.Count;
        var p = ed.Track.At(8, 1)!;
        ed.Press(8, 1);
        Assert.Same(p, ed.Carried);
        Assert.False(ed.CarriedIsNew);
        Assert.Null(ed.Track.At(8, 1));
        // While it's in the hand, the loop is broken there.
        Assert.False(ed.Check.Ok);
        ed.Release(8, 1);
        Assert.Same(p, ed.Track.At(8, 1));
        Assert.True(ed.Check.Ok);
        Assert.False(ed.CanUndo);
        ed.Press(8, 1);
        ed.Release(9, 7);
        Assert.Equal(PieceKind.Banked, ed.Track.At(9, 7)?.Kind);
        Assert.Contains("I2", ed.Check.Why);
        ed.Undo();
        Assert.Equal(PieceKind.Banked, ed.Track.At(8, 1)?.Kind);
        Assert.Null(ed.Track.At(9, 7));
        ed.Press(8, 1);
        ed.Release(-1, -1, overBox: true);
        Assert.Equal(n - 1, ed.Track.Pieces.Count);
        Assert.Contains(Sfx.Delete, ed.Heard);
    }

    [Fact]
    public void The_turn_tool_turns_and_the_eraser_takes_away()
    {
        var ed = new Editor(Tracks.Make("OXBOW OVAL"));
        ed.SetTool(Tool.Turn);
        ed.Press(5, 1);
        Assert.Equal(0, ed.Track.At(5, 1)!.Rot);
        Assert.False(ed.Check.Ok);
        ed.Press(5, 1);
        Assert.True(ed.Check.Ok);
        ed.SetTool(Tool.Erase);
        ed.Press(5, 1);
        Assert.Null(ed.Track.At(5, 1));
        Assert.Equal("THE ROAD STOPS: F2 IS EMPTY", ed.Check.Why);
        ed.Undo();
        Assert.True(ed.Check.Ok);
        ed.Press(0, 0);
        Assert.Contains(Sfx.Refused, ed.Heard);
    }

    [Fact]
    public void Keys_turn_and_delete_the_piece_chosen_and_clear_empties_the_grid_for_undo_to_bring_back()
    {
        var ed = new Editor(Tracks.Make("OXBOW OVAL"));
        ed.Press(3, 1);
        ed.Release(3, 1);
        Assert.Equal((3, 1), ed.Selected);
        ed.RotateSelected();
        Assert.Equal(0, ed.Track.At(3, 1)!.Rot);
        ed.DeleteSelected();
        Assert.Null(ed.Track.At(3, 1));
        int n = ed.Track.Pieces.Count;
        ed.Clear();
        Assert.Empty(ed.Track.Pieces);
        ed.Undo();
        Assert.Equal(n, ed.Track.Pieces.Count);
    }

    [Fact]
    public void Surface_planet_and_name_are_set_and_undone()
    {
        var ed = new Editor(Tracks.Make("OXBOW OVAL"));
        ed.SetSurface(Surface.Ice);
        ed.SetPlanet(Planet.Moon);
        ed.Rename("lunar loop");
        Assert.Equal((Surface.Ice, Planet.Moon, "LUNAR LOOP"), (ed.Track.Surface, ed.Track.Planet, ed.Track.Name));
        ed.Undo();
        ed.Undo();
        ed.Undo();
        Assert.Equal((Surface.Asphalt, Planet.Earth, "OXBOW OVAL"), (ed.Track.Surface, ed.Track.Planet, ed.Track.Name));
        Assert.False(ed.CanUndo);
        Assert.False(ed.Undo());
    }

    [Fact]
    public void Undo_goes_back_sixty_steps_and_no_further()
    {
        var ed = Blank();
        for (int i = 0; i < 70; i++) ed.Place(PieceKind.Straight, i % Track.Cols, i / Track.Cols % Track.Rows);
        int undone = 0;
        while (ed.Undo()) undone++;
        Assert.Equal(Editor.UndoDepth, undone);
    }

    [Fact]
    public void A_track_built_square_by_square_from_the_box_becomes_a_closed_loop_ready_to_race()
    {
        var ed = Blank();
        void Lay(PieceKind k, int c, int r, int rot)
        {
            ed.TakeFromBox(k);
            for (int i = 0; i < rot; i++) ed.TurnCarried();
            ed.Release(c, r);
        }
        Lay(PieceKind.Start, 3, 3, 1);
        Assert.Equal("THE ROAD STOPS: E4 IS EMPTY", ed.Check.Why);
        Lay(PieceKind.Straight, 4, 3, 1);
        Lay(PieceKind.Curve, 5, 3, 2);
        Lay(PieceKind.Curve, 5, 4, 3);
        Lay(PieceKind.Straight, 4, 4, 1);
        Lay(PieceKind.Straight, 3, 4, 1);
        Lay(PieceKind.Curve, 2, 4, 0);
        Assert.False(ed.Check.Ok);
        Lay(PieceKind.Curve, 2, 3, 1);
        Assert.True(ed.Check.Ok, ed.Check.Why);
        Assert.NotNull(ed.Course);
        Assert.Equal(8 * Pieces.Cell - 4 * (Pieces.Cell - Pieces.Length(new Road(0, 1, Rise.Flat))), ed.Course!.Length, 0);
    }
}

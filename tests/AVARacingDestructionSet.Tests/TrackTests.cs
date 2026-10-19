using AVARacingDestructionSet.Engine;

namespace AVARacingDestructionSet.Tests;

/// <summary>Tracks: whether they're closed loops, and if not, why not; and the ten in the box.</summary>
public class TrackTests
{
    private static Track Rect() => Tracks.Walk(2, 1, 1, Lab.Rectangle);

    [Fact]
    public void A_rectangle_of_straights_and_curves_with_a_start_line_is_a_closed_loop()
    {
        var t = Rect();
        var check = t.Check();
        Assert.True(check.Ok, check.Why);
        Assert.Contains("CLOSED LOOP", check.Why);
        Assert.Contains("26 SQUARES", check.Why);
        var course = Course.Build(t)!;
        // The loop is as long as its pieces: 22 straights and 4 quarter circles.
        Assert.Equal(22 * Pieces.Cell + 4 * Math.PI / 2 * Pieces.Cell / 2, course.Length, 0);
        // It starts on the start line, halfway across its square, heading the way the line points (east).
        Assert.Equal(new Vec(2.5 * Pieces.Cell, 1.5 * Pieces.Cell), course.Points[0]);
        Assert.Equal(Math.PI / 2, course.Dir(0).Angle, 3);
        Assert.True((course.Points[^1] - course.Points[0]).Length < Course.Spacing * 1.5);
    }

    [Fact]
    public void An_empty_grid_has_nothing_to_race_and_a_track_needs_one_start_line()
    {
        Assert.Contains("EMPTY", new Track().Check().Why);
        var t = Rect();
        var start = t.Pieces.Single(p => p.Kind == PieceKind.Start);
        start.Kind = PieceKind.Straight;
        start.Rot = 1;
        Assert.Equal("THE TRACK NEEDS A START LINE", t.Check().Why);
        var u = Rect();
        u.Put(new Piece(PieceKind.Start, 5, 1, 1));
        var check = u.Check();
        Assert.False(check.Ok);
        Assert.Contains("ONE START LINE ONLY", check.Why);
        Assert.Contains("F2", check.Why);
        Assert.Equal((5, 1), (check.Col, check.Row));
    }

    [Fact]
    public void A_gap_in_the_road_names_the_empty_square()
    {
        var t = Rect();
        t.Remove(8, 4);
        var check = t.Check();
        Assert.False(check.Ok);
        Assert.Equal("THE ROAD STOPS: I5 IS EMPTY", check.Why);
        Assert.Equal((8, 4), (check.Col, check.Row));
    }

    [Fact]
    public void A_piece_turned_the_wrong_way_is_a_bad_join()
    {
        var t = Rect();
        t.At(8, 4)!.Rot = 1;
        var check = t.Check();
        Assert.False(check.Ok);
        Assert.Equal("BAD JOIN AT I5: THE STRAIGHT DOESN'T MEET THE ROAD", check.Why);
        var u = Rect();
        u.At(8, 1)!.Rot = 1;
        Assert.StartsWith("BAD JOIN AT I2: THE CURVE", u.Check().Why);
    }

    [Fact]
    public void A_road_off_the_edge_of_the_grid_is_caught()
    {
        var t = new Track();
        t.Put(new Piece(PieceKind.Start, 0, 3, 3));
        var check = t.Check();
        Assert.False(check.Ok);
        Assert.Equal("THE ROAD RUNS OFF THE EDGE OF THE GRID AT A4", check.Why);
    }

    [Fact]
    public void A_ramp_must_face_the_way_the_race_goes()
    {
        var t = Rect();
        t.Put(new Piece(PieceKind.Ramp, 5, 1, 3));
        var check = t.Check();
        Assert.False(check.Ok);
        Assert.Equal("THE JUMP RAMP AT F2 FACES THE WRONG WAY: TURN IT ROUND", check.Why);
        t.Put(new Piece(PieceKind.Ramp, 5, 1, 1));
        Assert.True(t.Check().Ok);
        // Turn the start line round and the race goes the other way: the ramp now faces the wrong way.
        t.At(2, 1)!.Rot = 3;
        Assert.Contains("FACES THE WRONG WAY", t.Check().Why);
    }

    [Fact]
    public void Every_piece_must_be_part_of_the_loop_and_a_crossing_must_be_crossed_both_ways()
    {
        var t = Rect();
        t.Put(new Piece(PieceKind.Straight, 4, 4, 0));
        var check = t.Check();
        Assert.False(check.Ok);
        Assert.Equal("THE STRAIGHT AT E5 ISN'T PART OF THE LOOP", check.Why);
        var u = Rect();
        u.Put(new Piece(PieceKind.Cross, 5, 1));
        Assert.Equal("THE LOOP USES ONLY ONE ROAD OF THE CROSSOVER AT F2", u.Check().Why);
        var v = Rect();
        v.Put(new Piece(PieceKind.Bridge, 5, 1, 1));
        Assert.Equal("THE LOOP USES ONLY ONE ROAD OF THE BRIDGE AT F2", v.Check().Why);
    }

    [Fact]
    public void Ten_tracks_come_in_the_box_each_a_closed_loop_using_every_kind_of_piece_between_them()
    {
        Assert.Equal(10, Tracks.Names.Length);
        Assert.Equal(10, Tracks.Names.Distinct().Count());
        var kinds = new HashSet<PieceKind>();
        foreach (var name in Tracks.Names)
        {
            var t = Tracks.Make(name);
            var check = t.Check();
            Assert.True(check.Ok, $"{name}: {check.Why}");
            Assert.True(t.Pieces.Count >= 18, name);
            Assert.NotEmpty(Tracks.About(name));
            kinds.UnionWith(t.Pieces.Select(p => p.Kind));
        }
        Assert.Equal(Enum.GetValues<PieceKind>().Length, kinds.Count);
        // Every surface and several planets.
        var all = Tracks.Names.Select(Tracks.Make).ToList();
        Assert.Equal(3, all.Select(t => t.Surface).Distinct().Count());
        Assert.Contains(all, t => t.Planet == Planet.Moon);
        Assert.Contains(all, t => t.Planet == Planet.Jupiter);
    }

    [Fact]
    public void A_figure_eight_goes_through_its_crossing_twice_once_each_way()
    {
        var t = Tracks.Make("FIGURE EIGHT FAIR");
        var course = Course.Build(t)!;
        int cross = t.Pieces.FindIndex(p => p.Kind == PieceKind.Cross);
        var visits = course.Visits.Where(v => v.Piece == cross).ToList();
        Assert.Equal(2, visits.Count);
        Assert.NotEqual(visits[0].Road, visits[1].Road);
    }

    [Fact]
    public void The_walk_that_lays_the_tracks_refuses_to_lay_a_square_twice_or_cross_nothing()
    {
        Assert.Throws<ArgumentException>(() => Tracks.Walk(2, 2, 1, "F L L L L"));
        Assert.Throws<ArgumentException>(() => Tracks.Walk(2, 2, 1, "F X"));
        Assert.Throws<ArgumentException>(() => Tracks.Walk(2, 2, 1, "F Q"));
    }

    [Fact]
    public void Squares_are_named_by_column_letter_and_row_number()
    {
        Assert.Equal("A1", Track.CellName(0, 0));
        Assert.Equal("J8", Track.CellName(9, 7));
        Assert.True(Track.OnGrid(9, 7));
        Assert.False(Track.OnGrid(10, 0));
        Assert.False(Track.OnGrid(0, -1));
    }
}

using AVARacingDestructionSet.Engine;

namespace AVARacingDestructionSet.Tests;

/// <summary>Every piece's roads: where they join, how long they are, how they rise and fall, and a car driven across each.</summary>
public class PieceTests
{
    public static IEnumerable<TheoryDataRow<PieceKind>> Kinds() => Enum.GetValues<PieceKind>().Select(k => new TheoryDataRow<PieceKind>(k));

    [Theory]
    [MemberData(nameof(Kinds))]
    public void Every_road_of_every_piece_turned_every_way_runs_edge_middle_to_edge_middle_level_at_both_ends(PieceKind kind)
    {
        for (int rot = 0; rot < 4; rot++)
        {
            var p = new Piece(kind, 0, 0, rot);
            var roads = Pieces.Roads(p);
            Assert.Equal(kind is PieceKind.Cross or PieceKind.Bridge ? 2 : 1, roads.Length);
            foreach (var r in roads)
            {
                Assert.NotEqual(r.A, r.B);
                Assert.True((Pieces.Along(r, r.A, 0) - Pieces.Mid(r.A)).Length < 1e-9);
                Assert.True((Pieces.Along(r, r.A, 1) - Pieces.Mid(r.B)).Length < 1e-6);
                // From either end, the same road.
                Assert.True((Pieces.Along(r, r.B, 0.25) - Pieces.Along(r, r.A, 0.75)).Length < 1e-6);
                Assert.Equal(0, Pieces.Height(r.Rise, 0), 9);
                Assert.Equal(0, Pieces.Height(r.Rise, 1), 9);
                // A road's length is the distance along it.
                double run = 0;
                for (int k = 1; k <= 200; k++) run += (Pieces.Along(r, r.A, k / 200.0) - Pieces.Along(r, r.A, (k - 1) / 200.0)).Length;
                Assert.Equal(Pieces.Length(r), run, 1);
                // It stays inside its square.
                for (int k = 0; k <= 10; k++)
                {
                    var at = Pieces.Along(r, r.A, k / 10.0);
                    Assert.InRange(at.X, -1e-9, Pieces.Cell + 1e-9);
                    Assert.InRange(at.Y, -1e-9, Pieces.Cell + 1e-9);
                }
            }
        }
    }

    [Fact]
    public void Straights_join_opposite_edges_and_curves_neighbouring_ones()
    {
        Assert.Equal((0, 2), (Pieces.Roads(new Piece(PieceKind.Straight, 0, 0))[0].A, Pieces.Roads(new Piece(PieceKind.Straight, 0, 0))[0].B));
        Assert.Equal((1, 3), (Pieces.Roads(new Piece(PieceKind.Straight, 0, 0, 1))[0].A, Pieces.Roads(new Piece(PieceKind.Straight, 0, 0, 1))[0].B));
        for (int rot = 0; rot < 4; rot++)
        {
            var c = Pieces.Roads(new Piece(PieceKind.Curve, 0, 0, rot))[0];
            Assert.Equal((rot, (rot + 1) % 4), (c.A, c.B));
            Assert.False(c.Straight);
            // A quarter circle round the corner the two edges share: every point half a square from it.
            var corner = new Vec(c.A == 1 || c.B == 1 ? Pieces.Cell : 0, c.A == 2 || c.B == 2 ? Pieces.Cell : 0);
            for (int k = 0; k <= 8; k++) Assert.Equal(Pieces.Cell / 2, (Pieces.Along(c, c.A, k / 8.0) - corner).Length, 6);
        }
        Assert.True(Pieces.Roads(new Piece(PieceKind.Banked, 0, 0))[0].Banked);
        Assert.False(Pieces.Roads(new Piece(PieceKind.Curve, 0, 0))[0].Banked);
    }

    [Fact]
    public void A_ramp_and_the_start_line_point_one_way_and_turn_four_ways_a_crossover_only_one()
    {
        Assert.Equal(1, Pieces.Turns(PieceKind.Cross));
        Assert.Equal(2, Pieces.Turns(PieceKind.Straight));
        Assert.Equal(2, Pieces.Turns(PieceKind.Bridge));
        Assert.Equal(4, Pieces.Turns(PieceKind.Ramp));
        Assert.Equal(4, Pieces.Turns(PieceKind.Curve));
        for (int rot = 0; rot < 4; rot++)
        {
            // Unturned, a ramp is climbed going north; each turn turns the way it's climbed a quarter clockwise.
            var r = Pieces.Roads(new Piece(PieceKind.Ramp, 0, 0, rot))[0];
            Assert.Equal(((rot + 2) % 4, rot), (r.A, r.B));
            Assert.True(Pieces.OneWay(PieceKind.Ramp));
        }
    }

    [Fact]
    public void The_rises_hills_up_dips_down_a_bridge_high_enough_to_drive_under_and_a_ramp_to_its_lip()
    {
        Assert.Equal(Pieces.HillHeight, Pieces.Height(Rise.Hill, 0.5), 9);
        Assert.Equal(-Pieces.DipDepth, Pieces.Height(Rise.Dip, 0.5), 9);
        Assert.Equal(Pieces.BridgeHeight, Pieces.Height(Rise.Bridge, 0.5), 9);
        // The tallest vehicle passes under the bridge's deck.
        Assert.True(Pieces.BridgeHeight > Vehicles.All.Max(v => Vehicles.Of(v).Height) + 10);
        // A ramp climbs steadily to its lip, then the road beyond is back on the ground.
        Assert.Equal(Pieces.RampHeight / 2, Pieces.Height(Rise.Ramp, Pieces.RampLip / 2), 9);
        Assert.True(Pieces.Height(Rise.Ramp, Pieces.RampLip - 0.001) > Pieces.RampHeight * 0.99);
        Assert.Equal(0, Pieces.Height(Rise.Ramp, Pieces.RampLip + 0.001));
        Assert.Equal(0, Pieces.Height(Rise.Flat, 0.5));
    }

    [Fact]
    public void The_course_knows_the_road_height_under_a_point_exactly()
    {
        var t = Lab.Strip(PieceKind.Hill);
        var c = Course.Build(t)!;
        var hill = c.Humps.Single();
        int mid = c.IndexAt(c.Along[hill.Start] + Pieces.Cell / 2);
        Assert.Equal(Pieces.HillHeight, c.Height(c.Points[mid], mid), 1);
        Assert.Equal(Pieces.HillHeight, c.HeightAlong(c.Along[hill.Start] + Pieces.Cell / 2), 1);
        // Across the road the height is the same.
        Assert.Equal(c.Height(c.Points[mid], mid), c.Height(c.Points[mid] + c.Right(mid) * 20, mid), 6);
        Assert.Equal(0, c.HeightAlong(c.Along[hill.Start] + Pieces.Cell * 1.5));
    }

    [Fact]
    public void Under_a_bridge_the_lower_road_is_on_the_ground_and_over_it_the_deck_is_up_high()
    {
        var t = Tracks.Make("KNOTTED RIBBON");
        var c = Course.Build(t)!;
        int bridge = t.Pieces.FindIndex(p => p.Kind == PieceKind.Bridge);
        var p = t.Pieces[bridge];
        var middle = new Vec((p.Col + 0.5) * Pieces.Cell, (p.Row + 0.5) * Pieces.Cell);
        var samples = Enumerable.Range(0, c.Points.Length).Where(i => c.Piece[i] == bridge && (c.Points[i] - middle).Length < 3).ToList();
        Assert.Equal(2, samples.Count);
        Assert.Contains(samples, i => c.Z[i] > Pieces.BridgeHeight - 0.5);
        Assert.Contains(samples, i => c.Z[i] == 0);
        // Looked for from each road, and from high up or on the ground, the nearest sample is on the right road.
        var over = samples.Single(i => c.Z[i] > 1);
        var under = samples.Single(i => c.Z[i] == 0);
        Assert.Equal(over, c.Nearest(middle, over - 5, Pieces.BridgeHeight));
        Assert.Equal(under, c.Nearest(middle, under - 5, 0));
        Assert.Equal(over, c.Nearest(middle, -1, Pieces.BridgeHeight));
        Assert.Equal(under, c.Nearest(middle, -1, 0));
    }

    [Theory]
    [InlineData(PieceKind.Straight)]
    [InlineData(PieceKind.Hill)]
    [InlineData(PieceKind.Dip)]
    [InlineData(PieceKind.Ramp)]
    public void A_car_driven_steadily_across_each_piece_stays_on_the_road(PieceKind kind)
    {
        var w = Lab.Race(Lab.Strip(kind));
        Lab.Park(w, 1, w.Course.Length * 0.6);
        Lab.Put(w, 0, Pieces.Cell);
        var c = w.Cars[0];
        // Up to a steady 140 and over the piece in the middle of the top straight.
        for (int t = 0; t < 60 * 4; t++)
        {
            Lab.Step(w, 1, Pilot.Drive(w, c, 0) with { Gas = c.Speed < 140, Brake = c.Speed > 150 });
            Assert.True(Math.Abs(w.Course.Off(c.Pos, c.Index)) < Pieces.HalfWidth, $"{kind}: off the road at {c.Pos} after {t} steps");
            Assert.Equal(0, c.Towed);
        }
        Assert.True(c.Progress > 5 * Pieces.Cell, $"{kind}: only {c.Progress:0}");
    }

    [Fact]
    public void Each_kind_of_piece_has_a_name_a_short_name_and_a_description()
    {
        foreach (var k in Enum.GetValues<PieceKind>())
        {
            Assert.NotEmpty(Pieces.Name(k));
            Assert.True(Pieces.Short(k).Length <= 8, k.ToString());
            Assert.NotEmpty(Pieces.Describe(k));
        }
        Assert.Equal(Enum.GetValues<PieceKind>().Length, Pieces.Box.Distinct().Count());
    }
}

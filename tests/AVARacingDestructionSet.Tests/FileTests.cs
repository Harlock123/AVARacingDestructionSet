using AVARacingDestructionSet.Engine;

namespace AVARacingDestructionSet.Tests;

/// <summary>Tracks to and from their files.</summary>
public class FileTests
{
    /// <summary>A grid with one of every piece turned every way it turns, on ice on Mars.</summary>
    private static Track Everything()
    {
        var t = new Track { Name = "EVERYTHING", Surface = Surface.Ice, Planet = Planet.Mars };
        int i = 0;
        foreach (var k in Pieces.Box)
            for (int rot = 0; rot < Pieces.Turns(k); rot++, i++)
                t.Put(new Piece(k, i % Track.Cols, i / Track.Cols, rot));
        return t;
    }

    [Fact]
    public void Every_piece_turned_every_way_comes_back_from_a_file_just_as_it_went()
    {
        var t = Everything();
        string path = Path.Combine(Lab.TempFolder(), "everything" + TrackFile.Extension);
        TrackFile.Save(t, path);
        var back = TrackFile.Load(path);
        Assert.NotNull(back);
        Assert.Equal(TrackFile.ToJson(t), TrackFile.ToJson(back));
        Assert.Equal(t.Pieces.Count, back.Pieces.Count);
        foreach (var (a, b) in t.Pieces.Zip(back.Pieces)) Assert.Equal((a.Kind, a.Col, a.Row, a.Rot), (b.Kind, b.Col, b.Row, b.Rot));
        Assert.Equal((Surface.Ice, Planet.Mars, "EVERYTHING"), (back.Surface, back.Planet, back.Name));
    }

    [Fact]
    public void Every_track_in_the_box_round_trips_and_races_the_same_after()
    {
        foreach (var name in Tracks.Names)
        {
            var t = Tracks.Make(name);
            var back = TrackFile.FromJson(TrackFile.ToJson(t))!;
            Assert.Equal(TrackFile.ToJson(t), TrackFile.ToJson(back));
            Assert.Equal(Course.Build(t)!.Length, Course.Build(back)!.Length);
        }
    }

    [Fact]
    public void A_track_file_is_readable_json_with_its_format_named_and_its_pieces_by_name()
    {
        string json = TrackFile.ToJson(Tracks.Make("OXBOW OVAL"));
        Assert.Contains("\"format\": \"AVARacingDestructionSet track\"", json);
        Assert.Contains("\"version\": 1", json);
        Assert.Contains("\"name\": \"OXBOW OVAL\"", json);
        Assert.Contains("\"surface\": \"Asphalt\"", json);
        Assert.Contains("\"planet\": \"Earth\"", json);
        Assert.Contains("\"kind\": \"Banked\"", json);
        Assert.Contains("\"col\":", json);
    }

    [Fact]
    public void Broken_and_foreign_files_are_refused_and_a_hand_edited_one_is_put_right()
    {
        Assert.Null(TrackFile.FromJson("not json"));
        Assert.Null(TrackFile.FromJson("{}"));
        Assert.Null(TrackFile.FromJson("""{ "format": "AVAPinballConstructionSet table", "version": 1, "track": {} }"""));
        Assert.Null(TrackFile.FromJson("""{ "format": "AVARacingDestructionSet track", "version": 99, "track": {} }"""));
        Assert.Null(TrackFile.Load(Path.Combine(Lab.TempFolder(), "missing.rds.json")));
        var t = TrackFile.FromJson("""
            { "format": "AVARacingDestructionSet track", "version": 1, "track": {
              "name": "  a  hand~made one that is far too long a name  ", "surface": "Dirt", "planet": "Moon",
              "pieces": [ { "kind": "Straight", "col": 3, "row": 3, "rot": 5 }, { "kind": "Curve", "col": 3, "row": 3, "rot": -1 },
                          { "kind": "Ramp", "col": 40, "row": 2, "rot": 0 } ] } }
            """);
        Assert.NotNull(t);
        Assert.Equal("A HANDMADE ONE THAT", t.Name);
        Assert.Equal((Surface.Dirt, Planet.Moon), (t.Surface, t.Planet));
        // One piece a square (the last one wins), on the grid, turned 0 to 3.
        var p = Assert.Single(t.Pieces);
        Assert.Equal((PieceKind.Curve, 3), (p.Kind, p.Rot));
    }

    [Fact]
    public void Saved_tracks_are_named_after_the_track_and_listed_by_name()
    {
        var g = Lab.Engine();
        g.OpenBuiltIn("CRATER RIM");
        g.Editor.Rename("Moon Base 1!");
        string? path = g.SaveTrack();
        Assert.NotNull(path);
        Assert.Equal("moon-base-1" + TrackFile.Extension, Path.GetFileName(path));
        Assert.StartsWith(g.Folder, path);
        File.WriteAllText(Path.Combine(g.TracksFolder, "junk" + TrackFile.Extension), "nonsense");
        var list = g.SavedTracks();
        var (name, file) = Assert.Single(list);
        Assert.Equal("MOON BASE 1!", name);
        g.OpenBlank();
        Assert.True(g.OpenFile(file));
        Assert.Equal("MOON BASE 1!", g.Editor.Track.Name);
        Assert.Equal(Planet.Moon, g.Editor.Track.Planet);
        Assert.False(g.OpenFile(Path.Combine(g.TracksFolder, "junk" + TrackFile.Extension)));
        Assert.Equal("THAT ISN'T A TRACK", g.Editor.Message);
        Assert.Equal("untitled", TrackFile.Slug("!!"));
    }
}

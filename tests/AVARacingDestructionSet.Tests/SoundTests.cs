using AVARacingDestructionSet.Engine;

namespace AVARacingDestructionSet.Tests;

/// <summary>Every sound the engine asks for, and every vehicle's engine, is there to be played.</summary>
public class SoundTests
{
    private static string Folder => Path.Combine(AppContext.BaseDirectory, "Sounds");

    [Fact]
    public void Every_sound_has_its_file_and_every_vehicle_its_engine_loop()
    {
        foreach (var s in Enum.GetValues<Sfx>().Where(s => s != Sfx.MusicStop))
            Assert.True(File.Exists(Path.Combine(Folder, s + ".wav")), s.ToString());
        foreach (var v in Vehicles.All)
            Assert.True(File.Exists(Path.Combine(Folder, SoundManager.EngineSound(v) + ".wav")), v.ToString());
    }

    [Fact]
    public void An_engine_loop_is_a_whole_second_so_it_repeats_without_a_gap()
    {
        foreach (var v in Vehicles.All)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Folder, SoundManager.EngineSound(v) + ".wav"));
            int rate = BitConverter.ToInt32(bytes, 24);
            int data = BitConverter.ToInt32(bytes, 40);
            Assert.Equal(rate * 2, data);
        }
    }

    [Fact]
    public void A_silent_sound_manager_takes_every_sound_and_the_engines_without_complaint()
    {
        using var s = new SoundManager(silent: true);
        Assert.False(s.IsAvailable);
        foreach (var sfx in Enum.GetValues<Sfx>()) s.Play(sfx);
        s.Engines([(VehicleKind.StockCar, 1.2), (VehicleKind.MoonBuggy, 2)]);
        s.IsMuted = true;
        s.Engines(null);
        Assert.True(s.IsMuted);
    }
}

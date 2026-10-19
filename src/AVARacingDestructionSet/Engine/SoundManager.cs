using AVARacingDestructionSet.Audio;

namespace AVARacingDestructionSet.Engine;

/// <summary>
/// Plays the program's sounds through OpenAL Soft, which mixes as many at once as the race needs. The title tune
/// plays on the music channel. In a race the two engines loop, each its own vehicle's, pitched up as it revs:
/// player 1's on the ambience channel and player 2's on the music channel. In a virtual machine a larger mixing
/// buffer keeps the sound from breaking up.
/// </summary>
public sealed class SoundManager : IDisposable
{
    private readonly IAudioEngine _engine;
    private readonly string _folder;
    private (VehicleKind A, VehicleKind B)? _engines;

    public SoundManager(bool silent = false)
    {
        _folder = Path.Combine(AppContext.BaseDirectory, "Sounds");
        _engine = silent
            ? new NullAudioEngine("silent")
            : OpenAlAudioEngine.CreateOrSilent(AudioBuffer.PeriodFrames(AudioBuffer.Automatic, VirtualMachine.Detect()));
        _engine.SetVolumes(0.9f, 0.8f, 0.7f);
        _engine.SetAmbienceVolume(0.45f);
    }

    public bool IsAvailable => _engine.IsAvailable;

    public bool IsMuted
    {
        get => _muted;
        set
        {
            _muted = value;
            if (value) StopAll();
        }
    }
    private bool _muted;

    private string File(string name) => Path.Combine(_folder, name + ".wav");

    public static string EngineSound(VehicleKind v) => "Engine" + v;

    public void Play(Sfx sfx)
    {
        if (_muted) return;
        switch (sfx)
        {
            case Sfx.TitleTune:
                _engines = null;
                _engine.StopAmbience();
                _engine.SetMusicPitch(1);
                _engine.SetVolumes(0.9f, 0.8f, 0.7f);
                _engine.PlayMusic(File(sfx.ToString()), loop: true);
                return;
            case Sfx.MusicStop:
                if (_engines == null) _engine.StopMusic();
                return;
        }
        _engine.PlayEffect(File(sfx.ToString()), sfx switch
        {
            Sfx.Skid => 0.35f, Sfx.Bump => 0.55f, Sfx.Beep or Sfx.Click or Sfx.Turn => 0.6f, Sfx.Land or Sfx.Jump => 0.6f, _ => 1f,
        });
    }

    /// <summary>The engines: each car's loop (null: no race on, quiet), at a pitch for its revs.</summary>
    public void Engines((VehicleKind Vehicle, double Revs)[]? cars)
    {
        if (_muted || cars is not { Length: 2 })
        {
            if (_engines != null)
            {
                _engines = null;
                _engine.StopAmbience();
                _engine.StopMusic();
            }
            return;
        }
        if (_engines != (cars[0].Vehicle, cars[1].Vehicle))
        {
            _engines = (cars[0].Vehicle, cars[1].Vehicle);
            _engine.SetVolumes(0.9f, 0.8f, 0.4f);
            _engine.PlayAmbience(File(EngineSound(cars[0].Vehicle)));
            _engine.PlayMusic(File(EngineSound(cars[1].Vehicle)), loop: true);
        }
        _engine.SetAmbiencePitch((float)Math.Max(0.5, cars[0].Revs));
        _engine.SetMusicPitch((float)Math.Max(0.5, cars[1].Revs));
    }

    public void StopAll()
    {
        _engines = null;
        _engine.StopAmbience();
        _engine.StopMusic();
    }

    public void Dispose() => _engine.Dispose();
}

# AGENTS.md

Notes for anyone, human or AI agent, changing this code. `README.md` covers using the construction set, the
garage, racing and the controls; this file covers how the code is put together and the rules that keep it working.

## What this is

A racing construction set in the style of Electronic Arts' 1985 Racing Destruction Set, in C# on .NET 10 and
Avalonia 12, drawn through Avalonia's `DrawingContext` in the original's 3/4 view. Build a track from pieces on a grid, build a vehicle
in the garage, then race a friend or the computer on a split screen. One self-contained executable per platform
(Windows, macOS and Linux, x64 and ARM). Everything in it is original: the graphics are drawn in code, and the
sounds and music are synthesised by `tools/make_sounds.py`.

## Commands

```bash
dotnet build AVARacingDestructionSet.slnx         # the SDK version is in global.json (10.0.400 or later)
dotnet test AVARacingDestructionSet.slnx          # every test runs headless: no window opens
dotnet run --project src/AVARacingDestructionSet  # play it; --no-audio for silence
RDS_SHOTS=$PWD/Images dotnet test tests/AVARacingDestructionSet.Tests --filter ScreenshotTests   # refresh the README's pictures
RDS_PROBE=1 dotnet test tests/AVARacingDestructionSet.Tests --filter PilotProbe   # how the computer drivers do, track by track
python3 tools/make_sounds.py                      # rebuild src/AVARacingDestructionSet/Sounds (standard-library Python only)
```

`RDS_PROBE` also takes `destruction` (destruction races) or `lazy` (a careless car against the computer).

`Directory.Build.props` sets `TreatWarningsAsErrors`, nullable reference types and implicit usings for every
project: a warning fails the build, so keep it at zero.

## Layout

- `src/AVARacingDestructionSet/Engine/`: the program, with no Avalonia drawing in it.
  - `GameEngine.cs` runs the states (title, demonstration, construction set, garage, race, best laps), the
    fixed-step loop and the saving of tracks.
  - `Track.cs` has the pieces, surfaces and planets, and `TrackFile`, the `.rds.json` track format.
  - `Tracks.cs` has the ten tracks in the box, each written as a drive round it, square by square.
  - `Course.cs` turns a track into the loop the cars race round: the road, its humps, banks and jumps.
  - `Editor.cs` is the construction set: the hand, turn and eraser tools and the panel.
  - `Vehicles.cs` has the garage's builds and what each can do on a surface.
  - `Sim.cs` steps the race: the lights, grip, the ground under the cars, jumps, knocks, oil, mines and wrecks.
  - `World.cs` has `Controls`, the race rules and the race state; `Geometry.cs` has `Vec`, `Dice` and `Field`.
  - `Pilot.cs` is the computer driver, used by the demonstration, races against the computer and the tests.
  - `InputHandler.cs` (keys for two players) and `ControllerManager.cs` (SDL2 gamepads) produce `Controls`.
  - `BestLapTable.cs`, `InitialsEntry.cs` and `SoundManager.cs` (engine loops pitched with the revs) do what their
    names say.
- `src/AVARacingDestructionSet/Views/`: `GameCanvas`, split over partial files for the editor, the garage, the race
  and the 3/4-view world, draws the 1024x768 frame and runs the 60 Hz timer. Ctrl+S saves a screenshot to the
  Pictures folder.
- `src/AVARacingDestructionSet/Audio/`: the OpenAL Soft audio engine, shared with the author's other AVA games.
- `src/AVARacingDestructionSet/Sounds/`: the generated WAV files, copied next to the executable.
- `tests/AVARacingDestructionSet.Tests/`: xUnit v3 with `Avalonia.Headless.XUnit`, one file per area (pieces,
  tracks, files, editor, vehicles, physics, weapons, races, pilot, input, sound, UI). `Lab.cs` has the shared
  helpers.

## Rules that keep it working

- **Fixed steps.** `GameEngine` runs the race in fixed 1/60 s steps (`Field.Step`, `Sim.Step`) from an
  accumulator, independent of the frame rate, so tests can run whole races deterministically from a seed. Draw
  random numbers from the world's seeded `Dice` (`w.Dice`), never from `Random.Shared`.
- **Engine first, drawing second.** Gameplay belongs in `Engine/`, where tests can reach it without a window.
  `Views/` only reads the engine and draws it.
- **Input goes through `InputHandler`.** `Controls` is `Steer` (-1 left, 1 right) with `Gas`, `Brake` and `Fire`
  flags. Player 1 uses the arrows, player 2 W, A, S and D. Up is the gas and Down the brake. Test new controls
  through `InputHandler`, not by building `Controls` directly.
  - Keys are tracked by `PhysicalKey`, because on some Linux setups Avalonia reports the release of Shift as
    CapsLock, and Left Shift is player 2's weapon key. `ShiftReleaseTests` covers this.
- **SDL under one lock.** `ControllerManager` starts, polls and stops SDL only inside `lock (Sdl)`. The tests
  create many handlers in parallel, and unlocked SDL calls corrupt its heap.
- **Audio.** OpenAL Soft through Silk.NET. `Directory.Build.targets` swaps in the official Windows `soft_oal.dll`
  from `Audio/native/`, which needs no Visual C++ runtime. In a virtual machine `OpenAlConfig` writes a larger
  mixing period to `ALSOFT_CONF` so the sound doesn't break up.
- **Tests never touch the player's files.** Best laps (one file per track), the track being edited
  (`working.rds.json`) and saved tracks (`tracks/`) live in the app-data folder (`AVARacingDestructionSet`). Tests
  give the engine a temporary folder (`Lab.TempFolder()`); `Lab.Engine()` also makes it quiet.
- **Track format.** `TrackFile` writes a format name and version into every `.rds.json`. If you change what a track
  holds, keep old files loading and add a test to `FileTests.cs`.

## Making a change

- Add or update tests with every gameplay change. `dotnet test` must pass, with no warnings.
- If driving or the tracks change, run the pilot probe and make sure the computer drivers still get round.
- If the change shows on screen, refresh the screenshots with the command above, and look at them.
- Update `README.md` (pieces, vehicles, controls) to match.
- New sounds go into `tools/make_sounds.py`, not as hand-edited WAV files.

## CI and releases

`.github/workflows/build.yml` builds and tests every push and pull request. It then publishes self-contained
single-file builds for win-x64/x86/arm64, linux-x64/arm64/arm and osx-x64/arm64. Every run keeps the builds as
workflow artifacts. Only pushes to `main` replace the assets of the `latest` pre-release, and a `v*` tag makes a
proper release. After a release it asks the portfolio site to rebuild, if the `SITE_REFRESH_TOKEN` secret is set.

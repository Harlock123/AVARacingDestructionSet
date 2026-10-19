#!/usr/bin/env python3
"""Synthesises AVARacingDestructionSet's sounds into ../src/AVARacingDestructionSet/Sounds as 16-bit mono WAV.

Nothing is sampled: every sound is built here from square, triangle, saw, sine and noise voices in the manner of a
home computer's sound chip. Each vehicle's engine is a one-second loop of a whole number of firing cycles, so it
repeats without a click and can be pitched up as the engine revs. The title plays the opening theme of In the Hall
of the Mountain King (Edvard Grieg, 1875; public domain), getting faster each time round, as it does in the
original. Run it from anywhere:
    python3 tools/make_sounds.py
"""
import math
import os
import random
import struct
import wave

RATE = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "AVARacingDestructionSet", "Sounds")
RNG = random.Random(1985)


def write(name, samples, volume=0.7):
    peak = max(1e-9, max(abs(s) for s in samples))
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(s / peak * volume * 32767)) for s in samples))


def voice(kind, phase):
    p = phase % 1.0
    if kind == "square":
        return 1.0 if p < 0.5 else -1.0
    if kind == "pulse":
        return 1.0 if p < 0.25 else -1.0
    if kind == "thin":
        return 1.0 if p < 0.125 else -1.0
    if kind == "tri":
        return 4 * p - 1 if p < 0.5 else 3 - 4 * p
    if kind == "saw":
        return 2 * p - 1
    return math.sin(2 * math.pi * p)


def tone(seconds, f0, f1=None, kind="square", attack=0.004, release=1.0, vibrato=0.0, vib_rate=8.0, gain=1.0):
    """A note gliding from f0 to f1 (exponentially), with a short attack and a shaped decay. 'noise' is noise
    re-sampled at the voice's rate: a lower 'pitch' sounds rougher."""
    n = int(RATE * seconds)
    f1 = f1 or f0
    out, phase, held = [], 0.0, 0.0
    for i in range(n):
        t = i / RATE
        f = f0 * (f1 / f0) ** (i / max(1, n - 1))
        if vibrato:
            f *= 1 + vibrato * math.sin(2 * math.pi * vib_rate * t)
        step = f / RATE
        phase += step
        if kind == "noise":
            if int(phase) != int(phase - step):
                held = RNG.uniform(-1, 1)
            s = held
        else:
            s = voice(kind, phase)
        env = min(1.0, t / attack) * (1 - i / n) ** release
        out.append(s * env * gain)
    return out


def lowpass(samples, k):
    """A one-pole low-pass filter: k near 0 is dull, near 1 bright."""
    out, y = [], 0.0
    for s in samples:
        y += k * (s - y)
        out.append(y)
    return out


def silence(seconds):
    return [0.0] * int(RATE * seconds)


def cat(*parts):
    out = []
    for p in parts:
        out.extend(p)
    return out


def mix(*parts):
    n = max(len(p) for p in parts)
    out = [0.0] * n
    for p in parts:
        for i, v in enumerate(p):
            out[i] += v
    return out


def scale(samples, k):
    return [s * k for s in samples]


NOTES = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def hz(note):
    n = NOTES[note[0]]
    rest = note[1:]
    while rest and rest[0] in "#b":
        n += 1 if rest[0] == "#" else -1
        rest = rest[1:]
    return 440.0 * 2 ** ((12 * (int(rest) + 1) + n - 69) / 12)


def line(unit, notes, kind="pulse", gain=0.5, release=0.6, staccato=1.0):
    """Notes as (note or list of notes or '-', units); a unit is so many seconds."""
    out = []
    for note, units in notes:
        d = units * unit
        if note == "-":
            out.extend(silence(d))
            continue
        chord = note if isinstance(note, list) else [note]
        sound = mix(*[tone(d * staccato, hz(n), kind=kind, release=release, gain=gain) for n in chord])
        out.extend(sound + silence(d - len(sound) / RATE))
    return out


# ───────────────────────────── the engines ─────────────────────────────

def engine(fire, roughness, whine=0.0, kind="saw", buzz=0.0):
    """One second of an engine at idle: firing pulses at a whole number of hertz (so the loop is seamless), their
    harmonics, a little roughness, and for the electric ones a whine."""
    n = RATE
    out = []
    noise = [RNG.uniform(-1, 1) for _ in range(n)]
    noise = lowpass(noise, 0.15)
    for i in range(n):
        t = i / RATE
        p = (fire * t) % 1.0
        # Each firing: a sharp knock dying away over the cycle.
        knock = math.exp(-p * 6) * (1 if kind == "saw" else 0.6)
        s = 0.55 * knock + 0.35 * voice(kind, fire * t) + 0.18 * math.sin(2 * math.pi * fire * 0.5 * t)
        s += buzz * voice("square", fire * 2 * t)
        s += roughness * noise[i] * (0.6 + 0.4 * knock)
        if whine:
            s += whine * math.sin(2 * math.pi * fire * 8 * t) * 0.5
        out.append(s)
    return lowpass(out, 0.5)


ENGINES = {
    "StockCar": dict(fire=52, roughness=0.5),
    "BajaBug": dict(fire=66, roughness=0.7, buzz=0.12),
    "Dragster": dict(fire=40, roughness=1.0, buzz=0.2),
    "IndyCar": dict(fire=104, roughness=0.3, whine=0.25),
    "Pickup": dict(fire=46, roughness=0.65),
    "Motorcycle": dict(fire=88, roughness=0.45, buzz=0.25, kind="square"),
    "DumpTruck": dict(fire=32, roughness=0.9, buzz=0.1),
    "MoonBuggy": dict(fire=150, roughness=0.1, whine=0.7, kind="tri"),
    "LunarRover": dict(fire=180, roughness=0.05, whine=0.9, kind="sine"),
}


# ───────────────────────────── the title tune ─────────────────────────────

def mountain_king():
    """In the Hall of the Mountain King (Edvard Grieg, 1875): its opening theme, in A minor, in quavers. The
    theme creeps up the scale and back, twice over, over a plodding bass."""
    theme = [("A4", 1), ("B4", 1), ("C5", 1), ("D5", 1), ("E5", 1), ("C5", 1), ("E5", 2),
             ("D#5", 1), ("B4", 1), ("D#5", 2), ("D5", 1), ("A#4", 1), ("D5", 2),
             ("A4", 1), ("B4", 1), ("C5", 1), ("D5", 1), ("E5", 1), ("C5", 1), ("E5", 1), ("A5", 1),
             ("G5", 1), ("E5", 1), ("C5", 1), ("E5", 1), ("G5", 4)]
    bass = [("A2", 2), ("E3", 2), ("A2", 2), ("E3", 2),
            ("B2", 2), ("F#3", 2), ("A#2", 2), ("F3", 2),
            ("A2", 2), ("E3", 2), ("A2", 2), ("E3", 2),
            ("C3", 2), ("G3", 2), ("C3", 2), ("G3", 2)]
    return theme, bass


def up(notes, octaves):
    def shift(n):
        if n == "-":
            return n
        return n[:-1] + str(int(n[-1]) + octaves)
    return [(shift(n), d) for n, d in notes]


def drum(seconds, kick=True):
    hit = tone(min(seconds, 0.12), 120, 45, "sine", attack=0.002, release=1.4, gain=0.9) if kick else tone(min(seconds, 0.08), 3000, 1500, "noise", attack=0.001, release=2, gain=0.35)
    return hit + silence(seconds - len(hit) / RATE)


# ───────────────────────────── everything ─────────────────────────────

def main():
    os.makedirs(OUT, exist_ok=True)
    for f in os.listdir(OUT):
        if f.endswith(".wav"):
            os.remove(os.path.join(OUT, f))

    for name, params in ENGINES.items():
        write(f"Engine{name}.wav", engine(**params), 0.5)

    # The lights: a beep a second, then a higher one for go.
    write("Beep.wav", tone(0.2, 660, kind="square", release=0.3), 0.3)
    write("Go.wav", tone(0.6, 1320, kind="square", release=0.6), 0.34)
    write("Skid.wav", lowpass(tone(0.3, 2600, 1900, "noise", attack=0.02, release=0.8), 0.6), 0.2)
    write("Bump.wav", mix(tone(0.12, 180, 70, "square", release=0.8, gain=0.6), tone(0.08, 900, 300, "noise", release=1.2, gain=0.5)), 0.35)
    # A crash: a burst of noise and clanging metal (sines that aren't in tune with each other).
    clang = mix(*[tone(0.5, f, f * 0.97, "sine", release=2.5, gain=0.35) for f in (523, 811, 1247, 1630)])
    write("Crash.wav", mix(lowpass(tone(0.7, 1800, 200, "noise", release=0.6), 0.5), clang, tone(0.3, 110, 40, "sine", release=0.8, gain=0.9)), 0.5)
    # An explosion: a deep rumble, a crack on top, and the crackle of burning.
    rumble = lowpass(tone(1.6, 600, 60, "noise", attack=0.003, release=1.3), 0.25)
    crack = tone(0.15, 3000, 800, "noise", attack=0.001, release=2.5, gain=0.8)
    crackle = cat(silence(0.25), *[cat(tone(0.012, 4000, kind="noise", gain=RNG.uniform(0.1, 0.35)), silence(RNG.uniform(0.02, 0.09))) for _ in range(18)])
    write("Explosion.wav", mix(scale(rumble, 3), crack, tone(0.8, 70, 30, "sine", release=1.2, gain=0.9), crackle), 0.62)
    # Oil: a slosh and a glug.
    write("Oil.wav", mix(lowpass(tone(0.35, 700, 200, "noise", attack=0.01, release=1.2), 0.3), cat(silence(0.08), tone(0.18, 300, 120, "sine", release=0.8, gain=0.8))), 0.5)
    # A mine: a clunk as it's put down, and its arming beeps.
    write("Mine.wav", cat(tone(0.06, 160, 80, "square", release=1.5), silence(0.12), tone(0.06, 1760, kind="pulse"), silence(0.06), tone(0.06, 1760, kind="pulse")), 0.3)
    write("Empty.wav", cat(tone(0.03, 900, kind="square", release=2), silence(0.05), tone(0.03, 600, kind="square", release=2)), 0.25)
    write("Jump.wav", lowpass(tone(0.45, 400, 1800, "noise", attack=0.05, release=0.7), 0.4), 0.3)
    write("Land.wav", mix(tone(0.2, 140, 50, "sine", release=1, gain=0.9), lowpass(tone(0.15, 1200, 300, "noise", release=1.5, gain=0.6), 0.5)), 0.45)
    # The tow truck's horn, two tones together.
    write("Tow.wav", cat(mix(tone(0.25, 330, kind="square", release=0.2, gain=0.5), tone(0.25, 415, kind="square", release=0.2, gain=0.5)), silence(0.05),
                         mix(tone(0.35, 330, kind="square", release=0.4, gain=0.5), tone(0.35, 415, kind="square", release=0.4, gain=0.5))), 0.3)
    write("Respawn.wav", cat(*[tone(0.06, hz(n), kind="tri", release=0.3) for n in ["C5", "E5", "G5", "C6", "E6"]]), 0.32)
    write("Lap.wav", cat(tone(0.09, hz("G5"), kind="pulse", release=0.3), tone(0.16, hz("C6"), kind="pulse", release=0.4)), 0.3)
    write("FinalLap.wav", cat(*[tone(0.1, hz(n), kind="pulse", release=0.3) for n in ["C6", "C6", "C6", "G6"]]), 0.34)
    flag = [("G4", 1), ("C5", 1), ("E5", 1), ("G5", 2), ("E5", 1), ("G5", 3)]
    write("Flag.wav", mix(line(0.09, flag), line(0.09, [(["C3", "G3"], 9)], kind="tri", gain=0.4)), 0.42)
    win = [("C5", 1), ("C5", 1), ("C5", 1), ("C5", 3), ("G#4", 3), ("A#4", 3), ("C5", 2), ("A#4", 1), ("C5", 6)]
    write("Win.wav", mix(line(0.11, win), line(0.11, [(["C3", "G3"], 6), (["G#2", "D#3"], 3), (["A#2", "F3"], 3), (["C3", "G3"], 9)], kind="tri", gain=0.4)), 0.42)
    write("Lose.wav", cat(*[tone(0.22, hz(n), hz(n) * 0.95, kind="saw", release=0.4) for n in ["E4", "D#4", "D4"]], tone(0.7, hz("C#4"), hz("C4"), "saw", vibrato=0.03, vib_rate=6, release=0.4)), 0.36)
    write("Record.wav", mix(line(0.08, [("C5", 1), ("E5", 1), ("G5", 1), ("C6", 2), ("G5", 1), ("C6", 4)]), line(0.08, [("C3", 3), ("G3", 3), (["C3", "E3"], 4)], kind="tri", gain=0.4)), 0.42)

    # The construction set and the garage.
    write("Click.wav", tone(0.025, 1500, 900, "square", release=2), 0.2)
    write("Pick.wav", tone(0.07, 500, 900, "tri", release=0.6), 0.3)
    write("Place.wav", mix(tone(0.08, 220, 110, "square", release=1.2, gain=0.7), tone(0.05, 1200, 400, "noise", release=2, gain=0.4)), 0.32)
    write("Delete.wav", tone(0.16, 700, 150, "square", release=0.8), 0.25)
    write("Turn.wav", cat(tone(0.02, 2000, kind="square", release=2), silence(0.03), tone(0.02, 1600, kind="square", release=2)), 0.22)
    write("Undo.wav", tone(0.12, 300, 900, "tri", release=0.5), 0.3)
    write("Refused.wav", tone(0.18, 110, 100, "square", release=0.3), 0.25)
    write("Saved.wav", cat(tone(0.08, hz("E5"), kind="tri", release=0.4), tone(0.18, hz("A5"), kind="tri", release=0.6)), 0.32)

    # The title: the Mountain King, three times round, faster and higher each time.
    theme, bass = mountain_king()
    parts = []
    for bpm, octave, gain in [(132, 0, 0.8), (170, 0, 0.9), (220, 1, 1.0)]:
        unit = 60.0 / bpm / 2
        lead = line(unit, up(theme * 2, octave), kind="pulse", gain=0.55, release=0.5, staccato=0.85)
        low = line(unit, bass * 2, kind="tri", gain=0.6, release=0.3, staccato=0.6)
        beats = cat(*[drum(unit * 2, kick=b % 2 == 0) for b in range(len(lead) * 2 // int(RATE * unit * 2) // 2)])
        parts.append(scale(mix(lead, low, scale(beats, 0.6)), gain))
    ending = mix(tone(1.4, hz("A2"), kind="tri", release=1.2), tone(1.4, hz("A3"), kind="pulse", release=1.5, gain=0.4),
                 tone(1.4, hz("C5"), kind="pulse", release=1.5, gain=0.35), tone(1.4, hz("E5"), kind="pulse", release=1.5, gain=0.35),
                 lowpass(tone(1.2, 1500, 300, "noise", release=1.5, gain=0.5), 0.5))
    write("TitleTune.wav", cat(*parts, ending, silence(1.5)), 0.42)


if __name__ == "__main__":
    main()

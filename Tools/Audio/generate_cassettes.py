"""The car radio's cassettes (the unique upgrade «Магнитола»): one synthesised song per cassette, each its own style,
played by the radio near the car and on the drive between houses (Vehicles.CarRadio). Like all generated sounds:
deterministic, mono 16-bit 44.1 kHz, seamless loops; a cassette's character on top — a little wow and flutter, the
treble rolled off, tape hiss.

    python Tools/Audio/generate_cassettes.py      writes Assets/_Game/Art/Audio/Music/cassette_<n>.wav

  0  «Ночная смена»   lo-fi beat: warm Rhodes-like chords, dusty kick and snare, a lazy bass (84 BPM)
  1  «Неон»           synthwave: pulsing octave bass, gated pads, a bright lead arpeggio (108 BPM)
  2  «Прибой»         surf twang: a tremolo pluck riff over a driving bass and brushed drums (152 BPM)
  3  «Фанк гаража»    funk: a slap-like bass line, clavinet stabs, a tight hat groove (100 BPM)
"""
import os
import sys

import numpy as np
from scipy import signal

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import generate_sounds as g  # noqa: E402

SR = g.SR
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "_Game", "Art", "Audio", "Music")
COUNT = 4


def tape(x, seed):
    """A cassette: wow (slow pitch drift) and flutter, the top rolled off at 7 kHz, a bed of hiss, a touch of saturation."""
    r = np.random.default_rng(seed)
    n = len(x)
    t = np.arange(n) / SR
    wow = 0.0022 * np.sin(2 * np.pi * 0.55 * t + r.uniform(0, 6)) + 0.0006 * np.sin(2 * np.pi * 6.3 * t)
    # (a loop: the drift must come back to where it started — the wow's period divides the length closely enough)
    pos = np.clip(np.arange(n) + np.cumsum(wow) * SR / SR * 40, 0, n - 1)
    x = np.interp(pos, np.arange(n), x)
    x = g.lowpass(x, 7000)
    hiss = g.highpass(r.uniform(-1, 1, n), 3000) * 0.012
    return np.tanh((x + hiss) * 1.3) / np.tanh(1.3)


def snare(dur=0.18, bright=3000):
    n = int(dur * SR)
    body = np.sin(2 * np.pi * 190 * np.arange(n) / SR) * g.env(n, 0.001, 0.05, curve=4)
    return g.bandpass(g.noise(dur), 900, bright) * g.env(n, 0.001, dur * 0.6, curve=4) * 0.8 + body * 0.4


def keys(notes, dur, bright=1800):
    """A Rhodes-like chord: sine with a bell partial, a slow tremolo."""
    t = g.t_axis(dur)
    x = sum(np.sin(2 * np.pi * g.midi(n) * t) + 0.25 * np.sin(2 * np.pi * g.midi(n) * 3.01 * t) * np.exp(-t * 6) for n in notes)
    trem = 1 + 0.15 * np.sin(2 * np.pi * 4.5 * t)
    return g.lowpass(x * trem, bright) * g.env(len(t), 0.01, dur * 0.9, sustain=0.4, release=0.2, curve=2) / len(notes)


def song_lofi():
    bpm, bars = 84, 8
    beat = 60 / bpm
    L = bars * 4 * beat
    chords = [[50, 53, 57, 60], [55, 59, 62, 65], [48, 52, 55, 59], [45, 48, 52, 55]] * 2

    def render(L):
        out = g.silence(L + 2)
        for b, ch in enumerate(chords):
            s = b * 4 * beat
            out = g.mix(out, g.at(keys(ch, 4 * beat) * 0.55, s, L + 2))
            out = g.mix(out, g.at(g.bass(ch[0] - 12, 1.5 * beat) * 0.4, s, L + 2), g.at(g.bass(ch[0] - 12, beat) * 0.3, s + 2.5 * beat, L + 2))
            for k in (0, 2.5):
                out = g.mix(out, g.at(g.lowpass(g.kick(), 900) * 0.6, s + k * beat, L + 2))
            for k in (1, 3):
                out = g.mix(out, g.at(g.lowpass(snare(), 4000) * 0.35, s + k * beat + 0.02, L + 2))
            for k in range(8):
                out = g.mix(out, g.at(g.hat(0.04) * (0.07 if k % 2 else 0.11), s + k * beat / 2 + (0.03 if k % 2 else 0), L + 2))
        return out
    return g.loop(render, L)


def song_synthwave():
    bpm, bars = 108, 8
    beat = 60 / bpm
    L = bars * 4 * beat
    roots = [45, 41, 48, 43] * 2

    def render(L):
        out = g.silence(L + 2)
        for b, r in enumerate(roots):
            s = b * 4 * beat
            minor = [r + 12, r + 15, r + 19] if b % 4 != 2 else [r + 12, r + 16, r + 19]
            gate = g.pad(minor, 4 * beat, 2200, 0.01, attack=0.05, release=0.1)
            n = len(gate)
            chop = (np.floor(np.arange(n) / SR / (beat / 4)) % 2 == 0).astype(float) * 0.7 + 0.3
            out = g.mix(out, g.at(gate * g.lowpass(chop, 60) * 0.35, s, L + 2))
            for k in range(8):
                out = g.mix(out, g.at(g.bass(r - 12 + (12 if k % 2 else 0), beat / 2) * 0.35, s + k * beat / 2, L + 2))
            arp = [minor[0] + 12, minor[1] + 12, minor[2] + 12, minor[1] + 24]
            for k in range(16):
                out = g.mix(out, g.at(g.pluck(arp[k % 4], 0.22, 4500) * 0.13, s + k * beat / 4, L + 2))
            for k in range(4):
                out = g.mix(out, g.at(g.kick() * 0.5, s + k * beat, L + 2))
            for k in (1, 3):
                out = g.mix(out, g.at(snare(0.3, 6000) * 0.32, s + k * beat, L + 2))
        return out
    return g.loop(render, L)


def song_surf():
    bpm, bars = 152, 8
    beat = 60 / bpm
    L = bars * 4 * beat
    riff = [52, 52, 55, 57, 59, 57, 55, 52]
    roots = [40, 40, 45, 47] * 2

    def render(L):
        out = g.silence(L + 2)
        for b, r in enumerate(roots):
            s = b * 4 * beat
            for k in range(8):
                note = riff[k] + (r - 40)
                p = g.pluck(note, beat * 0.45, 3800)
                trem = 1 + 0.4 * np.sin(2 * np.pi * 9 * np.arange(len(p)) / SR)
                out = g.mix(out, g.at(p * trem * 0.2, s + k * beat / 2, L + 2))
            for k in range(4):
                out = g.mix(out, g.at(g.bass(r, beat * 0.9) * 0.4, s + k * beat, L + 2))
                out = g.mix(out, g.at(g.kick() * (0.45 if k % 2 == 0 else 0.25), s + k * beat, L + 2))
            for k in range(8):
                out = g.mix(out, g.at(g.bandpass(g.noise(0.09), 2500, 9000) * g.env(int(0.09 * SR), 0.004, 0.06) * 0.1, s + k * beat / 2, L + 2))
            for k in (1, 3):
                out = g.mix(out, g.at(snare(0.16) * 0.3, s + k * beat, L + 2))
        return out
    return g.loop(render, L)


def song_funk():
    bpm, bars = 100, 8
    beat = 60 / bpm
    L = bars * 4 * beat
    line = [(0, 40), (0.75, 40), (1.5, 52), (2, 43), (2.5, 45), (3.25, 40), (3.5, 47)]
    roots = [0, 0, 5, 3] * 2

    def render(L):
        out = g.silence(L + 2)
        for b, shift in enumerate(roots):
            s = b * 4 * beat
            for t0, n in line:
                pop = g.mix(g.bass(n + shift, beat * 0.35), 0.3 * g.highpass(g.pluck(n + shift + 12, beat * 0.2, 5000), 400))
                out = g.mix(out, g.at(pop * 0.42, s + t0 * beat, L + 2))
            for k in (0.5, 1.75, 2.5, 3.75):
                clav = g.highpass(g.mix(g.pluck(64 + shift, beat * 0.15, 6000), g.pluck(67 + shift, beat * 0.15, 6000)), 500)
                out = g.mix(out, g.at(clav * 0.16, s + k * beat, L + 2))
            for k in (0, 2.5):
                out = g.mix(out, g.at(g.kick() * 0.5, s + k * beat, L + 2))
            for k in (1, 3):
                out = g.mix(out, g.at(snare(0.14, 5000) * 0.34, s + k * beat, L + 2))
            for k in range(16):
                out = g.mix(out, g.at(g.hat(0.03) * (0.1 if k % 4 == 2 else 0.06), s + k * beat / 4, L + 2))
        return out
    return g.loop(render, L)


SONGS = [song_lofi, song_synthwave, song_surf, song_funk]


def main():
    os.makedirs(OUT, exist_ok=True)
    for i, song in enumerate(SONGS):
        x = tape(song(), 4100 + i)
        g.write(os.path.join(OUT, f"cassette_{i}.wav"), g.norm(x, 0.85), 0.85)


if __name__ == "__main__":
    main()

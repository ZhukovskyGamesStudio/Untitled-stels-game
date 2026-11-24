"""Recorded CC0 sounds that replace generated clips (the car's engine and horn sounded synthetic).

Run:  python Tools/Audio/import_cc0_sounds.py

1. Raw downloads go to Tools/Audio/cache/ (ignored by git), once.
2. Each is cut and converted to what generate_sounds.py writes (mono, 16 bit, 44.1 kHz, the same peak) and written over
   the generated clip in Assets/_Game/Art/Audio/<group>/ (generate_sounds.py no longer writes these names).
3. Assets/_Game/Art/Audio/CREDITS.md lists every recording with its source page (CC0: no attribution required).

- car_engine: domasx2, "racing car engine sound loops" (OpenGameArt), loop_0.wav — an idle loop; the game pitches it
  with the revs (Car.Sound). Its level creeps up by ~2 dB along the loop, which pulsed at every wrap: the slow
  envelope is flattened, the loop is cut where the firing is back in phase with its start and crossfaded there; the
  game keeps short loops uncompressed (AssetBuilder.Audio: Vorbis padded the seam with a click).
- car_horn: 15HPanska_Ruttner_Jan, "Car Horn.wav" (Wikimedia Commons, from freesound 461679) — two honks; the second,
  fuller one with its ring-out is kept.
"""
import os
import sys
import urllib.request
import wave

import numpy as np
from scipy import signal
from scipy.io import wavfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cc0_sources  # noqa: E402
import recorded  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
CACHE = os.path.join(ROOT, "Tools", "Audio", "cache")
AUDIO = os.path.join(ROOT, "Assets", "_Game", "Art", "Audio")
SR = 44100
UA = {"User-Agent": "stealth-prototype-asset-fetch"}

SOUNDS = [
    # (group, name, url, page, author, title, peak)
    ("Car", "car_engine", "https://opengameart.org/sites/default/files/loop_0.wav",
     "https://opengameart.org/content/racing-car-engine-sound-loops", "domasx2", "racing car engine sound loops (loop_0)", 0.8),
    ("Car", "car_horn", "https://upload.wikimedia.org/wikipedia/commons/8/8c/Car_Horn.wav",
     "https://commons.wikimedia.org/wiki/File:Car_Horn.wav", "15HPanska_Ruttner_Jan", "Car Horn.wav", 0.7),
]


def fetch(url):
    os.makedirs(CACHE, exist_ok=True)
    path = os.path.join(CACHE, os.path.basename(url))
    if not os.path.exists(path):
        with urllib.request.urlopen(urllib.request.Request(url, headers=UA)) as r, open(path, "wb") as f:
            f.write(r.read())
        print("downloaded", url)
    return path


def load(path):
    """Mono float at SR."""
    sr, x = wavfile.read(path)
    x = x.astype(np.float64)
    if x.ndim > 1:
        x = x.mean(axis=1)
    if sr != SR:
        x = signal.resample_poly(x, SR, sr)
    return x / max(1e-9, np.abs(x).max())


def write(path, x, peak):
    if peak is not None:
        x = x / max(1e-9, np.abs(x).max()) * peak
    data = (np.clip(x, -1, 1) * 32767).astype(np.int16)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())
    print("wrote", path, f"{len(data) / SR:.2f}s")


def engine(x):
    # the slow envelope (a 0.25 s window, wrapped round: it is a loop) divided out
    win = int(0.25 * SR)
    padded = np.concatenate([x[-win:], x, x[:win]])
    env = np.sqrt(np.convolve(padded ** 2, np.ones(win) / win, mode="same"))[win:-win]
    x = x / np.maximum(env, 1e-3) * env.mean()
    # the loop's length: where what follows looks most like its start (the engine's firing in phase), searched in the
    # last fifth; then the start crossfaded from that continuation (60 ms, equal power): at the wrap the sample after the
    # last is the very one the recording had there, and the firing never jumps
    f = int(0.06 * SR)
    head = x[:f] - x[:f].mean()
    best, length = -2.0, len(x) - f
    for end in range(int(len(x) * 0.8), len(x) - f):
        seg = x[end:end + f] - x[end:end + f].mean()
        c = float(np.dot(seg, head) / (np.linalg.norm(seg) * np.linalg.norm(head) + 1e-9))
        if c > best:
            best, length = c, end
    ramp = np.sin(np.linspace(0, np.pi / 2, f)) ** 2
    out = x[:length].copy()
    out[:f] = x[:f] * ramp + x[length:length + f] * np.cos(np.linspace(0, np.pi / 2, f)) ** 2
    print(f"car_engine loop: {length / SR:.3f} s, seam match {best:.3f}")
    return out


def horn(x):
    a, b = int(1.905 * SR), int(2.36 * SR)
    x = x[a:b].copy()
    fi, fo = int(0.004 * SR), int(0.06 * SR)
    x[:fi] *= np.linspace(0, 1, fi)
    x[-fo:] *= np.linspace(1, 0, fo) ** 2
    return x


CUTS = {"car_engine": engine, "car_horn": horn}


def main():
    rows = []
    for group, name, url, page, author, title, peak in SOUNDS:
        if "--recorded" not in sys.argv:
            x = CUTS[name](load(fetch(url)))
            write(os.path.join(AUDIO, group, name + ".wav"), x, peak)
        rows.append(f"| {group}/{name}.wav | {title} | {author} | {page} |")
    for name, x, peak in recorded.render():
        write(os.path.join(AUDIO, name + ".wav"), x, peak)
    by_set = {}
    for clip, sets in recorded.USED.items():
        for s in sets:
            by_set.setdefault(s, []).append(clip)
    for name, page, title, author, _ in cc0_sources.SETS:
        if name in by_set:
            rows.append(f"| {', '.join(sorted(by_set[name]))} | {title} | {author or 'see the page'} | {cc0_sources.SITE}/content/{page} |")
    with open(os.path.join(AUDIO, "CREDITS.md"), "w", encoding="utf-8") as c:
        c.write("# Recorded sounds\n\nEverything else here is generated (Tools/Audio/generate_sounds.py, generate_voices.py). "
                "These are recordings, licence [CC0](https://creativecommons.org/publicdomain/zero/1.0/) (public domain, "
                "no attribution required — listed for reference); cut by Tools/Audio/import_cc0_sounds.py.\n\n"
                "| Clip | Source | Author | Page |\n| --- | --- | --- | --- |\n")
        c.write("\n".join(rows) + "\n")
    print("DONE")


if __name__ == "__main__":
    main()

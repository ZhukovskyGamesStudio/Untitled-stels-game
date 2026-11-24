"""Decodes the downloaded recordings (Tools/Audio/cache/<set>/**, any of wav, ogg, flac, mp3) to mono 44.1 kHz float .npy
beside each other in Tools/Audio/cache/_pcm/<set>/<name>.npy: Blender's audio library reads them all.

    blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Audio/decode_cc0.py
"""
import os

import aud
import numpy as np

CACHE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "cache")
OUT = os.path.join(CACHE, "_pcm")
bad = 0
for d in sorted(os.listdir(CACHE)):
    if d.startswith("_") or not os.path.isdir(os.path.join(CACHE, d)):
        continue
    for root, _, files in os.walk(os.path.join(CACHE, d)):
        for f in sorted(files):
            if not f.lower().endswith((".wav", ".ogg", ".flac", ".mp3")) or "__MACOSX" in root or f.startswith("._"):
                continue
            out = os.path.join(OUT, d, os.path.splitext(f)[0] + ".npy")
            if os.path.exists(out):
                continue
            os.makedirs(os.path.dirname(out), exist_ok=True)
            try:
                s = aud.Sound(os.path.join(root, f))
                rate = s.specs[0]
                if int(rate) != 44100:
                    s = s.resample(44100, 3)
                x = s.data()
                x = x.mean(axis=1) if x.ndim > 1 else x
                np.save(out, x.astype(np.float32))
            except Exception as e:
                bad += 1
                print("cannot read", d, f, e)
print("DECODED, unreadable:", bad)

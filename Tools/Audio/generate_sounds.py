"""
Procedural sounds and music for «Айтишники: вылазка».

    python Tools/Audio/generate_sounds.py [Assets/_Game/Art/Audio]

Everything is synthesised with numpy (seeded, so the output is stable): footsteps, items, UI, robot signals,
the turret, radio, doors, lasers, ambience loops and three music loops (menu, exploring, chase).
Loops are rendered with a tail that is wrapped around to the start, so they repeat without a click.
Writes 16-bit mono WAV files into <out>/<Category>/<name>.wav.
"""
import math
import os
import sys
import wave

import numpy as np
from scipy import signal

SR = 44100
rng = np.random.default_rng(1337)


# --------------------------------------------------------------------------- primitives

def t_axis(dur):
    return np.arange(int(dur * SR)) / SR


def silence(dur):
    return np.zeros(int(dur * SR))


def noise(dur):
    return rng.uniform(-1, 1, int(dur * SR))


def brown(dur):
    w = rng.normal(0, 1, int(dur * SR))
    b = np.cumsum(w)
    b = signal.lfilter([1, -1], [1, -0.995], b)  # remove drift
    return b / (np.max(np.abs(b)) + 1e-9)


def sine(freq, dur, phase=0.0):
    t = t_axis(dur)
    if callable(freq):
        f = freq(t)
        return np.sin(2 * np.pi * np.cumsum(f) / SR + phase)
    return np.sin(2 * np.pi * freq * t + phase)


def sweep(f0, f1, dur, shape="exp"):
    t = t_axis(dur)
    if shape == "exp":
        f = f0 * (f1 / f0) ** (t / dur)
    else:
        f = f0 + (f1 - f0) * t / dur
    return np.sin(2 * np.pi * np.cumsum(f) / SR)


def square(freq, dur, duty=0.5):
    t = t_axis(dur)
    return signal.square(2 * np.pi * freq * t, duty)


def saw(freq, dur):
    t = t_axis(dur)
    return signal.sawtooth(2 * np.pi * freq * t)


def env(n_or_x, attack=0.005, decay=0.2, sustain=0.0, release=0.0, curve=4.0):
    """Attack-decay envelope over a signal length (exponential decay)."""
    n = n_or_x if isinstance(n_or_x, int) else len(n_or_x)
    t = np.arange(n) / SR
    a = np.clip(t / max(attack, 1e-4), 0, 1)
    d = np.exp(-curve * np.maximum(t - attack, 0) / max(decay, 1e-4))
    e = a * (sustain + (1 - sustain) * d)
    if release > 0:
        r = min(int(release * SR), n)
        e[-r:] *= np.linspace(1, 0, r)
    return e


def lowpass(x, cutoff, order=2):
    b, a = signal.butter(order, min(cutoff / (SR / 2), 0.99), "low")
    return signal.lfilter(b, a, x)


def highpass(x, cutoff, order=2):
    b, a = signal.butter(order, min(cutoff / (SR / 2), 0.99), "high")
    return signal.lfilter(b, a, x)


def bandpass(x, lo, hi, order=2):
    b, a = signal.butter(order, [lo / (SR / 2), min(hi / (SR / 2), 0.99)], "band")
    return signal.lfilter(b, a, x)


def mix(*parts):
    n = max(len(p) for p in parts)
    out = np.zeros(n)
    for p in parts:
        out[:len(p)] += p
    return out


def at(x, offset, total):
    out = np.zeros(int(total * SR))
    i = int(offset * SR)
    seg = x[:max(0, len(out) - i)]
    out[i:i + len(seg)] += seg
    return out


def norm(x, peak=0.9):
    m = np.max(np.abs(x))
    return x if m < 1e-9 else x * (peak / m)


def fade(x, fin=0.002, fout=0.01):
    x = x.copy()
    a, b = int(fin * SR), int(fout * SR)
    if a > 0:
        x[:a] *= np.linspace(0, 1, a)
    if b > 0:
        x[-b:] *= np.linspace(1, 0, b)
    return x


def loop(render, length):
    """Renders length + tail and folds the tail onto the start: seamless loop."""
    x = render(length)
    n = int(length * SR)
    out = x[:n].copy()
    tail = x[n:]
    out[:len(tail)] += tail[:n]
    return out


def write(path, x, peak=0.9):
    x = norm(np.asarray(x, dtype=np.float64), peak)
    data = (np.clip(x, -1, 1) * 32767).astype(np.int16)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())
    print("wrote", path, f"{len(data) / SR:.2f}s")


# --------------------------------------------------------------------------- player

def footstep(i):
    """Soft rubber sole on office carpet/linoleum: low thump + short scuff."""
    d = 0.16
    thump = lowpass(noise(d), 260 + 40 * i) * env(int(d * SR), 0.002, 0.05, curve=5)
    body = sine(70 + 8 * i, d) * env(int(d * SR), 0.001, 0.04, curve=6) * 0.6
    scuff = bandpass(noise(d), 1500 + 300 * i, 5000) * env(int(d * SR), 0.004, 0.03, curve=6) * 0.25
    return fade(mix(thump, body, scuff))


def jump():
    d = 0.25
    cloth = bandpass(noise(d), 600, 3000) * env(int(d * SR), 0.03, 0.12)
    push = lowpass(noise(d), 200) * env(int(d * SR), 0.002, 0.05) * 0.8
    return fade(mix(cloth * 0.6, push))


def land():
    d = 0.3
    thud = lowpass(noise(d), 180) * env(int(d * SR), 0.001, 0.1, curve=5)
    body = sine(55, d) * env(int(d * SR), 0.001, 0.08) * 0.8
    rattle = bandpass(noise(d), 2000, 6000) * env(int(d * SR), 0.002, 0.04) * 0.15
    return fade(mix(thud, body, rattle))


def hurt():
    """Electric shock from a robot."""
    d = 0.45
    t = t_axis(d)
    buzz = signal.sawtooth(2 * np.pi * (120 + 40 * np.sin(2 * np.pi * 30 * t)) * t)
    crackle = (noise(d) * (rng.uniform(0, 1, len(t)) > 0.97)) * 2
    x = mix(lowpass(buzz, 3000) * 0.6, highpass(crackle, 2000) * 0.5) * env(len(t), 0.002, 0.25, curve=3)
    hit = lowpass(noise(d), 300) * env(len(t), 0.001, 0.05) * 0.8
    return fade(mix(x, hit))


def downed():
    d = 1.0
    tone = sweep(440, 110, d) * env(int(d * SR), 0.01, 0.8, curve=2) * 0.5
    thud = at(land(), 0.55, d)
    return fade(mix(tone, thud * 0.9))


def revive():
    d = 0.9
    out = silence(d)
    for k, f in enumerate([392, 523, 659, 784]):
        n = sine(f, 0.5) * env(int(0.5 * SR), 0.005, 0.35) * 0.35
        n += sine(2 * f, 0.5) * env(int(0.5 * SR), 0.005, 0.15) * 0.1
        out = mix(out, at(n, k * 0.09, d))
    return fade(out)


# --------------------------------------------------------------------------- items

def pickup():
    d = 0.22
    out = mix(at(sine(660, 0.1) * env(int(0.1 * SR), 0.002, 0.06), 0, d),
              at(sine(990, 0.12) * env(int(0.12 * SR), 0.002, 0.08), 0.06, d))
    rustle = bandpass(noise(d), 1500, 6000) * env(int(d * SR), 0.01, 0.06) * 0.25
    return fade(mix(out * 0.6, rustle))


def drop():
    d = 0.3
    thunk = lowpass(noise(d), 500) * env(int(d * SR), 0.001, 0.06, curve=5)
    knock = sine(180, d) * env(int(d * SR), 0.001, 0.05) * 0.7
    return fade(mix(thunk, knock))


def sell():
    """Cash register: a key clunk and a bright bell."""
    d = 1.0
    clunk = at(lowpass(noise(0.06), 1200) * env(int(0.06 * SR), 0.001, 0.02), 0, d) * 0.6
    bell = silence(d)
    for f, a in [(1318.5, 0.5), (1975.5, 0.35), (2637, 0.2), (3951, 0.08)]:
        bell += at(sine(f, 0.9) * env(int(0.9 * SR), 0.002, 0.6, curve=3) * a, 0.05, d)
    sparkle = at(highpass(noise(0.3), 6000) * env(int(0.3 * SR), 0.002, 0.15) * 0.1, 0.05, d)
    return fade(mix(clunk, bell, sparkle))


def stash():
    d = 0.35
    box = lowpass(noise(d), 900) * env(int(d * SR), 0.001, 0.08, curve=5)
    wood = (sine(220, d) + 0.5 * sine(410, d)) * env(int(d * SR), 0.001, 0.06) * 0.5
    latch = at(bandpass(noise(0.03), 2500, 7000) * env(int(0.03 * SR), 0.001, 0.01), 0.12, d) * 0.5
    return fade(mix(box, wood, latch))


# --------------------------------------------------------------------------- UI

def ui_click():
    d = 0.06
    return fade(mix(sine(1800, d) * env(int(d * SR), 0.0005, 0.015, curve=6),
                    bandpass(noise(d), 3000, 8000) * env(int(d * SR), 0.0005, 0.006) * 0.4))


def ui_hover():
    d = 0.04
    return fade(sine(2600, d) * env(int(d * SR), 0.001, 0.012, curve=6))


def ui_open():
    """Backpack opened (Tab): a short zipper."""
    d = 0.18
    t = t_axis(d)
    teeth = (np.sin(2 * np.pi * (60 + 200 * t / d) * t) > 0.6).astype(float)
    z = bandpass(noise(d) * teeth, 1500, 7000) * env(len(t), 0.01, 0.12, curve=2)
    return fade(z)


def banner():
    d = 0.8
    a = sine(784, 0.6) * env(int(0.6 * SR), 0.005, 0.4) * 0.5
    b = sine(1046.5, 0.6) * env(int(0.6 * SR), 0.005, 0.45) * 0.5
    return fade(mix(at(a, 0, d), at(b, 0.12, d)))


def fail():
    d = 1.6
    out = silence(d)
    for k, f in enumerate([392, 370, 349, 262]):
        tone = (saw(f, 0.5) * 0.3 + sine(f / 2, 0.5) * 0.4) * env(int(0.5 * SR), 0.01, 0.35 if k < 3 else 0.9, curve=2)
        out = mix(out, at(lowpass(tone, 1800), k * 0.28, d))
    return fade(out)


def level_done():
    d = 1.4
    out = silence(d)
    for k, f in enumerate([523, 659, 784, 1046.5]):
        tone = (sine(f, 0.6) + 0.3 * sine(2 * f, 0.6)) * env(int(0.6 * SR), 0.005, 0.5)
        out = mix(out, at(tone * 0.4, k * 0.1, d))
    return fade(out)


def ping():
    """A teammate pings a spot: two bright, short tones going up."""
    d = 0.45
    a = (sine(1318.5, 0.2) + 0.25 * sine(2637, 0.2)) * env(int(0.2 * SR), 0.002, 0.16, curve=5)
    b = (sine(1760, 0.3) + 0.25 * sine(3520, 0.3)) * env(int(0.3 * SR), 0.002, 0.26, curve=5)
    return fade(mix(at(a * 0.6, 0, d), at(b * 0.6, 0.09, d)))


def mark():
    """A robot marked for the team: a low target-lock double beep."""
    d = 0.5
    beep = lambda f: lowpass(square(f, 0.12), 3500) * env(int(0.12 * SR), 0.002, 0.1, curve=3)
    return fade(mix(at(beep(880) * 0.45, 0, d), at(beep(880) * 0.45, 0.14, d), at(sine(440, 0.4) * env(int(0.4 * SR), 0.005, 0.3) * 0.3, 0, d)))


# --------------------------------------------------------------------------- robots

def blip(f0, f1, d=0.22, kind="square"):
    t = t_axis(d)
    f = f0 * (f1 / f0) ** (t / d)
    ph = 2 * np.pi * np.cumsum(f) / SR
    w = signal.square(ph, 0.35) if kind == "square" else np.sin(ph)
    return fade(lowpass(w, 4000) * env(len(t), 0.004, d * 0.7, curve=2))


def robot_suspicious():
    return mix(blip(320, 640, 0.25), at(blip(640, 700, 0.1, "sine") * 0.5, 0.22, 0.4))


def robot_alert():
    d = 0.5
    a = square(880, 0.16) * env(int(0.16 * SR), 0.002, 0.14, sustain=0.6)
    b = square(660, 0.2) * env(int(0.2 * SR), 0.002, 0.18, sustain=0.6)
    return fade(lowpass(mix(at(a, 0, d), at(b, 0.2, d)), 3500))


def robot_lost():
    return blip(600, 240, 0.4)


def robot_hacked():
    d = 0.8
    t = t_axis(d)
    f = 900 * (60 / 900) ** (t / d)
    ph = 2 * np.pi * np.cumsum(f) / SR
    x = signal.square(ph)
    crush = np.round(x * (2 + 6 * (1 - t / d))) / 8  # bit-crush that opens up
    hold = np.repeat(crush[::8], 8)[:len(t)]           # sample-rate reduction
    return fade(lowpass(hold, 5000) * env(len(t), 0.003, 0.6, curve=1.5))


def radio():
    """Walkie-talkie: squelch, garbled voice-like chirps, squelch."""
    d = 0.9
    sq = bandpass(noise(0.08), 1500, 4500) * env(int(0.08 * SR), 0.001, 0.05)
    out = at(sq, 0, d) + at(sq, 0.78, d)
    t = t_axis(0.6)
    formant = np.sin(2 * np.pi * (300 + 180 * np.sin(2 * np.pi * 7 * t) + 90 * np.sin(2 * np.pi * 13 * t)) * t)
    voice = bandpass(signal.square(2 * np.pi * 140 * t) * (0.6 + 0.4 * formant), 500, 2800)
    voice *= (0.5 + 0.5 * np.abs(np.sin(2 * np.pi * 5.5 * t))) * env(len(t), 0.02, 0.6, sustain=0.6, release=0.05)
    out = mix(out, at(voice * 0.6, 0.1, d), at(bandpass(noise(0.6), 2000, 5000) * 0.08, 0.1, d))
    return fade(out)


def turret_shot():
    d = 0.35
    zap = sweep(2400, 220, 0.25) * env(int(0.25 * SR), 0.001, 0.2, curve=2)
    crack = highpass(noise(0.06), 1500) * env(int(0.06 * SR), 0.0005, 0.02)
    boom = lowpass(noise(d), 200) * env(int(d * SR), 0.001, 0.1) * 0.6
    return fade(mix(at(zap * 0.7, 0, d), at(crack, 0, d), boom))


def seismic_thump():
    """Seismic robot stomps the floor: a deep thud and a rumble running away."""
    d = 1.2
    thud = sine(48, 0.5) * env(int(0.5 * SR), 0.003, 0.4, curve=3)
    knock = lowpass(noise(0.12), 400) * env(int(0.12 * SR), 0.001, 0.08)
    rumble = lowpass(brown(d), 120) * env(int(d * SR), 0.05, 1.0, curve=2) * 0.8
    return fade(mix(thud, at(knock, 0, d), rumble))


def camera_beep():
    """Short electronic beep of a camera that is noticing someone (repeats faster and faster)."""
    d = 0.07
    b = sine(2100, d) * 0.7 + sine(4200, d) * 0.15
    return fade(b * env(int(d * SR), 0.002, 0.06, sustain=0.7), 0.002, 0.015)


def robot_step():
    """Metal foot on the floor."""
    d = 0.2
    clank = (sine(310, d) + 0.6 * sine(523, d) + 0.3 * sine(1190, d)) * env(int(d * SR), 0.001, 0.07, curve=5)
    thud = lowpass(noise(d), 300) * env(int(d * SR), 0.001, 0.04)
    servo = at(sweep(900, 1300, 0.08) * env(int(0.08 * SR), 0.005, 0.06) * 0.15, 0.02, d)
    return fade(mix(clank * 0.5, thud, servo))


def robot_motor(length):
    """Loop: a robot's idle body — low motor, gear whine and a faint servo flutter."""
    def render(L):
        d = L + 0.2
        t = t_axis(d)
        motor = signal.sawtooth(2 * np.pi * 92 * t) * 0.3 + np.sin(2 * np.pi * 46 * t) * 0.4
        whine = np.sin(2 * np.pi * (1480 + 12 * np.sin(2 * np.pi * 0.5 * t)) * t) * 0.05
        flutter = lowpass(noise(d), 600) * (0.5 + 0.5 * np.sin(2 * np.pi * 9 * t)) * 0.25
        return lowpass(motor, 900) + whine + flutter
    return loop(render, length)


def drone_rotor(length):
    def render(L):
        d = L + 0.2
        t = t_axis(d)
        blade = signal.sawtooth(2 * np.pi * 172 * t) * (0.6 + 0.4 * np.sin(2 * np.pi * 43 * t))
        air = bandpass(noise(d), 300, 2500) * 0.4
        return lowpass(blade, 2500) * 0.6 + air
    return loop(render, length)


def laser_hum(length):
    def render(L):
        d = L + 0.2
        t = t_axis(d)
        hum = np.sin(2 * np.pi * 120 * t) * 0.5 + np.sin(2 * np.pi * 240 * t) * 0.25 + np.sin(2 * np.pi * 360 * t) * 0.1
        sizzle = highpass(noise(d), 4000) * (rng.uniform(0, 1, len(t)) > 0.995) * 1.5
        return hum * (0.85 + 0.15 * np.sin(2 * np.pi * 3 * t)) + sizzle * 0.2
    return loop(render, length)


def door_slide():
    """A soft pneumatic slide: a short hush of air and a muted stop (doors open all the time: keep it gentle)."""
    d = 0.55
    hiss = bandpass(noise(d), 600, 2500) * env(int(d * SR), 0.06, 0.35, curve=2) * 0.35
    motor = lowpass(saw(60, d), 250) * env(int(d * SR), 0.08, 0.4, sustain=0.2, release=0.12) * 0.25
    stop = at(lowpass(noise(0.06), 400) * env(int(0.06 * SR), 0.002, 0.03) * 0.5, 0.46, d)
    return fade(mix(hiss, motor, stop), 0.01, 0.05)


def motor_servo(length):
    """Loop: a legged robot's joints: a thin servo whir that breathes with its gait."""
    def render(L):
        d = L + 0.2
        t = t_axis(d)
        whir = np.sin(2 * np.pi * (620 + 40 * np.sin(2 * np.pi * 1.7 * t)) * t) * 0.15
        gear = bandpass(noise(d), 1500, 3500) * (0.5 + 0.5 * np.sin(2 * np.pi * 3.4 * t)) * 0.12
        body = lowpass(signal.sawtooth(2 * np.pi * 70 * t), 300) * 0.2
        return whir + gear + body
    return loop(render, length)


def motor_tracks(length):
    """Loop: heavy tracks: a low growl with the clatter of the links."""
    def render(L):
        d = L + 0.2
        t = t_axis(d)
        growl = lowpass(signal.sawtooth(2 * np.pi * 38 * t) + 0.5 * signal.square(2 * np.pi * 19 * t), 260) * 0.5
        clatter = bandpass(noise(d), 700, 2000) * (np.sin(2 * np.pi * 11 * t) > 0.6) * 0.35
        return growl + lowpass(clatter, 2200)
    return loop(render, length)


def motor_brush(length):
    """Loop: the sweeper: a fast brush on the floor and a little vacuum fan."""
    def render(L):
        d = L + 0.2
        t = t_axis(d)
        brush = bandpass(noise(d), 2500, 7000) * (0.6 + 0.4 * np.sin(2 * np.pi * 14 * t)) * 0.3
        fan = np.sin(2 * np.pi * 410 * t) * 0.08 + lowpass(noise(d), 500) * 0.2
        return brush + fan
    return loop(render, length)


def motor_turbine(length):
    """Loop: the hunter: a high turbine whine over a deep hum (heard from afar, it should feel dangerous)."""
    def render(L):
        d = L + 0.2
        t = t_axis(d)
        whine = np.sin(2 * np.pi * (2100 + 30 * np.sin(2 * np.pi * 0.7 * t)) * t) * 0.07
        hum = np.sin(2 * np.pi * 55 * t) * 0.4 + lowpass(signal.sawtooth(2 * np.pi * 110 * t), 600) * 0.2
        air = bandpass(noise(d), 400, 1400) * 0.15
        return whine + hum + air
    return loop(render, length)


def step_light():
    """A hound's paw: a light metal tick."""
    d = 0.12
    tick = (sine(820, d) + 0.5 * sine(1730, d)) * env(int(d * SR), 0.0005, 0.03, curve=6) * 0.5
    soft = lowpass(noise(d), 500) * env(int(d * SR), 0.001, 0.02) * 0.6
    return fade(mix(tick, soft))


def spider_step():
    """A spider's foot: a dry, tiny click."""
    d = 0.06
    click = highpass(noise(d), 2500) * env(int(d * SR), 0.0003, 0.008, curve=6)
    tone = sine(2600, d) * env(int(d * SR), 0.0003, 0.01, curve=6) * 0.3
    return fade(mix(click, tone), 0.0005, 0.01)


def speaker_music(length):
    """Loop: the lure speaker: loud, tinny, cheerful music through a cheap cone (made to be noticed)."""
    bpm = 128
    beat = 60 / bpm
    roots = [60, 65, 67, 65]
    def render(L):
        out = silence(L + 1)
        for b in range(int(L / (4 * beat)) + 1):
            r = roots[b % 4]
            start = b * 4 * beat
            for k in range(4):
                out = mix(out, at(kick() * 0.6, start + k * beat, L + 1))
            for k in range(8):
                n = [r, r + 4, r + 7, r + 12][k % 4] + 12
                out = mix(out, at(pluck(n, 0.2, 4000) * 0.4, start + k * beat / 2, L + 1))
        return bandpass(out, 400, 4500) * 1.4
    return loop(render, length)


def car_engine(length):
    """Loop: the pickup's engine at idle (pitched up in game with the revs): a four-cylinder burble."""
    def render(L):
        d = L + 0.2
        t = t_axis(d)
        f = 28.0
        firing = (np.sin(2 * np.pi * f * t) > 0.3).astype(float)
        burble = lowpass(firing * (0.7 + 0.3 * rng.uniform(0, 1, len(t))), 180) * 0.8
        body = lowpass(signal.sawtooth(2 * np.pi * f * 2 * t), 420) * 0.3
        valves = bandpass(noise(d), 1200, 3000) * 0.04
        return burble + body + valves
    return loop(render, length)


def car_start():
    """Starter motor cranking, then the engine catches."""
    d = 1.4
    crank = lowpass(saw(14, 0.8) * (0.6 + 0.4 * sine(7, 0.8)), 600) * env(int(0.8 * SR), 0.02, 0.8, sustain=0.9, release=0.05) * 0.7
    catch = at(lowpass(noise(0.6), 200) * env(int(0.6 * SR), 0.005, 0.5, curve=3), 0.75, d)
    rev = at(lowpass(sweep(40, 90, 0.6, "lin"), 400) * env(int(0.6 * SR), 0.01, 0.6, curve=2) * 0.5, 0.8, d)
    return fade(mix(at(crank, 0, d), catch, rev))


def car_stop():
    """The engine shuts off: a last cough and a settling body."""
    d = 0.9
    cough = lowpass(sweep(70, 25, 0.5, "lin"), 300) * env(int(0.5 * SR), 0.005, 0.45, curve=2) * 0.6
    creak = at(bandpass(noise(0.3), 300, 900) * env(int(0.3 * SR), 0.05, 0.25, curve=2) * 0.25, 0.5, d)
    return fade(mix(at(cough, 0, d), creak))


def car_horn():
    d = 0.6
    t = t_axis(d)
    h = (signal.square(2 * np.pi * 415 * t, 0.5) + signal.square(2 * np.pi * 523 * t, 0.5)) * 0.3
    return fade(lowpass(h, 2500) * env(len(t), 0.01, d, sustain=1, release=0.08, curve=1))


def car_bump():
    """A wheel hitting a bump / the bed rattling."""
    d = 0.25
    thud = lowpass(noise(d), 180) * env(int(d * SR), 0.001, 0.08)
    rattle = bandpass(noise(d), 1500, 4000) * env(int(d * SR), 0.002, 0.12, curve=3) * 0.3
    return fade(mix(thud, rattle))


def gate_open():
    """A heavy steel gate grinding up its rails and clanging at the top."""
    d = 2.2
    g = 1.9
    tg = t_axis(g)
    grind = bandpass(noise(g), 150, 900) * (0.6 + 0.4 * np.sin(2 * np.pi * 6 * tg)) * env(len(tg), 0.1, g, sustain=0.9, release=0.2) * 0.6
    chain = bandpass(noise(g), 2000, 5000) * (np.sin(2 * np.pi * 9 * tg) > 0.7) * 0.15
    clang = at((sine(180, 0.4) + 0.5 * sine(410, 0.4)) * env(int(0.4 * SR), 0.001, 0.3, curve=4) * 0.6, 1.85, d)
    return fade(mix(at(grind + chain, 0, d), clang))


def fuel_gulp():
    """Something goes into the engine's hopper: a crunch and a happy gurgle."""
    d = 0.7
    crunch = bandpass(noise(0.2), 800, 4000) * env(int(0.2 * SR), 0.001, 0.15, curve=3) * 0.6
    gurgle = at(lowpass(sweep(180, 90, 0.45), 600) * (0.6 + 0.4 * sine(18, 0.45)) * env(int(0.45 * SR), 0.01, 0.4) * 0.5, 0.18, d)
    return fade(mix(at(crunch, 0, d), gurgle))


def heal():
    """The med table: a soft rising chime."""
    d = 0.9
    notes = [at(pluck(n, 0.6, 2500) * 0.4, 0.12 * i, d) for i, n in enumerate([72, 76, 79, 84])]
    return fade(mix(*notes))


def craft():
    """The workbench: two hammer taps and a ratchet."""
    d = 0.8
    tap = (sine(1250, 0.15) + 0.5 * sine(2900, 0.15)) * env(int(0.15 * SR), 0.0005, 0.05, curve=5) * 0.5
    ratchet = bandpass(noise(0.3), 2500, 6000) * (np.sin(2 * np.pi * 40 * t_axis(0.3)) > 0.5) * 0.3
    return fade(mix(at(tap, 0, d), at(tap, 0.18, d), at(ratchet, 0.42, d)))


def stun_blast():
    """The robot programmer's pulse: a deep thump with a falling electric zap."""
    d = 1.2
    thump = sweep(120, 30, 0.6) * env(int(0.6 * SR), 0.001, 0.5, curve=3) * 0.8
    zap = highpass(sweep(5000, 300, 0.9) * signal.square(2 * np.pi * 70 * t_axis(0.9)), 300) * env(int(0.9 * SR), 0.002, 0.8, curve=2) * 0.25
    return fade(mix(at(thump, 0, d), at(zap, 0.02, d)))


def crate_search():
    """Rummaging through a crate: cardboard and a lid."""
    d = 0.8
    rustle = bandpass(noise(0.6), 900, 5000) * (0.5 + 0.5 * np.sin(2 * np.pi * 7 * t_axis(0.6))) * env(int(0.6 * SR), 0.05, 0.5, curve=1) * 0.4
    lid = at(lowpass(noise(0.1), 500) * env(int(0.1 * SR), 0.001, 0.05), 0.65, d)
    return fade(mix(at(rustle, 0, d), lid))


def unlock():
    """A key turning a heavy lock."""
    d = 0.5
    def click():
        return highpass(noise(0.04), 2000) * env(int(0.04 * SR), 0.0005, 0.01, curve=6)
    clunk = at(lowpass(noise(0.15), 400) * env(int(0.15 * SR), 0.001, 0.08), 0.3, d)
    return fade(mix(at(click(), 0.0, d), at(click(), 0.12, d), clunk))


# --------------------------------------------------------------------------- environment

def alarm(length):
    """Loop: building alarm — a wailing siren with a bell-like edge."""
    def render(L):
        d = L + 0.3
        t = t_axis(d)
        f = 700 + 250 * np.sin(2 * np.pi * t / L)  # one wail per loop
        ph = 2 * np.pi * np.cumsum(f) / SR
        w = signal.square(ph, 0.5) * 0.4 + np.sin(ph) * 0.6
        return lowpass(w, 3000)
    return loop(render, length)


def office_hum(length):
    """Loop: HVAC rumble, mains hum, a fluorescent buzz and a distant server fan."""
    def render(L):
        d = L + 1.0
        t = t_axis(d)
        hvac = lowpass(brown(d), 300) * 0.8
        mains = np.sin(2 * np.pi * 50 * t) * 0.08 + np.sin(2 * np.pi * 100 * t) * 0.05
        buzz = bandpass(signal.sawtooth(2 * np.pi * 100 * t), 2000, 4000) * 0.02
        fan = bandpass(noise(d), 800, 1600) * 0.05
        return hvac + mains + buzz + fan
    return loop(render, length)


def hand_dryer():
    """A hand dryer running for 4 s: a roar of air over a motor whine, spinning up and down (loud on purpose: it draws robots)."""
    d = 4.0
    t = t_axis(d)
    spin = np.clip(t / 0.35, 0, 1) * np.clip((d - t) / 0.5, 0, 1)
    air = bandpass(noise(d), 300, 4200) * 0.55
    whine = np.sin(2 * np.pi * (780 + 140 * spin) * t) * 0.05 + np.sin(2 * np.pi * 1560 * t) * 0.02
    hum = lowpass(signal.sawtooth(2 * np.pi * 95 * t), 400) * 0.18
    return fade((air + whine + hum) * spin, 0.01, 0.05)


def gas_wind(length):
    """Loop: the gas outside — wind with slow gusts and a low drone."""
    def render(L):
        d = L + 1.0
        t = t_axis(d)
        gust = 0.5 + 0.3 * np.sin(2 * np.pi * t / L) + 0.2 * np.sin(2 * np.pi * 3 * t / L + 1)
        wind = bandpass(noise(d), 200, 1200) * gust
        whistle = np.sin(2 * np.pi * (520 + 30 * np.sin(2 * np.pi * 2 * t / L)) * t) * 0.03 * gust
        drone = np.sin(2 * np.pi * 41 * t) * 0.15
        return wind + whistle + drone
    return loop(render, length)


# --------------------------------------------------------------------------- music

def midi(n):
    return 440.0 * 2 ** ((n - 69) / 12)


def pad(notes, dur, bright=1200, detune=0.006, attack=0.4, release=0.5):
    t = t_axis(dur)
    out = np.zeros(len(t))
    for n in notes:
        f = midi(n)
        for d in (-detune, 0, detune):
            out += signal.sawtooth(2 * np.pi * f * (1 + d) * t + rng.uniform(0, 6.28))
    out = lowpass(out / (3 * len(notes)), bright)
    return out * env(len(t), min(attack, dur / 4), dur, sustain=0.9, release=min(release, dur / 3), curve=0.5)


def pluck(n, dur=0.5, bright=3000):
    f = midi(n)
    t = t_axis(dur)
    x = signal.square(2 * np.pi * f * t, 0.3) * 0.5 + np.sin(2 * np.pi * f * t) * 0.5
    return lowpass(x, bright) * env(len(t), 0.003, dur * 0.5, curve=3)


def bass(n, dur):
    f = midi(n)
    t = t_axis(dur)
    x = np.sin(2 * np.pi * f * t) + 0.4 * signal.sawtooth(2 * np.pi * f * t)
    return lowpass(x, 500) * env(len(t), 0.005, dur * 0.8, sustain=0.5, release=0.05, curve=2)


def hat(dur=0.06):
    return highpass(noise(dur), 7000) * env(int(dur * SR), 0.001, dur * 0.5, curve=5)


def kick():
    d = 0.3
    return sweep(150, 45, d) * env(int(d * SR), 0.001, 0.2, curve=4)


def music_menu():
    """Relaxed loop for the menu: Am - F - C - G pads with a slow arpeggio (96 BPM, 8 bars)."""
    bpm, bars = 96, 8
    beat = 60 / bpm
    L = bars * 4 * beat
    chords = [[57, 60, 64], [53, 57, 60], [48, 52, 55], [55, 59, 62]] * 2
    def render(L):
        out = silence(L + 2)
        for b, ch in enumerate(chords):
            start = b * 4 * beat
            out = mix(out, at(pad(ch, 4 * beat + 0.5, 1400) * 0.6, start, L + 2))
            out = mix(out, at(bass(ch[0] - 12, 4 * beat) * 0.35, start, L + 2))
            arp = [ch[0] + 12, ch[1] + 12, ch[2] + 12, ch[1] + 12]
            for k in range(8):
                out = mix(out, at(pluck(arp[k % 4], 0.45) * 0.18, start + k * beat / 2, L + 2))
        return out
    return loop(render, L)


def music_explore():
    """Unobtrusive bed while sneaking: dark pads that drift into each other, no rhythm, no melody (~34 s).

    It must sit under the footsteps and the robots: everything above 500 Hz is filtered away, chords change every
    8.5 s with long swells, and the only movement is a slow breathing of the filter."""
    seg = 8.5
    chords = [[38, 45, 50, 53], [38, 45, 50, 52], [36, 43, 48, 52], [38, 45, 50, 53]]
    L = seg * len(chords)
    def render(L):
        total = L + 6
        out = silence(total)
        for b, ch in enumerate(chords):
            p = pad(ch, seg + 5.0, 420, 0.003, attack=3.5, release=4.0) * 0.5
            out = mix(out, at(p, b * seg, total))
        t = np.arange(len(out)) / SR
        breath = 0.8 + 0.2 * np.sin(2 * np.pi * t / L)
        air = lowpass(brown(len(out) / SR), 160)[:len(out)] * 0.12
        return lowpass(out, 500) * breath + air
    return loop(render, L)


def music_chase():
    """Driving loop for alarms and chases: pulsing bass, kick, hats, stabs (132 BPM, 8 bars)."""
    bpm, bars = 132, 8
    beat = 60 / bpm
    L = bars * 4 * beat
    roots = [45, 45, 41, 43] * 2
    def render(L):
        out = silence(L + 1)
        for b, r in enumerate(roots):
            start = b * 4 * beat
            for k in range(8):
                note = r + (12 if k % 4 == 3 else 0)
                out = mix(out, at(bass(note, beat / 2) * 0.4, start + k * beat / 2, L + 1))
            for k in range(4):
                out = mix(out, at(kick() * 0.5, start + k * beat, L + 1))
            for k in range(8):
                out = mix(out, at(hat() * (0.12 if k % 2 else 0.06), start + k * beat / 2 + beat / 4, L + 1))
            stab = pad([r + 12, r + 15, r + 19], beat * 0.5, 2500, 0.01) * 0.35
            out = mix(out, at(stab, start + 1.5 * beat, L + 1), at(stab, start + 3.0 * beat, L + 1))
        return out
    return loop(render, L)


# --------------------------------------------------------------------------- main

# --------------------------------------------------------------------------- the laughing gas

def _formants(src, formants):
    """A voiced source through vowel formants (centre Hz, bandwidth Hz, gain)."""
    return sum(bandpass(src, max(60, f - bw / 2), f + bw / 2, 2) * g for f, bw, g in formants)


def _ha(f0, dur, loud=1.0, breathy=0.3, gen=None):
    """One "ha" of a laugh: a puff of breath (h), then a voiced /a/ with the pitch falling a little."""
    gen = gen or rng
    h = int(0.035 * SR)
    puff = bandpass(gen.uniform(-1, 1, h), 600, 3500) * env(h, 0.005, 0.03) * 0.6
    n = int(dur * SR)
    t = np.arange(n) / SR
    f = f0 * (1.0 - 0.18 * t / dur) * (1 + 0.012 * np.sin(2 * np.pi * 6.5 * t))
    phase = np.cumsum(f) / SR
    pulses = 2 * (phase % 1.0) - 1.0                          # a saw of glottal pulses
    voiced = _formants(pulses, [(780, 160, 1.0), (1250, 200, 0.55), (2600, 300, 0.25), (3400, 400, 0.1)])
    voiced = voiced / (np.max(np.abs(voiced)) + 1e-9)
    air = bandpass(gen.uniform(-1, 1, n), 700, 4000) * breathy
    syll = (voiced * (1 - breathy * 0.5) + air) * env(n, 0.012, dur * 0.9, curve=2.5)
    return np.concatenate([puff, syll]) * loud


def gas_breath():
    """Heavy breathing in the gas: a strained inhale through the teeth, a long tired exhale with a little groan."""
    gen = np.random.default_rng(71)
    i = int(0.75 * SR)
    inhale = bandpass(gen.uniform(-1, 1, i), 900, 4200) * np.linspace(0.2, 1.0, i) ** 1.5 * env(i, 0.25, 0.75, curve=1.5) * 0.55
    e = int(1.0 * SR)
    t = np.arange(e) / SR
    exhale = bandpass(gen.uniform(-1, 1, e), 300, 1800) * env(e, 0.04, 1.0, curve=2.2)
    groan = _formants(2 * ((np.cumsum(np.full(e, 118.0) * (1 - 0.1 * t)) / SR) % 1.0) - 1, [(600, 150, 1.0), (1000, 200, 0.4)])
    groan = groan / (np.max(np.abs(groan)) + 1e-9) * env(e, 0.06, 0.6, curve=2.5) * 0.25
    return fade(np.concatenate([inhale, silence(0.08), exhale + groan, silence(0.12)]))


def gas_laugh(level):
    """A laugh, ever harder: 0 a nervous giggle (three high "hee"s), 1 a laugh, 2 a helpless fit with a wheezing gasp."""
    gen = np.random.default_rng(80 + level)
    count, f0, gap, loud, breathy = [(3, 330, 0.07, 0.6, 0.45), (5, 250, 0.06, 0.85, 0.35), (9, 225, 0.045, 1.0, 0.3)][level]
    parts = []
    for k in range(count):
        dur = 0.13 + gen.uniform(0, 0.04) + (0.06 if k == 0 else 0.0)
        parts.append(_ha(f0 * (1.0 - 0.05 * k / count) * (1 + gen.uniform(-0.04, 0.04)), dur, loud * (1.0 - 0.35 * k / count), breathy, gen))
        parts.append(silence(gap + gen.uniform(0, 0.03)))
    if level == 2:
        w = int(0.55 * SR)
        wheeze = bandpass(gen.uniform(-1, 1, w), 1200, 5000) * np.linspace(0.3, 1.0, w) * env(w, 0.3, 0.55, curve=1.4) * 0.5
        parts += [silence(0.06), wheeze]
    return fade(np.concatenate(parts))


def main():
    out = sys.argv[-1] if len(sys.argv) > 1 and not sys.argv[-1].endswith(".py") else "Assets/_Game/Art/Audio"
    P = lambda cat, name: os.path.join(out, cat, name + ".wav")
    for i in range(4):
        write(P("Player", f"footstep_{i}"), footstep(i))
    write(P("Player", "jump"), jump())
    write(P("Player", "land"), land())
    write(P("Player", "hurt"), hurt())
    write(P("Player", "downed"), downed())
    write(P("Player", "revive"), revive())

    write(P("Items", "pickup"), pickup())
    write(P("Items", "drop"), drop())
    write(P("Items", "sell"), sell())
    write(P("Items", "stash"), stash())

    write(P("UI", "ui_click"), ui_click(), 0.6)
    write(P("UI", "ui_hover"), ui_hover(), 0.4)
    write(P("UI", "ui_open"), ui_open(), 0.6)
    write(P("UI", "banner"), banner(), 0.7)
    write(P("UI", "fail"), fail(), 0.8)
    write(P("UI", "level_done"), level_done(), 0.8)
    write(P("UI", "ping"), ping(), 0.6)
    write(P("UI", "mark"), mark(), 0.6)

    write(P("Robots", "robot_suspicious"), robot_suspicious())
    write(P("Robots", "robot_alert"), robot_alert())
    write(P("Robots", "robot_lost"), robot_lost())
    write(P("Robots", "robot_hacked"), robot_hacked())
    write(P("Robots", "radio"), radio())
    write(P("Robots", "turret_shot"), turret_shot())
    write(P("Robots", "robot_step"), robot_step())
    write(P("Robots", "robot_motor"), robot_motor(2.0), 0.7)
    write(P("Robots", "drone_rotor"), drone_rotor(2.0), 0.7)

    write(P("Environment", "laser_hum"), laser_hum(2.0), 0.6)
    write(P("Environment", "door_slide"), door_slide(), 0.6)
    write(P("Environment", "alarm"), alarm(2.4), 0.8)
    write(P("Environment", "office_hum"), office_hum(12.0), 0.6)
    write(P("Environment", "gas_wind"), gas_wind(12.0), 0.7)
    write(P("Environment", "hand_dryer"), hand_dryer(), 0.8)

    write(P("Music", "music_menu"), music_menu(), 0.8)
    write(P("Music", "music_explore"), music_explore(), 0.5)
    write(P("Music", "music_chase"), music_chase(), 0.8)

    # Added later: keep them last so the seeded noise of everything above stays the same.
    write(P("Robots", "seismic_thump"), seismic_thump(), 0.9)
    write(P("Robots", "camera_beep"), camera_beep(), 0.6)
    write(P("Robots", "motor_servo"), motor_servo(2.0), 0.6)
    write(P("Robots", "motor_tracks"), motor_tracks(2.0), 0.7)
    write(P("Robots", "motor_brush"), motor_brush(2.0), 0.6)
    write(P("Robots", "motor_turbine"), motor_turbine(2.0), 0.7)
    write(P("Robots", "step_light"), step_light(), 0.7)
    write(P("Robots", "spider_step"), spider_step(), 0.6)
    write(P("Items", "speaker_music"), speaker_music(7.5), 0.9)
    write(P("Items", "fuel_gulp"), fuel_gulp(), 0.8)
    write(P("Items", "heal"), heal(), 0.7)
    write(P("Items", "craft"), craft(), 0.8)
    write(P("Items", "stun_blast"), stun_blast(), 0.9)
    write(P("Items", "crate_search"), crate_search(), 0.7)
    write(P("Items", "unlock"), unlock(), 0.8)
    write(P("Car", "car_engine"), car_engine(2.0), 0.8)
    write(P("Car", "car_start"), car_start(), 0.8)
    write(P("Car", "car_stop"), car_stop(), 0.7)
    write(P("Car", "car_horn"), car_horn(), 0.7)
    write(P("Car", "car_bump"), car_bump(), 0.7)
    write(P("Environment", "gate_open"), gate_open(), 0.8)
    write(P("Player", "gas_breath"), gas_breath(), 0.7)
    for level in range(3):
        write(P("Player", f"gas_laugh_{level}"), gas_laugh(level), 0.8)


if __name__ == "__main__":
    main()

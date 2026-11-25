"""The game's sounds cut and mixed from CC0 recordings (cc0_sources.py, decoded by decode_cc0.py), realistic rather than
cartoonish: what the hands and feet of people do, doors and locks, coughing, the alarm, the guards' drives. Every clip is a
recipe below; import_cc0_sounds.py runs them after its own and lists the sources in CREDITS.md. generate_sounds.py does not
write the names made here (CLIPS).

    python Tools/Audio/cc0_sources.py
    blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Audio/decode_cc0.py
    python Tools/Audio/import_cc0_sounds.py
"""
import os

import numpy as np
from scipy import signal

SR = 44100
PCM = os.path.join(os.path.dirname(os.path.abspath(__file__)), "cache", "_pcm")
USED = {}          # clip -> the sets it was cut from
_now = [None]


# ---------------------------------------------------------------- pieces

def src(name, a=0.0, b=None):
    """A piece of a recording (seconds), its rumble cut, its edges faded by 3 ms."""
    x = np.load(os.path.join(PCM, name + ".npy")).astype(np.float64)
    USED.setdefault(_now[0], set()).add(name.split("/")[0])
    x = x[int(a * SR):int(b * SR) if b else None]
    return edges(hp(x, 35), 0.003, 0.003)


def hp(x, f, order=2):
    return signal.sosfilt(signal.butter(order, f, "high", fs=SR, output="sos"), x)


def lp(x, f, order=2):
    return signal.sosfilt(signal.butter(order, f, "low", fs=SR, output="sos"), x)


def edges(x, fin=0.003, fout=0.03):
    x = x.copy()
    i, o = min(len(x), max(1, int(fin * SR))), min(len(x), max(1, int(fout * SR)))
    x[:i] *= np.linspace(0, 1, i)
    x[-o:] *= np.linspace(1, 0, o) ** 1.5
    return x


def trim(x, db=-32, pre=0.004, post=0.02):
    """From the sound's start to where it has died away."""
    n = int(0.004 * SR)
    e = np.sqrt(np.convolve(x ** 2, np.ones(n) / n, "same"))
    on = np.flatnonzero(e > e.max() * 10 ** (db / 20))
    if len(on) == 0:
        return x
    return x[max(0, on[0] - int(pre * SR)):on[-1] + int(post * SR)]


def quiet(x, db=-46):
    """A recording's hiss under a gentle gate: what lies far under the sound itself fades out."""
    n = int(0.012 * SR)
    e = np.sqrt(np.convolve(x ** 2, np.ones(n) / n, "same"))
    g = np.clip((20 * np.log10(e / (e.max() + 1e-9) + 1e-9) - (db - 10)) / 10, 0, 1)
    return x * np.convolve(g, np.ones(n) / n, "same")


def pitch(x, ratio):
    """Faster and higher (ratio above 1) or slower and lower."""
    n = int(len(x) / ratio)
    return np.interp(np.linspace(0, len(x) - 1, n), np.arange(len(x)), x)


def norm(x, peak=1.0):
    return x / (np.abs(x).max() + 1e-9) * peak


def level(x, rms=0.16, win=0.05):
    """Equally loud, by its loudest 50 ms (and never clipping: a soft knee)."""
    n = int(win * SR)
    e = np.sqrt(np.convolve(x ** 2, np.ones(n) / n, "same")).max() + 1e-9
    return np.tanh(x / e * rms * 1.15)


def mix(*parts, dur=None):
    """(sound, at seconds, gain) … added up."""
    n = max(int(at * SR) + len(x) for x, at, _ in parts)
    if dur:
        n = int(dur * SR)
    out = np.zeros(n)
    for x, at, g in parts:
        i = int(at * SR)
        m = min(len(x), n - i)
        if m > 0:
            out[i:i + m] += x[:m] * g
    return out


def room(x, t60=0.35, wet=0.12, seed=7, bright=3500):
    """A small hard room round it."""
    r = np.random.default_rng(seed)
    n = int(t60 * SR)
    tail = lp(r.standard_normal(n) * 10 ** (-3 * np.arange(n) / n), bright)
    tail[:int(0.008 * SR)] = 0
    tail /= np.sqrt((tail ** 2).sum()) + 1e-9
    w = signal.fftconvolve(x, tail)
    return mix((x, 0, 1.0), (w, 0, wet))


def seamless(x, f=0.08):
    """A loop: its end crossfaded into its start."""
    k = int(f * SR)
    out = x[:-k].copy()
    r = np.sin(np.linspace(0, np.pi / 2, k)) ** 2
    out[:k] = x[:k] * r + x[-k:] * (1 - r)
    return out


def tiled(x, n):
    """A loop repeated to n samples, each joint crossfaded."""
    y = seamless(x, min(0.08, len(x) / SR / 4))
    return np.tile(y, n // len(y) + 2)[:n]


# ---------------------------------------------------------------- feet

KENNEY = ["rpg50/footstep%02d" % i for i in range(10)]


def step(x, length=0.30, rms=0.16):
    return level(edges(trim(x)[:int(length * SR)], 0.002, 0.06), rms)


def step_concrete(i):
    a = src(KENNEY[[0, 2, 7, 8][i]])
    return step(mix((norm(lp(a, 5200)), 0, 1.0), (norm(lp(hp(src("subway/%s" % ["04-footstep", "03-footstep", "05-footstep", "06-footstep"][i]), 60), 900)), 0.0, 0.35)))


def step_tile(i):
    a = norm(hp(src(KENNEY[[1, 4, 5, 9][i]]), 140))
    bright = norm(hp(a, 2200))
    return step(room(mix((a, 0, 1.0), (bright, 0, 0.35)), 0.28, 0.16, 11 + i, 6000), 0.34)


def step_wood(i):
    a = src("sfx100v2/sfx100v2_footstep_wood_0%d" % (i + 1))
    body = src("diffsteps/%s" % ["wood01", "wood02", "wood03", "wood02"][i])
    return step(mix((norm(a), 0, 1.0), (norm(hp(body, 70)), 0.004, 0.45)), 0.30)


def step_soft(i):
    """Linoleum, rubber, resin: the sole without the click."""
    a = lp(src(KENNEY[[3, 6, 0, 7][i]]), 1700)
    return step(mix((norm(pitch(a, 0.94)), 0, 1.0), (norm(hp(src("sfx100v2/sfx100v2_footstep_0%d" % (1 + i % 2)), 900)), 0.01, 0.16)), 0.28, 0.14)


def step_carpet(i):
    thud = lp(pitch(src(KENNEY[[0, 3, 5, 8][i]]), 0.85), 520, 3)
    brush = lp(hp(src("rpg50/cloth%d" % (i + 1)), 350), 2400)
    return step(mix((norm(thud), 0, 1.0), (norm(trim(brush))[:int(0.2 * SR)], 0.012, 0.22)), 0.26, 0.11)


def step_metal(i):
    """A steel grate: the sole, the bars' short ring, the sheet under them."""
    heel = norm(lp(src(KENNEY[[1, 2, 4, 7][i]]), 4000))
    bar = norm(pitch(src("metalsteps/metal_steps_%s" % ["09", "11", "24", "25"][i]), 0.78))
    sheet = norm(lp(src("metalwood/metal_sheet_0%d" % [1, 2, 4, 5][i]), 3000))
    return step(mix((heel, 0, 0.8), (bar, 0.004, 0.55), (sheet[:int(0.26 * SR)], 0.006, 0.22)), 0.34)


def step_wet(i):
    a = src("sfx100v2/sfx100v2_footstep_wet_0%d" % (1 + i % 3))
    if i == 3:
        a = pitch(a, 0.9)
    return step(mix((norm(a), 0, 1.0), (norm(lp(src(KENNEY[[0, 2, 7, 8][i]]), 1800)), 0.015, 0.5)), 0.36)


def jump():
    push = norm(lp(src(KENNEY[2]), 2500))
    cloth = norm(quiet(trim(src("more202/Cloth_05"))))
    breath = norm(lp(hp(src("strain/slightscream-14", 0.12, 0.46), 250), 2600))
    return edges(mix((push, 0, 0.5), (cloth, 0.02, 0.6), (pitch(breath, 0.9), 0.02, 0.16)), 0.003, 0.08)


def land():
    a = norm(lp(pitch(src(KENNEY[0]), 0.78), 2600))
    b = norm(lp(pitch(src(KENNEY[2]), 0.74), 2200))
    body = norm(lp(src("rpg50/dropLeather"), 1500))
    low = norm(lp(hp(src("subway/04-footstep"), 45), 220))
    return level(edges(mix((a, 0, 0.9), (b, 0.028, 0.8), (body, 0.0, 0.5), (low, 0, 0.7)), 0.002, 0.1), 0.22)


def hurt():
    return edges(mix((norm(lp(src("sfx100/hit_03"), 3500)), 0, 0.8), (norm(src("strain/slightscream-08", 0.02, 0.38)), 0.03, 0.55)), 0.002, 0.06)


def downed():
    groan = norm(lp(src("grunts/death pain grunts", 6.93, 7.34), 4200))
    cloth = norm(quiet(src("rpg50/cloth1")))
    body = norm(lp(pitch(src("rpg50/dropLeather"), 0.62), 900))
    low = norm(lp(pitch(src("sfx100/slam_02"), 0.6), 260))
    arm = norm(lp(pitch(src("rpg50/footstep06"), 0.8), 1500))
    return edges(mix((groan, 0, 0.55), (cloth, 0.12, 0.5), (body, 0.40, 1.0), (low, 0.41, 0.6), (arm, 0.62, 0.35)), 0.003, 0.15)


def revive():
    return edges(mix((norm(quiet(src("rpg50/clothBelt2"))), 0, 0.5), (norm(quiet(trim(src("more202/Cloth_07")))), 0.25, 0.7),
                     (norm(lp(src(KENNEY[3]), 1200)), 0.75, 0.4)), 0.003, 0.1)


def gas_cough(level_):
    """Coughing in the gas: 0 a couple of short ones, 1 three hard ones, 2 a fit."""
    x = {0: lambda: src("sick/sickness", 4.13, 4.86), 1: lambda: src("sick/sickness", 0.0, 1.08), 2: lambda: src("sick/sickness", 1.46, 3.62)}[level_]()
    y = hp(x, 90)
    return edges(quiet(y, -38), 0.004, 0.08)


# ---------------------------------------------------------------- doors, furniture

def door_open():
    latch = norm(src("doorset/qubodup-DoorOpen08"))
    creak = norm(lp(src("rpg50/creak3"), 2500))
    return edges(mix((latch, 0, 1.0), (pitch(creak, 0.8), 0.16, 0.10)), 0.002, 0.1)


def door_shut():
    return edges(mix((norm(src("doorset/qubodup-DoorClose09")), 0, 1.0), (norm(src("rpg50/metalLatch")), 0.2, 0.22)), 0.002, 0.12)


def door_heavy_open():
    bolt = norm(src("metalwood/metal_open_01"))
    body = norm(lp(src("doorset/qubodup-DoorOpen05"), 1200))
    return edges(mix((bolt, 0, 0.8), (body, 0.1, 0.7), (norm(lp(src("metalwood/metal_sheet_03"), 900)), 0.14, 0.25)), 0.002, 0.2)


def door_heavy_shut():
    return edges(mix((norm(lp(src("doorset/qubodup-DoorClose06"), 2500)), 0, 1.0), (norm(src("metalwood/metal_close_01")), 0.14, 0.7)), 0.002, 0.2)


def door_slide():
    run = norm(lp(src("loops30/rolling"), 1400))[:int(0.42 * SR)]
    air = norm(hp(src("sfx100v2/sfx100v2_air_03"), 500))
    stop = norm(lp(src("sfx100v2/sfx100v2_door_01"), 2500))
    return edges(mix((edges(run, 0.05, 0.12), 0, 0.45), (air, 0, 0.25), (stop, 0.34, 0.8)), 0.004, 0.08)


def drawer_open():
    return edges(quiet(src("various/drawer_open")), 0.004, 0.08)


def drawer_close():
    return edges(quiet(src("various/drawer_close")), 0.004, 0.08)


def steel_drawer_open():
    run = norm(quiet(src("various/drawer_open")))
    return edges(mix((hp(run, 160), 0, 0.8), (norm(src("metalwood/metal_sheet_04")), 0.0, 0.16), (norm(src("rpg50/metalLatch")), 0.0, 0.3)), 0.003, 0.1)


def steel_drawer_close():
    run = norm(quiet(src("various/drawer_close")))
    return edges(mix((hp(run, 160), 0, 0.8), (norm(lp(src("metalwood/metal_slam_01"), 2500)), 0.26, 0.45)), 0.003, 0.12)


def cupboard_open():
    return edges(mix((norm(src("sfx100/wooded_box_open")), 0, 1.0), (norm(lp(src("metalwood/wood_squeak_02"), 2500)), 0.08, 0.10)), 0.002, 0.08)


def cupboard_close():
    return edges(norm(src("metalwood/wood_close_01")), 0.002, 0.1)


def steel_cupboard_open():
    return edges(mix((norm(src("metalwood/lock_open_01", 0.42, 0.74)), 0, 0.6), (norm(src("metalwood/metal_sheet_06")), 0.1, 0.35)), 0.002, 0.12)


def steel_cupboard_close():
    return edges(mix((norm(lp(src("metalwood/metal_slam_01"), 4000)), 0, 0.9), (norm(src("rpg50/metalLatch")), 0.06, 0.3)), 0.002, 0.12)


def crate_search():
    return edges(mix((norm(src("metalwood/wood_misc_09")), 0, 0.7), (norm(quiet(src("rpg50/cloth1"))), 0.45, 0.6), (norm(src("sfx100/paper_03")), 0.85, 0.5),
                     (norm(src("metalwood/wood_misc_03")), 1.15, 0.45), (norm(quiet(src("more202/Cloth_03", 0.1, 0.9))), 0.95, 0.4)), 0.003, 0.15)


def switch_on():
    return edges(trim(src("various/switch_on")), 0.001, 0.04)


def switch_off():
    return edges(trim(src("various/switch_off")), 0.001, 0.04)


def hatch_open():
    return edges(mix((norm(src("various/manhole_open")), 0, 1.0), (norm(lp(src("metalwood/metal_slam_01"), 1200)), 0.62, 0.5)), 0.003, 0.15)


def gate_open():
    run = norm(lp(src("sfx100/door_01"), 2600))
    return edges(mix((norm(src("metalwood/metal_open_01")), 0, 0.7), (run, 0.25, 0.8), (norm(lp(src("metalwood/metal_close_01"), 2500)), 2.75, 0.6)), 0.003, 0.25)


def grille_off():
    return edges(mix((norm(src("metalwood/metal_sheet_06")), 0, 0.8), (norm(lp(src("metalwood/metal_falling_02"), 4500)), 0.35, 0.4)), 0.003, 0.15)


# ---------------------------------------------------------------- locks and work

def unlock():
    """A key turning a lock: its bolt goes over."""
    return edges(mix((norm(src("metalwood/lock_open_01")), 0, 1.0), (norm(hp(src("doorlock/UnlockDoor", 0.12, 0.40), 120)), 0.46, 0.35)), 0.002, 0.08)


def _clicks(x, n, gap=0.4):
    """The loudest separate clicks of a long take, as they come."""
    h = hp(x, 900)
    e = np.abs(h)
    out = []
    e = e.copy()
    for _ in range(n):
        k = int(np.argmax(e))
        out.append(k)
        e[max(0, k - int(gap * SR)):k + int(gap * SR)] = 0
    return out


def work_pick(i):
    x = np.load(os.path.join(PCM, "lockpick/lockpicking.npy")).astype(np.float64)
    USED.setdefault(_now[0], set()).add("lockpick")
    x = hp(x[int(24 * SR):int(46 * SR)], 250)                   # the long stretch of picking (its start is handling noise)
    k = sorted(_clicks(x, 9))[2 * i + 1]
    y = x[max(0, k - int(0.02 * SR)):k + int(0.16 * SR)]
    return edges(quiet(y, -30), 0.003, 0.05)


def work_hack(i):
    a, b = [(3, 9), (17, 26), (5, 30), (12, 21)][i]
    return edges(mix((norm(trim(src("keyboard/keypress-%03d" % a))), 0, 0.9), (norm(trim(src("keyboard/keypress-%03d" % b))), 0.085, 0.75)), 0.002, 0.04)


def work_screw(i):
    return edges([lambda: src("sfx100/tools_02", 0.0, 0.36), lambda: src("metalwood/tools_05", 0.0, 0.4), lambda: src("metalwood/tools_09", 0.0, 0.42)][i](), 0.003, 0.06)


def work_unit(i):
    """Working at the robots' lock: a contact made, a probe on its board, the coil humming for a moment."""
    relay = norm(src("breaker/switch on", 0.47, 0.66)) if i != 1 else norm(src("breaker/switch off", 0.43, 0.60))
    key = norm(trim(src("sfx100v2/sfx100v2_switch_0%d" % (1 + i % 2))))
    n = int(0.2 * SR)
    hum = lp(np.sign(np.sin(2 * np.pi * 100 * np.arange(n) / SR)), 600) * np.sin(np.linspace(0, np.pi, n))
    return edges(mix((relay, 0, 0.6), (key, 0.13 + 0.03 * i, 0.4), (hum, 0.03, 0.06)), 0.002, 0.06)


def unlock_unit():
    relay = norm(src("breaker/switch off", 0.43, 0.62))
    motor = edges(norm(lp(hp(src("loops30/machine_04", 0.0, 0.38), 180), 2600)), 0.03, 0.08)
    return edges(mix((relay, 0, 0.8), (motor, 0.10, 0.30), (norm(pitch(src("metalwood/metal_close_01"), 1.15)), 0.44, 0.75)), 0.002, 0.15)


def safe_tick():
    return edges(norm(hp(src("sfx100v2/sfx100v2_switch_02", 0.0, 0.1), 500)), 0.001, 0.04)


def clank():
    """A wrong try: the bolt strikes back."""
    return edges(mix((norm(src("metalwood/metal_hit_03")), 0, 0.9), (norm(lp(src("metalwood/metal_slam_01"), 2500)), 0.0, 0.6)), 0.001, 0.12)


def latch():
    return edges(norm(src("rpg50/metalLatch")), 0.001, 0.06)


def terminal_key():
    return edges(norm(trim(src("keyboard/keypress-012"))), 0.001, 0.04)


# ---------------------------------------------------------------- things

def pickup():
    return edges(quiet(src("rpg50/handleSmallLeather")), 0.003, 0.05)


def drop():
    return edges(mix((norm(src("rpg50/bookPlace2")), 0, 0.9), (norm(lp(src("rpg50/dropLeather"), 2200)), 0, 0.35)), 0.002, 0.08)


def stash():
    return edges(mix((norm(quiet(src("rpg50/clothBelt"))), 0, 0.7), (norm(src("rpg50/bookPlace1")), 0.4, 0.35)), 0.003, 0.08)


def heal():
    return edges(mix((norm(src("more202/Velcro_01")), 0, 0.5), (norm(src("more202/Nasal_Spray")), 0.75, 0.45), (norm(quiet(src("more202/Cloth_04"))), 1.1, 0.6)), 0.003, 0.1)


def bandage():
    return edges(mix((norm(src("more202/Velcro_03")), 0, 0.55), (norm(quiet(src("more202/Cloth_06", 0.0, 0.9))), 0.4, 0.7)), 0.003, 0.1)


def drink():
    return edges(quiet(src("more202/Drink_05")), 0.004, 0.1)


def craft():
    return edges(mix((norm(src("metalwood/tools_04")), 0, 0.6), (norm(src("metalwood/hammer_02")), 0.75, 0.8), (norm(src("metalwood/hammer_03")), 1.02, 0.7),
                     (norm(src("metalwood/tools_02")), 1.3, 0.45)), 0.002, 0.12)


def stun_blast():
    """A capacitor bank let go: the breaker, the arc's crackle, everything round it running down."""
    r = np.random.default_rng(0x57)
    n = int(0.55 * SR)
    arc = np.zeros(n)
    for _ in range(90):
        k = int(abs(r.normal(0, 0.16)) * SR)
        if k < n:
            arc[k] += r.uniform(-1, 1) * (1 - k / n)
    arc = norm(signal.sosfilt(signal.butter(2, (900, 7500), "band", fs=SR, output="sos"), arc + 0.02 * r.standard_normal(n) * np.exp(-np.arange(n) / (0.12 * SR))))
    boom = norm(lp(src("sfx100/explosion"), 350))
    return edges(mix((norm(src("breaker/switch on", 0.47, 0.66)), 0, 0.8), (arc, 0.01, 0.75), (boom, 0, 0.7), (norm(src("poweroff/MachinePowerOff", 0.0, 1.5)), 0.12, 0.4)), 0.001, 0.3)


# ---------------------------------------------------------------- machines

def robot_hacked():
    """A robot switched off: its contactor drops out, its drives run down, it sags."""
    r = np.random.default_rng(0x58)
    n = int(0.2 * SR)
    spit = np.zeros(n)
    for _ in range(14):
        spit[int(r.uniform(0, n))] = r.uniform(-1, 1)
    spit = norm(signal.sosfilt(signal.butter(2, (1500, 8000), "band", fs=SR, output="sos"), spit))
    return edges(mix((norm(src("breaker/switch off", 0.43, 0.62)), 0, 0.7), (spit, 0.03, 0.25), (norm(hp(src("poweroff/MachinePowerOff"), 60)), 0.06, 0.8),
                     (norm(lp(src("metalwood/metal_slam_01"), 1200)), 1.75, 0.3)), 0.002, 0.3)


def guard_patrol():
    x = hp(src("loops30/machine_08"), 50)
    n = len(x)
    hum = tiled(lp(src("loops30/machine_02"), 900), n)
    return seamless(mix((norm(lp(x, 3800)), 0, 1.0), (norm(hum), 0, 0.22)), 0.12)


def guard_chase():
    x = pitch(hp(src("loops30/machine_08"), 50), 1.7)
    n = len(x)
    whine = tiled(hp(src("loops30/machine_07"), 180), n)
    return seamless(mix((norm(lp(x, 5200)), 0, 1.0), (norm(whine), 0, 0.5)), 0.1)


def alarm():
    """The building's evacuation sounder: a slow whoop through a horn, its hall round it; one whoop, looped."""
    x = np.load(os.path.join(PCM, "sirens/siren_0.npy")).astype(np.float64)
    USED.setdefault(_now[0], set()).add("sirens")
    x = x[int(3.4 * SR):int(6.9 * SR)]
    # the whoop's period: where the second rise repeats the first
    f = signal.resample_poly(x, 1, 20)
    env = np.abs(signal.stft(f, SR / 20, nperseg=256, noverlap=192)[2])
    cols = env / (np.linalg.norm(env, axis=0) + 1e-9)
    hop = 64 * 20 / SR
    best, at = -1.0, 0
    for lag in range(int(1.0 / hop), int(2.2 / hop)):
        c = float((cols[:, :-lag] * cols[:, lag:]).sum(axis=0).mean())
        if c > best:
            best, at = c, lag
    period = at * hop
    k = int(0.06 * SR)
    start = int(0.2 * SR) + int(period * SR)
    y = x[start:start + int(period * SR) + k]
    y = lp(hp(y, 320), 5200)
    y = hp(np.tanh(norm(y) * 1.6), 200)
    print(f"alarm: one whoop {period:.2f} s, match {best:.2f}")
    return seamless(y, k / SR)



# ---------------------------------------------------------------- Linus the raccoon (StorageBotSounds)

RACCOON = ["raccoon/raccoon_chatter", "raccoon/raccoon_chatter_baby_01", "raccoon/raccoon_chatter_baby_02"]


def mule_step(i):
    """A paw on the floor: a boot's step made small (higher, softer, no heel), a claw's tick on top."""
    pad = lp(pitch(src(KENNEY[[1, 4, 6, 9][i]]), 1.75), 2400)
    claw = hp(pitch(src("diffsteps/stone01"), 1.6), 3200)
    return step(mix((norm(pad), 0, 1.0), (norm(trim(claw))[:int(0.05 * SR)], 0.004, 0.12)), 0.16, 0.07)


def mule_pack(i):
    """The backpack on the run: canvas rustling, the mug and the keys on their carabiners clinking."""
    cloth = lp(hp(src("rpg50/cloth%d" % (1 + i)), 300), 5000)
    keys = pitch(src("metalwood/keys_0%d" % [2, 4, 6][i]), 1.15)
    return edges(level(mix((norm(trim(cloth))[:int(0.32 * SR)], 0, 0.6), (norm(trim(keys))[:int(0.3 * SR)], 0.03, 0.5)), 0.08), 0.004, 0.08)


def mule_chatter(i):
    """His chatter when he answers a call: the raccoon's own (the adult, and the two young ones)."""
    return edges(level(quiet(src(RACCOON[i], 0.0, 0.92)), 0.15), 0.004, 0.08)


def mule_happy():
    """Petted: a pleased young chatter and a little coo."""
    return edges(level(mix((norm(quiet(src(RACCOON[2], 0.0, 0.8))), 0, 0.8), (norm(pitch(src("creature80/cute_03"), 1.08)), 0.45, 0.6)), 0.14), 0.004, 0.1)


def mule_snore():
    """Asleep, curled in the trunk: a small snore."""
    return edges(level(lp(pitch(src("creature80/snore"), 1.35), 3000), 0.06), 0.02, 0.1)


def mule_sniff():
    """Sat waiting: a sniff about."""
    return edges(level(pitch(src("creature80/nose"), 1.2), 0.07), 0.004, 0.06)


def mule_squeak():
    """The alarm: a startled squeak as he bolts for the car."""
    return edges(level(mix((norm(src("squeakyrat/qubodupSqueakyRatAttack")), 0, 0.8), (norm(quiet(src(RACCOON[1], 0.0, 0.5))), 0.12, 0.5)), 0.15), 0.003, 0.08)


def mule_leap():
    """Up into a grate: an effort chirp, the grate's sheet ringing under the paws."""
    chirp = pitch(src("creature80/cute_07"), 1.15)
    sheet = lp(src("metalwood/metal_sheet_02"), 4200)
    return edges(level(mix((norm(chirp), 0, 0.55), (norm(trim(sheet))[:int(0.4 * SR)], 0.16, 0.8)), 0.14), 0.003, 0.1)


def mule_drop():
    """Out of a grate onto the floor: the sheet's knock, the pads landing, the backpack bumping."""
    sheet = lp(src("metalwood/metal_sheet_05"), 3500)
    land = lp(pitch(src(KENNEY[3]), 1.3), 1800)
    return edges(level(mix((norm(trim(sheet))[:int(0.3 * SR)], 0, 0.6), (norm(land), 0.28, 0.9), (norm(lp(src("rpg50/cloth2"), 3000))[:int(0.25 * SR)], 0.3, 0.35)), 0.13), 0.003, 0.1)


def mule_duct(i):
    """Crawling along a duct: the thin steel giving under the paws, dull through the ceiling."""
    knock = lp(pitch(src("metalwood/metal_sheet_0%d" % [1, 3, 4][i]), 0.92 + 0.06 * i), 1600)
    return edges(level(norm(trim(knock))[:int(0.28 * SR)], 0.08), 0.003, 0.08)


def mule_hop():
    """Into or out of the car's bed: a thump on its steel floor."""
    thud = lp(pitch(src(KENNEY[0]), 1.2), 1500)
    bed = lp(src("metalwood/metal_hit_02"), 2500)
    return edges(level(mix((norm(thud), 0, 1.0), (norm(trim(bed))[:int(0.3 * SR)], 0.005, 0.35)), 0.12), 0.003, 0.1)

# ---------------------------------------------------------------- the list: (group/name, recipe, peak)

def ladder_rung(i):
    """A hand and a boot on a steel rung: the bar's short ring (thinner than a grate's: higher), the glove's slap, the sole after it."""
    bar = norm(pitch(src("metalsteps/metal_steps_%s" % ["03", "07", "14", "19"][i]), 1.22))
    hand = norm(lp(src("rpg50/cloth%d" % (1 + i % 4)), 2600))
    sole = norm(lp(src(KENNEY[[2, 5, 1, 6][i]]), 3200))
    return step(mix((hand[:int(0.12 * SR)], 0, 0.35), (bar, 0.01, 0.8), (sole, 0.09, 0.45)), 0.36, 0.13)


def tone(freq, dur, attack=0.004):
    """A plain electronic beep (a terminal's speaker): a sine with a soft third harmonic, a quick decay."""
    t = np.arange(int(dur * SR)) / SR
    x = np.sin(2 * np.pi * freq * t) + 0.18 * np.sin(2 * np.pi * freq * 3 * t)
    return x * np.minimum(1.0, t / attack) * np.exp(-t * 6.0 / dur)


def terminal_granted():
    """Access granted: two rising beeps and the relay that lets go."""
    relay = norm(src("breaker/switch off", 0.43, 0.62))
    return edges(mix((norm(tone(880, 0.13)), 0, 0.5), (norm(tone(1318, 0.22)), 0.13, 0.55), (relay, 0.34, 0.8),
                     (norm(pitch(src("metalwood/metal_close_01"), 1.1)), 0.42, 0.4)), 0.002, 0.12)


def terminal_hack_done():
    """A machine taken over from the desk: a run of quick blips, a low confirming note."""
    blips = [(norm(tone(1500 + 220 * k, 0.045)), 0.05 * k, 0.4) for k in range(4)]
    return edges(mix(*blips, (norm(tone(520, 0.28)), 0.24, 0.6), (norm(src("breaker/switch off", 0.43, 0.55)), 0.24, 0.35)), 0.002, 0.1)


def console_solved():
    """One of the vault's consoles gives in: a confirming note, a heavy latch drawn back."""
    return edges(mix((norm(tone(660, 0.2)), 0, 0.5), (norm(src("metalwood/lock_open_01")), 0.14, 0.9), (norm(lp(src("metalwood/metal_slam_01"), 1800)), 0.42, 0.55)), 0.002, 0.15)


def vault_unlocked():
    """The last console: three bolts drawn one after another along the door, the mechanism winding down."""
    bolt = norm(lp(src("metalwood/metal_slam_01"), 2600))
    run = edges(norm(lp(hp(src("loops30/machine_04", 0.0, 0.9), 120), 1800)), 0.05, 0.3)
    return edges(mix((norm(tone(440, 0.3)), 0, 0.35), (bolt, 0.25, 0.9), (norm(pitch(bolt, 0.9)), 0.62, 0.9), (norm(pitch(bolt, 0.8)), 1.0, 1.0),
                     (run, 0.2, 0.3), (norm(src("metalwood/metal_open_01")), 1.25, 0.6)), 0.003, 0.3)


CLIPS = []
for _name, _fn in (("concrete", step_concrete), ("tile", step_tile), ("wood", step_wood), ("soft", step_soft), ("carpet", step_carpet), ("metal", step_metal), ("wet", step_wet)):
    for _i in range(4):
        CLIPS.append((f"Player/step_{_name}_{_i}", lambda f=_fn, i=_i: f(i), None))
CLIPS += [
    ("Player/jump", jump, 0.5), ("Player/land", land, None), ("Player/hurt", hurt, 0.8), ("Player/downed", downed, 0.85), ("Player/revive", revive, 0.6),
    ("Player/gas_cough_0", lambda: gas_cough(0), 0.7), ("Player/gas_cough_1", lambda: gas_cough(1), 0.8), ("Player/gas_cough_2", lambda: gas_cough(2), 0.85),
    ("Environment/door_open", door_open, 0.7), ("Environment/door_shut", door_shut, 0.8),
    ("Environment/door_heavy_open", door_heavy_open, 0.8), ("Environment/door_heavy_shut", door_heavy_shut, 0.85), ("Environment/door_slide", door_slide, 0.6),
    ("Environment/drawer_open", drawer_open, 0.6), ("Environment/drawer_close", drawer_close, 0.65),
    ("Environment/steel_drawer_open", steel_drawer_open, 0.6), ("Environment/steel_drawer_close", steel_drawer_close, 0.7),
    ("Environment/cupboard_open", cupboard_open, 0.55), ("Environment/cupboard_close", cupboard_close, 0.65),
    ("Environment/steel_cupboard_open", steel_cupboard_open, 0.55), ("Environment/steel_cupboard_close", steel_cupboard_close, 0.7),
    ("Environment/switch_on", switch_on, 0.5), ("Environment/switch_off", switch_off, 0.5),
    ("Environment/hatch_open", hatch_open, 0.6), ("Environment/gate_open", gate_open, 0.8), ("Environment/grille_off", grille_off, 0.7),
    ("Environment/alarm", alarm, 0.6),
    ("Items/crate_search", crate_search, 0.7), ("Items/unlock", unlock, 0.75), ("Items/unlock_unit", unlock_unit, 0.75),
    ("Items/work_pick_0", lambda: work_pick(0), 0.5), ("Items/work_pick_1", lambda: work_pick(1), 0.5), ("Items/work_pick_2", lambda: work_pick(2), 0.5), ("Items/work_pick_3", lambda: work_pick(3), 0.5),
    ("Items/work_hack_0", lambda: work_hack(0), 0.5), ("Items/work_hack_1", lambda: work_hack(1), 0.5), ("Items/work_hack_2", lambda: work_hack(2), 0.5), ("Items/work_hack_3", lambda: work_hack(3), 0.5),
    ("Items/work_screw_0", lambda: work_screw(0), 0.55), ("Items/work_screw_1", lambda: work_screw(1), 0.55), ("Items/work_screw_2", lambda: work_screw(2), 0.55),
    ("Items/work_unit_0", lambda: work_unit(0), 0.5), ("Items/work_unit_1", lambda: work_unit(1), 0.5), ("Items/work_unit_2", lambda: work_unit(2), 0.5),
    ("Items/safe_tick", safe_tick, 0.45), ("Items/clank", clank, 0.85), ("Items/latch", latch, 0.6), ("Items/terminal_key", terminal_key, 0.5),
    ("Items/pickup", pickup, 0.55), ("Items/drop", drop, 0.65), ("Items/stash", stash, 0.55),
    ("Items/heal", heal, 0.6), ("Items/bandage", bandage, 0.35), ("Items/drink", drink, 0.55), ("Items/craft", craft, 0.75), ("Items/stun_blast", stun_blast, 0.9),
    ("Player/ladder_rung_0", lambda: ladder_rung(0), None), ("Player/ladder_rung_1", lambda: ladder_rung(1), None),
    ("Player/ladder_rung_2", lambda: ladder_rung(2), None), ("Player/ladder_rung_3", lambda: ladder_rung(3), None),
    ("Items/terminal_granted", terminal_granted, 0.7), ("Items/terminal_hack_done", terminal_hack_done, 0.65),
    ("Items/console_solved", console_solved, 0.75), ("Items/vault_unlocked", vault_unlocked, 0.9),
    ("Robots/robot_hacked", robot_hacked, 0.8), ("Robots/guard_patrol", guard_patrol, 0.55), ("Robots/guard_chase", guard_chase, 0.65),
]
# Linus the raccoon
CLIPS += [(f"Mule/mule_step_{i}", lambda i=i: mule_step(i), None) for i in range(4)]
CLIPS += [(f"Mule/mule_pack_{i}", lambda i=i: mule_pack(i), None) for i in range(3)]
CLIPS += [(f"Mule/mule_chatter_{i}", lambda i=i: mule_chatter(i), None) for i in range(3)]
CLIPS += [(f"Mule/mule_duct_{i}", lambda i=i: mule_duct(i), None) for i in range(3)]
CLIPS += [("Mule/mule_happy", mule_happy, None), ("Mule/mule_snore", mule_snore, None), ("Mule/mule_sniff", mule_sniff, None),
          ("Mule/mule_squeak", mule_squeak, None), ("Mule/mule_leap", mule_leap, None), ("Mule/mule_drop", mule_drop, None),
          ("Mule/mule_hop", mule_hop, None)]

# what the generator no longer writes (the recordings' names, and those they replaced)
REPLACED = {name for name, _, _ in CLIPS} | {f"Player/gas_laugh_{i}" for i in range(3)} | {f"Player/footstep_{i}" for i in range(4)} | {
    "Items/work_hack", "Items/work_pick", "Items/work_screw", "Items/work_unit"}


def render():
    """Every clip, ready to write: (group/name, samples, peak or None: already levelled)."""
    for name, fn, peak in CLIPS:
        _now[0] = name
        yield name, fn(), peak

"""The CC0 recordings the game's sounds are cut from (all from OpenGameArt, licence CC0 1.0), and their download.

    python -I Tools/Audio/cc0_sources.py       downloads what is missing into Tools/Audio/cache/<set>/ (ignored by git) and unpacks it

import_cc0_sounds.py cuts the clips out of them and lists those it used in Assets/_Game/Art/Audio/CREDITS.md.
"""
import os
import subprocess
import time
import urllib.parse
import urllib.request
import zipfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
CACHE = os.path.join(ROOT, "Tools", "Audio", "cache")
UA = {"User-Agent": "generic-asset-client"}
SITE = "https://opengameart.org"
FILES = SITE + "/sites/default/files/"

# (set, its page, title, author, the files)
SETS = [
    ("doorset", "door-open-door-close-set", "Door Open, Door Close Set", "qubodup", ["qubodup-DoorSet.7z"]),
    ("sfx100", "100-cc0-sfx", "100 CC0 SFX", "rubberduck", ["100-CC0-SFX_0.zip"]),
    ("sfx100v2", "100-cc0-sfx-2", "100 CC0 SFX #2", "rubberduck", ["sfx_100_v2.zip"]),
    ("metalwood", "100-cc0-metal-and-wood-sfx", "100 CC0 metal and wood SFX", "rubberduck", ["100-CC0-wood-metal-SFX.zip"]),
    ("doorlock", "door-lock-sounds", "Door Lock Sounds", "Cough-E", ["DoorLockSounds.zip"]),
    ("lockpick", "lockpicking-sound", "Lockpicking Sound", "Tegurd", ["lockpicking.wav"]),
    ("more202", "202-more-sound-effects", "202 More Sound Effects", "OwlishMedia", ["MoreSounds.zip"]),
    ("various", "various-sound-effects", "Various Sound Effects", "laleksic", ["sounds_8.zip"]),
    ("woodfloor", "steps-in-wood-floor", "Steps in wood floor", "mikeask", ["steps%20in%20wood%20floor.wav"]),
    ("diffsteps", "different-steps-on-wood-stone-leaves-gravel-and-mud", "Different steps on wood, stone, leaves, gravel and mud", "TinyWorlds", ["%5Bkdd%5DDifferentSteps_0.zip"]),
    ("subway", "footsteps-0", "Footsteps", "GboxMikeFozzy", ["01-footstep_0.ogg", "02-footstep.ogg", "03-footstep.ogg", "04-footstep.ogg", "05-footstep.ogg", "06-footstep.ogg"]),
    ("metalsteps", "metal-footsteps-on-concrete", "Metal footsteps on concrete", "Thimras", ["metal_steps_48k24b.7z"]),
    ("rpg50", "50-rpg-sound-effects", "50 RPG sound effects", "Kenney", ["RPGsounds_Kenney.zip"]),
    ("water40", "40-cc0-water-splash-slime-sfx", "40 CC0 water / splash / slime SFX", "rubberduck", ["water-splash-slime-sfx.zip"]),
    ("oldcough", "old-man-cough", "Old Man Cough", "AntumDeluge", ["old-man-cough.flac"]),
    ("sick", "sick-noises", "Sick Noises", "frosty ham", ["sickness.wav"]),
    ("strain", "15-vocal-male-strainhurtpainjump-sounds", "15 vocal male strain/hurt/pain/jump sounds", "qubodup", ["slightscreams.7z"]),
    ("grunts", "grunts-male-death-and-pain", "grunts of male death and pain", "thebardofblasphemy", ["death%20pain%20grunts.wav"]),
    ("keyboard", "keyboard-soundpack-1-typing-and-single-keystrokes", "Keyboard Soundpack #1", "unicaegames", ["unicae_games_keyboard_soundpack_1_0.zip"]),
    ("poweroff", "machine-shutting-down", "Machine shutting down", "Cough-E", ["MachinePowerOff.ogg"]),
    ("breaker", "sfx-circuit-breaker", "SFX - Circuit breaker", "CleytonKauffman", ["Circuit%20Breaker.zip"]),
    ("loops30", "30-cc0-sfx-loops", "30 CC0 SFX loops", "rubberduck", ["sfx_loops.zip"]),
    ("sirens", "sirens-and-alarm-noise", "Sirens and Alarm Noise", "aquinn", ["siren_0.mp3"]),
]


def main():
    for name, page, title, _, files in SETS:
        folder = os.path.join(CACHE, name)
        os.makedirs(folder, exist_ok=True)
        for f in files:
            path = os.path.join(folder, os.path.basename(urllib.parse.unquote(f)))
            if not os.path.exists(path):
                with urllib.request.urlopen(urllib.request.Request(FILES + f, headers=UA), timeout=120) as r:
                    data = r.read()
                with open(path, "wb") as out:
                    out.write(data)
                print("downloaded", name, os.path.basename(path), len(data))
                time.sleep(1)
            unpacked = os.path.join(folder, "x")
            if path.endswith((".zip", ".7z")) and not os.path.isdir(unpacked):
                os.makedirs(unpacked)
                if path.endswith(".zip"):
                    with zipfile.ZipFile(path) as z:
                        z.extractall(unpacked)
                else:
                    # bsdtar (part of Windows 10 and later) reads 7z
                    subprocess.run([r"C:\Windows\System32\tar.exe", "-xf", path, "-C", unpacked], check=True)


if __name__ == "__main__":
    main()

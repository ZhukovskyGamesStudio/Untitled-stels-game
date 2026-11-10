"""
Robot voice lines for «Айтишники: вылазка» (Windows only).

    python Tools/Audio/generate_voices.py [Assets/_Game/Art/Audio]

Speaks each line with the Russian Windows text-to-speech voice (System.Speech, «Microsoft Irina Desktop»; any ru-RU voice
works), then makes it sound like a security robot: lower and slower, ring modulation, a narrow radio band, a metallic comb,
a little bit crushing and a radio chirp in front. Writes 16-bit mono WAV files into <out>/Robots/voice_<name>.wav.
The text-to-speech output is stable for the same voice, so re-running gives the same files.
"""
import os
import subprocess
import sys
import tempfile
import wave

import numpy as np
from scipy import signal

SR = 44100

LINES = {
    "voice_spotted": "Обнаружен нарушитель.",
    "voice_chase": "Веду преследование.",
    "voice_lost": "Нарушитель потерян.",
    "voice_eliminated": "Нарушитель устранён.",
}

SPEAK = r"""
Add-Type -AssemblyName System.Speech
$s = New-Object System.Speech.Synthesis.SpeechSynthesizer
$voice = $s.GetInstalledVoices() | Where-Object { $_.VoiceInfo.Culture.Name -eq 'ru-RU' } | Select-Object -First 1
if ($voice -eq $null) { Write-Error 'No ru-RU text-to-speech voice installed'; exit 1 }
$s.SelectVoice($voice.VoiceInfo.Name)
$s.Rate = 2
$fmt = New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo 44100, ([System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen), ([System.Speech.AudioFormat.AudioChannel]::Mono)
$s.SetOutputToWaveFile($env:VOICE_OUT, $fmt)
$s.Speak($env:VOICE_TEXT)
$s.Dispose()
"""


def speak(text, path):
    env = dict(os.environ, VOICE_OUT=path, VOICE_TEXT=text)
    subprocess.run(["powershell", "-NoProfile", "-NonInteractive", "-Command", SPEAK], check=True, env=env)
    with wave.open(path, "rb") as w:
        assert w.getframerate() == SR and w.getsampwidth() == 2 and w.getnchannels() == 1
        data = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float64) / 32768.0
    return data


def trim(x, threshold=0.01):
    idx = np.where(np.abs(x) > threshold)[0]
    return x if len(idx) == 0 else x[max(0, idx[0] - 200): idx[-1] + 2000]


def bandpass(x, lo, hi, order=3):
    b, a = signal.butter(order, [lo / (SR / 2), hi / (SR / 2)], "band")
    return signal.lfilter(b, a, x)


def chirp(f0, f1, dur):
    t = np.arange(int(dur * SR)) / SR
    f = np.linspace(f0, f1, len(t))
    x = np.sign(np.sin(2 * np.pi * np.cumsum(f) / SR)) * 0.25
    return x * np.minimum(1, np.minimum(t / 0.004, (dur - t) / 0.01))


def robotize(x):
    # Lower and slower: play 10% slower.
    x = signal.resample(x, int(len(x) / 0.9))
    t = np.arange(len(x)) / SR
    # Ring modulation with a low tone + the dry voice: the classic robot timbre, still intelligible.
    x = 0.55 * x + 0.75 * x * np.sin(2 * np.pi * 70 * t)
    # Metallic comb (a short resonant delay).
    d = int(0.006 * SR)
    y = x.copy()
    for i in range(d, len(y)):
        y[i] += 0.45 * y[i - d]
    # Radio band and a little bit crushing.
    y = bandpass(y, 280, 3600)
    y = np.round(y / np.max(np.abs(y)) * 48) / 48
    y = bandpass(y, 250, 4200, 2)
    return y


def write(path, x, peak=0.9):
    x = x / max(np.max(np.abs(x)), 1e-9) * peak
    data = (np.clip(x, -1, 1) * 32767).astype(np.int16)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())
    print("wrote", path, f"{len(data) / SR:.2f}s")


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join("Assets", "_Game", "Art", "Audio")
    with tempfile.TemporaryDirectory() as tmp:
        for name, text in LINES.items():
            voice = robotize(trim(speak(text, os.path.join(tmp, name + ".wav"))))
            # Radio chirp in front (up for alarms, down for "lost"), then the line.
            up = name not in ("voice_lost", "voice_eliminated")
            pre = np.concatenate([chirp(900, 1500, 0.07) if up else chirp(1500, 900, 0.07), np.zeros(int(0.05 * SR))])
            x = np.concatenate([pre * 0.6, voice / max(np.max(np.abs(voice)), 1e-9), np.zeros(int(0.05 * SR))])
            write(os.path.join(out, "Robots", name + ".wav"), x)


if __name__ == "__main__":
    main()

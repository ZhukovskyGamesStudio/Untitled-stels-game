"""
Starts a program minimized without taking the keyboard focus and waits for it (Tools/dev.sh run).

    python Tools/launch_quiet.py <exe> [args...]

The autotest player then keeps its window behind all others and gives the focus back to the window that had it
(DevWindow.StayBehind), so a developer can keep working while scenarios run. FRONT=1 Tools/dev.sh run … starts it normally.
"""
import ctypes
import subprocess
import sys

SW_SHOWMINNOACTIVE = 7

# The window the developer works in: the player hands the focus back to it whenever it grabs it (-return-focus).
previous = ctypes.windll.user32.GetForegroundWindow() or 0

info = subprocess.STARTUPINFO()
info.dwFlags |= subprocess.STARTF_USESHOWWINDOW
info.wShowWindow = SW_SHOWMINNOACTIVE
sys.exit(subprocess.call(sys.argv[1:] + ["-return-focus", str(previous)], startupinfo=info))

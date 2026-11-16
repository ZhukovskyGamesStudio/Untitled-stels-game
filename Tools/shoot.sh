#!/usr/bin/env bash
# KINDS=Restroom,Meeting Tools/shoot.sh <scenario> <seed> <level> [style] [port] — one quiet autopilot window, shots in Builds/shots/<scenario>_<seed>_<level>
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
sc="$1"; seed="$2"; level="$3"; style="${4:-S06_Storybook}"; port="${5:-7801}"
dir="$ROOT/Builds/shots/${sc}_${seed}_${level}"; rm -rf "$dir"; mkdir -p "$dir"
python "$ROOT/Tools/launch_quiet.py" "$ROOT/Builds/Windows/StealthPrototype.exe" -nosteam -host-local -port "$port" -autotest "$sc" -seed "$seed" -level "$level" -peaceful -style "$style" ${KINDS:+-kinds $KINDS} \
  -shotdir "$(cygpath -w "$dir")" -screen-width ${RESW:-1600} -screen-height ${RESH:-900} -screen-fullscreen 0 -logFile "$(cygpath -w "$ROOT/Builds/shoot_${sc}_${seed}_${level}.log")"
grep -h "AUTOTEST" "$ROOT/Builds/shoot_${sc}_${seed}_${level}.log" | grep -v screenshot | cut -c1-400
grep -h -i "exception" "$ROOT/Builds/shoot_${sc}_${seed}_${level}.log" | sort | uniq -c | head -5
ls "$dir" | wc -l

#!/usr/bin/env bash
# Dev loop helpers (Git Bash on Windows). Usage:
#   Tools/dev.sh tests            compile + EditMode tests
#   Tools/dev.sh assets           rebuild generated assets and the scene
#   Tools/dev.sh player           build Windows player
#   Tools/dev.sh run <scenario> [clients]   run the build with -autotest <scenario>
set -u
UNITY="${UNITY:-/d/Apps/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
P="$(cygpath -w "$ROOT" 2>/dev/null || echo "$ROOT")"
LOGS="$ROOT/Logs"
mkdir -p "$LOGS"

case "${1:-}" in
  tests)
    "$UNITY" -batchmode -nographics -projectPath "$P" -runTests -testPlatform EditMode \
      -testResults "$P/Logs/tests.xml" -logFile "$P/Logs/tests.log"
    code=$?
    grep -E "error CS" "$LOGS/tests.log" | sort -u | head -30
    python - "$LOGS/tests.xml" <<'EOF'
import sys, xml.etree.ElementTree as ET
try:
    r = ET.parse(sys.argv[1]).getroot()
except Exception as e:
    print("no test results:", e); sys.exit(0)
print("tests: total=%s passed=%s failed=%s" % (r.get("total"), r.get("passed"), r.get("failed")))
for tc in r.iter("test-case"):
    if tc.get("result") == "Failed":
        msg = tc.find("failure/message")
        print("FAILED", tc.get("fullname"), "-", (msg.text or "").strip()[:400] if msg is not None else "")
EOF
    exit $code ;;
  assets)
    "$UNITY" -batchmode -nographics -projectPath "$P" -executeMethod Stealth.EditorTools.PrototypeBuilder.BuildAllBatch -logFile "$P/Logs/build_assets.log"
    code=$?
    grep -E "error CS|Exception|\[Builder\] (Prototype|.*hash.*0)" "$LOGS/build_assets.log" | sort -u | head -30
    exit $code ;;
  player)
    "$UNITY" -batchmode -nographics -projectPath "$P" -executeMethod Stealth.EditorTools.PrototypeBuilder.BuildPlayerBatch -logFile "$P/Logs/build_player.log"
    code=$?
    grep -E "error CS|\[Builder\] Player" "$LOGS/build_player.log" | sort -u | head -30
    exit $code ;;
  run)
    scenario="${2:-full}"; clients="${3:-0}"
    shots="$P/Builds/shots/$scenario"
    rm -rf "$ROOT/Builds/shots/$scenario"; mkdir -p "$ROOT/Builds/shots/$scenario"
    exe="$ROOT/Builds/Windows/StealthPrototype.exe"
    rm -f "$ROOT"/Builds/client*.log
    linger=""; [ "$clients" -gt 0 ] && linger="-autotest-linger"
    "$exe" -nosteam -host-local -autotest "$scenario" $linger -shotdir "$shots" -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -logFile "$P/Builds/host.log" &
    pids=($!)
    for i in $(seq 1 "$clients"); do
      sleep 5
      "$exe" -nosteam -join-local 127.0.0.1 -autotest "client" -shotdir "$shots" -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -logFile "$P/Builds/client$i.log" &
      pids+=($!)
    done
    for pid in "${pids[@]}"; do wait "$pid"; done
    shopt -s nullglob
    logs=("$ROOT/Builds/host.log" "$ROOT"/Builds/client*.log)
    grep -h -E "AUTOTEST" "${logs[@]}" | grep -v screenshot
    grep -h -i -E "exception|error" "${logs[@]}" | grep -v -i -E "memorysetup|d3d12|licens" | sort | uniq -c | head -20
    ls "$ROOT/Builds/shots/$scenario" ;;
  *) echo "usage: $0 tests|assets|player|run <scenario> [clients]"; exit 1 ;;
esac

#!/usr/bin/env bash
# Dev loop helpers (Git Bash on Windows). Usage:
#   Tools/dev.sh tests            compile + every EditMode test (FILTER=<name>: only those). With the editor open (its MCP
#                                bridge answers on port 6400) they run there, no second Unity is started; BATCH=1: batchmode anyway
#   Tools/dev.sh plans            pictures of floor plans: Logs/plans/plan_<seed>_<house>.png
#   Tools/dev.sh assets           build missing assets (FORCE=1: regenerate everything from code)
#   Tools/dev.sh player           build Windows player
#   Tools/dev.sh run <scenario> [clients]   run the build with -autotest <scenario> (SEEDS=7,42,3: one window per seed, at once)
#   Tools/dev.sh cycle           ONE Unity launch: missing assets + EditMode tests + development player (scripts only when
#                                only code changed). FILTER / FORCE / REBUILD as above; NOASSETS / NOTESTS / NOPLAYER / FULL = 1
set -u
UNITY="${UNITY:-/d/Apps/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
P="$(cygpath -w "$ROOT" 2>/dev/null || echo "$ROOT")"
LOGS="$ROOT/Logs"
mkdir -p "$LOGS"

case "${1:-}" in
  tests)
    if [ -z "${BATCH:-}" ] && python "$ROOT/Tools/unity_mcp.py" ping >/dev/null 2>&1; then
      # the editor is open (batchmode would not start): compile there, then its test runner
      echo "the editor is open: the tests run in it (BATCH=1 to close it and use batchmode)"
      python "$ROOT/Tools/unity_mcp.py" refresh_unity '{"mode":"force","scope":"all","compile":"request"}' >/dev/null 2>&1
      sleep 3
      for i in $(seq 1 120); do python "$ROOT/Tools/unity_mcp.py" ping >/dev/null 2>&1 && break; sleep 1; done   # (the bridge is down while it reloads)
      python "$ROOT/Tools/unity_mcp.py" read_console '{"action":"get","types":["error"],"count":50}' 2>/dev/null | grep -o 'error CS[^"\\]*' | sort -u | head -30
      GROUP=1 "$ROOT/Tools/unity_test.sh" "${FILTER:-Stealth.Tests}"
      exit $? ;
    fi
    rm -f "$LOGS/tests.xml"
    "$UNITY" -batchmode -nographics -projectPath "$P" -runTests -testPlatform EditMode \
      -testResults "$P/Logs/tests.xml" -logFile "$P/Logs/tests.log" ${FILTER:+-testFilter "$FILTER"}
    code=$?
    grep -E "error CS" "$LOGS/tests.log" | sort -u | head -30
    PYTHONIOENCODING=utf-8 python - "$LOGS/tests.xml" <<'EOF'
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
  plans)
    rm -rf "$LOGS/plans"
    FILTER="Stealth.Tests.PlanPictures" "$0" tests
    ls "$LOGS/plans" 2>/dev/null
    exit 0 ;;
  assets)
    "$UNITY" -batchmode -nographics -projectPath "$P" -executeMethod Stealth.EditorTools.AssetBuilder.BuildAllBatch ${FORCE:+-force} -logFile "$P/Logs/build_assets.log"
    code=$?
    grep -E "error CS|Exception|\[Builder\] Assets built" "$LOGS/build_assets.log" | sort -u | head -30
    exit $code ;;
  player)
    "$UNITY" -batchmode -nographics -projectPath "$P" -executeMethod Stealth.EditorTools.AssetBuilder.BuildPlayerBatch -logFile "$P/Logs/build_player.log"
    code=$?
    grep -E "error CS|\[Builder\] Player" "$LOGS/build_player.log" | sort -u | head -30
    exit $code ;;
  cycle)
    start=$(date +%s)
    "$UNITY" -batchmode -nographics -projectPath "$P" -executeMethod Stealth.EditorTools.DevCycle.RunBatch ${FORCE:+-force} -logFile "$P/Logs/cycle.log"
    code=$?
    grep -E "error CS|Exception|\[Builder\] Assets built|\[Cycle\]" "$LOGS/cycle.log" | sed 's/^\[Cycle\] //' | awk '!seen[$0]++' | head -60
    echo "cycle: $(( $(date +%s) - start )) s, exit $code"
    exit $code ;;
  run)
    scenario="${2:-full}"; clients="${3:-0}"
    if [ -n "${SEEDS:-}" ] && [ "$clients" -eq 0 ]; then
      # one quiet window per seed, all at once: shots in Builds/shots/<scenario>_<seed>, logs in Builds/host_<seed>.log
      exe="$ROOT/Builds/Windows/USG.exe"
      pids=()
      port=7790
      for seed in ${SEEDS//,/ }; do
        dir="$ROOT/Builds/shots/${scenario}_$seed"; rm -rf "$dir"; mkdir -p "$dir"
        port=$((port + 1))   # each window hosts on its own UDP port
        python "$ROOT/Tools/launch_quiet.py" "$exe" -nosteam -host-local -port $port -autotest "$scenario" -seed "$seed" ${PEACEFUL:+-peaceful} ${LEVEL:+-level $LEVEL}           -shotdir "$(cygpath -w "$dir" 2>/dev/null || echo "$dir")" -screen-width ${RESW:-1280} -screen-height ${RESH:-720} -screen-fullscreen 0           -logFile "$P/Builds/host_$seed.log" &
        pids+=($!)
      done
      for pid in "${pids[@]}"; do wait "$pid"; done
      for seed in ${SEEDS//,/ }; do
        echo "== seed $seed"
        grep -h -E "AUTOTEST" "$ROOT/Builds/host_$seed.log" | grep -v screenshot
        grep -h -i -E "exception" "$ROOT/Builds/host_$seed.log" | sort | uniq -c | head -5
      done
      exit 0
    fi
    shots="$P/Builds/shots/$scenario"
    rm -rf "$ROOT/Builds/shots/$scenario"; mkdir -p "$ROOT/Builds/shots/$scenario"
    exe="$ROOT/Builds/Windows/USG.exe"
    rm -f "$ROOT"/Builds/client*.log
    linger=""; [ "$clients" -gt 0 ] && linger="-autotest-linger"
    cscenario="client"; [ "$scenario" != "full" ] && cscenario="$scenario"
    # Muted and behind other windows (the developer keeps working); SOUND=1 / FRONT=1 to hear / watch it.
    launch=(python "$ROOT/Tools/launch_quiet.py" "$exe")
    [ -n "${FRONT:-}" ] && launch=("$exe")
    extra=(); [ -n "${SOUND:-}" ] && extra+=(-autotest-sound); [ -n "${FRONT:-}" ] && extra+=(-autotest-front)
    "${launch[@]}" -nosteam -host-local "${extra[@]}" -autotest "$scenario" $linger ${SEED:+-seed $SEED} ${PEACEFUL:+-peaceful} ${LEVEL:+-level $LEVEL} ${HOSTILE:+-hostile} ${KINDS:+-kinds $KINDS} -shotdir "$shots" -screen-width ${RESW:-1280} -screen-height ${RESH:-720} -screen-fullscreen 0 -logFile "$P/Builds/host.log" &
    pids=($!)
    for i in $(seq 1 "$clients"); do
      sleep 5
      "${launch[@]}" -nosteam -join-local 127.0.0.1 "${extra[@]}" -autotest "$cscenario" -shotdir "$shots" -screen-width ${RESW:-1280} -screen-height ${RESH:-720} -screen-fullscreen 0 -logFile "$P/Builds/client$i.log" &
      pids+=($!)
    done
    for pid in "${pids[@]}"; do wait "$pid"; done
    shopt -s nullglob
    logs=("$ROOT/Builds/host.log" "$ROOT"/Builds/client*.log)
    grep -h -E "AUTOTEST" "${logs[@]}" | grep -v screenshot
    grep -h -i -E "exception|error" "${logs[@]}" | grep -v -i -E "memorysetup|d3d12|licens" | sort | uniq -c | head -20
    ls "$ROOT/Builds/shots/$scenario" ;;
  *) echo "usage: $0 cycle|tests|assets|player|run <scenario> [clients]"; exit 1 ;;
esac

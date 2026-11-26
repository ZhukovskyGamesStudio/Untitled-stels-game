#!/usr/bin/env bash
# Bakes every piece of furniture's own maps (Tools/Blender/shapebake.py): runs the furniture scripts with SHAPEBAKE=all,
# two at a time, and writes Logs/shapebake/<script>.log. Hours of CPU. Then: Tools/dev.sh assets (the builder lists the
# pictures in Data/ShapeLibrary) and REBUILD=<the props> so their prefabs name their slices.
#   Tools/Blender/shapebake_all.sh [script ...]        (default: all the furniture scripts)
#   SIZE=2048 (the pictures' side; the game's arrays want one size and read it full, half or a quarter)   JOBS=3
set -u
cd "$(dirname "$0")/../.."
BLENDER="${BLENDER:-C:/Program Files (x86)/Steam/steamapps/common/Blender/blender.exe}"
SCRIPTS=("$@")
[ ${#SCRIPTS[@]} -eq 0 ] && SCRIPTS=(rooms2_work rooms2_tech rooms2_home rooms2_body rooms2_fun rooms2_special rooms_paper rooms_fit rooms_fun rooms_wet hospital garage apartments poi)
mkdir -p Logs/shapebake
run() {
    local s=$1
    SHAPEBAKE=all SHAPEBAKE_SIZE="${SIZE:-2048}" "$BLENDER" -b --factory-startup --python-exit-code 1 \
        --python "Tools/Blender/$s.py" -- Assets/_Game/Art/Models > "Logs/shapebake/$s.log" 2>&1
    echo "$s: exit $? — $(grep -c '^SHAPEBAKE [a-z]' "Logs/shapebake/$s.log") baked, $(grep -c 'SHAPEBAKE FAILED' "Logs/shapebake/$s.log") failed"
}
i=0
for s in "${SCRIPTS[@]}"; do
    run "$s" &
    i=$((i + 1))
    if [ $((i % ${JOBS:-3})) -eq 0 ]; then wait; fi
done
wait
echo "ALL DONE"

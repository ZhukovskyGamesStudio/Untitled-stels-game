#!/usr/bin/env bash
# Builds one or more styles into <root> and renders lineups to look at (review.py, front and 3/4 view of each model).
#   Tools/Blender/bestiary/review_style.sh <root> <style>[,<style>...] [model,model...] [robots|props|all]
# Writes <root>/<style>_robots.png (12 robots), <root>/<style>_devices.png (devices and sensor modules) and, for props,
# <root>/<style>_props.png (furniture and level pieces).
set -e
HERE="$(cd "$(dirname "$0")/.." && pwd)"
B="${BLENDER:-D:/Apps/Steam/steamapps/common/Blender/blender.exe}"
ROOT="$1"; STYLES="$2"; ONLY="${3:+--only $3}"; SET="${4:-robots}"
"$B" -b --factory-startup --python "$HERE/build_bestiary.py" -- "$ROOT" --styles "$STYLES" $ONLY --set "$SET" 2>&1 \
  | grep -E "CONTRACT|floats|DONE|Error|Traceback|line [0-9]|MESHFIX .*: [1-9][0-9]* pieces attached" || true
ROBOTS="robot_guard robot_hunter robot_hound robot_sorter robot_listener robot_worker robot_thumper robot_sweeper robot_drone robot_spider robot_turret robot_vacuum"
DEVICES="sensor_lidar sensor_pir sensor_plate security_camera laser_emitter attach_eye attach_mic attach_seismic attach_nose"
SETS="robots devices"
[ "$SET" = props ] && SETS="props"
[ "$SET" = all ] && SETS="robots devices props"
for S in ${STYLES//,/ }; do
  D="$ROOT/Styles/PerStyle/$S"
  for set in $SETS; do
    case $set in
      robots) list=$ROBOTS ;;
      devices) list=$DEVICES ;;
      props) list=$(ls "$D" | sed -n "s/__$S\.fbx\$//p" | grep -v -E "^(robot_|sensor_|attach_|security_camera|laser_emitter)") ;;
    esac
    files=()
    for m in $list; do [ -f "$D/${m}__$S.fbx" ] && files+=("$D/${m}__$S.fbx"); done
    cols=4; [ $set = props ] && cols=8
    if [ ${#files[@]} -gt 0 ] && "$B" -b --factory-startup --python "$HERE/review.py" -- "$ROOT/${S}_$set.png" "${files[@]}" \
         --lineup $cols --no-check >/dev/null 2>&1; then
      echo "lineup: $ROOT/${S}_$set.png"
    fi
  done
done

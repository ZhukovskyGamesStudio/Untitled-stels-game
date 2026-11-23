#!/bin/bash
# Pictures of the rigged humans in every pose (Stealth → Human Pose Pictures) in batch → Logs/poses/<who>, with contact sheets.
P="$(cd "$(dirname "$0")/.." && pwd -W 2>/dev/null || pwd)"
UNITY="${UNITY:-/d/Apps/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe}"
"$UNITY" -batchmode -projectPath "$P" -executeMethod Stealth.EditorTools.HumanPoseSheet.Batch -logFile "$P/Logs/poses.log"
code=$?
grep -E "error CS|Exception" "$P/Logs/poses.log" | head -20
for w in backend qa sysadmin designer; do python "$P/Tools/sheet.py" "$P/Logs/poses/$w" 5 4 > /dev/null; done
exit $code

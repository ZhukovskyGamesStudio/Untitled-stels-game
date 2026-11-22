#!/bin/bash
# Close-ups of the rigged humans a detail at a time (Stealth → Human Detail Pictures) in batch → Logs/poses/_details/<who>.
# WHO=backend — one person; ONLY=hand_r,head — these details.
P="$(cd "$(dirname "$0")/.." && pwd -W 2>/dev/null || pwd)"
UNITY="${UNITY:-/d/Apps/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe}"
HUMAN_DETAIL_WHO="$WHO" HUMAN_DETAIL_ONLY="$ONLY" "$UNITY" -batchmode -projectPath "$P" -executeMethod Stealth.EditorTools.HumanDetailSheet.Batch -logFile "$P/Logs/details.log"
code=$?
grep -E "error CS|Exception|\[Details\]" "$P/Logs/details.log" | head -20
exit $code

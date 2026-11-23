#!/bin/bash
# Close-ups of the rigged humans a detail at a time (Stealth → Human Detail Pictures) in batch → Logs/poses/_details/<who>.
# WHO=backend — one person; ONLY=hand_r,eye — these details; POSE=idle — these poses; TILE=900 — pixels of a view; FLAT=1 — without normal maps (→ _details_flat); PARTS=AccentHead — only these renderers; SHADOW=1 — cast shadows (off: a shadow passes for a flaw); NOCUT=1 — nothing cut away (a cut passes for a flaw too); WEAR=bob,glasses — try wardrobe items on (→ _details_wear).
# Each view is its own picture: Logs/poses/_details/<who>/<detail>_<pose>/<side>.png (what is in front of the detail is cut away).
P="$(cd "$(dirname "$0")/.." && pwd -W 2>/dev/null || pwd)"
UNITY="${UNITY:-/d/Apps/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe}"
HUMAN_DETAIL_WHO="$WHO" HUMAN_DETAIL_ONLY="$ONLY" HUMAN_DETAIL_POSE="$POSE" HUMAN_DETAIL_TILE="$TILE" HUMAN_DETAIL_FLAT="$FLAT" HUMAN_DETAIL_PARTS="$PARTS" HUMAN_DETAIL_SHADOW="$SHADOW" HUMAN_DETAIL_NOCUT="$NOCUT" HUMAN_DETAIL_WEAR="$WEAR" "$UNITY" -batchmode -projectPath "$P" -executeMethod Stealth.EditorTools.HumanDetailSheet.Batch -logFile "$P/Logs/details.log"
code=$?
grep -E "error CS|Exception|\[Details\]" "$P/Logs/details.log" | head -20
exit $code

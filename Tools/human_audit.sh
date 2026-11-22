#!/bin/bash
# The rigged humans' audit (Stealth → Human Rig Audit) in batch: Logs/human_audit.txt, pictures in Logs/poses/_audit.
# TRY=1: the game's side is a fresh copy configured by the builder's code as it is now (before rebuilding the assets).
P="$(cd "$(dirname "$0")/.." && pwd -W 2>/dev/null || pwd)"
UNITY="${UNITY:-/d/Apps/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe}"
HUMAN_AUDIT_TRY="${TRY:-0}" "$UNITY" -batchmode -projectPath "$P" -executeMethod Stealth.EditorTools.HumanRigAudit.Batch -logFile "$P/Logs/human_audit.log"
code=$?
grep -E "error CS|Exception" "$P/Logs/human_audit.log" | head -20
cat "$P/Logs/human_audit.txt"
exit $code

#!/bin/sh
# Run a Plasma editor entry point headless.  Usage: tools/sandbox/unity.sh <Method> [Android|Linux64]
#   Methods: Setup | BuildAndroid | BuildAndroidStore | BuildLinux | Sweep   (Plasma.EditorTools.PlasmaBuild.*)
# Log: Logs/unity_<Method>.log ; prints the [Plasma] lines and compile errors.
set -e
ROOT=$(cd "$(dirname "$0")/../.." && pwd)
M=${1:?method}; T=${2:-Android}
mkdir -p "$ROOT/Logs"; LOG="$ROOT/Logs/unity_$M.log"
rm -f "$ROOT/Temp/UnityLockfile"
set +e
"$(dirname "$0")/run_unity.sh" -batchmode -nographics -projectPath "$ROOT" -buildTarget "$T" \
  -executeMethod "Plasma.EditorTools.PlasmaBuild.$M" -quit -logFile "$LOG"
RC=$?
set -e
grep -a "\[Plasma\]\|error CS\|Fatal\|Exception:" "$LOG" | grep -v "^Start importing" || true
echo "exit=$RC log=$LOG"
exit $RC

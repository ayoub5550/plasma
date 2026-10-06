#!/bin/sh
# Run a Plasma editor entry point headless.  Usage: tools/sandbox/unity.sh <Method> [Android|Linux64]
#   Methods: Setup | BuildAndroid | BuildAndroidStore | BuildLinux | Sweep   (Plasma.EditorTools.PlasmaBuild.*)
# Log: Logs/unity_<Method>.log ; prints the [Plasma] lines and compile errors.
set -e
ROOT=$(cd "$(dirname "$0")/../.." && pwd)
M=${1:?method}; T=${2:-Android}
mkdir -p "$ROOT/Logs"; LOG="$ROOT/Logs/unity_$M.log"
rm -f "$ROOT/Temp/UnityLockfile"
# Debug signing key: Gradle uses ~/.android/debug.keystore of the *OS* home, which does not survive sandbox restarts.
# Keep the real copy under $UNITY_ROOT (outside the repo, never committed) so every APK has the same certificate
# and installs over the previous version. (v0.3.1 = first build with the persisted key.)
KS_KEEP=${UNITY_ROOT:-/work/unity}/keys/debug.keystore; OS_HOME=$(getent passwd "$(id -u)" | cut -d: -f6); OS_HOME=${OS_HOME:-$HOME}
mkdir -p "$OS_HOME/.android" "$(dirname "$KS_KEEP")"
[ -f "$KS_KEEP" ] && cp "$KS_KEEP" "$OS_HOME/.android/debug.keystore"
set +e
"$(dirname "$0")/run_unity.sh" -batchmode -nographics -projectPath "$ROOT" -buildTarget "$T" \
  -executeMethod "Plasma.EditorTools.PlasmaBuild.$M" -quit -logFile "$LOG"
RC=$?
set -e
[ ! -f "$KS_KEEP" ] && [ -f "$OS_HOME/.android/debug.keystore" ] && cp "$OS_HOME/.android/debug.keystore" "$KS_KEEP" || true
grep -a "\[Plasma\]\|error CS\|Fatal\|Exception:" "$LOG" | grep -v "^Start importing" || true
echo "exit=$RC log=$LOG"
exit $RC

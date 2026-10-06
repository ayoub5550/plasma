#!/bin/sh
# Compile Assets/Plasma/Scripts/Sim/*.cs + Program.cs with Unity's bundled Mono and run the career sweep.
set -e
ROOT=$(cd "$(dirname "$0")/../.." && pwd)
MONO=${UNITY_MONO:-/work/unity/editor/Editor/Data/MonoBleedingEdge}
OUT=${TMPDIR:-/tmp}/plasma_sim.exe
"$MONO/bin/mono" "$MONO/lib/mono/4.5/csc.exe" -nologo -optimize+ -out:"$OUT" "$ROOT"/Assets/Plasma/Scripts/Sim/*.cs "$ROOT/tools/simharness/Program.cs"
"$MONO/bin/mono" "$OUT" "$@"

#!/bin/sh
# Render real gameplay frames from the Linux player (no GPU: Xvfb + Mesa llvmpipe) and encode a video.
# Usage: tools/sandbox/capture.sh <outdir> [level=4] [frames=900] [skill=0.9]
# Env: PLASMA_LANG=en|ar (UI language), MENU_FRAMES=45.
# Needs Builds/linux/Plasma.x86_64 (tools/sandbox/unity.sh BuildLinux Linux64).
# NOTE: -popupwindow is REQUIRED - with a normal window Unity waits forever for a window-manager resize
#       event under Xvfb and renders exactly one frame.
set -e
ROOT=$(cd "$(dirname "$0")/../.." && pwd)
OUT=${1:?outdir}; LEVEL=${2:-4}; FRAMES=${3:-900}; SKILL=${4:-0.9}
rm -rf "$OUT"; mkdir -p "$OUT"
export LP_NUM_THREADS=${LP_NUM_THREADS:-16}
export LD_PRELOAD=${UNITY_ROOT:-/work/unity}/shim/libschedfix.so
timeout 1800 xvfb-run -a -s "-screen 0 540x1170x24" "$ROOT/Builds/linux/Plasma.x86_64" \
  -screen-width 540 -screen-height 1170 -popupwindow -force-glcore -logFile "$OUT/player.log" \
  -plasmaCapture "$OUT/frames" -plasmaLevel "$LEVEL" -plasmaFrames "$FRAMES" -plasmaSkill "$SKILL" -plasmaMenuFrames ${MENU_FRAMES:-45} -plasmaLang ${PLASMA_LANG:-en} >/dev/null 2>&1 || true
grep -a "\[Plasma\]\|Exception" "$OUT/player.log" | head -20
N=$(ls "$OUT/frames" | wc -l)
echo "frames: $N"
[ "$N" -gt 0 ] && ffmpeg -v error -y -framerate 30 -i "$OUT/frames/f%05d.png" -c:v libx264 -pix_fmt yuv420p -crf 20 "$OUT/gameplay.mp4" && echo "video: $OUT/gameplay.mp4"

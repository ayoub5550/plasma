#!/bin/sh
# Launch the Unity Editor installed by setup_unity.sh (gVisor sandbox: GTK libs + FMOD shim).
U=${UNITY_ROOT:-/work/unity}
export LD_LIBRARY_PATH=$U/libs/usr/lib/x86_64-linux-gnu:$LD_LIBRARY_PATH
export HOME=${UNITY_HOME:-$U/home}
export LD_PRELOAD=$U/shim/libschedfix.so${LD_PRELOAD:+:$LD_PRELOAD}
exec $U/editor/Editor/Unity "$@"

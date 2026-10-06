#!/bin/sh
# One-shot installer for Unity 2022.3.62f3 + Android (SDK/NDK/JDK) in a rootless Linux sandbox
# (validated in a gVisor sandbox, 17 cores, no GPU, 2026-10-06; ~6 min with a fast link).
#
#   tools/sandbox/setup_unity.sh            # installs into $UNITY_ROOT (default /work/unity)
#   then activate once (Personal licence, the account owner's credentials, never commit them):
#   tools/sandbox/run_unity.sh -batchmode -nographics -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD" -quit -logFile /tmp/act.log
#   (look for 'Serial number assigned to: "...UnityPers..."'; the licence is cached in $UNITY_ROOT/home)
set -e
HERE=$(cd "$(dirname "$0")" && pwd)
U=${UNITY_ROOT:-/work/unity}
REV=96770f904ca7; VER=2022.3.62f3
mkdir -p "$U/dl" && cd "$U/dl"
cat > urls.txt <<URLS
https://download.unity3d.com/download_unity/$REV/LinuxEditorInstaller/Unity-$VER.tar.xz
https://download.unity3d.com/download_unity/$REV/MacEditorTargetInstaller/UnitySetup-Android-Support-for-Editor-$VER.pkg
https://download.unity3d.com/download_unity/open-jdk/open-jdk-linux-x64/jdk11.0.14.1-1_c17a2ae6fe1b4281fb613fed32027cf93e0251795387941bd8c1fcb6c74f9db4.zip
https://dl.google.com/android/repository/android-ndk-r23b-linux.zip
https://dl.google.com/android/repository/build-tools_r34-linux.zip
https://dl.google.com/android/repository/platform-tools_r32.0.0-linux.zip
https://dl.google.com/android/repository/platform-34-ext7_r02.zip
https://dl.google.com/android/repository/platform-35_r01.zip
https://dl.google.com/android/repository/platform-36_r02.zip
https://dl.google.com/android/repository/commandlinetools-linux-8092744_latest.zip
URLS
echo "[setup] downloading (parallel)"
xargs -P 6 -n 1 curl -sSfLO --retry 3 < urls.txt
# integrity (vendor metadata: services.api.unity.com/unity/editor/release/v1/releases?version=$VER)
echo "737e59766b1eb7ff16a65aef5ea8361f  Unity-$VER.tar.xz" | md5sum -c -
echo "04169061f6c79bb9cf290021bea8e7fa  UnitySetup-Android-Support-for-Editor-$VER.pkg" | md5sum -c -

echo "[setup] editor"
mkdir -p "$U/editor" && tar -xJf "Unity-$VER.tar.xz" -C "$U/editor"
AP="$U/editor/Editor/Data/PlaybackEngines/AndroidPlayer"

echo "[setup] Android module (.pkg = XAR -> gzip cpio 'odc'; extracted with python, no installer scripts run)"
mkdir -p "$U/pkg" && cd "$U/pkg"
python3 "$HERE/xar_extract.py" "$U/dl/UnitySetup-Android-Support-for-Editor-$VER.pkg" >/dev/null
mkdir -p "$AP" && python3 "$HERE/cpio_odc.py" TargetSupport.pkg.tmp_Payload "$AP"

echo "[setup] JDK / SDK / NDK"
cd "$U/dl"
T=$(mktemp -d)
unzip -q jdk11*.zip -d "$T/jdk" && mv "$T/jdk" "$AP/OpenJDK"
unzip -q android-ndk-r23b-linux.zip -d "$T" && mv "$T/android-ndk-r23b" "$AP/NDK"
mkdir -p "$AP/SDK/build-tools" "$AP/SDK/platforms" "$AP/SDK/cmdline-tools"
unzip -q platform-tools_r32.0.0-linux.zip -d "$AP/SDK"
unzip -q build-tools_r34-linux.zip -d "$T/bt" && mv "$T/bt/android-14" "$AP/SDK/build-tools/34.0.0"
for p in platform-34-ext7_r02.zip platform-35_r01.zip platform-36_r02.zip; do unzip -q "$p" -d "$T/p_$p"; mv "$T/p_$p"/* "$AP/SDK/platforms/"; done
# Unity 2022.3 looks for cmdline-tools/6.0 specifically ("latest" alone is reported as missing)
unzip -q commandlinetools-linux-8092744_latest.zip -d "$T/clt" && mv "$T/clt/cmdline-tools" "$AP/SDK/cmdline-tools/6.0"
rm -rf "$T"

echo "[setup] GTK libs for the editor (no root: Debian packages unpacked into $U/libs)"
mkdir -p "$U/libs/debs" && cd "$U/libs/debs" && apt-get download libgtk-3-0 >/dev/null && for d in *.deb; do dpkg -x "$d" "$U/libs"; done

echo "[setup] gVisor fixes: FMOD sched shim + qemu-wrapped shader compiler"
mkdir -p "$U/shim" "$U/home" && gcc -shared -fPIC -O2 -o "$U/shim/libschedfix.so" "$HERE/schedfix.c"
mkdir -p "$U/qemu" && cd "$U/qemu" && apt-get download qemu-user-static >/dev/null && dpkg -x qemu-user-static_*.deb sysroot
TOOLS="$U/editor/Editor/Data/Tools"
if [ ! -f "$TOOLS/UnityShaderCompiler.real" ]; then
  mv "$TOOLS/UnityShaderCompiler" "$TOOLS/UnityShaderCompiler.real"
  printf '#!/bin/sh\nexec %s/qemu/sysroot/usr/bin/qemu-x86_64-static "$(dirname "$0")/UnityShaderCompiler.real" "$@"\n' "$U" > "$TOOLS/UnityShaderCompiler"
  chmod +x "$TOOLS/UnityShaderCompiler"
fi
LD_LIBRARY_PATH="$U/libs/usr/lib/x86_64-linux-gnu" ldd "$U/editor/Editor/Unity" | grep "not found" && echo "WARNING: missing libs above" || true
"$HERE/run_unity.sh" -version
echo "[setup] done. Next: activate the licence (see header), then tools/sandbox/unity.sh Setup"

#!/usr/bin/env bash
# Reproducible build of Assets/_Game/Plugins/x86_64/StealthVoice.dll (Opus + RNNoise, static CRT).
# Needs CMake and Visual Studio 2022 (MSVC x64). Run from Git Bash: Tools/Voice/build_native.sh
# Env: CMAKE (path to cmake), GENERATOR (default "Visual Studio 17 2022"), BUILD (default Temp/voice_build).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
BUILD="${BUILD:-$ROOT/Temp/voice_build}"
CMAKE="${CMAKE:-$(command -v cmake || echo /e/Programs/CMake/bin/cmake)}"
GENERATOR="${GENERATOR:-Visual Studio 17 2022}"
OUT="$ROOT/Assets/_Game/Plugins/x86_64"

OPUS_TGZ=opus-1.5.2.tar.gz
OPUS_URL=https://downloads.xiph.org/releases/opus/opus-1.5.2.tar.gz
OPUS_SHA=65c1d2f78b9f2fb20082c38cbe47c951ad5839345876e46941612ee87f9a7ce1
RNN_TGZ=rnnoise-0.2.tar.gz
RNN_URL=https://github.com/xiph/rnnoise/releases/download/v0.2/rnnoise-0.2.tar.gz
RNN_SHA=90fce4b00b9ff24c08dbfe31b82ffd43bae383d85c5535676d28b0a2b11c0d37

winpath() { if command -v cygpath >/dev/null; then cygpath -m "$1"; else echo "$1"; fi; }

fetch() { # file url sha
  local f="$BUILD/$1"
  if [ ! -f "$f" ]; then
    echo "Downloading $2"
    curl -fL --retry 3 -o "$f.part" "$2"
    mv "$f.part" "$f"
  fi
  local got
  got="$(sha256sum "$f" | cut -d' ' -f1)"
  if [ "$got" != "$3" ]; then
    echo "sha256 mismatch for $1: got $got, want $3" >&2
    exit 1
  fi
  echo "OK $1 sha256 $got"
}

mkdir -p "$BUILD"
fetch "$OPUS_TGZ" "$OPUS_URL" "$OPUS_SHA"
fetch "$RNN_TGZ" "$RNN_URL" "$RNN_SHA"

# Fresh extraction every time: the archives are data, nothing inside them is run except opus's CMakeLists.
rm -rf "$BUILD/src" "$BUILD/cmake"
mkdir -p "$BUILD/src"
tar -xzf "$BUILD/$OPUS_TGZ" -C "$BUILD/src"
tar -xzf "$BUILD/$RNN_TGZ" -C "$BUILD/src"

"$CMAKE" -S "$(winpath "$ROOT/Tools/Voice/native")" -B "$(winpath "$BUILD/cmake")" \
  -G "$GENERATOR" -A x64 \
  -DOPUS_SRC_DIR="$(winpath "$BUILD/src/opus-1.5.2")" \
  -DRNNOISE_SRC_DIR="$(winpath "$BUILD/src/rnnoise-0.2")"
"$CMAKE" --build "$(winpath "$BUILD/cmake")" --config Release --target StealthVoice

mkdir -p "$OUT"
cp "$BUILD/cmake/Release/StealthVoice.dll" "$OUT/StealthVoice.dll"
echo "Built $OUT/StealthVoice.dll ($(stat -c %s "$OUT/StealthVoice.dll") bytes)"

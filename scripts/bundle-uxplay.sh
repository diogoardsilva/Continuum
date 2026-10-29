#!/usr/bin/env bash
# Builds UxPlay (AirPlay mirroring receiver) inside MSYS2 UCRT64 and gathers
# uxplay.exe, its DLLs and the GStreamer plugins into a portable folder.
#
# Usage (from an MSYS2 UCRT64 shell): scripts/bundle-uxplay.sh <uxplay-src> <out-dir>
set -euo pipefail

src=$(realpath "$1")
out=$(realpath -m "$2")
prefix=/ucrt64

cmake -S "$src" -B "$src/build" -G Ninja -DCMAKE_BUILD_TYPE=Release
cmake --build "$src/build"

mkdir -p "$out/bin" "$out/lib/gstreamer-1.0" "$out/libexec/gstreamer-1.0"
cp "$src/build/uxplay.exe" "$out/bin/"
cp -r "$prefix/lib/gstreamer-1.0/"*.dll "$out/lib/gstreamer-1.0/"
cp "$prefix/libexec/gstreamer-1.0/gst-plugin-scanner.exe" "$out/libexec/gstreamer-1.0/"

# Copy every DLL from the MSYS2 prefix that the executables or plugins load.
copy_deps() {
    ldd "$1" | awk '{print $3}' | grep -i "^$prefix/" | while read -r dll; do
        [ -f "$out/bin/$(basename "$dll")" ] || cp "$dll" "$out/bin/"
    done
}
for f in "$out/bin/uxplay.exe" "$out/libexec/gstreamer-1.0/gst-plugin-scanner.exe" "$out/lib/gstreamer-1.0/"*.dll; do
    copy_deps "$f"
done

cp "$(dirname "$0")/../video/uxplay.cmd" "$out/"
cp "$src/LICENSE" "$out/UXPLAY-LICENSE.txt" 2>/dev/null || true
echo "Bundle ready at $out ($(du -sh "$out" | cut -f1))"

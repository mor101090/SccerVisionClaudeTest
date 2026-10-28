#!/usr/bin/env bash
# Builds the standalone demo page (docs/index.html) and the match engine it runs in the browser (docs/engine: the C#
# engine compiled to WebAssembly, played in a Web Worker). Needs the .NET 8 SDK, node and python3.
#   tools/demo-page/build.sh            # writes docs/index.html and docs/engine/<version>/
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
work="$(mktemp -d)"
rm -rf "$root/docs/engine" "$root/docs/conditions"
(cd "$root/sim/SoccerVision.Wasm" && dotnet publish -c Release -o "$work/wasm" -v q >/dev/null)
# a folder per engine build (named after its content): a cached old engine can never be served with a new page
cp "$root/tools/demo-page/engine-worker.js" "$work/wasm/wwwroot/worker.js"
ver=$(cd "$work/wasm/wwwroot" && find . -type f -print0 | sort -z | xargs -0 sha1sum | sha1sum | cut -c1-12)
mkdir -p "$root/docs/engine/$ver"
cp -r "$work/wasm/wwwroot/." "$root/docs/engine/$ver/"
export ENGINE_DIR="engine/$ver/"
(cd "$root/web" && npx --yes esbuild src/audio/matchAudio.ts --bundle --format=iife --global-name=SVAudio --outfile="$work/audio.iife.js" \
  && npx --yes esbuild src/replay/textures.ts --bundle --format=iife --global-name=SVTex --outfile="$work/textures.iife.js")
python3 "$root/tools/demo-page/build_page.py" "$work" "$root/docs/index.html"
rm -rf "$work"

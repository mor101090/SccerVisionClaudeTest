#!/usr/bin/env bash
# Builds the classic single-page game (tools/demo-page) into app/public/classic/ so the full legacy
# game keeps working at /classic while its screens are ported to React.
# Uses the engine build from build-engine.sh (run that first) and the app's own TS audio/texture code.
#
# Needs node, python3. Usage (from app/): yarn classic:build
set -euo pipefail

app="$(cd "$(dirname "$0")/.." && pwd)"
root="$(cd "$app/.." && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

manifest="$app/public/engine/current.json"
[ -f "$manifest" ] || { echo "Run scripts/build-engine.sh first" >&2; exit 1; }
base=$(node -e 'console.log(JSON.parse(require("fs").readFileSync(process.argv[1],"utf8")).base)' "$manifest")

esbuild="$app/node_modules/.bin/esbuild"
"$esbuild" "$app/src/engine/audio/matchAudio.ts" --bundle --format=iife --global-name=SVAudio \
  --outfile="$work/audio.iife.js" --log-level=warning
"$esbuild" "$app/src/engine/replay/textures.ts" --bundle --format=iife --global-name=SVTex \
  --outfile="$work/textures.iife.js" --log-level=warning

out="$app/public/classic"
rm -rf "$out"
mkdir -p "$out"
ENGINE_DIR="$base" python3 "$root/tools/demo-page/build_page.py" "$work" "$out/index.html"
cp -R "$root/docs/sounds" "$out/sounds"
# Portraits are optional (tools/faces makes them); without any, the page draws vector faces. An empty manifest keeps the page from asking for
# a file that is not there (a 404 on every load).
mkdir -p "$out/faces"
[ -f "$root/docs/faces/manifest.json" ] && cp -R "$root/docs/faces/." "$out/faces/" || echo '{}' > "$out/faces/manifest.json"

echo "Classic page -> public/classic (engine $base)"

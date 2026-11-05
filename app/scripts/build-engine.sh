#!/usr/bin/env bash
# Publishes the C# match engine (sim/SoccerVision.Wasm) as WebAssembly into app/public/engine/<hash>/
# and writes app/public/engine/current.json pointing at it.
#
# The folder name is a content hash, so a cached old engine is never served with a new page
# (public/engine/* is served with an immutable 1-year cache, see next.config.ts).
#
# Needs the .NET 8 SDK. Usage (from app/): yarn engine:build
set -euo pipefail

app="$(cd "$(dirname "$0")/.." && pwd)"
root="$(cd "$app/.." && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
dotnet="${DOTNET:-dotnet}"
command -v "$dotnet" >/dev/null 2>&1 || dotnet="$HOME/.dotnet/dotnet"

echo "Publishing engine (Release)..."
(cd "$root/sim/SoccerVision.Wasm" && "$dotnet" publish -c Release -o "$work/wasm" -v q -nologo >/dev/null)

src="$work/wasm/wwwroot"
# Vercel compresses on the fly; drop precompressed copies and debug symbols.
find "$src" \( -name '*.br' -o -name '*.gz' -o -name '*.pdb' \) -delete
# The classic page (/classic) loads <engine>/worker.js. Copied before hashing so it is part of the version.
cp "$root/tools/demo-page/engine-worker.js" "$src/worker.js"
if command -v sha1sum >/dev/null 2>&1; then hasher=sha1sum; else hasher="shasum -a 1"; fi
ver=$(cd "$src" && find . -type f -print0 | LC_ALL=C sort -z | xargs -0 $hasher | $hasher | cut -c1-12)

rm -rf "$app/public/engine"
mkdir -p "$app/public/engine/$ver"
cp -R "$src/." "$app/public/engine/$ver/"

# Pointer to the current build. Not immutable-cached (see next.config.ts), unlike the hashed folder.
printf '{"version":"%s","base":"/engine/%s/"}\n' "$ver" "$ver" > "$app/public/engine/current.json"

echo "Engine $ver -> public/engine/$ver ($(du -sh "$app/public/engine/$ver" | cut -f1))"

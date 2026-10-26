#!/usr/bin/env bash
# Builds the standalone demo page (docs/index.html): six full matches recorded with the real C# engine, replayed on a canvas
# with sound, commentary, a pace slider and a "New match" button. Needs the .NET 8 SDK, node and python3.
#   tools/demo-page/build.sh            # writes docs/index.html
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
work="$(mktemp -d)"
seeds=$(python3 -c "import re;print(' '.join(re.search(r'SEEDS = \(([0-9, ]+)\)', open('$root/tools/demo-page/build_page.py').read()).group(1).replace(',',' ').split()))")
(cd "$root/sim" && dotnet build -c Release SoccerVision.SimRunner -v q >/dev/null)
for s in $seeds; do
  dotnet "$root/sim/SoccerVision.SimRunner/bin/Release/net8.0/SoccerVision.SimRunner.dll" --seed "$s" --replay "$work/e$s.json" --fps 10 | tail -1
done
(cd "$root/web" && npx --yes esbuild src/audio/matchAudio.ts --bundle --format=iife --global-name=SVAudio --outfile="$work/audio.iife.js")
python3 "$root/tools/demo-page/build_page.py" "$work" "$root/docs/index.html"
rm -rf "$work"

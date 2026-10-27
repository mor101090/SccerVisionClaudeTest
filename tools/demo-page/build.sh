#!/usr/bin/env bash
# Builds the standalone demo page (docs/index.html): six full matches recorded with the real C# engine, replayed on a canvas
# with sound, commentary, developer settings and a "New match" button. Needs the .NET 8 SDK, node and python3.
# The same matches are also recorded under other weather and pitch conditions (docs/conditions/*.txt); the page loads one
# of those when the conditions are changed in its developer settings.
#   tools/demo-page/build.sh            # writes docs/index.html and docs/conditions/
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
work="$(mktemp -d)"
read_tuple() { python3 -c "import re;print(' '.join(re.search(r'$1 = \(([0-9, ]+)\)', open('$root/tools/demo-page/build_page.py').read()).group(1).replace(',',' ').split()))"; }
seeds=$(read_tuple SEEDS)
variant_seeds=$(read_tuple VARIANT_SEEDS)
(cd "$root/sim" && dotnet build -c Release SoccerVision.SimRunner -v q >/dev/null)
runner="$root/sim/SoccerVision.SimRunner/bin/Release/net8.0/SoccerVision.SimRunner.dll"
jobs="$work/jobs.txt"
: > "$jobs"
for s in $seeds; do echo "--seed $s --replay $work/e$s.json --fps 10" >> "$jobs"; done
# Variants: weather d(ry)/r(ain)/s(now) x maintenance 2 (perfect) / 1 (average) / 0 (neglected) x wind 0 (calm) / 1 (strong).
# The wind blows from a direction fixed per match.
for s in $variant_seeds; do
  angle=$(( (s * 97) % 360 ))
  for w in d r s; do
    weather=$([ $w = d ] && echo sunny || ([ $w = r ] && echo rain || echo snow))
    for m in 2 1 0; do
      maint=$([ $m = 2 ] && echo 1 || ([ $m = 1 ] && echo 0.5 || echo 0))
      for wd in 0 1; do
        [ "$w$m$wd" = d20 ] && continue
        echo "--seed $s --replay $work/v$s-$w$m$wd.json --fps 10 --weather $weather --maintenance $maint --wind $([ $wd = 1 ] && echo 12 || echo 0) --windangle $angle" >> "$jobs"
      done
    done
  done
done
xargs -P "$(nproc)" -L 1 dotnet "$runner" < "$jobs" | grep -c "Replay written" | sed 's/^/replays recorded: /'
(cd "$root/web" && npx --yes esbuild src/audio/matchAudio.ts --bundle --format=iife --global-name=SVAudio --outfile="$work/audio.iife.js")
python3 "$root/tools/demo-page/build_page.py" "$work" "$root/docs/index.html"
rm -rf "$work"

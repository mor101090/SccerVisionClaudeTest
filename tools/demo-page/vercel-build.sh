#!/usr/bin/env bash
# The Vercel build (see vercel.json): installs the .NET 8 SDK if the build machine has none (Vercel's does not), builds the
# page and the match engine (C# compiled to WebAssembly) with tools/demo-page/build.sh, and puts the finished site - and only
# the site - in public/, which Vercel serves.
#   bash tools/demo-page/vercel-build.sh          # the same thing on your own machine; the site is in public/
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$root"

export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1   # (the build machine has no ICU libraries; the engine does not need them)

if ! (command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks 2>/dev/null | grep -qE '^(8|9|10)\.'); then
  dir="${DOTNET_INSTALL_DIR:-$HOME/.dotnet}"
  echo "Installing the .NET 8 SDK into $dir ..."
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o "${TMPDIR:-/tmp}/dotnet-install.sh"
  bash "${TMPDIR:-/tmp}/dotnet-install.sh" --channel 8.0 --install-dir "$dir"
  export PATH="$dir:$PATH" DOTNET_ROOT="$dir"
fi
dotnet --version

bash tools/demo-page/build.sh          # writes docs/index.html and docs/engine/<version>/

rm -rf public && mkdir public
cp docs/index.html public/index.html
cp -r docs/engine public/engine
cp -r docs/sounds public/sounds
printf 'Built from %s (%s) at %s\n' "${VERCEL_GIT_COMMIT_SHA:-$(git rev-parse HEAD 2>/dev/null || echo local)}" "${VERCEL_GIT_COMMIT_REF:-$(git branch --show-current 2>/dev/null || echo local)}" "$(date -u +%FT%TZ)" > public/BUILD.txt
echo "Site written to public/ ($(du -sh public | cut -f1))"

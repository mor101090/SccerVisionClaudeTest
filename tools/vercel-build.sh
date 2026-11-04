#!/usr/bin/env bash
# Vercel build: installs the .NET 8 SDK (not on Vercel's build image), builds the playable demo page
# (tools/demo-page/build.sh) and puts the site in public/ (index.html, engine/, sounds/).
# Deployed by .github/workflows/vercel.yml: every PR gets a preview, main is production (https://soccer.dev-spirit.com).
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
export PATH="$DOTNET_ROOT:$PATH"
if ! dotnet --list-sdks 2>/dev/null | grep -q '^8\.'; then
  curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel 8.0 --install-dir "$DOTNET_ROOT" | tail -2
fi
bash "$root/tools/demo-page/build.sh"
rm -rf "$root/public" && mkdir -p "$root/public"
cp "$root/docs/index.html" "$root/public/index.html"
cp -r "$root/docs/engine" "$root/public/engine"
cp -r "$root/docs/sounds" "$root/public/sounds"
printf 'Built from %s (%s) at %s\n' "${VERCEL_GIT_COMMIT_SHA:-local}" "${VERCEL_GIT_COMMIT_REF:-local}" "$(date -u +%FT%TZ)" > "$root/public/BUILD.txt"

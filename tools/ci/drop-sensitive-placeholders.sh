#!/usr/bin/env bash
# `vercel pull` can't read Sensitive variables back: it writes a placeholder instead of the value, and a build reads
# that placeholder as the value (app/src/env.ts then rejects it). This drops every variable the Vercel API lists as
# type "sensitive" for the target environment, plus empty values. The deployment gets the real values at runtime.
# Prints names only, never values.
#   usage: drop-sensitive-placeholders.sh <pulled env file> <production|preview>
# Needs VERCEL_TOKEN and VERCEL_PROJECT_ID (and VERCEL_ORG_ID for the team scope) in the environment.
set -euo pipefail
f="$1"; target="$2"
[ -f "$f" ] || { echo "no $f"; exit 0; }
keys=$(npx -y vercel@latest api "/v10/projects/${VERCEL_PROJECT_ID}/env${VERCEL_ORG_ID:+?teamId=$VERCEL_ORG_ID}" ${VERCEL_TOKEN:+--token="$VERCEL_TOKEN"} 2>/dev/null |
  node -e '
    let t = require("fs").readFileSync(0, "utf8"); t = t.slice(t.indexOf("{"));
    const target = process.argv[1];
    for (const e of JSON.parse(t).envs ?? [])
      if (e.type === "sensitive" && (e.target ?? []).includes(target)) console.log(e.key);
  ' "$target" | sort -u)
[ -n "$keys" ] || echo "::warning::No Sensitive variables listed for $target (API unreachable?)"
pattern='^[A-Z0-9_]+="?"?$'
for k in $keys; do pattern="$pattern|^$k="; done
dropped=$(grep -E "$pattern" "$f" | cut -d= -f1 | tr '\n' ' ' || true)
grep -vE "$pattern" "$f" > "$f.tmp" || true
mv "$f.tmp" "$f"
echo "Dropped from the pulled env (set at runtime by Vercel, or empty): ${dropped:-none}"

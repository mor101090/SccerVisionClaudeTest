# Soccer Vision

Football-management game. A deterministic C# match engine runs in the browser as .NET 8 WebAssembly.
The site is a Next.js fullstack app (`app/`) backed by Supabase. **The classic game is the default site:** `/` redirects to
`/classic/index.html` (built from `tools/demo-page/`, where Mor's game flow lives); the Next.js landing page is `/home`.

## Repo map

| Path                                                 | What                                                                                                                |
| ---------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------- |
| `app/`                                               | **Next.js 16 app** (App Router, TS strict, Tailwind 4, Yarn 4). The production site.                                |
| `unity/SoccerVision/Assets/SoccerVision/Simulation/` | The engine (pure C#, no UnityEngine refs). Source of truth for match logic.                                         |
| `sim/`                                               | .NET 8 solution: engine build, xUnit tests, SimRunner CLI, Replay recorder, `SoccerVision.Wasm` (browser host).     |
| `tools/demo-page/`                                   | Classic single-page game. Built into `app/public/classic/` by `yarn classic:build`.                                 |
| `web/`                                               | Legacy React/Vite shell (Unity WebGL + replay viewer). Not deployed; replay code was copied into `app/src/engine/`. |
| `data/`                                              | Static teams and names JSON.                                                                                        |
| `docs/ARCHITECTURE.md`                               | Engine/bridge architecture. Read before touching engine contracts.                                                  |

## Stack (app/)

- Next.js 16 with `cacheComponents` (Partial Prerendering). Request data (cookies, params, searchParams) must be read inside `<Suspense>`.
- Supabase: Postgres + Auth (email + 6-digit code, Google, Facebook, Discord) + Storage (`replays`, private) + Realtime
  (live lobbies). Projects: dev `mcrglsqpxyrbazwzrnzg` (eu-west-2; local, previews, e2e) and prod (see the runbook
  `docs/runbooks/supabase-cutover.md`). Dashboard-only settings (providers, SMTP, email templates, redirect URLs) are
  listed there too.
- Drizzle ORM (`app/src/server/db/schema.ts`, migrations in `app/drizzle/`). `auth.*`, `storage.*` and `realtime.*` are
  owned by Supabase; our tables reference `auth.users(id)`. Every public table has RLS on with no policies: only the
  server (the `postgres` role) reads them, never the Data API. Custom SQL (bucket, Realtime trigger) lives in
  `drizzle-kit generate --custom` migrations.
- Engine: `yarn engine:build` publishes WASM to `public/engine/<hash>/` + `public/engine/current.json`.
  Typed worker `src/engine/engine.worker.ts`, client `src/engine/engine-client.ts`, renderer `src/engine/replay/`.
- Supabase agent skills are in `.claude/skills/supabase*`; the Supabase MCP server (dev project) is in `.mcp.json`. Use them
  before writing Supabase code.
- Next.js 16 differs from older versions: read `app/node_modules/next/dist/docs/` for unfamiliar APIs (see `app/AGENTS.md`).

## Commands (run in `app/`)

```bash
yarn dev            # dev server. First builds the engine WASM + classic page if missing or their sources changed
                    # (tools/ensure-assets.ts; needs .NET 8 SDK + python3, else it warns and starts anyway; SKIP_ASSETS=1 skips)
yarn assets:build   # force a rebuild of engine WASM + classic page
yarn check          # lint + typecheck + format check + unit tests (run before every PR)
yarn test:e2e       # Playwright (starts dev server on :3100). Test users from tests/e2e/fixtures.ts on Supabase dev (`yarn e2e:seed`).
yarn build
yarn db:generate --name <x>   # after schema.ts changes; then yarn db:migrate (targets DATABASE_URL_UNPOOLED in .env.local)
```

Engine tests (repo root): `dotnet test sim/` takes **28–45 minutes**. Run a filtered subset (`--filter`) for engine changes.

## Environments

|                                          | Vercel env                                    | Supabase project                              |
| ---------------------------------------- | --------------------------------------------- | --------------------------------------------- |
| Local                                    | — (`.env.local`, values from the dev project) | dev                                           |
| Pull request (CI only, nothing deployed) | —                                             | — (daily e2e uses dev, migrates and seeds it) |
| Production                               | Production                                    | prod                                          |

Env (`app/src/env.ts`): `DATABASE_URL` (transaction pooler, :6543), `DATABASE_URL_UNPOOLED` (direct, :5432, migrations only),
`NEXT_PUBLIC_SUPABASE_URL`, `NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY`, `SUPABASE_SECRET_KEY` (server only: Storage, seeding).

**PRs are checked, `main` is deployed.** `.github/workflows/pr.yml` runs these checks on every PR, all required by the
`main` ruleset (`.github/rulesets/main.json`: PR required, no reviews, no force push/deletion):
`check (lint|typecheck|format:check|test)` (a matrix, in parallel), `build` (after all four are green: `vercel build`
with the Production env, no deploy). The full Playwright suite runs daily at 18:00 UTC on weekdays
(`.github/workflows/e2e.yml`, main's code against `yarn dev` in CI, on Supabase dev; also Run workflow by hand). `.github/workflows/vercel.yml` runs only on `main`: engine build,
migrate Supabase prod, `vercel build` + `deploy --prebuilt --prod`, then `production-check` (`/`, `/home`, `/play`,
`/auth/sign-in` on soccer.dev-spirit.com must answer 200). Vercel's own Git deployments are off (root `vercel.json`).
Migrations reach `dev` from the daily e2e run and local `yarn db:migrate`, so they must be additive. `vercel pull` writes Sensitive variables as placeholders:
both builds run `tools/ci/drop-sensitive-placeholders.sh` (drops the keys the Vercel API lists as `sensitive`; Vercel
sets the real values at runtime), and `env.ts` treats empty values as unset. Rulesets need GitHub Pro/Team for a private
repo; apply with `gh api -X POST repos/SoccerVision/soccer-vision/rulesets --input .github/rulesets/main.json`.

**Drizzle quirk:** `drizzle-kit generate` emits `CREATE TABLE "auth"."users"` the first time a migration references it.
Delete that statement from the generated SQL (see `drizzle/0000_supabase_baseline.sql`).

## Rules

- **Branch → PR → green checks → merge.** Every branch gets its own PR. `main` is production (soccer.dev-spirit.com). Never commit to `main` directly.
- **All new code is TypeScript.** Ported demo-page JS is converted, never copied as `.js`.
- **Engine contracts are shared across runtimes.** Changing `sim/SoccerVision.Wasm/Program.cs` (LiveEngine exports), the replay
  frame layout (`sim/SoccerVision.Replay/ReplayRecorder.cs`) or `BridgeProtocol.cs` requires updating `app/src/engine/` (and
  `tools/demo-page/engine-worker.js`, `web/src/bridge/protocol.ts`). Determinism (seed → identical match) must not break.
- **Online play is lockstep** (`src/features/lobby/online-match.tsx`): host and guest run the same engine; the host assigns the
  frame of every input and publishes a watermark. Online matches must always step 5 frames at a time (lead > 0).
  `tests/e2e/online.spec.ts` proves both screens end with the same frame checksum — keep it green.
- **Lobby sync uses Supabase Realtime** (`features/lobby/lobby-sync.ts`, `drizzle/0002_lobby_realtime.sql`): a trigger
  broadcasts `changed` on the private channel `lobby:<id>` after each committed lobby / input change (RLS on
  `realtime.messages`: host and guest only). The client then re-reads `GET /api/lobbies/[id]`, which reads the watermark
  before the inputs — never apply pushed row data directly, or a watermark could arrive before its inputs. One read at a
  time. The host writes only when it applied an input or every 30 frames. Polling is only the fallback when Realtime is
  unavailable (`/api/lobbies/[id]/live` 503, channel error).
- **Env vars** are validated in `app/src/env.ts`. Never read `process.env` directly elsewhere.
- Server data access: check the session in every Route Handler / Server Action (`currentUser()` / `withUser`), not only in `proxy.ts`.
- **Auth screens follow Figma** (file `xMPwRHYFfISl5Q6nRADVM6`, group "Authentication flow" node 160-214). Colors come from
  the tokens in `src/app/globals.css` (Figma "Primitives (Mor)" → `--mor-*`, "SoccerVision Web" → semantic `--color-*`);
  the reference for color roles is the Login screen (node 541-2333). Never hard-code hex in components.
- **Auth flow** (`features/auth/actions.ts`): every sign-in path ends in `nextPathAfterSignIn()` → `/auth/finish` until the
  profile has a username + terms (`profiles` table), then `redirectTo`. Password logins are throttled per email
  (`server/login-throttle.ts`, 5 failures / 15 min), one atomic increment per attempt in the Server Actions.
  Supabase's password-verification hook is a paid feature, so the app does not use it. The sign-in form gives the
  same wrong-email-or-password answer for a wrong password, an unknown address, and a pause. A password session
  the app did not create is not signed in, so a direct Auth API password grant is not a session. A password
  session opened after the pause began is not signed in either; a session from before it, and a social session,
  keep working. Classify Supabase Auth errors with `classifyAuthError()`. Emails
  pending verification/reset live in an httpOnly cookie, never in URLs. Password, sign-up, code and reset flows go
  through the Server Actions. The browser client refuses password grants (`password-grant.ts`); it is only for social
  sign-in (`signInWithOAuth`, PKCE → `/auth/callback`), linking and Realtime.
  Reset links carry `?token=<token hash>`, redeemed only when the new password is submitted. `updateUser` runs only
  for the recovery session that redeemed the token (or a retry of that same session), never for an ordinary login.
  `currentUser()` returns `{ id, email, name }` (JWT via `getClaims()`; name from `profiles.display_name` / username).
  Users moved from Neon have no password (Better Auth hashes can't be imported): they reset it once.
  **Connected accounts** (`/settings`, `features/auth/connected-accounts.tsx`, `linking.ts`): Connect = browser
  `linkIdentity` → `/auth/callback` → `/settings?linking=<p>` (success is read from the identities, a provider error
  arrives as `linkError`); Disconnect = `unlinkProviderAction`, refused for the last identity. Needs Supabase
  "Allow manual linking" ON (else `manual_linking_disabled`). Same-email verified sign-ins are linked automatically.
- **Create your team** (`/team/new/{manager,club,kits,squad}`, `features/team/`, `server/teams.ts`): squads come from the
  team generator API (`SQUAD_API_URL`, POST `/api/v2/team`, 2 GK · 5 FB · 5 DEF · 5 MID · 2 WNG · 3 ST). The XI picker
  `features/team/lineup.ts` and attribute mapping `engine-team.ts` are ports of `tools/teams/build_teams.py`;
  `lineup.test.ts` proves they reproduce `GeneratedTeams.cs` (XI, bench, attributes, feet in stars 1-5 via
  `weak-foot.ts`) — change both sides together. A ready club plays via the
  engine export `LiveEngine.UseHomeTeam(text)` (called by the worker before `Start`; format documented in Program.cs).
  Kit colours are data (`features/team/kits.ts`), with the clash rules from the Figma "Your kits" screen.
  Nationalities and flags come from flagcdn.com (`server/countries.ts`: `/en/codes.json`, cached a day; built-in list
  if it is down; `NOT_A_NATIONALITY` excludes e.g. PS, EU, UN). The manager step prefills the country from
  `x-vercel-ip-country` (no location prompt), else the browser language's region.
  Squad writes take `lockTeam()` (a transaction advisory lock) in their batch; regenerations are taken with a guarded
  decrement; one player per XI slot (`players_team_starter_uq`). A match played with the club is saved with
  `home_team_id` + `home_squad` (the UseHomeTeam text, checked by `/api/matches` against the stored club).

## app/src layout

```
app/              (site)/: home play matches lobby terms settings · team/new/: manager club kits squad · auth/: sign-in sign-up verify-email forgot-password[/sent]
                  reset-password finish link-account callback error · api/*
components/       shared UI: brand/header, ui/ (button, text-field, checkbox, alert, icons)
features/<domain> auth, matches, lobby (client UI, zod schemas, server actions)
server/           db, auth, storage, matches, lobbies, http (server-only)
engine/           WASM worker + typed protocol + replay renderer
lib/              pure helpers
env.ts            env schema
proxy.ts          Next 16 proxy: refreshes the Supabase session, redirects signed-out users from member pages
```

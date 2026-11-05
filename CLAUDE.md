# Soccer Vision

Football-management game. A deterministic C# match engine runs in the browser as .NET 8 WebAssembly.
The site is a Next.js fullstack app (`app/`) backed by Neon. **The classic game is the default site:** `/` redirects to
`/classic/index.html` (built from `tools/demo-page/`, where Mor's game flow lives); the Next.js landing page is `/home`.

## Repo map

| Path | What |
|---|---|
| `app/` | **Next.js 16 app** (App Router, TS strict, Tailwind 4, Yarn 4). The production site. |
| `unity/SoccerVision/Assets/SoccerVision/Simulation/` | The engine (pure C#, no UnityEngine refs). Source of truth for match logic. |
| `sim/` | .NET 8 solution: engine build, xUnit tests, SimRunner CLI, Replay recorder, `SoccerVision.Wasm` (browser host). |
| `tools/demo-page/` | Classic single-page game. Built into `app/public/classic/` by `yarn classic:build`. |
| `web/` | Legacy React/Vite shell (Unity WebGL + replay viewer). Not deployed; replay code was copied into `app/src/engine/`. |
| `data/` | Static teams and names JSON. |
| `docs/ARCHITECTURE.md` | Engine/bridge architecture. Read before touching engine contracts. |

## Stack (app/)

- Next.js 16 with `cacheComponents` (Partial Prerendering). Request data (cookies, params, searchParams) must be read inside `<Suspense>`.
- Neon project `soccer-vision` (`winter-waterfall-49930057`, `aws-eu-central-1`), declared in `app/neon.ts`:
  Postgres + Neon Auth (managed Better Auth) + Object Storage (`replays` private, `media` public) + Realtime (beta; live lobbies).
- Drizzle ORM (`app/src/server/db/schema.ts`, migrations in `app/drizzle/`). `neon_auth.*` is owned by Neon Auth.
- Engine: `yarn engine:build` publishes WASM to `public/engine/<hash>/` + `public/engine/current.json`.
  Typed worker `src/engine/engine.worker.ts`, client `src/engine/engine-client.ts`, renderer `src/engine/replay/`.
- Neon agent skills are in `.claude/skills/neon*`; the Neon MCP server is in `.mcp.json`. Use them before writing Neon code.
- Next.js 16 differs from older versions: read `app/node_modules/next/dist/docs/` for unfamiliar APIs (see `app/AGENTS.md`).

## Commands (run in `app/`)

```bash
yarn assets:build   # engine WASM + classic page (needs .NET 8 SDK, python3). Run once, and after engine changes.
yarn dev            # dev server
yarn check          # lint + typecheck + format check + unit tests (run before every PR)
yarn test:e2e       # Playwright (starts dev server on :3100). Uses test users from tests/e2e/fixtures.ts on Neon `dev`.
yarn build
yarn db:generate --name <x>   # after schema.ts changes; then yarn db:migrate (targets the branch in .env.local)
```

Engine tests (repo root): `dotnet test sim/` takes **28–45 minutes**. Run a filtered subset (`--filter`) for engine changes.

## Environments

| | Vercel env | Neon branch |
|---|---|---|
| Local | — (`.env.local`, from `neon checkout dev`) | `dev` |
| PR preview | Preview | `dev` (shared). Label **`own-db`** → own branch `preview/pr-<n>` |
| Production | Production | `main` |

CI (`.github/workflows/vercel.yml`) builds the engine, migrates the target branch, deploys prebuilt, adds the deployment URL
to that branch's Neon Auth trusted domains, comments the link, and runs Playwright smoke tests against previews.
Migrations on `dev` must be additive (shared by all previews); destructive ones need `own-db`.
Previews get their runtime env from that Neon branch at deploy (`vercel deploy -e`, incl. a fresh
`NEON_AUTH_COOKIE_SECRET`); this overrides the per-git-branch `DATABASE_URL` the Vercel<>Neon integration sets.

**Drizzle quirk:** `drizzle-kit generate` emits `CREATE TABLE "neon_auth"."user"` the first time a migration references it.
Delete that statement from the generated SQL (see `drizzle/0000_init.sql`).

## Rules

- **Branch → PR → Vercel preview.** Every branch gets its own PR. `main` is production (soccer.dev-spirit.com). Never commit to `main` directly.
- **All new code is TypeScript.** Ported demo-page JS is converted, never copied as `.js`.
- **Engine contracts are shared across runtimes.** Changing `sim/SoccerVision.Wasm/Program.cs` (LiveEngine exports), the replay
  frame layout (`sim/SoccerVision.Replay/ReplayRecorder.cs`) or `BridgeProtocol.cs` requires updating `app/src/engine/` (and
  `tools/demo-page/engine-worker.js`, `web/src/bridge/protocol.ts`). Determinism (seed → identical match) must not break.
- **Online play is lockstep** (`src/features/lobby/online-match.tsx`): host and guest run the same engine; the host assigns the
  frame of every input and publishes a watermark. Online matches must always step 5 frames at a time (lead > 0).
  `tests/e2e/online.spec.ts` proves both screens end with the same frame checksum — keep it green.
- **Lobby sync uses Neon Realtime** (`features/lobby/lobby-sync.ts`, `server/realtime.ts`): ONE live query (lobby row
  UNION ALL its inputs) so commits arrive atomically and in order — never split it into two subscriptions. Reads are
  pushed; the host writes only when it applied an input or every 30 frames. Polling is only the fallback when Realtime
  is unavailable (503 / origin not allowed). Allowed origins: `neon.ts` + the CI step that adds each preview URL.
- **Env vars** are validated in `app/src/env.ts`. Never read `process.env` directly elsewhere.
- Server data access: check the session in every Route Handler / Server Action (`currentUser()` / `withUser`), not only in `proxy.ts`.
- **Auth screens follow Figma** (file `xMPwRHYFfISl5Q6nRADVM6`, group "Authentication flow" node 160-214). Colors come from
  the tokens in `src/app/globals.css` (Figma "Primitives (Mor)" → `--mor-*`, "SoccerVision Web" → semantic `--color-*`);
  the reference for color roles is the Login screen (node 541-2333). Never hard-code hex in components.
- **Auth flow** (`features/auth/actions.ts`): every sign-in path ends in `nextPathAfterSignIn()` → `/auth/finish` until the
  profile has a username + terms (`profiles` table), then `redirectTo`. Password logins are throttled per email
  (`server/login-throttle.ts`, 5 failures / 15 min). Neon's server SDK rewrites Better Auth error codes, so classify
  errors with `classifyAuthError()` — never compare `error.code` directly. Emails pending verification/reset live in an
  httpOnly cookie, never in URLs. Facebook is not offered by Neon Managed Auth (button shows "coming soon").
  The browser proxy `app/api/auth/[...path]` is an allowlist (get-session, token, sign-in/social, link-social,
  sign-out): password, sign-up, OTP and reset paths must go through the Server Actions, or the lockout is skipped.
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
app/              (site)/: home play matches lobby terms · team/new/: manager club kits squad · auth/: sign-in sign-up verify-email forgot-password[/sent]
                  reset-password finish link-account callback error · api/*
components/       shared UI: brand/header, ui/ (button, text-field, checkbox, alert, icons)
features/<domain> auth, matches, lobby (client UI, zod schemas, server actions)
server/           db, auth, storage, matches, lobbies, http (server-only)
engine/           WASM worker + typed protocol + replay renderer
lib/              pure helpers
env.ts            env schema
proxy.ts          Next 16 proxy: Neon Auth redirect for member pages
```

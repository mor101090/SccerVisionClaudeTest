# Phase 3: code cleanup and debugging (2026-11-06)

Branch `chore/phase3-cleanup`, from `main` at `48989f8`. Scope: `main` as it is on this date. The ball-physics work is still on the
unmerged branch `feature/ball-physics` (stages 0-2), so the engine's ball code was not changed here. No gameplay, physics or
audio behaviour was changed.

## Fixes

| # | What was wrong | Fix | Checked by |
| - | -------------- | --- | ---------- |
| 1 | **Daily e2e run red since Nov 5.** `team.spec.ts` always waited for the "Squad complete" celebration, but the shared test account `E2E_Tester` has had a finished club since 2026-11-05 13:32, so every later run opens straight on the squad. | The test accepts either path: celebration (fresh account) or the squad page (existing club). | `team.spec.ts` passes locally. |
| 2 | **`match.spec.ts` broken by the homepage change (PR #15).** Signing in now lands on `/home`; the test still expected the match screen. | The test opens `/play` after signing in. | `match.spec.ts` passes (40 s). |
| 3 | **The Messages badge could never clear.** Nothing ever marked a message read (`markMessagesRead` existed but had no caller). | Opening Messages marks the inbox read (`markMessagesReadAction`, `features/messages/mark-read.tsx`) and refreshes the top bar. | In the browser: badge 1 → 0, `read_at` set in the database. |
| 4 | **New messages, notifications and transfer offers showed up late (minutes).** These lists were cached per user for "minutes", and a write by another person (the sender, a buyer) can't clear the recipient's cache. | `inbox`, `allNotifications` and `offersOf` are no longer cached; their pages call `connection()` (fresh per request, and allowed to compute "2m ago"). | In the browser: a message inserted in the database appears on the next load. No console errors on the three pages. |
| 5 | Error message said "Google sign-in didn't finish" after a failed **Facebook** login. | Now "Social sign-in didn't finish". | Shipped in PR #15 (`fa6833b`). |

## Cleanup

- Removed unused code: `toneText` / `toneFill` (`components/app-shell/ui.tsx`), `PlayerRow` and `squadForRequest` (`server/teams.ts`).
- One loading placeholder for match pages: `MatchSkeleton` (`components/ui/skeleton.tsx`) replaces three identical copies (Play, Replay, Online match).

## What was checked and found fine

- **Console errors**: every signed-in page (24 routes) loaded in Chrome with the browser console captured: no errors or warnings after the fixes above.
  A full match on `/play` at 64× ran with a clean console. The `ERROR_INVALID_RP_ID` passkey warning seen earlier on localhost is already gone
  (passkey autofill only runs on soccer.dev-spirit.com).
- **Event handling and memory**: every `addEventListener`, `setInterval` and `requestAnimationFrame` in `app/src` has its cleanup; every
  `EngineClient` (the WASM worker) is disposed when its page unmounts. Measured: opening and leaving the match page 5 times inside the app,
  JS heap 46 MB → 40 MB (no growth).
- **State management**: server data goes through cached per-user functions plus `revalidatePath` after the user's own writes; client state
  is local (`useState`), with `useSyncExternalStore` for localStorage, media queries and online status. The only problem found was #4.
- **Duplicated code**: a scan for repeated 8-line blocks found three; one fixed (MatchSkeleton), two left on purpose (see debt).
- **Integration**: the e2e suite runs the real flow end to end (sign-in, create a club, play with it, save, replay, online lockstep).

## Regression tests

| Suite | Result |
| ----- | ------ |
| `yarn check` (lint, typecheck, format, unit) | pass, 113 / 113 |
| Playwright, all but online (`auth`, `classic`, `match`, `team`) | 14 / 14 after fixes 1-2 |
| Playwright `online.spec.ts` (lockstep, same final checksum on both screens) | 2 / 2 (realtime and polling: identical result on both screens), 2.0 min locally |
| `dotnet test` filtered: SameSeed, Deterministic, StagedMatch, UseHomeTeam, Wasm, Replay | 14 / 14 (1 min). The full engine suite (28-45 min) was not run: no engine code changed. |

## Remaining technical debt

Ranked, most useful first.

1. **E2E test data is never reset.** `yarn e2e:seed` resets passwords and profiles but keeps clubs, so the "Squad complete" step is only
   tested on a brand-new account. Option: delete the test user's club in the seed (or create a fresh user per run).
2. **Old match and lobby screens use raw colours** (59 `zinc-*` / `emerald-*`… classes in `features/lobby/*`, `features/matches/*`,
   `app/(app)/matches/*`), against the token rule in CLAUDE.md. They need a Figma design for the match screens first; converting
   class by class would restyle them blindly.
3. **Top bar badges can lag about a minute** (`getShellData`, cached "minutes" so tab switching stays fast). Opening Notifications or
   Messages clears them. A Realtime push (like the lobby) would make them live.
4. **`app/src/engine/` keeps copies of `web/src`** (replay renderer, audio), held identical by `copies-in-sync.test.ts`. The app
   doesn't use the audio copy (`MatchSound`, `crowdReactions`) or part of the settings copy yet. Decide in the audio phase:
   wire audio into the app, or delete `web/` and the copies.
5. **Duplicated SQL in `server/account-match.ts`** (`accountForGoogleSignIn` / `accountSummary`): same query, different filters.
   Left alone: security-sensitive auth code, small. Merge into one query builder if it changes again.
6. **Unit tests spend 20 s creating jsdom 23 times** (vitest notice). `pool: "vmThreads"` or `isolate: false` would cut it.
7. **`online.spec.ts` took 8.5 minutes in CI** (2 minutes locally), most of the daily e2e run.
8. **Unused exports kept on purpose**: `SLOT_LABELS` (`features/team/lineup.ts`, port of `build_teams.py`, kept identical),
   `leagueMembers` (schema table for leagues, not built yet), `MEASURED_LEVEL` / `applyKit` / `loadSettings` / `saveSettings` /
   `samePlay` (copies of `web/`, see 4).
9. **Test messages on the dev database**: three messages from `E2E_Guest` to `E2E_Tester`, added while checking fix #3 and #4
   (deleting them was declined). Harmless; remove with
   `delete from messages where body like 'Phase 3 check%';` when convenient.
10. **Ball physics** (`feature/ball-physics`) is not merged; when it is, run the full engine suite and the golden fingerprint
    (`SimRunner --golden`).

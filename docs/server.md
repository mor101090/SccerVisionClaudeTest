# Server: APIs, database, caching, performance, security

How the server side of `app/` is put together, and the rules every new endpoint or table follows. Written for
engineers adding a feature; `CLAUDE.md` has the per-feature notes.

## 1. Layers

| Layer                     | Where                                    | Rule                                                                                                                                                                                                     |
| ------------------------- | ---------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Pages (Server Components) | `app/**/page.tsx`                        | Read data through `server/*` inside `<Suspense>` (Cache Components). Never query the DB directly.                                                                                                        |
| Server Actions            | `features/<domain>/actions.ts`           | Every action starts with `requireUser()`; inputs are parsed with zod (`safeParseAsync` when a refinement is async, e.g. `isCountry`). Return `{ error \| fields \| values \| ok }`; redirect on success. |
| Route Handlers            | `app/api/**/route.ts`                    | JSON APIs for the browser (engine, lobbies, replays). Wrapped in `withUser` (`server/http.ts`): 401 without a session. Only `/api/invite` is public (it only sets a cookie and redirects).               |
| Data access               | `server/*.ts` (`import "server-only"`)   | The only code that talks to Postgres (Drizzle) and Storage. Functions take the acting user's id and scope every query by it.                                                                             |
| Database                  | Supabase Postgres, `drizzle/` migrations | RLS on for every public table with no policies: the browser's Supabase client can't read app tables; only the server (`postgres` role) does.                                                             |

### Route handlers

| Route                                                                                           | Methods   | Purpose                                                                                                               |
| ----------------------------------------------------------------------------------------------- | --------- | --------------------------------------------------------------------------------------------------------------------- |
| `/api/matches`                                                                                  | GET, POST | List / save my matches (replay to the private `replays` bucket, squad checked against the stored club).               |
| `/api/matches/[id]/replay`                                                                      | GET       | My replay bytes (owner only).                                                                                         |
| `/api/lobbies`, `/join`, `/[id]`, `/[id]/start`, `/[id]/inputs`, `/[id]/progress`, `/[id]/live` | various   | Online lockstep (host/guest only; see CLAUDE.md "Online play").                                                       |
| `/api/invite?u=`                                                                                | GET       | Public. Stores the inviter's username (validated, must exist) in the httpOnly `sv_invite` cookie, then opens sign-up. |

### Server actions added with profiles, clubs and leagues

`features/profile/actions.ts`: `saveProfileAction`, `saveClubSettingsAction`, `deleteClubAction` (typed club name must
match), `donateAction` (amount €1–€500, Stripe Checkout), `followLeagueAction` (uuid checked).

## 2. Database

- Migrations are additive (dev is migrated by the daily e2e run and `yarn db:migrate`, prod by the deploy workflow).
  Custom SQL goes in the generated file below a `statement-breakpoint` (example: the `REVOKE` in `0009`).
- Every foreign key has a covering index (`0009_fk_indexes`), so cascades and joins never scan. Unique indexes double
  as lookups (`teams_owner_uq`, `profiles_username_lower_uq`, `donations_session_uq`).
- "No row" means the new-club state for the club-world tables (`server/home.ts`). Readers never invent numbers.
- Tables added for these screens: `donations` (one row per paid Stripe Checkout session, unique session id),
  `league_follows` (user × league). Columns: `profiles.bio / favourite_club / show_profile / show_history`,
  `teams.city / stadium_name`.

## 3. Caching

| Data                                                   | Strategy                                                                                                                                 | Why                                                                                                   |
| ------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------- |
| The signed-in user, profile, club, squad               | `"use cache: private"` + `cacheLife("minutes")`, plus React `cache()` per request (`currentUser`, `profileForRequest`, `teamForRequest`) | Read on every page by the layout and the page; writes call `revalidatePath`.                          |
| Countries                                              | `cacheLife` a day (`server/countries.ts`)                                                                                                | flagcdn list rarely changes.                                                                          |
| Inbox, notifications, offers                           | Not cached (`connection()`)                                                                                                              | Other people write them; a stale list was a bug (#16).                                                |
| Profiles, other managers, clubs, leagues, achievements | Not cached across requests; React `cache()` dedupes `managerById` / `clubOf` within one                                                  | Other managers' actions change them (a match saved, a profile hidden); queries are indexed and small. |
| Engine builds                                          | `Cache-Control: immutable` on `/engine/<hash>/`, `no-cache` on `current.json`                                                            | Content-hashed.                                                                                       |

## 4. Performance

- Profile pages read only the two `records` arrays from `matches.summary` (`summary->'home'->'records'`), not the full
  header (kilobytes a row), and at most 300 matches.
- The invites list counts "played a match" with one `EXISTS` per invited manager on `matches_user_played_idx`.
- Leagues list: one grouped query plus two `IN (...)` lookups (leaders, follows); no N+1.
- Pages stream: the shell renders at once, each page's data in its own `<Suspense>` with a skeleton.

## 5. Security

- **Authorization**: every action and route handler checks the session (`requireUser` / `withUser`), not just
  `proxy.ts`. Member routes are also listed in `proxy.ts` (`/profile`, `/managers`, `/clubs`, `/club`, `/invite`,
  `/support` added).
- **Privacy**: other managers see a club's positions, ages and overall ratings, never attributes or fitness.
  `show_profile = false` hides the profile (only the club stays visible); `show_history = false` hides results. Emails
  are never shown. Donor names and totals are never shown.
- **Payments**: amounts are validated on the server; `/support/thanks` trusts nothing from the URL except the session
  id (format-checked), then asks Stripe and requires `payment_status = paid` and `client_reference_id = this user`
  before inserting (idempotent on the session id). The Stripe key is server-only (`serverEnv.STRIPE_SECRET_KEY`).
- **Destructive actions**: deleting a club needs the exact club name, typed, checked on the server.
- **Headers** (`next.config.ts`): `X-Frame-Options: DENY`, `frame-ancestors 'none'`, `base-uri 'self'`,
  `form-action 'self' https://checkout.stripe.com`, `nosniff`, strict referrer, a restrictive `Permissions-Policy`.
- **Database**: RLS on everywhere with no policies (Supabase advisor 0008 lists them as INFO: intended). The
  `rls_auto_enable()` event-trigger function is not executable by `anon` / `authenticated` (`0009`).

## 6. To do outside the code

1. Supabase Auth → enable **leaked password protection** (advisor warning; a Pro-plan feature).
2. Set `STRIPE_SECRET_KEY` in Vercel (Production) to open donations; use a test key on previews.
3. Optional, later: a Stripe webhook (`checkout.session.completed`) so a donation is recorded even if the manager
   closes the tab before `/support/thanks`; monthly donations need the Stripe customer portal for cancelling.

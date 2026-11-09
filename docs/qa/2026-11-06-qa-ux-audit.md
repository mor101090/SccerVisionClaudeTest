# Soccer Vision: QA and UX audit

**Target:** https://soccer.dev-spirit.com (production, `main` at `48989f8`) · **Date:** 2026-11-06 · **Method:** hands-on in Chrome, Playwright device emulation, page instrumentation (Performance API, DOM checks), HTTP probes.

**Merged:** this report combines two independent reviews of the same build: this one (A) and a second live review (B, headless Chromium, two isolated sessions for online play). Findings only B reported are marked **[B]**: BUG-16, BUG-17, BUG-18 and observation O-1. Where the two disagree, section 10 says so.

---

## 0. Fix status (branch `fix/qa-audit`)

Fixed on `fix/qa-audit` and checked in a browser (local server, same code as production plus these changes). "Open" items need a product decision or a design first.

| Bug | Severity | Status | Fix |
| --- | -------- | ------ | --- |
| BUG-01 Classic page broken on phones | High | **Fixed** | Doctype, `lang`, viewport, icons; stray meta removed. The classic CSS already had phone rules, which now apply: no page-level horizontal scroll at 390 px. |
| BUG-02 Contact Us → 404 | High | **Fixed** | `mailto:` from `NEXT_PUBLIC_SUPPORT_EMAIL`; the link is hidden until the variable is set. |
| BUG-16 [B] Start match ignored while the engine loads | High | **Fixed** | The button is disabled with "Preparing the match..." until the engine sends the match; if the engine fails it says so. |
| BUG-03 Unsaved tactics lost | Medium | **Fixed** | Leaving with unsaved changes asks first (in-app links and reload/close). |
| BUG-04 Squeezed page headers on phones | Medium | **Fixed** | The title column keeps at least 18 rem; actions wrap below (PageHeader, Home, Tactics). |
| BUG-05 Kits screen opens at the bottom | Medium | **Fixed** | Focus without scrolling; the screen opens at the top. |
| BUG-06 No `<main>` on visitor pages | Medium | **Fixed** | Visitor layout is `<main id="main">`; skip link on every app page. |
| BUG-07 Empty settings sections, no Escape | Medium | **Fixed** | Pitch, Weather and Team colours are developer-only headings; players see "Display"; Escape closes. |
| BUG-08 Tactics tabs | Medium | **Fixed** | Tab panel, `aria-controls`, roving tabindex, arrow keys, Home/End. |
| BUG-17 [B] Manager tabs clipped on phones | Medium | **Fixed** | Edge fade while more tabs are off screen; the selected tab scrolls into view; arrow keys. |
| BUG-09 Opponent strength on the online path | Low | **Fixed** | Root cause: CSS `display` rules overrode the `hidden` attribute (also caused UX-4). One global `[hidden]` rule. |
| BUG-10 Login error announced twice | Low | **Fixed** (announcement) · **Kept** (notice) | The field message is no longer a second alert. The migration notice stays the same for everyone on purpose: showing it only to migrated accounts would reveal which emails exist. |
| BUG-11 Focus not moved to the first error | Low | **Fixed** | All auth forms focus the first invalid field. |
| BUG-12 favicon, robots, sitemap | Low | **Fixed** | `/favicon.ico` → app icon; `robots.ts`; `sitemap.ts`. |
| BUG-13 Gendered copy | Low | **Fixed** | "their / they" for the friend. Football players' role texts keep "he". |
| BUG-14 Start screen scrolls behind its overlay | Low | **Fixed** | Page scroll locked while a start screen is open. |
| BUG-15 Small tap targets | Low | **Fixed** | 24 px minimum: header links, breadcrumb, card links, classic icon buttons. |
| BUG-18 [B] Password toggle 26 px | Low | **Fixed** | 44×44 hit area. |
| UX-4 Mode asked twice | — | **Fixed** | Same root cause as BUG-09. |
| UX-6 Invite link needs a Join click | — | **Fixed** | Joins at once from the link. |
| UX-7 Logout lands on the classic game | — | **Fixed** | Lands on `/home`. |
| UX-14 Empty pitch on `/play` | — | **Fixed** | A hint until the first match. |
| UX-2 Landing without sign-up | — | **Partly** | "Create your club" is the landing's main call to action; the full landing redesign is open. |
| UX-1, UX-3, UX-5, UX-8, UX-9, UX-10, UX-11, UX-12, UX-13 | — | Open | Product or design decisions: see section 7. |
| H-1 to H-4, O-1 | — | Open | Not confirmed; see section 4. |

---

## 1. Executive summary

The product works. Every route answers, protected pages redirect correctly, the session survives reloads and new tabs, logout is clean, tactics persist, the match engine runs at 120 fps, and online play between two browsers stays in sync and survives a disconnect. Across 7 public production pages, the classic game and 20 signed-in pages at three screen sizes, I saw **no JavaScript errors and no failed requests**.

The biggest problems are at the **front door** and on **phones**:

1. **The default page (`/`, the classic game) is broken on phones.** It has no doctype and no viewport tag, so browsers render it in quirks mode as a 980 px desktop page shrunk onto the screen; 79 text elements in the match view are under 12 px. This is the first thing every visitor sees.
2. **"Contact Us" on every login and sign-up page leads to a 404**: it points at the GitHub issues of a private repository.
3. **Unsaved tactics are lost silently** when you leave the Tactics page.
4. Several smaller layout, accessibility and copy issues, listed below.

There are two products in one site: the classic single-page game (`/classic`) and the Next.js platform (`/home` and the club pages). They differ in look, navigation, team naming and quality bar. Most UX recommendations come down to joining them up.

| | Count |
| - | - |
| Confirmed bugs | 18 (High 3 · Medium 7 · Low 8), 3 of them from review B |
| Hypotheses (need confirmation) | 4 |
| UX / design suggestions | 14 |

---

## 2. Test coverage and limitations

### Covered

| Area | How | Where |
| ---- | --- | ----- |
| All routes (37 URLs): status, redirects, headers | HTTP probe | Production |
| Public pages: landing, rules, terms, login, sign-up, forgot password, visitor `/play` | Chrome + Performance API + DOM accessibility checks | Production |
| Classic game: start screen, kits, pre-match, line-ups, live match vs computer, in-match formation change, settings | Chrome, hands-on | Production |
| Classic online play: create game, join by link + code in a **second tab**, kick-off sync, host behaviour when the guest leaves | Chrome, two tabs | Production |
| Responsive: 390×844 (phone), 768×1024 (tablet), 1440×900 (desktop) | Playwright emulation, overflow / tap-target / font-size checks, screenshots | Production (public) and local (signed in) |
| Sign-in errors, redirect after login, session persistence (reload, new tab), logout, Back after logout | Playwright, test account | Local* |
| Club pages (20): home, squad, tactics, games, training, coaches, transfer, league, rankings, finances, stadium, forum, notifications, messages, search, settings, matches, lobby, play, rules | Playwright at 3 sizes + console / network capture | Local* |
| Tactics: change formation, keyboard on tabs, leave without saving, save, reload | Playwright | Local* |
| Club match, save, My matches, replay; app online lobby (two accounts, realtime and polling) | Existing e2e suite (`match.spec`, `online.spec`), all passing | Local* |
| Security headers, compression | HTTP | Production |

\* **Local** = the same code as production (`main` plus PR #16's small fixes) on `localhost:3000` against the dev Supabase project, signed in with the repository's e2e test accounts.

### Not covered, and why

- **No sign-up or login on production.** Creating accounts or entering passwords on the live site is outside what I'm allowed to do; every signed-in check ran locally on the same code. Re-running section 9's tests against production with a dedicated test account would close this gap.
- **Email flows** (verification code, password reset email) and **social login** (Google, Facebook, Discord) need a real inbox or provider account.
- **Passkeys** only work on soccer.dev-spirit.com, so they need a production session.
- **Real devices, Safari and Firefox**: layouts were emulated in Chromium only; no iOS Safari, no Firefox.
- **Screen readers**: accessibility was checked from the DOM (names, labels, landmarks, ARIA, focus) and keyboard behaviour, not with VoiceOver or NVDA.
- **Lighthouse** was not run; performance figures come from the browser's Performance API on production.
- **Signed-in load times** were measured on a local dev server and are not representative of production.

---

## 3. Confirmed bugs (by severity)

Severity: **High** = blocks or badly degrades a main path for many users · **Medium** = a real defect with a workaround · **Low** = polish, minor accessibility or copy.

### BUG-01 · High · The default page is unusable on phones (quirks mode, no viewport)

- **Where:** `/` → `/classic/index.html` (built from `tools/demo-page/template.html`).
- **Steps:** Open https://soccer.dev-spirit.com on a phone (or emulate 390×844).
- **Expected:** a mobile layout with readable text.
- **Actual:** the page is rendered as a 980 px desktop layout scaled down. In the match view, 79 text elements are under 12 px (most around 6–8 px on screen). The document starts at `<meta charset>`: there is no `<!doctype html>`, no `<html lang>`, no `<meta name="viewport">`, and no favicon. A stray `<meta name="claude:viewport-fit-sides">` is present.
- **Evidence:** `document.compatMode === "BackCompat"`, `doctype: false`, `lang: ""`, `viewport: false` (production, Chrome). Screenshot: `mobile_classic_match.png`. Responsive run: `sw=980/390` on every classic screen.
- **Fix:** add `<!doctype html><html lang="en">`, `<meta name="viewport" content="width=device-width, initial-scale=1">` and a favicon link to the template; remove the stray meta; then fix whatever the switch to standards mode changes.

### BUG-02 · High · "Contact Us" leads to a 404

- **Where:** header of every auth page (`/auth/sign-in`, `/auth/sign-up`, `/auth/forgot-password`, …).
- **Steps:** Open `/auth/sign-in` → click **Contact Us**.
- **Expected:** a way to reach the team.
- **Actual:** goes to `https://github.com/SoccerVision/soccer-vision/issues`; the repository is private, so visitors get GitHub's 404.
- **Evidence:** `curl` → `404`; `gh repo view` → `PRIVATE`.
- **Fix:** link to a public contact page, a `mailto:`, or a public issue tracker.

### BUG-03 · Medium · Leaving Tactics discards unsaved changes without warning

- **Steps:** `/tactics` → change **Shape** from 4-4-2 to 4-3-3 (button changes to "Save tactics") → click **Home** in the sidebar → go back to `/tactics`.
- **Expected:** a "You have unsaved changes" prompt, or a draft kept.
- **Actual:** no prompt; the shape is back to 4-4-2.
- **Evidence:** Playwright: `left dirty tactics: dialog? false` → `tactics after leave (unsaved): 4-4-2`.

### BUG-04 · Medium · Page headers squeeze the title and subtitle on phones

- **Where:** shared `PageHeader` (`components/app-shell/ui.tsx`), seen on Home and Tactics.
- **Steps:** Sign in, open `/home` or `/tactics` at 390 px width.
- **Expected:** title and subtitle full width, actions wrap underneath.
- **Actual:** the subtitle column shrinks beside the action buttons and wraps every one or two words ("Leagues / are coming / soon. …"; "Saved plan · / used in your / next match …").
- **Evidence:** `local_mobile_home.png`, `local_mobile_tactics.png`. Cause: the text column is `flex-1 min-w-0` in a `flex-wrap` row, so it shrinks instead of wrapping.

### BUG-05 · Medium · The kits screen opens scrolled to the bottom

- **Steps:** Open an invite link (`/classic/index.html#game=<code>`) or click a mode card after scrolling the start screen.
- **Expected:** the kits screen opens at the top ("Your kits").
- **Actual:** it opens at the bottom: the kit pickers are off screen and the first thing visible is the mode buttons.
- **Evidence:** focus lands on `#kitGo` / `#kitHost` / `#kitJoin` (the bottom buttons); the browser scrolls them into view. Screenshots taken during the session.
- **Fix:** focus the screen heading, or `focus({ preventScroll: true })` and `scrollTo(0, 0)`.

### BUG-06 · Medium · Visitor pages have no `<main>` landmark

- **Where:** `/rules`, `/terms`, `/play` when signed out (`/home` and the auth pages are fine; all signed-in pages are fine).
- **Evidence:** `mains: 0` on production for all three.
- **Impact:** screen-reader users can't jump to the content; "skip to content" can't work.
- **Fix:** the visitor branch of `(app)/layout.tsx` wraps the page in a `<div>`; make it `<main>`.

### BUG-07 · Medium · Classic settings: empty sections, and Escape doesn't close the panel

- **Steps:** In a classic match, click the gear (⚙).
- **Actual:** "Pitch" and "Weather" are headings with nothing under them (their rows are `dev-only` and hidden). "Gestures and emotions" and "Colour-blind" sit under "Team colours", which they aren't. Pressing **Escape** leaves the panel open.
- **Evidence:** DOM: the next sibling of each heading is `.srow.dev-only` with `display: none`; screenshot after Escape still shows the panel.

### BUG-08 · Medium · Tactics tabs aren't a working tab widget

- **Where:** `/tactics` (`features/tactics/tactics-editor.tsx`).
- **Actual:** buttons have `role="tab"`, but there are no `role="tabpanel"` elements and no `aria-controls`, and Left/Right arrow keys don't move between tabs.
- **Evidence:** Playwright: `ArrowRight moves focus to "Formation & lineup"` (stays on the same tab), `tabpanel count 0`.
- **Fix:** add the tab panels and roving tabindex with arrow-key handling, or drop the tab roles and use plain buttons.

### BUG-09 · Low · Online path offers an "Opponent strength" setting

- **Steps:** Classic → **Play a friend online** (or an invite link).
- **Actual:** the kits screen shows **Opponent: Weak / Medium / Strong**, which only means something against the computer.

### BUG-10 · Low · Login error is announced twice; migration notice shown to everyone

- **Steps:** Local: sign in with a valid email and a wrong password.
- **Actual:** two `role="alert"` regions: "Wrong email or password. We upgraded our login: if you signed up with email before, reset your password once." and "Wrong email or password." Screen readers read both, and every user sees the migration notice.

### BUG-11 · Low · Focus isn't moved to the first invalid field

- **Steps:** `/auth/sign-in` → click **Login** with empty fields.
- **Actual:** errors appear correctly (`aria-invalid`, linked messages), but focus stays on the button.

### BUG-12 · Low · Missing `/favicon.ico`, `robots.txt`, `sitemap.xml`

- **Evidence:** all three return 404 on production. The app pages declare `icon.png`, but the classic page has no icon link, so browsers fall back to `/favicon.ico` → 404.

### BUG-13 · Low · Gendered copy

- "join one with **his** code", "He opens SoccerVision, picks **his** kit", "Your friend picks **his** own kits", "waiting for **him** to come back". Use "their / them".

### BUG-14 · Low · The start screen scrolls behind its overlay

- **Steps:** `/` → scroll.
- **Actual:** the scrollbar moves (the 2,966 px match page sits behind the fixed start overlay) but nothing on screen changes.

### BUG-15 · Low · Small tap targets in headers and breadcrumbs

- **Evidence:** "Play" 30×22, "Login" 40×22, breadcrumb "Home" 34×16, "Contact Us" 79×22, classic "Pause" 34×18; on the signed-in Home: "Fixtures" and "Stats" links. WCAG 2.2 target size (AA) asks for at least 24×24.

### BUG-16 · High · [B] Start match is silently ignored while the classic engine loads

- **Steps:** Fresh browser → `/` → **Play the computer** → **Play the computer** → press **Start match** at once, before the formation list fills.
- **Expected:** the button is disabled or shows that the match is being prepared.
- **Actual:** nothing happens: the pre-match box stays, no message. The handler (`#kickoff` in `16-interaction.js`) returned while the match header `R` was still empty. A press after the engine is ready works.
- **Evidence:** review B reproduced it in a fresh session (empty formation list right after the press; 12 formations and a working button once ready). Confirmed in the code (`if (!R || KICKED) return;`).

### BUG-17 · Medium · [B] Classic manager tabs are clipped on phones with no hint

- **Steps:** Classic match at 375–390 px → scroll to the manager panel.
- **Actual:** the tab strip scrolls sideways with its scrollbar hidden; "Corners" shows as "Cor" and the remaining tabs are invisible.
- **Evidence:** review B at 375×812; reproduced here at 390×844 once BUG-01 was fixed (`fix_mobile_mtabs.png`).

### BUG-18 · Low · [B] Show-password button is a 26×26 px target

- **Where:** password fields on the auth screens. It meets WCAG 2.2's 24 px minimum but sits next to 44 px controls; on a phone it's easy to miss.

---

## 4. Hypotheses (not confirmed)

| # | Observation | What would confirm it |
| - | ----------- | --------------------- |
| H-1 | `[auth] passkey autofill stopped {code: ERROR_PASSTHROUGH_SEE_CAUSE_PROPERTY}` logged on `/auth/sign-in` in headless Chromium on production. Browsers without a platform authenticator may log it for real users. | Open the login page in a desktop browser with no passkey support; check the console. |
| H-2 | Top bar badges (notifications, messages, offers) lag up to about a minute after someone else writes (`getShellData` is cached "minutes"). Known from PR #16. | Two accounts: send a message, watch the other's badge. |
| H-3 | A classic online match can't be ended by the host while waiting for a disconnected friend; the only way out is Settings → "Back to the start screen". | Disconnect mid-match and look for an end/forfeit action. |
| H-4 | Signed-in pages may feel slow on first load in production (local dev: 0.7–1.5 s per page; production prefetch was measured earlier at 10–50 ms per click). | Real-user monitoring or Lighthouse on a signed-in production session. |

---

## 5. UX/UI review

### What works well
- **Auth screens** are polished: clear labels, correct `autocomplete`, accessible errors, social and passkey options, a mobile layout that drops the hero image.
- **Signed-in app shell**: consistent sidebar and top bar, bottom tab bar on phones, every page with a clear `<h1>`, honest "coming soon" states instead of fake numbers.
- **The match itself**: smooth (120 fps), line-up presentation, live commentary, in-match tactics with an immediate redraw, a substitution planner, colour-blind mode.
- **Online play**: a 6-digit code plus link, a sync that holds, and a clear message when the friend drops.

### Problems

| # | Issue | Impact |
| - | ----- | ------ |
| UX-1 | **Two products, two looks.** The classic game (Barlow, navy, broadcast style) and the platform (Inter, purple, Mor tokens) differ in type, colour, navigation and naming. The visitor `/play` page is a third style (monospace scoreboard, emerald buttons, an empty green box). | Feels unfinished; users don't know which is "the game". |
| UX-2 | **The front door is the classic game, not the product.** `/` opens the classic mode picker; the platform hides behind "Real website platform". The logged-out `/home` is just a logo, one sentence and three buttons, with no sign-up call to action. | Low sign-up conversion; new players never discover clubs, squads and saved matches. |
| UX-3 | **A match against the computer takes 20+ minutes**: "10 real minutes per half", and only 1× speed is offered in a live match (2×–16× exist but are hidden). | Long first session; high drop-off risk. |
| UX-4 | **Mode chosen twice.** After "Play the computer", the kits screen offers Play the computer / Create a game / Join / Invite again. | Confusing; undoes the first choice. |
| UX-5 | **Teams are "Team A" and "Team B"** in the classic game, and the opponent on the signed-in homepage's last match is "Team B". | Breaks immersion; your own club name isn't used. |
| UX-6 | **Invite link still needs a click on "Join"** after pre-filling the code. | One extra step for the friend. |
| UX-7 | **Logout lands on the classic game** (`/classic/index.html`), not the login or landing page. | Disorienting; looks like you've left the product. |
| UX-8 | **Squad table on phones** scrolls sideways inside its card (760–900 px minimum width), and the filter tabs are cut off at the edge. | Hard to scan a squad on a phone. |
| UX-9 | **180 kit-colour swatches are each a Tab stop** before the "Play" button. | Keyboard users need 180+ presses. |
| UX-10 | **Header inconsistency**: platform pages show "Play / Login"; auth pages show "Contact Us"; the classic page has no header at all. | Navigation changes from page to page. |
| UX-11 | **Club crest field** has a placeholder but no label, and "Create" gives no hint of what it does (AI-generated crest?). | Unclear feature. |
| UX-12 | **Terminology mix**: "Login" (noun) used as a verb, "Log in with a passkey", "Sign out" vs "Log out". | Minor polish. |
| UX-13 | **Developer mode is offered to everyone** on the start screen. | Clutters the first choice. |
| UX-14 | **Empty pitch** on the visitor `/play` page before a match: a green box with no explanation. | Looks broken. |

---

## 6. Responsive and accessibility findings

### Responsive (390 / 768 / 1440 px)

| Surface | Phone | Tablet | Desktop |
| ------- | ----- | ------ | ------- |
| Platform public pages (7) | OK, no horizontal scroll | OK | OK |
| Platform signed-in pages (20) | OK except BUG-04 (squeezed headers) and UX-8 (squad table) | OK | OK |
| Classic game (start, kits, match) | **Broken (BUG-01)**: 980 px layout scaled down | Scaled down (980 > 768) | OK |

### Accessibility

| Check | Result |
| ----- | ------ |
| Page language | Platform `lang="en"` ✓ · Classic missing ✗ (BUG-01) |
| Landmarks | Signed-in pages and auth: one `<main>` ✓ · Visitor `/rules`, `/terms`, `/play` none ✗ (BUG-06) · Classic none ✗ |
| Headings | Every platform page has one `<h1>` ✓ |
| Names and labels | No unnamed buttons or links; all platform inputs labelled ✓ · Classic crest input unlabelled ✗ |
| Form errors | `aria-invalid` + `aria-describedby` + `role="alert"` ✓ · duplicate announcement (BUG-10) · focus not moved (BUG-11) |
| Keyboard | Tactics tabs (BUG-08) · Escape on classic settings (BUG-07) · 180 swatch stops (UX-9) |
| Toggles | Classic swatches and settings use `aria-pressed` ✓ |
| Target size | Several under 24×24 (BUG-15) |
| Skip link | None on any page |
| Reduced motion | Supported in the platform CSS ✓ |

### Performance (production, desktop, logged out)

| Page | TTFB | FCP | LCP | CLS | JS (compressed) |
| ---- | ---- | --- | --- | --- | --------------- |
| `/home` | 71 ms | 1,172 ms | 1,256 ms | 0 | 276 KB |
| `/rules` | 72 ms | 520 ms | 520 ms | 0 | 276 KB |
| `/terms` | 77 ms | 668 ms | 668 ms | 0 | 274 KB |
| `/auth/sign-in` | 70 ms | 300 ms | 356 ms | 0.002 | 259 KB |
| `/auth/sign-up` | 67 ms | 296 ms | 928 ms | 0 | 273 KB |
| `/play` | 70 ms | 680 ms | 680 ms | 0 | 290 KB |
| `/classic/index.html` | 141 ms | 244 ms | n/a | 0 | inline (230 KB HTML, Brotli) |

- All pages are well within "good" Core Web Vitals. `/home`'s FCP (1.2 s with a 71 ms TTFB) is the slowest; it is worth checking what blocks first paint there.
- The match runs at **120 fps**, worst frame 10 ms; JS heap 13 MB. Opening and leaving the app's match page 5 times went from 46 MB to 40 MB (no leak).
- Security headers are good: CSP `frame-ancestors 'none'`, `X-Frame-Options: DENY`, HSTS (2 years), `nosniff`, a strict referrer policy and a permissions policy.

---

## 7. Recommended redesigns and user flows

1. **One front door.** Make `/` the platform landing page: hero, "Play now (no account)" and "Create your club" calls to action, three feature tiles (build a club, set tactics, play friends), and a "Classic game" link in the footer. Signed-in users go straight to `/home`.
2. **First match in under 5 minutes.** Add speed (2×/4×/8×) to live matches against the computer, or a "Quick match" (5-minute halves) next to the full one.
3. **A single match flow for both products.** Choose opponent (computer / friend) → kits (prefilled from your club when signed in) → line-up → match. Ask the mode once. Use your club's name and crest, not "Team A".
4. **Friend invite.** The link joins immediately and shows "Joining Harbour Rovers' game…", with a fallback to typing the code.
5. **A squad that works on phones.** On phones, show a card per player (name, position badge, age, level, a fitness bar), with sticky position filters as horizontally scrollable chips.
6. **Safe tactics editing.** Autosave a draft, or warn when leaving with unsaved changes; also show "Unsaved changes" next to the Save button.
7. **Logout** returns to the landing page with a "You're signed out" toast.
8. **Consistent header** across classic, auth and platform: logo, Play, Rules, Log in / avatar. A real contact route (BUG-02).

---

## 8. Prioritized engineering backlog

| P | Item | Bugs | Effort |
| - | ---- | ---- | ------ |
| P0 | Classic template: doctype, `lang`, viewport, favicon; remove the stray meta; fix the fallout from standards mode | BUG-01, BUG-12 | 0.5–1 day |
| P0 | Replace the Contact Us link | BUG-02 | 15 min |
| P1 | Unsaved-changes guard (or draft autosave) on Tactics | BUG-03 | 2–3 h |
| P1 | `PageHeader`: let actions wrap below the title on narrow screens | BUG-04 | 30 min |
| P1 | Visitor layout: `<main>` instead of `<div>`; add a skip link to both layouts | BUG-06 | 30 min |
| P1 | Kits screen: no scroll jump on open | BUG-05 | 30 min |
| P1 | Speed control (or quick match) for live matches against the computer | UX-3 | 0.5 day |
| P2 | Classic settings: hide empty sections, regroup rows, close on Escape | BUG-07 | 1 h |
| P2 | Tactics tabs: proper tab panels and arrow keys | BUG-08 | 1–2 h |
| P2 | Login: one announced error; show the migration notice only to migrated users; focus the first invalid field | BUG-10, BUG-11 | 1–2 h |
| P2 | Hide "Opponent strength" on the online path; ask the mode only once | BUG-09, UX-4 | 1–2 h |
| P2 | Logout destination; consistent header; terminology pass | UX-7, UX-10, UX-12 | 0.5 day |
| P2 | Mobile squad cards | UX-8 | 0.5–1 day |
| P3 | Neutral copy (their/them) | BUG-13 | 15 min |
| P3 | Tap targets to 24×24 or larger in headers and breadcrumbs | BUG-15 | 1 h |
| P3 | `robots.txt`, `sitemap.xml`, Open Graph tags | BUG-12 | 1 h |
| P3 | Swatch groups with roving tabindex (one Tab stop per row) | UX-9 | 1–2 h |
| P3 | Lock page scroll behind the classic start overlay | BUG-14 | 15 min |
| Product | Landing page and unified match flow (section 7, items 1, 3, 4) | UX-1, UX-2, UX-5, UX-6, UX-13, UX-14 | design first |

---

## 9. Suggested automated regression tests

Playwright, in `app/tests/e2e/` unless noted. The first five would have caught every High and Medium bug above.

| Test | Asserts | Catches |
| ---- | ------- | ------- |
| `classic-standards.spec` | `/classic/index.html`: `document.compatMode === "CSS1Compat"`, `html[lang]`, `meta[name=viewport]`; at 390 px no element wider than the viewport and no visible text under 12 px on the start, kits and match screens | BUG-01 |
| `links.spec` | Every external link in the header and footer of public pages answers 200 (HEAD request) | BUG-02 |
| `tactics-unsaved.spec` | Change the shape, click Home → expect a confirm dialog (or the draft restored on return) | BUG-03 |
| `landmarks.spec` | For every public and signed-in route: exactly one `main`, one `h1`, `html[lang]` | BUG-06, BUG-01 |
| `mobile-layout.spec` | At 390×844 for all signed-in routes: no horizontal page scroll; the `h1` and its subtitle are at least 60 % of the viewport wide | BUG-04 |
| `a11y.spec` | Run `@axe-core/playwright` (devDependency) on all routes, signed out and signed in; fail on serious or critical | BUG-06, BUG-08, BUG-15, future regressions |
| `classic-kits.spec` | Open `#game=123456` → `scrollY === 0` and the "Your kits" heading is in view; the online path has no "Opponent" control | BUG-05, BUG-09 |
| `classic-settings.spec` | Open settings → no section heading without visible rows; Escape closes it | BUG-07 |
| `tactics-tabs.spec` | Focus the first tab → ArrowRight selects the second; each tab has `aria-controls` → an existing `tabpanel` | BUG-08 |
| `auth-errors.spec` | Wrong password → exactly one `role=alert`; empty submit → focus on the email field | BUG-10, BUG-11 |
| `logout.spec` | Log out → lands on the agreed page; Back doesn't show account data; protected route → sign-in with `redirectTo` | UX-7, regression |
| `session.spec` | After login: reload, new tab, and a 30-minute idle with a refreshed token keep the session | regression |
| `classic-online.spec` | Two contexts: host creates, guest joins by link, both clocks equal within 1 s after kick-off; the guest closes → the host shows the "connection lost" banner | regression (the app lobby is already covered by `online.spec`) |
| `perf-budget.spec` (CI, production) | `/home` and `/auth/sign-in`: LCP < 2.5 s, CLS < 0.1, JS < 350 KB | performance regressions |
| `seo-basics.spec` | `/robots.txt` 200, `/favicon.ico` 200 or an icon link present on every page | BUG-12 |

---

## 10. Merging the two reviews

| Topic | Review A (this report) | Review B | Merged view |
| ----- | ---------------------- | -------- | ----------- |
| Start-match race | Not hit (pressed after the line-ups loaded) | **Found** (fresh context, immediate press) | Confirmed (BUG-16), fixed. |
| Classic on phones | Quirks mode, 980 px layout (BUG-01) | "No document-level horizontal overflow at 375 px" | Both are true: with no viewport tag the page lays out at 980 px and scales down, so nothing overflows; it is just unreadable. Fixed. |
| Manager tabs on phones | Not seen (page was scaled) | Clipped "Corners" | Visible once BUG-01 is fixed; fixed as BUG-17. |
| Login validation | Custom validation (`noValidate`), accessible errors | "Native validation" | The form uses `noValidate` and server-side messages; both reviews agree the errors are clear. |
| Password toggle | Not measured | 26×26 px | BUG-18, fixed. |
| Online play | Code + link, sync, disconnect message | Code, kit clash resolution, sync | Both confirm it works; A adds the disconnect path. |
| Signed-in platform | Covered locally (same code) | Not covered | Covered by A only; a production pass with a test account is still recommended. |
| Passkey console warning | H-1 | Same observation | One open hypothesis (H-1). |
| Root `/` opens the classic game | UX-2 | Same | One open product decision. |

**O-1 · [B] observation:** the sign-up page made one cancelled `_rsc` request for `/terms` during navigation (a prefetch cancelled by the navigation). No user-visible effect; not reproduced. Not a bug unless it recurs.

**From review B's backlog, still open:** instrument engine start-up time, ignored start attempts, lobby join and connection failure reasons, and WebAssembly errors (P2); a production pass with disposable approved accounts (P1).

**Added regression tests (from B):** block the engine worker and assert a visible error with a retry path; a fresh classic session asserts **Start match** is disabled until the formations are ready (now implemented behaviour).

## Appendix: evidence

Screenshots (Playwright, 390 / 768 / 1440 px) are in the session scratchpad:
`/private/tmp/claude-501/-Volumes-Lexar-projects-soccer-vision/f5810980-9fe6-4b6c-acda-7a04e292f50e/scratchpad/qa/` (classic start, kits and match; public pages; signed-in home, squad, tactics, games, play and settings). The script used for the public responsive sweep is `responsive.mjs` in the same folder.

Test data created: one online classic game (code 173277, anonymous, abandoned). No production account was created and no other user's data was accessed.

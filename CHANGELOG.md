# Changelog

Each round of changes is developed on its own branch, named after what it changes. Newest first.

## `feature/key-events-and-keeper`

- **The colours of the lines** (the key moments under the statistics, the commentary, and the line under the pitch): one scheme everywhere.
  A goal is green, a yellow card yellow, a red card red, a substitution light blue - everything else (the woodwork too, fouls,
  injuries, penalties, offsides, shots) in the plain colour. The engine's `Commentary.ToneOf` follows it, so the React viewer and Unity
  show the same.
- **The key moments are the events a coach needs**: goals, cards, penalties, injuries, substitutions, the woodwork, a dangerous free kick,
  a direct free-kick shot, the shoot-out - and now **the dangerous chances** (a shot the model rated 0.3 or more). Offsides are no
  longer there. A trick (a fake shot, a heel flick, a dummy) has no line at all, in the moments, the commentary or the bar under the pitch.
  (The tricks still happen in the match - the fake shot sells the defender and the player goes round him - but the animation is only
  the swing of the leg, which cannot be told from a shot: a line for it was telling the viewer what he could not see.)
- **A heel flick is a heel flick**: to a team-mate behind him or beside him (it was only behind) he plays it where he stands - he
  does not turn first - and the foot goes the other way: forward in the windup, back through the ball; he keeps looking forward
  (and running) for a moment instead of turning to watch it go. (Over 16 matches: 54 heel flicks, all of them with the player
  looking away from the ball.)
- **A fake shot moves the ball**: the foot swings the whole way as if kicking, the ball is nudged to the side he is about to go (in or
  out, whichever way the fake goes) and carried on, the defender who was sold is out of it for a second (was three quarters), and
  then the player dribbles on or passes. (Over 16 matches with flair players: 57 fake shots, the ball nudged in 35.)
- **Nutmegs** (the ball through the opponent's legs): a dribbler with a defender coming at him head on, a metre or two away, pushes the
  ball between his legs and goes round to collect it. Flair players do it far more: the trick-lovers (Showboat), the dribblers
  (LovesToDribble) and the ones with the touch and the nerve (dribbling, creativity, composure); a player with no special ability
  hardly tries. It does not always work - a poor touch or a crowd and the ball hits a leg and goes to the defender. When it works the
  defender is out of the game for a second with his legs drawn open, and the dribbler is sent after his own ball. In the statistics:
  `Nutmegs` and `NutmegsWon`. Over 60 matches: about 3.4 a match (both teams), 1.8 of them working, 1.3 where the dribbler got the
  ball back; a squad of flair players 8 a match against 0.4 for a squad with no special ability.
- **Opening the legs - the dummy - is more frequent**: a receiver with a marker on him lets the pass run through his legs to a team-mate
  further along its line, and now looks for the team-mate in the best spot - above all with a better angle and distance for a shot -
  rather than the first one he finds; the better placed the man behind, the likelier it is. The flair players (Showboat) do it nearly
  twice as often, a player with no feel for the game hardly at all. About 4.5 a match for both teams (it was 1.7), his own legs drawn
  open while the ball goes through. In the statistics: `Dummies`.
- The unselfish player shoots a little less than before (the trait's weight on the shot x0.62, was x0.8): the full-suite run showed
  the effect, small, was lost in the noise of a sample of twelve matches (16.6 shots against 16.8); the test now plays 24.
  Not while he cuts inside: the winger's curler is his signature (bisecting a drop in the cut-inside curlers from 0.7 to 0.15 a match
  found this very change: the home winger has the trait). A heel flick to the side is three times rarer than one straight back
  (4.4 a match, was 6.3). The cut-inside test plays 36 matches.
- **The inverted winger cuts in and shoots - at least three times a match** (a winger with his strong foot on the inside: a
  right-footer on the left, a left-footer on the right). He was at about half a shot a match. The run in towards goal is worth much
  more to him, and once he is cutting in the shot is what he goes for (from the flank, up to 24 m wide of the posts and from the edge
  of the box); the others' shots from range are rarer to make room. Over 100 matches the home winger takes 4.5 shots after a cut
  inside a match, the away one 2.9; there are about 7.5 for both teams, against 2.2 before. Goals per match 2.45 (they were 2.6-2.7):
  these shots are mostly saved - the finishing and the keepers want another look.
- The long-shots style keeps its weight against the cut-in change (x3.2 on the shot from range, and it is spared the general
  reduction of other shots): over 60 matches 9.8 shots from 18 m+ against 6.3 for normal play (which includes the winger's own
  cut-in shots from the flank). The two tests that count them play more matches (24) and ask for a third more, not a half.
- **The manager's instruction beats the trait**: a winger with the inverted-winger role cuts in and shoots whatever his skill and
  whatever his traits (an unselfish one too; before he needed a skill of 45), and the unselfish trait does not hold his shot back.
  (Test: two unselfish wingers with the instruction take 9.6 cut-in shots a match between them.)
- **The keeper goes up for a high ball**: the jump grows with the height of the ball (a ball in the top corner: up to 0.7 m, his cap
  is 0.3 m more than a field player's) and he is drawn larger and higher.
- **The keeper's dive**: no gloves, no trail, and no jump back to the round disc at the end - the stretch comes on in about a tenth of
  a second and relaxes over about a quarter of one. The disc stretches along the line of the dive and squashes a little across it (a
  little less than before), the arms reach as it stretches, the shadow stays on the grass, and a keeper who jumps high for a ball in the
  top corner is drawn larger and higher.

## `feature/player-intelligence`

The players read the game better (from tactical analyses of top teams: splitting the press, tempo control, deep completions) -
behaviours, not tactics to set (`AI/MatchEngine.TeamAI.Intelligence.cs`):

- **Splitting the presser**: the two players nearest the ball offer it on both sides of the man closing it down (he cannot shut two
  lanes), at a distance that makes him choose - not so close that he covers both, not so far that the pass is slow. When the man on the
  ball faces his own goal or is squeezed, one of them drops off behind him for the way out. How well a player does it is his reading of the
  game (off the ball, decisions). In a sample of matches the second man takes the other side of the presser in about three of four choices.
- **The tempo of the moment**: the manager's tempo is the base; the team calms down against a press in its own half (draws it on, plays
  short) and speeds up for a few seconds after winning the ball, while the opponents are out of shape; against a deep block it keeps the
  ball moving. It eases over about a second. The passing (weight, the value of going forward or back) and the time on the ball follow it.
- **Deep completions**: a ground ball into the space within ~20 m of the goal, in front of it or in a half-space, is worth more to a
  creative passer than a hopeful cross; counted in the team statistics (`DeepCompletions`, about 6-7 per team a match).
- Calibration (60 matches, before -> after): goals 3.72 -> 3.67, shots 35.8 -> 35.0, passes 403 -> 411, completion 77.9% -> 78.4%,
  headed goals 11.7% -> 14.1% of goals; the rest within the noise.
- **Fewer hopeful balls in behind** (a match ended 0-0 with ball after ball over the top for the keeper): measured over 30 matches there
  were ~25 balls in behind a match, a third reached the runner and ~9 ended in the keeper's hands. The passer now weighs the keeper's race
  (he reads the ball as it is played, comes off his line and may use his hands; a passer with poor decisions overrates his runner), a ball
  the runner has little chance of reaching is not considered, and the ball in behind is a gamble taken when it is on, not the default:
  ~9 a match, ~2 for the keeper. (Balls into the path of a team-mate already running are unchanged.)
- **Fix: a ball on the roof of the net stayed there for the rest of the match** - the roof stopped it every frame and a ball was only
  collected lying on the grass. It now rolls off the back of the roof and drops behind the goal, and no ball that went out is shown for
  more than 9 s.
- **The teams are Team A and Team B** (they were "Red Rovers" and "Blue Falcons" - odd once you change their colours): in the engine, the
  page, the commentary and the React app.
- **A ball that goes out is gone once it leaves the picture**: nothing beyond the lines (the advertising boards used to) sends it back
  into view. It still bounces off the outside of the net.
- **Finishing like a forward, keeping goal like a keeper** (forwards ran on until they stood on the keeper, then shot; the
  keeper charged out on a crooked path or from a tight angle and left the goal empty):
  - The forward's idea of his chance now sees the keeper: how much of the goal mouth the keeper covers from where the forward
    stands (the spread body close in, the dive further out), and for carrying the ball on, where the keeper will be by then. So
    he shoots while there is goal to aim at, squares it to a free team-mate, or goes round the keeper - he does not run into him.
    (Before, the estimate knew only the distance and the angle, so every step closer looked better.)
  - First-time finishes: a pass into the box at a good angle - a good finisher has set his body and hits it at once, before the
    keeper and the defenders close him down (about 2.4 a match, 16% go in).
  - A keeper right on the forward is a wall: a shot that is not past his body hits him (the close-range bonus for the shooter
    applied even then).
  - The keeper gets onto the line between the ball and the goal before he comes out (no crooked run), guards his near post
    from a tight angle (no charge that leaves the goal for the cut-back), comes less when another forward is free in the box,
    and no further than 6-10 m (the bold keeper the furthest).
  - Measured over 100 matches: shots with the keeper within 3 m in front of the shooter 36% -> 18% of open-play shots; goals
    2.70 a match (in the real range); open-goal goals ~1 a match.
- The long-shots style weighs a shot from range a little more (x2.6, was x2.2): over 60 matches it now gives ~2.2 times the long
  shots of normal play (7.1 v 3.2), as normal play shoots from range a bit more than before.
- Tests: `FinishingIntelligenceTests`; the samples of a few rare-event tests (long throws, dives, traits) enlarged - the effects
  are the same, four or six matches were too few to see them reliably (also the long-shots style and a plan moving a midfielder
  up: four or twelve matches instead of one or six). The big-game player test follows the player, not the agent (a substitute takes
  over the agent of the man he replaces - the star had been substituted).
- Test fix: the half-length check allows the Law 7 extension when a penalty is given at the very end of a half.
- Tests: `IntelligenceTests`.

## `feature/faces-and-logos`

Groundwork for portraits that are made once for each player, and for club crests made from a sentence (tools/faces/README.md):

- **How a player looks** (`PlayerAppearance`): look (European, Mediterranean, Middle Eastern, East African, West African, Latin American, East Asian...),
  skin tone 1-6, hairstyle, hair colour, facial hair, build, and a seed - decided when the player is created, by where he comes from
  (`PlayerProfile.Origin`: Jewish, Arab, Russian, Ethiopian, Spanish, American; an Ethiopian Israeli looks East African, with the matching skin
  tones; older players have beards and receding hairlines more often). It has its own random stream and plays no part in the match: the
  fingerprint of the matches without their headers (`--golden`, line PLAY) is unchanged (`0370C365C06CFABB`). It is in the replay header
  (`look`). `SimRunner --export-roster N` writes the roster for the face generator.
- **The generator** (`tools/faces`, Python, standard library + Pillow): prompts from the look and the age (hair greys, lines and wrinkles come with
  age; no shirt in the picture), a ComfyUI client and two Qwen-Image workflows (text to image; image edit for ageing), `generate_faces.py`
  (a portrait per age and a manifest, resumable), `generate_logo.py` and a small local `server.py` (POST `/logo` from a prompt, POST `/face`).
  Tested with a stand-in ComfyUI (4 tests); **the workflows have not been run against a real ComfyUI** (no GPU here) - the README says how to check them.
- **On the page**: the player's page shows his portrait (the one for his age: he ages with the years) over a **vector shirt in the team's colours**;
  until a portrait exists, a **vector face** drawn from the same look data (skin, hair, beard, build, greying and lines with age). "Club crest" in
  the user's kit screen: write what it should look like, the page asks the local server, the crest is kept and shown on the club's line-up.
- **Realistic portraits** (after a test with Qwen-Image): the prompt follows the style that worked (ultra-realistic studio portrait, head and
  shoulders, 85mm, direct look), the shirt is asked for in plain chroma green and lifted out (`split_shirt`): the portrait keeps its folds
  and light in grey with a mask beside it, and the page colours it in the team's kit (checked in the browser with the Qwen test picture).
- **The age chain** (`age_chain.py`): one ComfyUI queue per player makes all his ages, 19 to 35 - a base portrait, then an edit of
  that base per age with the same seed (so the man stays the same and does not drift); the generator's default; an example graph ready to load.
- Tests: `AppearanceTests` (origin and look fit, the spread of hair and beards, determinism, the match does not depend on the faces).

## `chore/cleanup`

- **Cleanup, no change in behaviour.** Removed what nothing uses: engine members (`BallPhysics.PositionAtTime`,
  `Formation.IndicesOf`, `MatchClock.PeriodElapsed`, `MatchConditions.AffectsPlay`, `MatchConfig.CardShowSeconds`,
  `NearestExit`, `RealSecondsPerClockSecond`, `Pitch.GoalLineX` / `InPenaltyAreaOrArc`, `PlayerAgent.IsActive`,
  `TeamSetup.CornerZoneCount`, the unused attribute-order list of the squad generator); on the demo page the old line-up
  layout's styles, the `kitrow` and `solo` rules, `addrow` and the unused `benchNow`; two dead helpers in the tests.
  No compiler warnings in the engine, the browser build, the runner or the tests.
- **Three tests that had been failing on `main` (not caused by the cleanup)**: the full run that followed the finishing round
  ended with four failures (one of them fixed in the next commit), and I read it halfway through and pushed to `main`
  without waiting for the end. Now:
  - *Momentum after a goal*: the test measured the level afterwards, which depends on where the team started; it now
    measures the swing the goal causes.
  - *Cut-in curlers*: the traits and the finishing rule had halved them (21 -> 7 per 12 matches). The winger who is cutting inside
    now keeps running at the full-back (the finishing rule leaves his dribble alone) and shoots a little more readily; about 15
    curlers in 24 matches (0.6 a match); the test counts 24 matches.
  - *The diver*: 16 matches instead of 6 (a rare event).
- **Reorganisation, no change in behaviour - proved, not assumed.**
  - *A fingerprint tool*: `SimRunner --golden` plays 20 fixed matches (leagues, the staged match as the page runs it, rain,
    snow and wind, night, cup and league cup with shoot-outs, random plans, computer managers) through the replay recorder and
    hashes every frame, event and statistics row. The fingerprint is `21195E340097B26F` before the cleanup, after it and after
    each step below (the cut-in change above is a change of behaviour, made after them: the fingerprint from then on is `5662DC3522CDEFF8`).
  - *The engine*: the four largest files, 1100-1800 lines each, split by topic into files of 130-620 lines (moves of whole
    methods with their comments, nothing rewritten): `OnBall` (decision, `.Passing`, `.Dribbling`, `.Shooting`), `TeamAI`
    (shape, `.Marking`, `.Pressing`, `.Players`, `.Movement`), `SetPieces` (start, `.Layout`, `.Execution`), `Contacts`
    (control and headers, `.Goalkeeper`, `.Tackles`). The map is in TECHNICAL_OVERVIEW.
  - *The demo page*: the 3300-line `match-template.html` is `template.html` (the markup) plus `css/NN-topic.css` (10 files) and
    `js/NN-topic.js` (25 files, one per topic); the build puts them back together at the `@include` lines, and the assembled
    page is identical to the old template byte for byte.
  - TECHNICAL_OVERVIEW brought up to date (the frame is 157 numbers, the file map, the fingerprint, the page's parts).

## `feature/online-play`

- **Play a friend online** (user mode). After the kits: *Play the computer* or *Invite a friend*. The host gets an invite
  link and sends it (WhatsApp, mail...); the friend opens it, picks his own kit and keeper (nothing that looks like the
  host's kit or keeper is offered) and gets a reply code to send back; the host pastes it and the two are connected. Both
  pick their eleven, formation and instructions; the match kicks off when both have pressed *Start match*.
  - The host's browser runs the engine (a new "online" mode: no computer manager for the away team) and streams the match
    to the friend as it is played; the friend's instructions and changes go back to the engine for the away team (new
    engine calls `SetPlanFor` / `SubstituteFor`).
  - Each sees his own team as his: on the friend's side the data is turned round (his team is the "home" team in his view).
  - The two browsers connect directly (WebRTC). A public STUN server is used only to tell each browser its own internet
    address - no game data passes through it; there is no game server.
  - Messages too large for the connection go in pieces.
- The logo is in the page once (the start screens borrow it).

## `feature/finishing-and-saves`

- **Shooting in the box.** With the ball in the box a player chose to dribble eight times as often as he shot (127 v 15
  decisions a match), and an open goal was shot at only a third of the time - he ran on into the keeper. Now the choice
  there is the shot: a dribble is worth the shot it leads to (the xG from where he would be a moment later, if he keeps the
  ball), not the "progress"; holding the ball is worth less still; the cut-back or square ball to a team-mate is worth his
  shot. An open goal is always a shot (no fake shot either). Dribbles that end on the keeper: 23 -> 3 a match; open goals
  put away 90% of the time; 35 shots, 16-17 on target and 13-14 saves a match (9 of them parries), about 3.3 goals.
- **The keeper's dive is drawn**: stretched out low along the line of the dive, arms and gloves reaching for the ball,
  with a short trail (the replay frames carry it as flag 1024).
- **Substitution opportunities** are counted per stoppage, not per restart: a restart set up again while the players were
  changing counted as a second opportunity, so a change was refused with one still left. Test: `TwoChangesAtOneStoppageUseOneOpportunity`.
- Tests: `FinishingTests`.

## `feature/user-mode`

- **Start screen: User or Developer.** Before anything opens the page asks how to play.
  - **Developer**: everything as it was - the staged, random and live matches and every setting.
  - **User**: pick the home kit, the away kit and the goalkeeper kit (ten shirt colours, seven keeper colours; a clash is
    not offered, the keeper never wears an outfield colour), then the eleven, the formation and the instructions in the
    pre-match, then **Start match** - a live match against the computer. No match-mode buttons (the time line under the
    pitch is longer), no skipping the half-time break. The settings hold only Sound (moved there from under the pitch),
    Gestures and emotions, Colour-blind and Reset; everything else is the default - sunny, day, stripes across, league
    match, a perfect pitch, no wind. The user's choices are kept apart from the developer's. "Back to the start screen"
    in the settings (both modes).
- **Kits in four parts** (user): shirt, trim (the ring round the circle), socks and number, for the home kit, the away kit
  and the goalkeeper (the keeper's socks are drawn too). Clashes are not offered: the two shirts never look alike (a close
  shade is out too, not only the same colour), the keeper's shirt never looks like either team's - pick his colour for a
  team and the keeper changes - and the number is always readable on its shirt. The opponents' keeper and the referees
  are dressed automatically in colours that clash with none of the shirts on the pitch.
- **Line-ups**: the away team's card now animates like the home team's (the rows and shirts of both cards played their
  animation when the page opened, so the away team's had finished by the time it came on).
- **Developer, live match: "Skip the break" works** (the live moment may now move to the second half, which is already
  generated - the break makes no frames).

## `feature/character-gestures`

The human side of the match - players that behave like people, not robots:

- **Twelve more traits** (Traits tab): *attacks every high ball*, *holds his position*, *roams*, *runs in behind*, *presses
  relentlessly*, *saves his energy*, *leader*, *big-game player*, *fragile*, *draws fouls*, and for keepers *bold* (off his
  line, claims crosses) and *stays on his line*. Never two that contradict (holds position / roams or runs in behind,
  presses / saves energy, bold / stays on the line, leader / draws fouls). The cool-headed trait was left out - there is the
  composure skill - and so was age-related tiring - stamina is the skill that does it.
  Effects: the roamer drifts up to 4 m (7 in possession) towards the ball and leaves a hole; the position-holder does not
  chase, run in behind or leave his zone; the runner times his runs and calls for the ball; the presser is first to the ball
  and burns 10% more energy, the saver 10% less; the fragile one tires 12% faster and is hurt about twice as often; the bold
  keeper stands higher and claims crosses; the diver near the box either wins the free kick or penalty or is booked for
  simulation; the charger goes further and stretches for every ball in the air.
- **Leader**: with one on the pitch the back line works as one (the defensive chemistry +10%), he calls the step up with an arm
  out after a clearance and before a set piece, a goal against hurts the team's momentum 15% less, and after a goal against
  or a bad pass he goes to the player who let the side down.
- **Personal confidence**: a goal (+), an assist, a save or a tackle won lifts a player; a miss, a conceded goal or a lost
  pass drains him; it fades over about seven match minutes. It moves the skills of his head and feet by up to 5%, in steps.
- **Big-game player**: +7% in the last fifteen minutes of a close match or in extra time, -4% when the match is settled.
- **Crowd pressure on the referee** grows (up to +60%) when the home team is behind late, or level with time running out:
  the 50-50 calls and cautions tilt more to the home side. Clear decisions are unaffected.
- **Gestures and emotions on the circles** (settings: Gestures and emotions): an arm out (the leader calling the line, a
  protest to the referee, a pat for a team-mate), a hand up and an "!" for a run in behind, sound waves for the keeper's shout,
  hands on the head and "..." after a miss or a goal conceded, hands up and "?" after a bad pass, arms up, a gold pulse and a
  star for the scorer and a heart for those who join him. They are in the replay frames (22 more numbers) and fade out.
- **Fixes**:
  - **Names**: the Israeli list is four separate lists again - Jewish, Arab, Russian-speaking and Ethiopian Israelis (about 72 / 12 /
    10 / 6 per cent of an Israeli squad) - and a player's first name and surname always come from the same one (never a
    Muhammad Cohen); the file's headings ("Israeli arabs first name:") no longer end up as names. Test: `TheListsAreSeparateAndHoldNoHeadings`.
  - **Player page**: the Traits panel was shown under Attributes too (the CSS `display: grid` beat `hidden`); now only in its tab.
  - **League cup board**: the red cards sit above the name plates like on the other two boards.
  - **Finishing**: in a good position (xG 0.18 or more, in the box area) the shot is favoured over running on with it; a cool
    head waits a beat longer. Dribbles that end standing on the keeper were halved (50 -> 25 a match), goals 3.05 a match.
  - **Key moments on a computer too** scroll to the pitch (centred, smoothly) and play. (The list still only shows moments the engine has generated.)
  - **Penalty shoot-out**: the keeper of the next kick was still walking to his goal (up to 4 m off the middle) when the kick was taken; the other keeper now waits at the edge of the area, walks faster, the kick waits until he is on his line, and he is placed in the exact middle. Test: `TheKeeperStandsInTheMiddleOfTheGoalWhenEveryPenaltyIsTaken`.
  - Four statistical tests loosened after the finishing and tactics changes (cut-in curlers, crosses by style, the siege line, dribbling decisions by trait).
  - **The formation board** draws the players in the team's shirt colours (the keeper in his), with the number in the kit's number colour, instead of white discs; it follows the colours picked in the settings and the colour-blind mode.
  - **Phone: one tap is enough.** The tooltips and the hover card appeared on the tap's emulated mouseover, so iOS took the first tap as a hover and swallowed the click; on a touch screen they are now off (a tap on a player opens his page directly), and buttons have no double-tap-zoom delay.
  - **Phone**: tapping a key moment moved the clock but left the pitch far above the screen, so nothing seemed to happen; now the page scrolls to the pitch and plays.
  - The staged match as the page runs it (page seed, names, traits, walk-out) is now tested: `TheStagedMatchAsThePageRunsItHasAllItsIncidents`.
- Tests: `CharacterTests` (traits per kind of player, every gesture seen and encoded, the leader's effect, confidence, the
  big-game lift, the crowd, roamers v position-holders, runners, pressers v savers, bold keepers, the diver). Fixed the five
  tests the previous round loosened (marking by distance, offsides, momentum after a goal, cut-in shots, the high line after a clearance).

## `feature/tactics-realism`

Principles from tactical analysis (the-footballanalyst.com guides to box defending, man-marking distance, the low block,
the 4-1-4-1, 5-3-2 and 5-4-1, the regista and the trequartista, set-piece coaching; Hansi Flick's high line; line-height
guides), so the players move like a team rather than like robots:

- **Marking by distance from the ball**: the closer a man is to the ball, the tighter he is marked; far from it he is
  given room, and on the far side his marker lets him go a little and tucks in towards the middle.
- **Ball-watching**: now and then (a few times a match) a defender near the ball loses his man for a second and only
  watches the ball - more often a poor or tired marker - and the runner gets away from him.
- **The line pushes out after a clearance** - the whole block steps up 10-15 m together (the second ball is played in a
  shorter space, anyone left in the box is offside).
- **Rebounds**: right after a shot near goal the attackers follow it in towards the six-yard box (a parry, a post, a
  block) and the defenders turn and get between them and the goal.
- **Blocking the shot comes first near the box**: in shooting range the covering defender gets into the line of the shot
  instead of the diagonal cover position.
- **The forward steers the press**: a striker pressing a centre-back curves his run to shut the square pass to the other
  centre-back and shows him down one side (he cannot chase both).
- **Against a low block** the switch of play to the far side is worth more (draw the block over, then switch to the
  winger one against one).
- **New roles**: the **regista** (drops in front of / between the centre-backs, switches and clips it over the press,
  defends by reading the lanes) and the **trequartista** (free between the lines, hardly defends, the killer pass, the
  shot from the edge, the late run into the box).
- **Set-piece goals** are counted (goals within ten seconds of a corner, free kick, penalty or throw-in in the opponents'
  half); about 28% of goals, as in the top leagues (25-35%).
- Traits: "cuts inside" goes mostly to wingers with the strong foot inside, and still crosses sometimes (winger crosses
  were down to a third).
- **Calibration** (96 matches): the traits had added about six shots and half a goal a match, and the hard tacklers far
  too many penalties (1.5 a match; real football about 0.3). Now: one or two hard men per squad (the best tacklers); in
  the box defenders jockey and stay on their feet (fewer challenges, fewer fouls, a hard man too); the habits shift which
  shots a player takes rather than how many a team takes; the keepers a touch stronger. Result: 3.35 goals, 34 shots,
  11.4 saves and 0.63 penalties a match, 32% of the goals from set pieces.
- Tests: `TacticsRealismTests` (each behaviour is seen, ball-watching stays rare, the set-piece share, the two roles'
  positions).

## `feature/player-traits`

- **Player traits** (player page - new **TRAITS** tab, next to ATTRIBUTES): every player has about three habits on the
  ball (two for a keeper), weighted by his position and skills, regenerated with the squad each match, never two that
  contradict each other (cuts inside v back to goal, outside of the foot v weaker foot, dribbler v unselfish, long shots
  v unselfish). They tip his choices in the engine:
  1. *Cuts inside and shoots* - from the flank he always comes inside, and shoots more after the cut.
  2. *Loves to dribble* - runs with the ball far more often.
  3. *Sets up his team-mates* - fewer shots, looks for the better-placed man.
  4. *Outside of the foot* - the trivela instead of the weaker foot.
  5. *Uses his weaker foot* - does not shift onto the strong foot, and his weaker one is a grade better.
  6. *Likes tricks* - backheels, dummies, fake shots and backheel finishes more often.
  7. *Goes in hard* - only for an aggressive player (70+; the trait gives him it): more tackles and slides, more fouls.
  8. *Shoots from distance* - has a go from range.
  9. *Switches the play* - values the long ball to the other flank.
  10. *Back to goal* - holds it up, stronger in the body, lays it off to the runners.
- The traits are in the replay header (`traits` per player). Tests: counts and conflicts, determinism, and that dribblers
  dribble, long-range shooters shoot, hard tacklers slide and foul, unselfish players shoot less.
- Commentary test updated for names-only commentary.

## `feature/cup-modes-live-rewind-lineup-board`

- **Cup modes** (settings - Competition): a league match ends after 90 minutes; a **cup** tie that is level goes straight to
  penalties; a **league cup** tie that is level plays extra time (two periods of 15 minutes, ends changed in between) with
  one more substitution and one more opportunity (IFAB Law 3), then penalties. The shoot-out (Law 10): five kicks each at
  one goal, in turn, the keepers in goal for the other side; a team that can no longer be caught has won, then sudden death;
  the manager's penalty takers kick first, then the best of the rest, the keeper last. Commentary for extra time and every
  kick; the shoot-out is not rated. The scoreboard says "League match", "Cup match" or "League Cup match" and takes the look of
  two of the Illustrator references (cup: yellow clock, black name boxes; league cup: yellow clock, light score box, the
  logo strip above), with the SoccerVision logo; "ET", "PENS" and a penalties box during the shoot-out, "AET" at the end.
- **The ball that goes out rolls on** beyond the line - off the advertising boards, off the outside of the net (it never
  passes through it into the goal) - until it stops and is collected, while the restart is taken at once with a new ball.
  The replay frames carry it (3 more numbers per frame).
- **Open goal**: with the keeper out of the way and nobody in the lane, the player shoots instead of dribbling on.
- **The formation board**: drag a player to an empty spot and his position moves there - the role follows the spot (a
  holding midfielder dragged up becomes an attacking midfielder, out wide a winger); the engine builds the formation from
  the moved positions (plan key `pos`).
- **Tactics lock what contradicts them**: park the bus keeps the line deep (and no fast tempo or relentless pressure), a high
  press, an offside trap or a high backline keep it up, counter-attacks are never slow and never short combinations,
  possession is not played long or fast, time wasting is slow, crosses go wide, runs in behind need balls played behind;
  locked options are greyed and say why.
- **Long shots** shoot on sight - from the edge of the box too, not only from 20-30 m.
- **Corners**: one player per zone (choosing him again swaps), and the routine's spot (near post, far post, penalty spot,
  pulled back, short) is fixed on the Attack board - you can change who stands there, not where.
- **Live match**: rewind to anything that has happened (the slider and the key moments), never past the live moment, which
  carries on meanwhile; the Live button becomes "Back to live". The opponents' data (hover card, profile) is hidden.
- **Sounds**: an away goal is a small cheer from one corner and booing from the rest of the ground; a card for a home player
  angers the crowd every time; a home save, the visitors off the woodwork or a save turned behind for a corner get
  applause; at full time the home crowd cheers a win, boos a defeat and applauds a draw; the shoot-out kicks get their
  reactions.
- **Tests**: the "faster players cover more ground" check averages six matches instead of three (pace adds about 3-5% of distance).
- **The league cup scoreboard** is the reference bar redrawn as a vector (the reference is a picture): the clock, white and
  team-coloured lightning-bolt separators, a light score box, the slanted end - in the page's palette.
- **High press** locks nothing (it is just pressing high).
- **Substitutions are planned, then confirmed**: changes show at once as planned; "Confirm" sends them all together, so they
  are made at the same stoppage and use one opportunity; "Cancel" drops them.
- **Back to live**: the red LIVE badge itself becomes "BACK TO LIVE" while you watch a replay of the live match.
- **Shirt numbers as football uses them**: 1 the keeper, 2 right-back, 3 left-back, 4 and 5 centre-backs, 6 holding
  midfielder, 7 right winger, 8 central midfielder, 9 centre-forward, 10 playmaker or second striker, 11 left winger;
  the bench from 12 (the reserve keeper) up.
- **Commentary** names the player only (no shirt number).
- **Finishing**: at an open goal the player rolls it in - low, inside the posts (84% scored over 40 matches; the rest are
  blocked or scrambled away); keepers save a little more of the rest (about 10 saves a match, 3.2 goals).
- **Substitutions on the pitch**: the player coming off walks to the benches at the halfway line, the substitute waits
  there for a high five, then comes on while the other walks to the bench - no more jumps from one point to another (the
  replay frames carry the second figure: 4 more numbers).
- **Smoothness**: the red cards, goals and assists and the shoot-out score are counted once a tenth of a second, not over all
  the match's events in every frame.
- **Fix**: the player profile opens again (a slip in the last round broke it).
- **Names**: the demo squads (and the managers) are named from the name databases (`data/names`: Israeli, Spanish,
  American; duplicates merged; a name may be both a first and a surname), anew for every match. The squads are Israeli with
  14 foreigners in the whole match - 7 Spanish and 7 American - spread over the two squads; the two teams never share a
  surname; the same seed gives the same names, so a replay stays the same.
- **The new ball**: when the ball goes out, a new one is rolled in from beyond the touchline to the restart spot (nobody is
  seen bringing it), while the old one rolls on and stops.
- **Line-ups, as a broadcast show**: each team in turn (7.5 s): the starting XI list on the left (numbers, the keeper), the
  coach and the substitutes; the shirts drop onto their spots on a pitch in the middle, staggered keeper first; the shirt
  and formation on the right; the SoccerVision logo, in the page's palette. Skip at any time.
- **Scoreboards**: the cup bar is slimmer and in the page's colours (steel-blue clock, dark plates); the league cup bar has
  slanted ends and a light score box between the two teams' colours, as in the reference.
- **Player ratings 1-10**, live through the match and final at full time (everyone starts at 6.0: a goal +1.1, an assist
  +0.7, a save +0.3, a tackle won +0.12, an interception +0.1, take-ons, the woodwork, passing completion, the score and a
  clean sheet; fouls, cards, a penalty given away, an own goal and goals conceded cost). The cards and the team sheet show
  each player's rating in colour, a ball per goal, a boot per assist and a star for the man of the match; the hover card
  shows any player's rating (the visitors' too). The replay's stats rows carry the 22 ratings and the final header each
  team's records.
- **New moves**: the **bicycle kick** (a ball at chest-to-head height in the box, back to goal; only technical players,
  rare - about one in 20 matches), the **backheel** finish and pass, the **fake shot** (shapes to shoot, the defender is
  sold and he cuts past) and the **dummy** (the receiver lets the pass run through his legs to a team-mate behind; decisions
  and creativity decide). Each has its own line of commentary (event `Skill`, shot details 15 and 16).
- **The last man** with only the keeper to beat never heads or passes back: he shoots, or heads the ball into his own path.
- **Injuries**: a player already carrying a knock is twice as likely to get a serious one; a muddy, badly kept pitch raises
  the risk (up to +45% on a neglected pitch, more in the mud itself).
- **Walk-out**: before the kick-off the teams and the officials come out of the tunnel, line up and go to their places
  (the clock does not run; the viewers only). The kick-off is a coin toss (it already was - either team can start).
- **Random match**: both managers pick a random formation, eleven, roles, individual and team instructions - the manager
  panel shows what the home manager picked (read-only; the header now carries each team's roles, instructions and team
  tactics). Only in a live match do you pick the home team; the staged match plays the squads as they are. Your own plan is
  kept for the live match.
- **A smarter computer manager**: a booked player who keeps fouling comes off before a second yellow; a knock, a poor game
  (rating under 5.8) or tired legs; a tactical change (a forward on when chasing the game, a defender to hold a lead); and the
  shape changes with the score (4-3-3, 3-4-3 two goals down, 4-5-1 to protect a lead).
- **Corner set-ups**: the Corners tab has three sub-tabs - Takers, Attack, Defence - with the automatic set-up drawn on the box; drag a
  player anywhere, click him to choose who stands there, "Back to automatic" to reset (plan keys `cpa`, `cpd`; mirrored for
  a corner on the other side).
- **Sounds**: the crowd's reactions, the net and the woodwork are recordings now (`docs/sounds/`, also in the React app);
  every synthesized reaction is gone - only the steady crowd, the ball and the whistles are still made in code. The goal
  roar is played from 2.05 s into its recording (where the roar starts), so it comes with the goal, and the booing from
  0.6 s. the home fans' anger at a
  decision against them (a free kick, a penalty, a card) and at an away goal - from all round the ground; the celebration
  of a home goal, while an away goal is only celebrated in one far corner, quieter; the net, the crossbar and the post,
  louder and with more echo the harder the ball hits. Booing (a red card, the away penalty taker, the visitors
  wasting time) uses `booing.mp3` (cut at 14.5 s, before its applause) and applause after a home shot that does not go
  in uses `clapping.mp3`.
- **Sound balance**: the recordings play at about 40% of their level against the ball, the whistles and the steady crowd,
  with a fade-in and a long fade-out; the long ones are shortened (the anger 7 s, the booing 8 s, the goal roar 8 s); the
  same reaction never piles up on itself (a foul and its card); the post, the bar and the net are louder the harder the hit,
  with an echo. A home shot turned behind for a corner gets its applause too.
- **Line-ups before the walk-out**: both teams' starting eleven on a pitch in their formation, the list with numbers and
  positions, the substitutes and the manager (new `Manager` in the team set-up and the header), in the page's palette - ten
  seconds or "Skip"; in a live match after "Kick off".
- **Snow**: blue lines next to yellow and orange, and a "Covered (white)" pitch next to the cleared one (looks only).
- **Colour-blind mode** in the settings: blue against orange, keepers in pink and yellow.

## `feature/set-piece-plans-shots-passing`

- **Penalties and home advantage**: the home taker's skill is lifted (x1+0.06 x home advantage) and the away taker's lowered
  (x1-0.16 x home advantage); the home keeper saves a little more (+8%), the away keeper less (-6%), and the stake of the kick
  weighs on the away taker. Measured over 960 kicks: about 75-77% with a neutral ground, 80.4% for the home team and 71.9% for
  the away team with home advantage.
- **Shots**: new techniques - laces (power), half-volley, knuckleball (free kicks from range, harder for the keeper to read) -
  next to placed near/far, curler, chip, volley, outside of the boot and the weaker foot.
- **One-twos**: about 17-18 completed per team per match (was about 10) with a success rate near 74%; a failed one is now an
  interception or a return pass hit too hard.
- **Forward passing**: a player under no pressure who has a forward option now prefers it (forward pass x1.3, backward or
  holding x0.6); forward-pass misses fell from 19% to 7%.
- **Goals spread by position like real football** (published breakdowns put forwards at about 47-60% of goals, midfielders
  30-37%, defenders about 11%): strikers shoot less greedily in the box, central midfielders arrive late and are found by
  the passer, wingers cut in and shoot. Over 60 matches: strikers 51%, wingers 21%, central midfielders 16%,
  centre-backs 10%, full-backs 2%; 3.3 goals and 30 shots a match. A winger on his strong side still goes to the by-line
  and crosses. SimRunner shows shots and conversion per position.
- **Attributes act as described**: Teamwork - a team player passes up a shot when a mate is clearly better placed and is
  less selfish in front of goal; Decisions - picks the right option more often (less perception noise); Composure - calmer
  in front of the keeper (less error within 14 m of him); Creativity - sees and values the through ball and the pass into
  space others do not; Physical power - the build of the body (duels, shielding, aerial battles), no longer the power of a
  pass; the keeper's Kicking - how accurate his kicks and clearances are.
- **Set-piece plans**: a list of takers (corners right and left, free kicks right and left, penalties) - the next on the list
  takes over if the first is substituted, injured or sent off; a corner routine for each side (varied, near post, far post,
  penalty spot, pulled back, short) and a place in the box for each of six zones. Plan keys: `ctr ctl fkr fkl pen crr crl czr czl`
  (`takers=` still works).
- **Demo page**: the hover card shows only the five categories of the pentagon (athletic, defence, attack - goalkeeping for a
  keeper -, mental, playmaking); corner, free-kick and penalty tabs: click a player to add him next in the queue (rating shown), separate right and left
  lists for corners and free kicks, plus a corner routine and a placement of the players in the box; drag a playing player's circle or row onto a substitute's row (and the other way) to make a change.

## `feature/momentum-home-advantage-ui-round`

- **Home advantage** (the home team scores about a third of a goal more than the away team in the top leagues): the home
  players play a little more confidently (+2% on the skills of the head and feet, not pace; the away team -0.8%), the
  referee gives the home team a kinder whistle (8% fewer fouls given against it, 12% fewer cautions; the away team 5% /
  8% more) and the away team tires 3% faster (the travel). Measured over 240 matches: the home team's goal margin
  grows by +0.33 (0.86 v 0.53 without it; the Red squad is the stronger one). `HomeAdvantage` in the match config (0 =
  neutral ground; SimRunner `--homeadv`).
- **Momentum**, after reading the research (goals are what players say moves it most; goals cluster - a goal in the last
  five minutes lifts the chance of another in the next five from about 15% to 18% - and perceived momentum is higher after
  a team's own goal, most of all a late one or an equaliser; the evidence on stoppages is thin and some "goal before the
  break" claims are myths, so here stoppages simply break the rhythm). One number from the home side to the away side:
  - It moves with goals (+0.42; a second goal within 8 minutes +0.22 more; an equaliser x1.25; a goal after 75 minutes
    x1.25; an answer within 4 minutes x1.15), shots (0.012 + 0.07 x chance quality), the woodwork, a penalty, a red card
    against the other team.
  - It fades with time (a time constant of 8 match minutes) and every stoppage takes some away: a foul 3.5%, an offside
    1.5%, a card 7%, a substitution 15%, a light injury 10%, a serious one 20%.
  - The team with the momentum is confident: +3.5% on its skills at the top, and it plays higher, faster and braver
    (mentality, pressing and tempo up). The team under pressure is rattled (-3.5%) and **calms the game down**: slower
    tempo, less attacking, and shorter passing when the swing is big.
  - A line of commentary when one team clearly has the upper hand ("Red Rovers have the momentum - Blue Falcons are
    rattled and slow the game down"). `MomentumStrength` in the config (0 = off; SimRunner `--momentum`).
  - Calibration: goals 2.96 per match, pass completion 77%.
- **Fix: a red card with no contact in the staged match.** The scripted foul picked a defender up to 6 m from the
  player on the ball and skipped the contact rule. Now the nearest defender is sent at the carrier and the foul comes
  only when he reaches him (within 1.6 m). Test: in the staged match every foul is between two players within 4 m.
- **Squads**: the bench now shows each player's real position (GK, CB, RB, LB, DM, CM, AM, RW, LW, ST) instead of SUB
  (positions from the generator's roles; full-backs and wingers by foot; midfielders by their skills). The full-back and
  winger picks look for a left-footer on the left and a right-footer on the right when the squad has good ones of each:
  both left-backs are now left-footed, and Red's left midfielder is left-footed.
- **Demo page**:
  - New formation **4-4-2 diamond wide**.
  - Fitness on the hover card in colour: Peak green, Moderate yellow, Low red. The same for the bench rows.
  - The stamina heart empties from the top with the energy (and goes green to red); everybody starts at 100%.
  - **Substitutions are easier**: pick a player and a bench player (or the other way) and the two cards slide to each
    other's place at once - the change is made at the next stoppage and you can go on with the next one (the waiting
    players are marked and locked). **Drag and drop** works on the cards and on the formation board: a player onto a
    bench player is a change, onto another player on the pitch a swap of positions.
  - **The tactic arrows are back** on the formation board: passing and tempo (the flow speed), attack (crosses, runs in
    behind, long shots), the extra style, pressing, the offside trap, the high line with the keeper sweeping behind it,
    inverted full-backs, the bunker.
  - **Names and order**: every row reads from the defensive / weaker choice on the left, through balanced in the middle,
    to the attacking / aggressive one on the right. "Style" is now **Aggression**, "Extra tactic" is now **Style** and its
    "None" is **Balanced** (the attack's "Mixed" too); the wing-back role is now **Attacking full-back** (and the full-back
    roles read defensive - inverted - attacking); individual instructions read Less / Mixed / Often (long shots,
    dribbling) and Gentle / Mixed / Aggressive.
  - The scoreboard logo is another 3% smaller.
- Tests: home advantage and momentum (6), left-backs and positions, the wide diamond.
- A team keeping the ball plays short to feet a bit more (the short ball 1.2 instead of 1.12 in value), and three older
  tests got sturdier (take-ons and fouls measured over the whole team, a smaller margin on the passes of the possession
  style) after the new squads and the home advantage moved the numbers a little.

## `fix/high-line-at-halfway`

- **The high backline now follows the reference** (the high-line diagrams): the back four stand on the halfway line
  while the opponent has the ball in his half (measured: +3.9 m from halfway with the ball past +10; the normal line
  stands 5 m behind halfway there), the midfield 10-15 m ahead of them and the forwards up to about 30 m, "high line =
  high pressure" (the style adds pressing). With the ball near halfway the line holds at about 10 m behind it, and it
  drops with the ball in its own half. Before, it stood about 10 m too deep.
- **The price of a high line**: now and then one defender is late with the step-up and splits the line, and the
  opponent's through balls against a high backline are worth more. The net goal advantage of the style fell from +0.7
  (before this fix) to +0.3 over 60 matches; offsides caught: about 10 per match.
- **The formation board** (demo) shows the dashed defensive line like the reference, and with the high backline the
  back four on the halfway line, the midfield and the forwards compact in front of it.
- Test: the back line stands on the halfway line while the opponent attacks in his own half, the block short.
- Fixes to older tests for the nine-man bench and the substitution event's new third part (the shirt number that went off).

## `feature/roles-instructions-substitutions`

- **Formations (11)**: 4-4-2, 4-4-2 narrow, 4-4-2 diamond, 4-3-3, 4-3-3 defensive, 3-5-2, 3-4-3, 3-4-1-2, 4-2-3-1,
  4-1-4-1 and 4-5-1. Back-three systems have wing-backs who drop into a back five without the ball. A change of
  formation moves every player to the nearest place in the new shape (before kick-off or live). All of them play full
  matches cleanly (tests).
- **Player roles** for every position, with their effect on play:
  - Keeper: shot-stopper, sweeper-keeper, dominant keeper.
  - Centre-back: stopper, ball-playing defender, cover defender.
  - Full-back: wing-back, inverted full-back, defensive full-back.
  - Holding midfielder: destroyer, deep-lying playmaker, box-to-box, anchor.
  - Central midfielder: all-rounder, tempo setter, carrier.
  - Number 10: classic playmaker, shadow striker, wide playmaker.
  - Winger: inverted winger, classic winger, wide target man.
  - Striker: centre forward, target forward, second striker, false nine.

  Roles change where the player stands (in and out of possession), his runs, pressing, tackling, passing choices
  (through balls, long balls, the target man as the outlet), crossing, cutting inside, and the keeper's position, sorties
  and claims. "Auto" plays as before.
- **Individual instructions** per player: long shots (more / mixed / less), dribbling (a lot / mixed / little),
  aggression (very aggressive / mixed / gentle). Tested: more long shots, more dribbles, more fouls.
- **Set-piece takers**: choose who takes the corners, the free kicks at goal and the penalties (Auto: the best one on the
  pitch).
- **Substitutions by IFAB Law 3 as the top leagues play it**:
  - Nine on the bench (both squads regenerated with nine substitutes).
  - Five changes in three opportunities during play; half-time and the second-half kick-off do not use one.
  - A change waits for a stoppage. The player coming off leaves by the nearest point on the boundary line, and the
    substitute comes on at the halfway line while the restart waits.
  - A substituted player cannot return, and a sent-off player cannot be replaced.
  - The computer makes its own changes: about two around the hour, two after 70 minutes and one after 80, the tired and
    the knocked first.
- **The manager panel** (demo page), in the palette of the reference screens (navy, slate-blue selection, green /
  yellow / red values), across the whole page:
  - **IN / OUT cards** for the eleven and the bench, with live energy, cards and injuries. Pick a player, then a bench
    player, for a quick change; or two players on the pitch to swap their positions.
  - **Tabs**:
    - Team Tactics: a formation picker, a formation board like the reference, and a team sheet with position, role,
      stamina heart and fitness, followed by the team instructions.
    - Player Instruction: the role and the three individual instructions per position.
    - Corners, Free Kicks, Penalty Kickers: the set-piece takers.
  - **The player page** (click a player on the pitch, or double-click a card): photo, age, height, feet, the five
    categories on a pentagon, gauges and every skill, form and set pieces.
  - **Tooltips** on every team instruction, role and individual instruction.
- **Live match: pick your team before kick-off**. The match waits at the pre-match screen while you choose the eleven
  (bench players included), the formation, roles, instructions and takers; "Kick off" starts it. In the staged and random
  matches, a change of eleven or instructions replays the match with it.
- The scoreboard logo is 3% smaller.
- Engine: `TeamPlan` (the manager's plan as text, from the page to the engine), `SubstituteCommand`, `LineupCommand`. The
  replay header carries every skill, the formations and the substitution rules. SimRunner: `formation=`.
- Tests: formations, the starting eleven from the squad, takers, substitutions (stoppage, no return, five in three, half
  time free), computer changes, individual instructions, roles, a live reshape (19 tests).

## `feature/tactics-intelligence-articles`

Read and applied ten tactics articles (the-footballanalyst.com: high backline, inverted full-backs, counter-movements, rest
defence, the third-man principle, set-piece coaching, cover shadows, dynamic advantages, long throw-ins, wide overloads).

- **Two new defence options** (next to the offside trap, demo tactics panel and engine):
  - **High backline** (+34 on the line slider): the back four squeeze up towards halfway, so the whole team stays compact (block about 3 m
    shorter out of possession). The line steps up together harder on every back pass or turned carrier, and it hardly
    drops for a free carrier. The keeper plays as a sweeper further off his line. The risk is the ball over the top: the
    computer answers it with runs in behind and direct balls.
  - **Inverted full-backs**: in possession the full-backs come off the flank into midfield beside the holding man (a
    double pivot, a 3-2 base). The centre-backs split wider and the wingers hold the touchline. The flanks are left open:
    the computer answers with width and counter-attacks.
- **Smarter players, every team**:
  - **Rest defence**: while attacking in the opponent's half, at least five players stay behind the ball (2+3 / 3+2,
    full-backs tucked into the half-spaces). Three stay in a siege, four in a counter or all-out attack, six when parking
    the bus or wasting time.
  - **Counter-press**: for the first seconds after losing the ball (longer for a pressing team), the players round it swarm
    the new carrier, with a second man too. About 68 per match, about 7 won back.
  - **Counter-movements**: a forward or winger making a run often first comes short to drag his marker up, then spins in
    behind (about 85 per match; better movers do it more).
  - **Run timing (dynamic advantage)**: runs are made when the carrier is facing forward with his head up, much less when
    he has his back to goal.
  - **Third-man runs**: A plays into B, and C (whom A could not reach) sets off as the ball travels. B lays it off first
    time into C's run, and the defenders, watching the ball, are caught facing the wrong way. About 36 runs, 6 lay-offs and
    3 completed per match.
  - **Wide overloads**: with the ball on a flank, the ball-side central midfielder joins the full-back and winger in the
    half-space. The far winger holds the far touchline, and the switch to a winger left one against one is valued more
    ("overload to isolate").
  - **Long throws**: near the box, the strongest thrower comes over. A flat, fast throw goes to the near post for the
    flick-on, with runners at the far post and in the middle, one man blocking the keeper and one at the edge of the box
    for the second ball. The defenders pack the box: man-markers on each attacker, plus zonal cover at the near post and
    the six-yard line.
  - (Cover shadows, where the presser curves his run to block the pass into midfield, were already in.)
- Calibration (60 matches): goals 2.82, pass completion 77%, offsides 3.4-4.1. A team keeping the ball now gambles even less and circulates a little faster (tempo -0.1
  instead of -0.2), and builds on its rest defence from deeper. The high backline plays a little higher still (+34 on the line).
- SimRunner: new rows (third-man runs, counter-movements, counter-presses, overloads, long throws) and
  `defence=highline|inverted`.
- Tests: high backline (higher, more compact), inverted full-backs (inside in possession), rest defence (4.5+ behind the
  ball), the new principles all appear, long throws, the computer answers (6 tests). The card test now uses the staged
  match (which always has a second yellow), and the distance test averages three matches.

## `fix/tactic-sliders-stay-live`

- **Bug: with some tactics chosen, the defensive-line and width sliders did nothing.** The defence, attack and extra
  tactics pinned the line or width (a high press forced the line to at least 80%, parking the bus to at most 3%, crosses
  forced the width up, ...). They now only SHIFT the sliders, so the sliders always work: a high press plays higher than
  the same slider without it, a bus lower. Measured: the line slider moves the back line 22 -> 31 m with the bus, 34 -> 43 m with
  relentless pressure. The tactics board follows. Passing and tempo buttons are only changed by a style when they are at
  their default (mixed / normal).
- Test: the sliders move the team with any style (1 test).

## `fix/referee-flag-cards-injuries`

- **The assistant's flag**, checked against the rules: it points the way the team awarded the restart attacks (a team
  playing left to right: flag right) for offsides, balls out, and now **fouls** too (test over three matches: never
  wrong, both sides used).
- **No foul without contact**: a foul needs the two players within reach (3.2 m, a slide's reach) - otherwise no free kick,
  no card, no injury (handballs excepted).
- **Injuries come from tackles**: muscle strains only for an exhausted player (energy under 50%) or one in poor shape
  (low fitness and stamina), and half as often as before; tackle and foul injuries a little more likely.
- **Play restarts only after the referee has put the card away**: no restart while a card is shown or a second-yellow red is
  due. The card is also a little smaller (demo, React, Unity).
- Tests: flag direction, no restart during a card, injuries from contact (3 tests).

## `fix/assistant-feet-flag-direction`

- **The assistants' and referee's feet point the way they move** (an assistant running along the touchline faces along
  it); standing still, the referee watches the ball and an assistant faces the pitch (demo, React, Unity).
- **The assistant's flag points the way the restart goes**: for an offside (or a ball out) the flag is raised towards the
  goal the team awarded the free kick / throw-in attacks - left for a team playing to the left, right for one playing to
  the right, not always the same side. The recorder carries it (flag 4 = towards -x).

## `feature/corners-bounce-onetwos-cleanup`

- **Bug: the player's edge stayed black when the colours changed.** The edge of the marker is the team's trim colour
  again (the trim picker changes it); the demo teams' trim is black by default, as in the design (demo page, React, Unity).
- **Boots colour**: a picker per team and for the referees (settings: shirt · edge · numbers · boots), demo page and React.
- **Feet**: a little closer together. The referee and the assistants use the same marker as the players (same size,
  same inset edge, same feet), so their feet show the same way.
- **Random match**: every click draws a new seed (as before); changing the rain, snow, wind or pitch upkeep now also
  draws a new match; and both teams have a manager who changes tactics during the match.
- **Commentary without "#"**: "9 Ilvane passes to 8 Corvel" (engine, demo page, React, Unity tooltip).
- **Corners are varied**: near post, far post, into the middle, pulled back to the edge of the box for the players waiting
  outside, or short to a team-mate who comes for it. The swing follows the taker's foot and the side - an in-swinger or an
  out-swinger ("swings an in-swinger to the near post"); the team wants an in-swinger for about 6 corners in 10 and picks
  the taker whose foot gives it. Corners bend more.
- **Bounces and curves**: some headers are nodded down to bounce up past the keeper, some volleys are kept down, some
  low drives skip off the turf (about 5 bouncing finishes a match); struck shots swerve a little more.
- **One-twos: about 21 a match per team, 12 of them coming off** (were ~3 a match in all): the wall pass is short and
  into the runner's stride, and only played when the lane is open.
- Cleanup and checks: dead code removed (unused page helpers, an unused WebAssembly export), stale comments fixed; all
  modes run without console errors in the browser; calibration: goals 2.97, pass completion 78%.
- Tests: corners, bounces, one-twos per team, labels without "#" (4 tests).

## `feature/snow-ball-reference`

- **Snow ball as in the reference photo**: a bright orange ball (255,104,26) with pale grey-white panels and darker
  orange seams - no halo, no glow (demo page, React, Unity).

## `feature/tactic-styles-feet-restarts`

- **Tactics you choose, not slide** (the panel under the pitch; the engine reads them through `TeamTactics.Effective()`):
  - **Defence**: *Balanced*; *High press* (wins the ball high, a high line and hard pressing, but tires the legs ~40%
    faster); *Offside trap* (the whole line takes a step up together to leave the forwards offside - 5 caught offside a
    match instead of 1 - and now and then one man misses the step, so the trap fails); *Park the bus* (a deep bunker, narrow, one
    forward left up, wasting time - the back line 19 m from goal instead of 33, and the legs stay fresh).
  - **Attack**: *Mixed*; *Crosses into the box* (the wide men deliver from everywhere wide, more men in the box - 17
    crosses a match instead of 9); *Runs in behind* (through balls and runs, 20 through balls instead of 11); *Long
    shots* (from 20-30 m, 19 shots from 18 m+ instead of 8).
  - **Extra tactic**: *Possession* (short passing, no gambles, 4.7 long balls instead of 9); *Time wasting* (restarts
    taken 1.9x slower, the keeper and the carrier take their time, the clock runs); *Counter-attacks* (win it in your own
    half and break at once, everybody forward, through balls); *Relentless pressure* (everybody up round the box,
    full-backs and centre-backs high, crosses and shots - and it tires the legs).
  - The board shows the shape and the arrows of the chosen styles. The computer manager uses them too (balls in behind
    against a high press, crosses against a bunker, a bunker and time wasting when it leads late, relentless pressure
    when it trails).
- **Feet**: tucked closer under the player and a little back. A kick or a pass swings the kicking foot (back, forward
  through the ball, settle - the foot that really kicks it, by the direction); the legs never "run". The referee and
  the assistants have feet too (the referee watches the ball, the assistants face the pitch). Demo page, React, Unity.
- **Restarts face the pitch**: the throw-in taker faces into the field, the corner taker the box, the goal-kick taker
  upfield, a free-kick or penalty taker the goal.
- **Snow ball**: flat marker-orange, no halo and no glow (demo, React, Unity).
- Tests: the styles (11 tests).

## `feature/team-tactics-one-twos`

- **Team tactic panel** (demo page, under the pitch): you are the home team.
  - **Defensive line** slider (deep block .. high line) and **width** slider (narrow .. wide), **passing** (short
    combinations / mixed / balls in behind), **tempo** (slow / normal / fast) and **style** (gentle / balanced /
    aggressive). A small tactics board shows the shape, the passing (arrows moving at the tempo) and the pressing.
  - In a **live match** an instruction goes to the players at once (commentary: "Red Rovers change their tactics: ...").
    In the staged and random matches it applies from kick-off: the match is replayed with it.
  - **The computer manager** (live match): every few minutes it reads your tactics and the score and answers: balls in
    behind against a high line, pushing up and keeping the ball against a deep block, going wide against a narrow team,
    quick passing against an aggressive one; chasing the game when losing after the hour, sitting deep when winning
    late. Its current set-up and its changes are shown next to your panel. In Unity the Live button gets the same
    computer manager.
  - Measured (60 matches each): back line 27 m from goal (deep) v 41 m (high); outfield spread 40 m (narrow) v 51 m
    (wide); 297 passes and 8 long balls (short) v 238 and 18 (direct); 5.4 fouls (gentle) v 12.4 (aggressive).
- **One-twos**: after a short pass the passer may burst past his man for the ball straight back, played first time
  ("#8 Corvel and #10 Fendar play a one-two"). Players who move well off the ball do it more; a short-passing team
  much more (about 3 a match, half of them coming off; more with short passing).
- **A pause before restarts**: when the ball goes out, or for a foul or an offside, play stops for 1.5 s - players pull
  up, the ball rolls to a stop - before the restart is set up, so you can see what happened (the clock keeps running).
- **Snow ball glows for real**: lit from inside (a hot yellow-orange core, white-hot pentagons), an orange haze, an
  additive bloom and a pool of light on the snow under it, breathing gently (demo page, React, Unity).
- **Goal flash**: the commentary bar flashes about seven times a second, alternating shirt colour on trim and trim on
  shirt colour, the text always in the other colour (demo page, React, Unity).
- Engine: `PassingStyle` tactic, `TacticsChanged` event, `ComputerManager`; the defensive line and width sliders act
  more strongly; the tackling style also changes how clean the challenges are. Calibration unchanged (goals 3.0-3.2,
  fouls 21.7, yellows 4.0 per match).
- **Squads from the team generator**: Red Rovers and Blue Falcons now field two teams made with
  https://soccer-simulation.onrender.com/api/team (22 players each, saved under `data/teams/`;
  `tools/teams/build_teams.py` turns them into `Simulation/Demo/GeneratedTeams.cs`). Every skill, the height, the feet
  (strong foot 3, the other by the weak-foot level), the stamina and the fitness come from the generator, so players
  really differ: pace (31-88 speed), strength, height (162-189 cm), left- and right-footed. The engine plays them that
  way: speed and acceleration set how far they run, height and jumping decide headers and the keeper's reach, the weak
  foot makes shots and passes worse. The 4-4-2 line-up follows the generator's positions; left-footed full-backs and
  wingers go on the left. (Goals per match stay about 2.9; fewer fouls and cards, since these players are less
  aggressive.)
- **The whole team moves with the defensive line** (as a real high block: the forwards press near the opponents'
  defenders, the midfield right behind, the back line near halfway, about 30 m from back to front). Measured (60
  matches): deep block 25 / 39 / 56 m from own goal for the back line / midfield / forwards, normal 33 / 46 / 64, high
  line 42 / 54 / 72. A high block also engages the ball earlier. Its risk: balls in behind succeed more the more room
  is left behind the line.
- **Tactics board**: the defensive line is one solid line; the "balls in behind" arrows start from the two wingers;
  the whole shape moves with the line.
- **Player marker as in the design**: a plain circle in the shirt colour with a thin black edge, the number, and two
  small boots peeking out under it, pointing where the player faces (static) - demo page, React, Unity.
- **Logo without the white box**: the scoreboard's SoccerVision logo is the new transparent PNG (demo, React, Unity).
- **Player card on hover** (design doc: tooltip with the player's skills): number and name, position, height and age,
  right-/left-footed and the weak-foot level, fitness, **stamina left now** (a live bar), a pentagon of the five skill
  categories with their values (ATT, ATH, PLA, DEF, MEN, as on the web game's player page) and attacking, speed,
  passing and stamina. The replay header now carries these, and the bench; the stats rows carry every player's energy.
- **The engine is published in a folder named after its build** (`docs/engine/<version>/`): a browser or the CDN can
  no longer pair a new page with old cached engine files (that is why the card showed ATT 0 and no foot or fitness).
- **Skills checked against the Stats spec** (Notion, "Stats"): every skill does what the spec says. Changed to match it:
  - **Fitness** changes all of a player's skills: low -10%, moderate unchanged, peak +5% (before, it only changed how
    fresh he started).
  - **Preferred leg** 1-3: 3 two strong feet; 2 he can use the other foot (kicks with it a little worse); 1 he cannot
    use it at all (he tries harder to get the ball onto his strong foot, and when he cannot, the kick is poor).
  - **Creative** (unpredictable moves): helps a dribbler past his man, and his passes are harder to read and cut out.
  - **Teamwork**: how hard a player works back for his team-mates.
  - **Positioning** is a defending skill only (going for a loose ball in possession uses decisions instead).
  - **Age and nationality** are kept with each player (age shapes development between matches, not a single match).
  - Already as in the spec: tackling (win it cleanly, fewer fouls), marking, composure, aggression (more fouls and
    cards), decisions, finishing, long shots, off the ball, jumping (with height), heading, speed, acceleration,
    physical power, dribbling, ball control (first touch), stamina, free kicks, penalty, and the keeper's reflexes
    (close range), kicking, aerial reach, one on one and handling.
- Fixes found by the full test run: the restart pause lets the clock run (as in a real match), so halves keep their
  10 real minutes; nobody drifts off the pitch while pulling up.
- Tests: tactics, one-twos, the restart pause and the computer manager (9 tests); generated squads (5 tests).
  Two older calibration tests now test the role, not the old made-up players (the holding midfielder fouls at least
  as often as the others; about 4 winger crosses a match). Full suite: 98 tests pass.

## `feature/three-game-modes`

- **Three game buttons** replace "New match" (demo page; React viewer and Unity HUD have the same buttons):
  - **Staged match**: the same scripted match every time (for the trailer): a light injury, a yellow card, a direct
    red, a serious injury with a substitution, then a second yellow and red. It is staged inside the engine
    (`MatchScript`), through the normal rules, so it holds in any weather.
  - **Random match**: a new match every time, which can be rewound and fast-forwarded.
  - **Live match**: you are the home team against the computer, played as it happens (no rewinding). Watch-only for
    now; substitutions and tactics will let you steer it (the engine already takes tactic commands).
- **The demo page now runs the real engine in the browser**: the C# engine is compiled to WebAssembly
  (`sim/SoccerVision.Wasm`, published as `docs/engine`) and plays in a Web Worker, about 30 times faster than real
  time. The prebaked recordings are gone. Replay recording is shared (`sim/SoccerVision.Replay`) between the
  command-line runner and the browser. Scores of a seed can differ slightly from the native runner (floating point).
- **Snow ball**: a bright, self-lit orange with white pentagons, a glowing halo and a dark orange outline, so it reads
  clearly on the white pitch (demo page, React, Unity).
- Tests: the staged match (5 tests).

## `perf/cached-background-adaptive-quality`

- **Faster drawing** (demo page and React), without changing what you see:
  - **Static parts drawn once.** Turf, mud, puddles, the painted lines and the light that does not move (a night
    match's floodlit dark, a grey sky's dull light, the warm sun and the stands' shade) are drawn once into an image,
    and each frame copies it. The heaviest cost was a full-screen tint layer drawn every frame.
  - **Shade on players.** Players in the stands' shade are darkened individually.
  - **Snow.** The snow is trodden in place, and the picture is recomposed twice a second instead of every frame.
  - **Cloud shadows** are drawn at an eighth of the size.
  - **Page text.** The page text (clock, score, time) is only written when it changes. In React the page around the
    pitch updates 10 times a second; the pitch itself still runs every frame.
- **Adaptive quality for weak devices.** If frames take too long for a few seconds, the quality steps down in turn:
  - first the resolution (2× → 1.5×) and less rain and snow, with no cloud shadows;
  - then 1× resolution;
  - finally an even 30 frames a second, instead of stuttering below 60.
- **Measured** in a headless browser with no graphics card (frames a second, before → after):

  | View | Demo page | React |
  | --- | --- | --- |
  | Sunny day | 24 → 46 | 60 |
  | Night | 44 → 56-60 | 59 |
  | Snow | 20 → 43 | 46 |
  | Rain on a neglected pitch | 30 → 33-42 | – |

  A device with a graphics card is faster still.

## `feature/stadium-shadows-night-perf-breeze-kits`

- **Shade from the stands on a sunny day**: the low sun comes from the top left, the same direction the players'
  shadows fall. Part of the pitch lies in the stands' shade with a sharp edge, as in the reference. New setting
  **Stadium**:
  - **Open**: no shade;
  - **Main stand**: a slanting band of shade along the far touchline, with the steps of the roof trusses;
  - **Bowl**: the curved roof shades the top and the left of the pitch.
  Same in the demo page, React and Unity.
- **Night no longer drops frames**: every frame used to add four full-screen light gradients on top of the darkness.
  The floodlit darkness is now drawn once per screen size and laid over as a single image. Cloud shadows are drawn
  into an image an eighth of the size. The canvas resolution is capped at 2× on 3× phone screens. In a headless
  browser, night went from about the same as day to almost twice as fast (40+ frames a second).
- **The wind is always shown**: with no wind set there is a light breeze, about 5 to 16 km/h from a direction fixed
  per match. It shows on the weather chip, the corner flags lean with it and the clouds drift with it. It is too light
  to move the ball, so the matches themselves are unchanged.
- **Goalkeepers and referees get colours like the players**:
  - each team's goalkeeper: shirt, trim (the ring around him) and number;
  - the referees: shirt, trim and letter (R/L). Trim and letter follow the shirt in black or white until they are
    picked.

## `fix/snow-cleared-pitch-coloured-lines`

- **Snow as in the reference games** (demo page, React and Unity):
  - The pitch has been cleared to a light, speckled dusting, so the green and the mowing stripes show.
  - Beyond the touchlines the snow lies thick, piled into a soft bank just outside the lines where it was pushed off.
  - The lines are painted **yellow** or **orange** so they read on the snow.
- New developer setting **Snow lines: Yellow / Orange** (default yellow), also in the Unity HUD and over the bridge
  (`snowLines`).

## `fix/colours-frame-snow-mud-night`

- **More colours in the developer settings** (demo page, React and Unity):
  - shirt numbers for each team; until picked, the numbers follow the shirt in black or white;
  - goalkeeper's shirt for each team (his number follows it);
  - the referees' shirt (the trim and letter switch to black or white to stay readable).
- **The goal frame stays white**: the net colour now changes only the netting. The crossbar and posts are drawn white
  over it.
- **Snow**: the high-visibility ball is now orange (with navy pentagons). The lines stay white, crisp, with a soft dark
  edge so they read on the frost. The snow is no longer an even white sheet: it is a powdery frost, thick in places and
  thin in others, with the green showing through in specks and streaks along the mowing direction (as in the reference
  photos).
- **Mud and puddles textured again from layers**: seeded Perlin noise, its fractal sum ("cloud" noise), a
  domain-warped cloud for organic, seamless edges, and fine Perlin plus per-pixel grain for grass tufts.
  - Every render uses a new texture seed; the patches stay where the engine has them, because they affect play.
  - **Mud**: sandy, yellowish dirt in several tones (light dry sand, ochre, damp patches, clods and lighter stones),
    broken up by tufts of grass and fading into thin, yellowed grass around it.
  - **Water**: a shallow mirror of the sky. Silvery, with drifting cloud reflections, long soft sheen streaks,
    sparkles and a bright rim. Where it is shallow the grass shows through and blades poke out. It sits in a ring of
    dark, soaked grass. At night it reflects a dark sky.
  - Textures are at twice the previous resolution.
  - One shared module (`web/src/replay/textures.ts`) is used by React and bundled into the demo page; Unity has the
    same layers in C#.
- **Night lit by the four floodlights**: the pitch is now brightly lit, with only the outskirts dark, plus a cool white
  light from the towers.

## `feature/dev-settings-weather-pitch-colours`

- **Developer settings** (gear, marked DEV) in the demo page, both React views and the Unity HUD:
  - **Match pace** slider (50-100%) is back.
  - **Grass**: stripes along the pitch, stripes across it, or rings around the centre spot.
  - **Pitch upkeep**: perfect, average or neglected.
  - **Goal net colour**.
  - **Sky**: sunny, cloudy, rain or snow.
  - **Wind**: calm or strong (12 m/s, about 43 km/h, from a direction fixed per match).
  - **Kick-off**: day or night.
  - **Team colours**: shirt and trim for both teams. The number switches to black or white, whichever reads best.
    Everything follows the colours: players, scoreboard, possession colour of the commentary bar, goal flash.
  - Settings are remembered in the browser. Unity takes them in the inspector, in its HUD, or from React (`setLook`).
- **Weather and pitch in the engine** (`MatchConditions`, `PitchSurface`). On a dry, perfect pitch in still air nothing
  changes: the same seed plays exactly the same match as before (checked event by event, and by a test).
  - **Mud and puddles map**: built per match from Perlin noise (fractal sums), shaped by where a pitch really wears:
    goalmouths, penalty spots, the centre circle, the middle, and the assistants' channels. Coverage:
    - average pitch: about 4% mud;
    - neglected pitch: about 15% mud;
    - in rain, puddles cover about 2% of an average pitch and about 9% of a neglected one;
    - a perfect pitch drains, so no puddles.
    The renderers draw this same map, so the ball stops in the puddles you see.
  - **Rain**: the ball skids on and keeps its pace (rolling resistance 35% lower, a lower bounce that skids on). Ball
    control, first touches, dribbles and shots are a little harder. Players lose grip and slip now and then (a player
    on the ball loses it). Slides go further. Keepers spill more.
  - **Snow**: the ball dies in the snow and hardly bounces. Players are a little slower and slip more.
  - **Mud and standing water**: slow and stop a rolling ball, kill its bounce, take away grip, and spoil close control.
  - **Wind**: air drag works on the ball's speed relative to the air. A lofted ball carries further with the wind,
    shorter against it, and drifts with a crosswind (a 20 m/s + 12 m/s kick in a 12 m/s crosswind lands more than 2 m
    off). Kickers aim off to allow for it; the better the technique (passing, crossing, long shots, keeper kicking),
    the more of the drift they take out.
  - New event "Slip" in the commentary ("slips on the wet grass / mud / snow", "loses his footing and the ball runs
    away from him").
- **Pictures of the conditions** (demo page, React and Unity):
  - mud from worn khaki grass to dark bare mud (glossier in rain), edges broken up with layered value noise;
  - puddles dark where deep, reflecting the grey sky towards the edge, with a light rim and raindrop rings;
  - rain as fast streaks blown along by the wind, with splashes;
  - snow cover over the whole ground with the lines painted blue, and falling flakes drifting with the wind. Where
    players run and the ball rolls the snow is trodden down (web views). In snow the ball is the high-visibility
    yellow one.
  - cloud shadows drifting with the wind; a grey sky dims the light;
  - **night**: dark beyond the reach of four floodlight towers, the towers' glare in the corners, and four faint
    shadows per player (one from each tower);
  - **day**: one long, sharp shadow from the sun, or a soft faint one under clouds;
  - the corner flags stream out downwind in a strong wind (and hang limp in still air), and the assistant's flag
    flies with the wind;
  - weather and wind (arrow and km/h) shown under the scoreboard.
- **Demo page**: the six matches are also recorded in every combination of weather (dry, rain, snow), upkeep and wind
  for three of them (51 recordings). Picking conditions loads that recording at the same moment of the match.
  Recordings are stored in a compact column-by-column delta form: the page went from 11.6 MB to 4.5 MB, and each extra
  recording is about 0.7 MB, fetched only when needed.
- React's replay viewer plays the same match again in the chosen conditions through the dev server
  (`/api/new-replay?...&weather=&maintenance=&wind=&windangle=`).
- Tests: `ConditionsTests` covers:
  - default conditions unchanged;
  - wet roll longer and snow shorter;
  - wind carry and drift;
  - upkeep decides mud, and rain fills puddles;
  - a puddle stops a rolling ball;
  - slips in rain and snow, and full matches in every condition with no errors.

## `fix/top-view-flags`

- **Assistant's flag**: back to how it was. He holds it up only when he signals (offside, ball out) and does not carry
  it around the rest of the time. Only the flag itself changed: black handle and checked orange / neon-yellow cloth,
  moving calmly.
- **Corner flags seen from above**: the pitch is a top view, so the flags are now drawn from the same angle. You see
  the white top of the pole on the corner and the yellow cloth hanging out away from the pitch, with its shadow. It
  sways slowly in the breeze. Same in the demo page, React and Unity.

## `fix/goals-contacts-substitution-assistants-flags`

- **Ugly goals**: measured over 60 matches before and after the attacking round. Rebounds were 20% of goals both
  times, but goals had risen from 2.88 to 3.55 per match. Every goal now records what the defence had just done, and
  that showed where the bad ones came from:
  - **Defenders gambling on reading a pass**: for every 3 successful reads there were 4 failed gambles, and a failed
    gamble came before 19% of goals. That is the defender running the wrong way while the striker scores. Now a back
    never gambles in his own third (he stays between his man and the goal), and elsewhere a defender goes only when he
    is clearly first. Goals after a missed read: 0.60 to 0.28 per match.
  - **Parries**: a keeper used to push 40% of his parries back into the middle, sometimes up into the air. Now 70% go
    over the bar or round the post, and the rest are pushed wide and low.
  - **Letting a ball run out**: only when it is clearly going out (well past the line) and close to the line.
  - **Shooting accuracy** slightly lower: there were too many shots on target and saves, hence rebounds.
  - Goals per match: about 3.2.
- **The ball only moves when something touches it**: every kick, deflection and control now checks how far the player
  was from the ball. About 2% of touches (22 per match) came from out of reach. Causes and fixes:
  - **Set pieces**: the taker played the ball from up to 1.5 m away (a penalty from 2.2 m!). The restart now waits
    until he is at the ball, and the penalty taker strikes it from right behind it.
  - **Standing tackles**: the reach was measured to the man, not the ball. A tackle now needs the ball within reach
    of the defender's leg; the rate was re-tuned so fouls stay realistic.
  - **Slide tackles**: shorter foot reach.
  - **Blocked shots**: the blocker must be within reach of the ball (it was up to 2.2 m).
  - **Blocked crosses**: shorter reach.
  - Now 0.5 per match, all blocks a few centimetres over the limit (measured on a moving ball). Test:
    `TheBallIsOnlyKickedOrDeflectedByAPlayerWhoCanReachIt`.
- **Injury substitution**: the injured player is now helped off towards his team's bench (top touchline, by the
  halfway line). The substitute comes on at that same spot, so nobody jumps across the pitch.
- **Assistant referees**:
  - Each runs on his own touchline (on the line), in his own half only, and no longer overshoots past the corner flag.
  - At a corner he stays on his line a few metres from the flag.
  - Test: `AssistantsStayOnTheirOwnTouchlineInTheirOwnHalf`.
- **Flags** (demo page, React, Unity), like the photos:
  - the **corner flag** is a white pole with a yellow cloth;
  - the **assistant's flag** has a black handle and four orange / neon-yellow quadrants, held down at his side, or up
    above his head for offside;
  - the cloth no longer flaps: a slow sway and a soft ripple towards the free edge, as in a light breeze.
- **Cards**:
  - "Red card ... sent off" is written in red (new commentary tone `Red`; demo page, React, Unity).
  - For a **second yellow** the referee first shows the yellow card, then about 1.6 s later the red one; the player
    leaves with the red.
  - The **referee holds the card up** above his head (yellow or red) for a couple of seconds, a marker like the
    offside and injury ones. It is in the replay (official flag 2 = yellow, 3 = red) and drawn in all three viewers.
  - Test: `ARefereeShowsTheCardAndASecondYellowIsFollowedByTheRed`.
- **Fouls and cautions** re-tuned after the tackle change: 19 fouls and 3.4 yellows per match.
- **Demo**: the staged first match is now **seed 7274**, re-picked because the engine changed. It has exactly one
  direct red card (66'), one light injury (42') and one serious injury with a substitution (58'), plus 2 goals.

## `feature/staged-first-demo-match`

- **The demo page opens on a staged match** (seed 7141), so a red card and the injuries can be examined at once. It
  has exactly one direct red card (#4 Novak, 27'), one light injury (#9 Brakko, 19': he carries on, with the white
  square and red cross) and one serious injury (#7 Morvak, 26': treated, helped off, #15 Quorin comes on).
  - "New match" then picks one of the other five at random.
  - Injuries and substitutions are now listed under "Key moments", so each can be jumped to with one click.
  - Injuries are shown in the commentary in the foul colour, substitutions in the info colour.
- **`main` is the main branch**: every round is merged into `main` (fast-forward) as soon as it is done, so `main`
  always holds the latest code, and the page is published from it as well.

## `feature/corner-runup-holding-mids-stats-assistant`

- **Corner run-up**: the taker used to step back into the pitch and run at the ball from the wrong side. Now he
  stands behind the ball: he steps back off the pitch, behind the corner, on the side of his kicking foot, and runs
  at it towards the goal area, so the ball is in front of him. (He may stand up to 3.5 m off the pitch for this.)
- **Holding and central midfielders**:
  - Of the two central midfielders, the one built for the dirty work (tackling, positioning, marking, aggression) is
    the **holding midfielder**:
    - out of possession he sits in front of the back four, in the middle, towards the ball;
    - in possession he stays behind the ball as the safe pass and the pivot of the build-up;
    - he presses first in the middle third, reads passes best, tackles most and has the most robust challenges;
    - he is the one most likely to stop a counter-attack with a foul.
  - Both central midfielders come to show for the ball, look for the forward pass and are where the build-up goes
    (defenders look for them first). They tackle and intercept more than the others.
  - Over 200 matches, per player:
    - holding midfielder: 1.5 fouls, 5.6 tackles, 31 passes (the most of anyone);
    - other central midfielder: 0.9 fouls, 4.3 tackles, 29 passes;
    - centre-backs: 0.7 fouls, 22 passes;
    - strikers: 10 passes.
- **New statistics**: possession, shots on target, tackles, tackles won, dribbles and dribbles won (take-ons; new
  `Dribbles` / `DribblesWon`).
  - Demo page: the "Match facts" table now shows them all, updating as the replay plays, from a stats timeline the
    engine writes into the replay once a second.
  - React: full-time table. Unity: side panel and the half-time / full-time table. Bridge: the new fields.
- **Assistant referee at corners**: at a corner on his side he no longer stands next to the ball. He waits behind
  the goal line about 4 m from the corner flag; at a corner on the far side he stands on his touchline near the
  goal line. (This is the same kind of fix as the referee keeping out of the centre circle at kick-offs.)
- **Squash and stretch** a little more visible: a lying player 1.22 x 0.93 (was 1.12 x 0.96), jumping +10% (was 6%),
  a slightly wider slide trail. The injury marker is a bit bigger.
- **Demo**: seed 7130 replaced by **7141**, a match with a direct red card, a light injury and a serious one with an
  automatic substitution.
- **Calibration over 200 matches**: goals 3.38 (real 2.97), shots 32.5, yellows 4.5, reds 0.18, offsides 3.1, pass
  completion 82%, 0.8 light and 0.2 serious injuries per match.
- The red-card test now plays batches of matches until it has seen both a direct red and a second yellow (red
  cards are rare, so a fixed set of 40 matches could happen to contain no direct red).
- Tests (`MidfieldCornerStatsTests`):
  - the holding midfielder does the dirty work and the central midfielders move the game;
  - corner takers run up from behind the ball towards the box, and the assistant keeps more than 3 m away;
  - the new team stats are counted.

## `feature/shot-techniques-red-cards-injuries`

- **`main` brought up to date**: it was still at the initial commit and now carries everything up to the previous
  round (a fast-forward, no history rewritten).
- **Ways of striking a shot**: the player picks one from the situation (distance, angle, where the keeper stands,
  which foot the ball is on) and from his own technique, composure and weaker foot. Each has its own physics.
  - **Side-footed into the near corner** (finesse): a "pass into the net" close in. Less pace, more control, kept low.
    Near post when the angle is tight or the keeper leaves it open.
  - **Side-footed into the far corner**: across the keeper, the same technique.
  - **Curler into the far corner**: from the edge of the box at an angle. Only when the strong foot's natural curl
    brings the ball back in. It starts wide of the post and bends back, with a fine margin. Cutting-in wingers always
    try it.
  - **Volley**: as before.
  - **Chip**: only when the keeper has come well off his line and stands between the shooter and the goal. A soft,
    high ball dropping under the bar. Rare (about 0.3 per match).
  - **Outside of the boot** (trivela): the ball is on the weaker side and the player is very technical with a poor
    weaker foot. The outside of the strong foot bends it the other way, towards the corner that curl brings it back
    to. Hard to get right, rare (about 0.5 per match).
  - **Weaker foot**: everybody else when the ball is on the weaker side (about a fifth of the time, less with good
    close control). Less pace and control the poorer that foot is (rating 1-3).
  - **Driven shot**: the rest.
  - Commentary for each shot and each goal ("curls it into the far corner", "chips the keeper", "with the outside of
    the boot", "on his weaker foot" ...). Side-footed shots and chips sound softer (demo page, React, Unity).
  - Per match: about 7 driven, 5 into the far corner, 2.6 into the near corner, 3.4 curlers, 4.5 volleys, 3 on the
    weaker foot, 0.5 with the outside of the boot, 0.3 chips, 6.7 headers.
- **Red cards (checked)**: over 300 matches, 38 reds, about 0.13 per match (0.10-0.15 in the top leagues).
  - 14 were direct: 6 for denying an obvious goal-scoring opportunity and 8 for serious foul play.
  - 24 were for a second yellow. The second yellow card is always followed by the red card.
  - None of the six demo matches happened to have one; the demo now includes a match with a direct red.
  - Test: `DirectRedCardsAndSecondYellowsBothSendPlayersOff`.
- **Injuries**:
  - Elite men's football has roughly 20-30 time-loss injuries per 1000 match hours (UEFA Elite Club Injury Study),
    close to one per match.
  - Causes: fouls (more often from behind or in a slide, and much more with a red-card challenge), tackles, slide
    tackles, shoulder duels, and muscle strains from sprinting on tired legs.
  - **Light**: the player goes down for a moment and plays on, 10% slower and less precise. He shows a white square
    with a red cross, like the card marker (demo page, React, Unity).
  - **Serious**: he stays down.
    - The referee stops play if it is still going.
    - The physio treats him, he is helped off at the nearest touchline, and a substitute from the bench comes on at
      the halfway line by his team's bench: same position, a keeper for a keeper, at most five changes (Law 3).
    - The time is added on.
    - Play restarts with the free kick if there was a foul, otherwise with a **dropped ball** (Law 8) for the team
      that last touched it (the keeper in his own area), with everybody else 4 m away.
    - With no change left, the team plays a man short.
  - The substitute takes over the injured player's shirt slot. The viewers show his number and name from the moment
    he comes on, using a `Substitution` event in the replay.
  - Per match: 0.8 light injuries, 0.25 serious (each followed by a substitution).
- Tests (`InjuryAndShotTests`):
  - a serious injury: treatment, the substitute on, play only restarts after it;
  - light injuries slow a player and are marked;
  - at most five changes, and a keeper only for a keeper;
  - both kinds of red card;
  - all the ways of striking a shot occur, and chips and the outside of the boot are rare.
- **Calibration over 200 matches** (real-world reference in brackets):

  | Measure | Result |
  |---|---|
  | Goals | 3.34 (2.97) |
  | Shots | 33.9 (25.9) |
  | Yellow cards | 4.2 (4.2) |
  | Red cards | 0.15 (0.10-0.15) |
  | Offsides | 3.2 (3.4) |
  | Added time | 3.4 + 4.0 min |

## `feature/freekick-box-fullbacks-wingers-stamina`

All of this applies to both teams.

- **Free kicks in the attacking third**:
  - The players used to stand in a straight line. Two causes:
    - at the edge of the box, the attackers lined up on the defence's line;
    - near the goal line, that line was computed beyond the pitch, so everybody ended up along the goal line.
  - Now it looks like a corner, with organised chaos in the box: the best headers go to the near post, far post,
    penalty spot, both sides of the area and the edge of the box.
    - Each has a defender goal-side of him, as a pair, some tight (man to man) and some standing off.
    - Two or three players wait at the edge of the box for the second ball. One centre-back and the full-backs stay
      back against the counter. The defending team's strikers stay high.
    - Attackers are kept onside, with the wall as before.
  - The cross goes to one of the players waiting in the box (the better in the air, the likelier), not to a random spot.
  - Tests: `SetPieceLayoutTests`. Against the old layout they fail (30 of 30 kicks had the attackers in a line,
    and 6 of 8 deep kicks had four or more players on the goal line).
- **Full-backs**:
  - How far they go forward depends on the team's mentality and on the player (crossing, pace, stamina, running
    off the ball). An attacking full-back goes up beside the winger and beyond the ball to the by-line, and leaves a
    bigger hole behind him. A cautious team keeps them back.
  - The far-side full-back tucks in and holds the line.
  - **Switch of play**: when a crowd of opponents is round the ball carrier, or he is under real pressure, a pass to
    a free team-mate on the other flank gets extra value. Full-backs do it more.
    - Commentary: "switches the play to ...".
    - Stats: `Switches` and `SwitchesCompleted`.
  - A full-back at the edge of the box shoots and crosses.
- **Wingers**:
  - They like to take it down the line towards the by-line.
  - New: **cut-back**, a low ball from the by-line square to a team-mate arriving in the middle.
  - A winger whose strong foot is inside cuts in more often, and his shot is a **curler into the far corner**.
    - He starts the ball wide of the far post and lets the curl bring it back, as in a free kick. It's harder to
      score, so there is more margin for error.
- **Stamina by position**: the drain is multiplied by a work rate.
  - Full-backs 1.25, wide midfielders 1.15, central midfielders 1.1, centre-backs 1.05, strikers 0.95,
    goalkeepers 0.35.
  - The average over the whole team stays as before.
  - Typical final energy: full-backs 0.43, wide midfielders 0.41, central midfielders 0.58, centre-backs 0.62,
    strikers 0.65, goalkeepers about 1.0.
- **Squash and stretch** kept minimal: a player on the ground after a slide is drawn almost round (1.12 x 0.96
  instead of 1.6 x 0.85) with a short, faint trail, and a player who jumps grows by 6% instead of 20%. Demo page,
  React and Unity.
- Tests (`WideAndStaminaTests`):
  - an attacking team pushes its full-backs further forward than a cautious one;
  - full-backs and wingers cross, shoot and switch the play;
  - cut-in wingers curl the ball from the flank;
  - defenders and wide men tire most, goalkeepers barely.
- **Calibration over 200 matches**: shots 33, goals about 3.4 (real 2.97), headed goals 20% of goals (13-19%).
  - The through-ball and set-piece changes of the earlier rounds keep the goal count a little above the real average.
  - I switched each new feature off in turn: none of them changes the goal count alone.
  - A lower striker shot tendency made it worse (more goals, because fewer, better shots), so that stays at its old
    value.

## `feature/interceptions-shielding-cover-line-through-balls`

All of this is engine behaviour for **both teams**. The only differences between red and blue are the two team
instructions chosen in the previous round: Marking (red zonal, blue man-to-man) and Tackling (red Normal, blue Get stuck in).

- **Interceptions (reading the game)**:
  - When a pass is played to an opponent, his marker (or the nearest defender) works out whether he can get to the ball
    first. If so, he steps in front of the receiver instead of waiting goal-side.
    - How early and how safely he reads it depends on Positioning, Decisions and Marking.
    - Commentary: "... reads it and steps in front of ...".
  - The pressing player curves his run so his body also blocks the carrier's nearest forward pass (cover shadow).
- **Body play**:
  - *Shielding the ball out*: a ball rolling out of play for our restart (goal kick, throw-in), with an opponent
    chasing it, is not played. The defender who gets there first puts his body between the ball and the chaser and
    escorts it out.
    - The chaser is held off according to physical strength.
    - Commentary: "... shields it out from ... for a goal kick".
  - *Shoulder to shoulder*: two opponents racing side by side for a loose ball lean into each other. The stronger,
    taller and more determined one holds his line and gets there first; the other is knocked off stride.
    - Rarely, it's a careless push (a foul).
    - Commentary: "... muscles ... off the ball".
- **Pressure, cover, balance** (diagonal defending): when the ball carrier is pressed in or near our half, the nearest
  team-mate takes up a position about 6 m diagonally behind the presser, on the inside (cover). The far-side full-back
  tucks in so there is no gap through the middle (balance).
- **A disciplined back line**:
  - Each team has a *defensive chemistry* score, built from its defenders' Teamwork, Positioning and Decisions.
  - With good chemistry the back four:
    - stays flat;
    - steps up together when the opponent has to turn back or plays the ball backwards;
    - leaves a forward standing beyond the line offside instead of dropping off with him (only while the ball is
      under pressure; an unpressured passer could play it over the top, so then the line drops);
    - in a zonal team, does not step out of the line to follow a man into midfield.
  - Fixed along the way: line defenders strolled the last couple of metres, so the line lagged behind where it
    should be. They now move with it.
- **Smarter midfield**:
  - Through balls are aimed at the widest gap in the back line near the runner (between the centre-backs, or full-back
    and centre-back).
  - New: passes **into space**, played ahead of a team-mate who is already running, so he takes it in his stride.
    Commentary: "... plays it into space for ...".
  - Central midfielders now arrive late from deep around the box (third-man runs).
  - The passer's estimate of a through ball's chance was far too optimistic (it predicted about 65-70% success
    against about 30% in practice), so it now allows for sweeping keepers and recovering defenders.
- Fixes found by the tests:
  - the referee now walks round the centre circle at kick-offs, not across it;
  - a player who ran or slid past a line just before a goal is brought back onto the pitch during the celebration.
- **Calibration over 200 matches** (both teams together; real-world reference in brackets):

  | Measure | Result |
  |---|---|
  | Goals | 3.27 (2.97) |
  | Shots | 31.6 (25.9) |
  | Offsides | 3.8 (3.4) |
  | Pass completion | 80% (80-85%) |
  | Yellow cards | 4.2 (4.2) |
  | Through balls | 28, of which 9 arrive |
  | Passes into space | 17, about half arrive |
  | Interceptions where the defender read the pass | 20 |
  | Balls shielded out | 3 |
  | Shoulder duels won | 28 |

  - Switching each feature off in turn (150 matches each) shows that none of them raises the goal count by itself:
    3.09 goals with all of them off, 3.21 with all on, within noise.
  - Together they cut shots from 37 to 32 per match.
- Tests:
  - both teams read passes, shield balls out and win shoulder duels;
  - a back line with better understanding holds a flatter line;
  - through balls and passes into space are found and some arrive.

## `feature/defending-marking-and-tackles`

- **Added time** box on the scoreboard: white text instead of green (demo page, React, Unity).
- **Marking systems** (new team instruction `Marking`; red plays zonal, blue man-to-man):
  - *Zonal*: each defender and midfielder owns an area around his place in the block (about 9 m for defenders, 10.5 m
    for midfielders), which moves with the ball. He picks up the most dangerous opponent who enters it and stays
    goal-side, but never leaves his zone. When the runner moves on, he is handed over to the next zone.
    `MarkingSwitches` counts the hand-overs.
  - *Man-to-man*: when the ball is lost, the four defenders take the four most advanced opponents and the midfielders
    take the next ones, nearest first. Each keeps his man for the whole defensive phase and follows him out of position.
    He stays tight and goal-side (about 1.3 m, 0.9 m in the box, looser when the ball is far away), and a poorer
    `Marking` attribute gives the man more room. The strikers don't mark. If a man is sent off, his marker picks up a
    free opponent.
- **Standing tackle**: defenders mostly jockey and choose their moment. A challenge comes more often, and wins the
  ball more often, when the carrier is exposed: still controlling the ball, back to goal, or turning.
- **Slide tackle**: when a carrier is getting away and the defender is beside or behind him, the defender may go to
  ground. He lunges about 0.4 s towards where the ball will be.
  - Reaching the ball wins it and knocks it on, often out of play near the touchline.
  - Hitting the man is a foul, with a higher card risk, especially from behind (reckless, or rarely excessive force).
  - The attacker may also ride the challenge.
  - Afterwards the slider lies on the ground for about 1 s: he can't move, play the ball or press.
  - Slides are rare in a team's own box and for players already booked.
- **Tackling** team instruction: `StayOnFeet` / `Normal` / `GetStuckIn`. It scales how often a team challenges and,
  more strongly, how often it slides. Red plays Normal, blue plays Get stuck in.
- New events and commentary:
  - "Great sliding tackle by ..." (Tackle detail 1);
  - "... goes to ground but ... rides the challenge" (`SlideTackleMissed`).
- **Drawing**: a player on the ground is shown lying stretched along the slide direction. While sliding, a streak of
  churned-up grass trails behind him. Applies to the demo page, React and Unity (replay flags 16 = sliding,
  32 = on the ground).
- **Bridge**: `setTactics` can carry `marking` and `tackling`. If they are left out, the team's current choice is kept
  (before, a slider change would have reset it).
- **Calibration over 150 matches** (real-world reference in brackets):

  | Measure | This version | Real world | Before |
  |---|---|---|---|
  | Goals | 3.01 | 2.97 | |
  | Fouls | 21.9 | 22.1 | |
  | Yellow cards | about 4.4 | 4.2 | |
  | Red cards | 0.16 | 0.10–0.15 | |
  | Tackle attempts | 106 | | 108 |
  | – of which slides | about 10 | | |

  Slide fouls are about 20% of slides. Most tackle attempts now come from take-on duels.
- Tests (`DefendingTests`):
  - man-markers keep their man and stay close to him;
  - zonal markers stay in their zone and hand over;
  - both kinds of tackle happen, and the tackling instruction changes how often a team slides;
  - a slider ends up on the ground, out of play.

  The two offside tests now try a few matches instead of depending on a single seed having an offside.

## `fix/throwin-ball-spin-offside-marker-official-size`

- **Ball spin at throw-ins**: the ball still spun after going out because, while the throw-in was being set up, nobody "owned"
  it yet and it kept the spin it had when it rolled out. Now the ball does not spin at all while play is stopped (throw-in,
  goal kick, corner, free kick, ...) or held; once it is in play it rolls on the ground with full spin and spins gently
  in the air.
- **Offside marker**: only the small chequered square above the caught player is left (no "OFFSIDE" text, no pole or flag).
  The assistant's raised flag is unchanged.
- **Officials**: the referee and the assistants are now the same size as the players (circle and letter).

## `chore/auto-publish-demo-page`

- **Automatic demo link**: `.github/workflows/publish-demo.yml` rebuilds the playable demo page on every push to any branch and
  publishes it to the `demo` branch, so one fixed link (README) always shows the latest change. The built page
  (`docs/index.html`, 12 MB) is no longer committed; it is git-ignored and produced by `tools/demo-page/build.sh`.

## `fix/ball-spin-corner-bounds-goal-colours-hud-sounds`

- **Ball spin**: a ball held by the goalkeeper or carried overhead for a throw-in no longer rotates (it only rolls when it is
  free or at a player's feet).
- **Players outside the pitch**: free kicks near the corner or the goal line put wall players up to 3.5 m behind the line
  (the "keep 9.15 m away" push went radially outwards), and players could run well past the lines in open play. Now the
  9.15 m spot swings round the circle to the nearest spot inside the field, everybody except a throw-in or corner taker stays
  on the pitch during a set-up, and players may run at most 1.5 m past a line in open play. Test:
  `PlayersStayOnThePitchWhileRestartsAreSetUp` (it failed 3 of 10 matches before the fix, one of them at a corner).
- **Ball size**: back to the original size in the demo page, the React viewer and Unity.
- **Goal flash**: the commentary bar flashes in the two colours of the team that scored (blue and white for the blue team,
  red and white for the red team) instead of one colour of each team.
- **HUD**: smaller, thinner scoreboard; the settings button is now an ordinary button (dark rounded square, thin border,
  standard gear icon).
- **Post and net sounds** (all viewers), built from what is known about the physics:
  - post / crossbar: a hollow steel or aluminium tube. Layers: the contact click and the ball's dull thud; the frame's bending
    modes (free-free beam ratios 1 : 2.76 : 5.40 : 8.93), whose audible modes 2-4 give the deep "tonk" - the crossbar is
    lighter and rings longer (about 0.55 s) than the ground-socketed post (about 0.3 s); the tube's shell "ping" at 2-3 kHz
    (cylinder modes; an aluminium bat rings at 2200 and 2800 Hz); and the rattle of net fixings right after;
  - net: slack nylon cord - no ring and no hiss. A soft low "whump", a muffled swish of cords (about 150 Hz to 2 kHz with a
    quick flutter, 0.4 s) and a few tiny cord ticks. The old net sound was a bright high-passed hiss, and a goal played it
    twice; now only the net-hit event plays it, louder for a harder strike;
  - both are louder for a harder strike (the events carry the ball speed). Measured offline: the net peaks 0.25 against
    0.33-0.48 for the woodwork.

## `feature/broadcast-view-offside-referee`

Requested: referee position at kick-offs, a clear offside, a broadcast look like the reference screenshots and the
`broadcast_scoreboard.ai` design, no pace slider and no legs for now.

- **Referee at kick-offs**: he used to walk to the centre spot (the code that keeps him 12-25 m from the ball does nothing
  when the ball is exactly at 0,0). Now he waits outside the centre circle, on his diagonal, in the half of the team that
  is not kicking off - after every goal and at the start of each half. Test: `RefereeStaysAwayFromTheCentreAtKickOffs`.
- **Offside**: the free kick waits at least 4.5 real seconds after the whistle, and an offside flag with an "OFFSIDE" label
  is shown above the player who was caught (4 s), besides the assistant's raised flag. Replays carry it as player flag bit 8.
  Test: `OffsideShowsAFlagOnThePlayerAndPausesBeforeTheFreeKick`.
- **Look** (demo page, React viewer, Unity):
  - turf: natural, slightly muted green with mown bands along the pitch and a fine grain;
  - technical areas (coaches) above the top touchline, either side of the halfway line, where the top assistant runs;
  - 3D ball: a truncated icosahedron (black pentagons, white hexagons) lit from the top left, rotating as it rolls
    (and spinning on in the air);
  - scoreboard from `broadcast_scoreboard.ai` (bottom variant): clock with seconds, team names on dark blocks with the team
    colour underneath, the score on slate blue, a red mark above a team per red card, and the "Ligue match" strip with the
    SoccerVision logo;
  - play/pause (works) and settings (empty for now) at the top right;
  - commentary bar under the pitch in the colour of the team on the ball; it flashes between both teams' colours for 6 s
    after a goal and turns yellow or red for 4 s after a card.
- **Removed**: the match pace slider (the playtested 90% pace is built in) and the players' legs (a new design will follow).
- Demo page build simplified: `tools/demo-page/match-template.html` is now the complete page (the patch scripts are gone).

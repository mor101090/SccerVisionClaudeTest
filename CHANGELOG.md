# Changelog

Each round of changes is developed on its own branch, named after what it changes. Newest first.

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

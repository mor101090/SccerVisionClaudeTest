# Changelog

Each round of changes is developed on its own branch, named after what it changes. Newest first.

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

# Changelog

Each round of changes is developed on its own branch, named after what it changes. Newest first.

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

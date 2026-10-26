# Changelog

Each round of changes is developed on its own branch, named after what it changes.

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

# Research notes and calibration

These are the sources behind the numbers in the engine. Each line says where the source is used.

## Laws of the Game (IFAB 2025/26)

- **Law 1 – Field**: 105 × 68 m, goal 7.32 × 2.44 m, posts ≤ 12 cm, goal area 5.5 m, penalty area 16.5 m, penalty mark 11 m,
  arcs 9.15 m, corner arc 1 m. Used in `Core/Pitch.cs` and `PitchRenderer`.
- **Law 7 – Duration**: allowance for time lost (substitutions, injuries, time-wasting, cards, goal celebrations, VAR,
  other delays). The fourth official shows the minimum, which may be increased but not reduced. The half is extended
  for a penalty to be completed. → `CheckAddedTime`, `CheckEndOfHalfLive`.
  <https://www.theifab.com/laws/latest/the-duration-of-the-match/>
- **Law 8 – Kick-off**: all players in their own half, opponents 9.15 m away, ball in play when kicked and it clearly
  moves. IFAB allows any direction; **the design document requires a back pass**, which is configurable via
  `MatchConfig.KickOffMustGoBackwards`. **Dropped ball**: the ball goes to the goalkeeper inside the area, otherwise to
  the team that last touched it, with others 4 m away.
  <https://www.theifab.com/laws/latest/the-start-and-restart-of-play/>
- **Law 11 – Offside**: in the opponents' half and nearer the goal line than both the ball and the second-last opponent
  (being level is onside). It is penalised only on involvement. There is no offside from a goal kick, throw-in or
  corner. A deliberate play by a defender resets it; a deliberate save does not. The sanction is an indirect free kick
  where the offence occurred. <https://www.theifab.com/laws/latest/offside/>
- **Law 12**: cards, DOGSO (yellow instead of red inside the area if the defender genuinely played for the ball),
  second yellow. **2025/26 change**: a goalkeeper holding the ball for more than 8 s → a corner kick to the opponents.
  <https://www.theifab.com/news/the-ifab-tackles-goalkeeper-time-wasting>,
  <https://www.premierleague.com/en/news/4373884/whats-new-in-2025-26-season-ifab-laws-and-premier-league-football-principles>
- **Law 13 – Free kicks**: opponents 9.15 m away (unless on their goal line between the posts), attackers 1 m from a
  wall of 3+, ball in play when kicked and it moves, no double touch.
  <https://www.theifab.com/laws/latest/free-kicks/>
- **Law 14 – Penalty**: keeper on the line, everyone else outside the area and arc and behind the mark. A rebound off
  the keeper can be replayed by the kicker; a rebound off the woodwork cannot.
  <https://www.theifab.com/laws/latest/the-penalty-kick/>
- **Law 16 – Goal kick**: the ball can be anywhere in the goal area, and opponents stay outside the penalty area until
  it is in play. <https://www.theifab.com/laws/latest/the-goal-kick/>

- **Law 12 – Goalkeeper releasing the ball**: an opponent may not prevent the goalkeeper releasing the ball from the
  hands (indirect free kick). Opponents therefore drift at least 10 m away from a keeper holding the ball, cannot touch
  his kick in its first 0.4 s, and the keeper waits until nobody is within 6 m before releasing. This fixed a measured
  bug: a keeper's kick out of the hands rebounding off an adjacent attacker into his own net (64 charge-downs and
  8 goals per 100 matches before; 0 and 0 after, guarded by `KeeperReleaseIsNeverChargedDown`).

### The referee's whistle

IFAB *Practical guidelines for match officials – body language, communication and whistle*
(<https://www.theifab.com/laws/latest/guidelines/body-language-communication-and-whistle/>):

- The whistle is **needed** to start play (both halves, after a goal); to stop play for a free kick or penalty, a
  suspension or abandonment, and at the end of each half; to restart free kicks where the 9.15 m distance is enforced
  and penalty kicks; and to restart after a caution, sending-off, injury or substitution.
- It is **not needed** to stop play for a clear goal kick, corner, throw-in or goal, nor to restart most free kicks,
  goal kicks, corners, throw-ins or dropped balls. "A whistle which is used too frequently/unnecessarily will have less
  impact when it is needed."

The IFAB does not prescribe blast lengths. We follow common refereeing practice: the tone matches the offence (a light
tweet for offside, a sharp firm blast for a foul, one long strong blast for a penalty or a sending-off offence), two
blasts at half time and three at full time (short-short-long). → `WhistleKind`, `Restart.WaitForWhistle`.

The sound is modelled on a pea whistle (Acme Thunderer): a bright tone near 3 kHz with strong 2nd and 3rd harmonics, a
sliding onset and end, and irregular frequency and amplitude modulation caused by the pea, visible mostly in the
harmonics, together with breath noise in narrow bands around them. The measured level is about 103 dB at 1 m.
<https://www.windytan.com/2015/10/pea-whistle-steganography.html> → `matchAudio.ts`, `MatchAudio.cs`.

### Restarts at a realistic pace

Broadcast timing (match clock): throw-in 10-20 s, goal kick 20-30 s, corner 25-40 s, free kick 15-40 s, goal to
kick-off 45-75 s, penalty 60-90 s. Each restart now has a minimum set-up time **and** waits until (nearly) everyone has
reorganised into position (at most 40 s). Measured averages in match-clock seconds: throw-in 10, goal kick 19, corner 24,
free kick 20, penalty 40, goal to kick-off ≈ 40.

## Real-match statistics (calibration targets)

- Premier League 2024/25: 2.97 goals per match, ≈4.2 yellow cards per match, 52 reds in 380 matches (≈0.14).
  <https://extratime.com/stats/2129/2287/premier-league/>,
  <https://theanalyst.com/articles/premier-league-2024-25-data-trends-stats>
- Ball in play ≈ 57 min per match (2024/25), and a match lasted about 101.5 min in 2023/24.
  <https://theanalyst.com/articles/premier-league-rules-throw-ins-goal-kicks-ball-in-play>
- Corners ≈ 9.3–10.7 per match across the top five leagues. <https://footystats.org/stats/corner-stats>
- Throw-ins ≈ 39–43 per match. <https://shura.shu.ac.uk/35132/1/Stone-AnalysisOfThrowInsStrategy(AM).pdf>
- Penalty conversion 75–80% (PL range 74.8%–89.6% by season). <https://theanalyst.com/articles/premier-league-penalties-like-free-goal>
- Fouls ≈ 20–25 per match. <https://statof.com/stats/fouls>

## Physics and athletics

- Top sprint speeds of PL players are 32–37 km/h (≈ 9–10 m/s). A sprint means > 25 km/h. → `PlayerAgent.MaxSpeed` is 7.0–9.6 m/s.
  <https://theanalyst.com/articles/premier-league-players-distance-run-fastest-slowest-sprints-walking>
- Ball roll on natural turf: about 90% of measured decelerations fall between 0.45 and 1.5 m/s². → `RollingDecel` plus grass drag.
  <https://www.isss-sportsurfacescience.org/downloads/documents/A7ZD60AH55_Gabrielsen_Ball_Roll.pdf.pdf>
- Pass and kick speeds: long passes ≈ 22–23 m/s post-impact; a typical pass ≈ 16 m/s initial speed.
  <https://arxiv.org/pdf/2005.04020>
- Ball: size-5, 68–70 cm circumference, 410–450 g. Air drag uses ½ρC<sub>d</sub>A/m with C<sub>d</sub>≈0.25. The Magnus
  effect provides curl and dip (design doc: "Physics System – Ball").

- Jumping: a standing or running header leap raises the centre of mass by 0.35-0.75 m (vertical jump tests of
  footballers are typically 40-60 cm). Take-off speed is v0 = √(2gh) and hang time 2·v0/g ≈ 0.55-0.75 s. Players take
  off early so that the **apex meets the ball** (about 70% of high headers are won at the apex and 22% while rising or
  falling), and cannot change direction in the air. Because motion runs at 2.25× real time, the visual jump uses
  g / MotionSpeed, so a leap stays visible for about half a second on screen. The shadow stays on the grass and
  shrinks and fades with height.

## Decision models (football analytics)

- **Expected Threat (xT)**: Karun Singh's open 12×8 grid, embedded in `AI/ValueModels.cs`. It values passes, carries
  and supporting positions. <https://karun.in/blog/expected-threat.html>
- **Expected Goals**: a logistic regression on angle, distance, X, C, X², C² and angle·X, fitted on Wyscout open data
  (Soccermatics, D. Sumpter). <https://soccermatics.readthedocs.io/en/latest/gallery/lesson2/plot_xGModelFit.html>
- **Pitch control / time-to-intercept**: the player keeps his velocity during his reaction time, then accelerates to
  top speed (Spearman, Shaw). Interceptions, pass safety and pressing are built on it.
  <https://arxiv.org/pdf/2501.04712>
- **Support spots**: the best supporting position near the carrier, after Buckland, *Programming Game AI by Example*
  (Simple Soccer). <https://github.com/luiskarlos/soccer>
- **React ↔ Unity**: react-unity-webgl v10 event system (`dispatchReactUnityEvent`, `sendMessage`).
  <https://react-unity-webgl.dev/docs/api/event-system>

## Calibration snapshot

Run: `dotnet run -c Release --project sim/SoccerVision.SimRunner -- --matches 100 --seed 9000`.
Real references: Premier League 2024/25, 380 matches, club totals from StatMuse summed and divided by 380:
offsides 1,284 → **3.38**, fouls 8,389 → **22.1**, corners ≈3,891 → **10.2**, shots 9,846 → **25.9** per match.
<https://www.statmuse.com/fc/ask/premier-league-team-offsides-2024-25>,
<https://www.statmuse.com/fc/ask?q=premier+league+team+stats+most+corners+per+game+2024-25>.
Headed goals are 13-19% of all Premier League goals per season (13.3-13.6% in 2019/20, 19.3% in 2010/11).
<https://www.premierleague.com/news/1657048>

```
Matches: 100   Finished: 100/100   recovered errors: 0
Per match (both teams)        sim     real-world reference
  Goals                        2.99   PL 24/25: 2.97
  Shots                       33.50   PL 24/25: 25.9
  Shots on target             13.52   8-9
  Corners                      8.64   PL 24/25: 10.2
  Fouls                       20.47   PL 24/25: 22.1
  Yellow / red cards     3.86 / 0.12   4.19 / 0.14
  Offsides                     3.27   PL 24/25: 3.38
  Penalties                    0.25   0.25-0.35
  Direct free-kick shots       0.64   ~0.6-1
  Headed goals % of goals     15.4    13-19
  Volley goals % of goals      9.7    n/a
  Woodwork hits                0.62   ~0.6-0.8 (estimate)
  Keeper catches / parries / punches   16.6 / 7.8 / 1.8
  Pass completion %           82.8    80-85
```

Tuning in this round: crosses can be blocked by the defender closing the crosser (it usually loops towards
the goal line, often for a corner); shots have a wider aim error (real on-target share is about 33%); keepers save a little
less, and much less against close-range headers.

### Why per-match totals cannot all match reality exactly

A real match has about 57 minutes of ball in play. Our match shows 2 × 10 real minutes at `MotionSpeed` 2.25, which is
about 45 minutes of football motion including stoppages. To reach real **per-match totals** (goals, cards, corners),
events must happen about 1.5× more often per minute of play than in reality. This explains why passes total about 620
rather than 900. Raising `MotionSpeed` packs more football into the 20 minutes; the cost is faster-looking players.

### Pace, run-ups and player build (round 3)
- **Pace**: playtested at 90% of the original speed, so `MotionSpeed` is 2.025 sim s per real s; the clock still runs 4.5
  clock s per real s, so a half still lasts 10 real minutes plus added time. Fewer sim seconds per half meant fewer
  events (fouls, offsides), so foul probability and off-the-ball runs were nudged up (fouls 20.4, offsides 2.8-3.1).
- **Run-ups**: penalty 4.6 m back at a slight angle (side of the non-kicking foot), shooting free kicks 5.5 m, other free kicks
  with a wall 3.5 m, corners 3.6 m x 3.0 m diagonally into the pitch, goal kicks 3.6 m, a keeper's long kick from the
  hands 3.2 m. Short free kicks, throw-ins and kick-offs have none.
- **Height** (165-200 cm, centre-backs and keepers tall, wingers shorter): header reach +0.9 cm per cm of height,
  aerial duel weight +1.2% per cm, cross target choice and corner attackers pick tall players, and ground speed -0.12% per cm
  above 180 cm.
- **Feet**: right-footers bend the ball to the left, left-footers to the right (inside curl); corner takers who can swing the ball
  in are preferred, free-kick takers aim at the side their curl brings the ball back to.

### Next tuning targets
- Shots (33 vs 26) and saves are high; corners (8.6 vs 10.2) and throw-ins (19 vs ~40) are low.
- Throw-ins, corners and offsides are below real values (more play close to the touchline, deflections behind, and offside traps).

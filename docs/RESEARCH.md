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

```
Matches: 100   Finished: 100/100   recovered errors: 0
Per match (both teams)        sim     real-world reference (top leagues)
  Goals                        3.50   2.7-3.0 (PL 24/25: 2.97)
  Shots                       35.50   24-27
  Shots on target             16.65   8-9
  Shots blocked                6.58   6-7
  Corners                      7.56   9.3-10.7
  Fouls                       22.82   20-24
  Yellow cards                 3.54   3.5-4.3
  Red cards                    0.12   0.10-0.15
  Offsides                     2.84   3.5-4.5
  Throw-ins                   23.04   39-43
  Goal kicks                  18.98   14-18
  Penalties                    0.47   0.25-0.35
  Penalty conversion %        70.21   75-80
  Pass completion %           81.75   80-85
  Home possession %           51.63   50 +/- 15
  Added time 1st / 2nd half    1.91 / 3.11 min
  Real 1st half / break / 2nd  625.4 s / 120.0 s / 642.7 s
Goals distribution: 0:1 1:8 2:19 3:26 4:19 5:16 6:8 7:2 8:1
```

### Why per-match totals cannot all match reality exactly

A real match has about 57 minutes of ball in play. Our match shows 2 × 10 real minutes at `MotionSpeed` 2.25, which is
about 45 minutes of football motion including stoppages. To reach real **per-match totals** (goals, cards, corners),
events must happen about 1.5× more often per minute of play than in reality. This explains why passes total about 620
rather than 900. Raising `MotionSpeed` packs more football into the 20 minutes; the cost is faster-looking players.

### Next tuning targets
- Shots and shots on target are high, and goals are about 0.3–0.5 above the league average (chance quality in the box).
- Throw-ins, corners and offsides are below real values (more play close to the touchline, deflections behind, and offside traps).

# Changelog

Each round of changes is developed on its own branch, named after what it changes. Newest first.

## `feature/marking-balance`

**The zonal / man-to-man gap closed; a contested header goes wider.** Measured: all four marking pairings, both seats, 100 matches each, on two seeds (1,600 matches), and 800 default matches.
- **What was wrong.** A man-to-man defence conceded about 0.25 goals a match more than a zonal one (1.58 v 1.34 against the same attacks; man v man 3.3-3.5 goals a match, zonal v zonal 2.5-2.7). The counters showed where: completed through balls 8 -> 14 a match, shots from inside 6 m +47%, dribbles at the keeper 51 -> 86. Every defender followed his man (up to 8 m into the other half) and nobody was left behind them; the assignments were made once, when the ball was lost, and never changed.
- **A free man**: a back line of four or more leaves one centre-back without a man (the cover defender if there is one, else the most central): he stands 3 m behind the deepest of his markers, across towards the ball, and does not hold the offside line (the libero's price: offsides 4.1 -> 3.0 a match).
- **Handing a man on**: a defender lets his man go once he drops 14 m into midfield (he holds the back line).
- **Swaps**: two markers of a line swap men when the crossover saves them 3 m between them.
- **Result**: goals conceded by a man-to-man defence 1.58 -> 1.43 (zonal 1.34, unchanged): the systems now concede alike (0.09 apart, from 0.25). Win share, seats pooled, zonal over man-to-man: -3.0 and -2.5 points on the two seeds (+7.2 and -1.2 before; about 4.5 points of noise a seed). Man v man 2.9 goals a match (3.3-3.5).
- **A contested header goes wider** (`HeaderContestError` 2, class D): with a defender within 1.5 m, a header's aim error grows up to three times (fully contested at 0.5 m). The marking fix alone raised the headed share of goals from 20.6% to 23.7% (with through balls shut, more attacks end in crosses); with the contest it is back to 20.6%, and headed goals a match fall 0.61 -> 0.58. **The share did not come down below where it was**: the driver is the volume of headed shots (5.6 a match, from about 31 crosses), left for a round of its own.
- **Default match** (800 matches): goals 2.94 -> 2.80 (real 2.83), shots 26.4 -> 27.9, on target 8.8 -> 8.5, corners 9.6 -> 10.1, pass completion 79.7% -> 80.1%, through balls 10.8 -> 7.1, offsides 4.1 -> 3.0.
- SimRunner switches for each part: `--nofreeman`, `--passon`, `--swapgain`, `--coverdepth`, `--headercontest`.
- **Tests**: `MarkingBalanceTests` (4).

## `feature/ball-parameters` (stage 12)

**Ball physics, stage 12: every parameter classified.** **The match does not change.**
- `docs/BALL_PARAMETERS.md`: every parameter of the ball's physics (the ball, the air, flight, the ground, the foot, collisions, deformation, the integrator) with its value, its class (A standardised, B measured, C derived, D approximation, E unknown) and its source. 62 rows: 5 A, 11 B, 11 C, 28 D, 7 E.
- The E list, what needs calibration, in order of how much it matters: the ground's restitution slope, the spin of a laces drive, ball-post and ball-body friction, the kick spin error, the inertia factor (a D with no measurement), the knuckleball, pressure in the bounce.
- **Tests**: `BallParameterTests` (8): every field of the six model classes has a row; every row has a class; the summary counts them.

## `feature/ball-test-bench` (stage 11)

**Ball physics, stage 11: the test bench.** Details: `docs/BALL_PHYSICS.md`; the table: `docs/BALL_TEST_BENCH.md`. **The match does not change.**
- `BallTestBench` runs the specification's TESTS 2-14 on the engine's own physics and records speed in and out, spin, flight time, distance, bounce height, energy lost and the response for every scenario (TEST 9, the knuckleball, is listed as not built: no model).
- FIFA's tests run as FIFA runs them, and pass: vertical rebound 0.76 m on grass (0.60-1.00), 0.69 m on turf (0.60-0.85); ramp roll 6.4 m (4-10); angle ball rebound 59-60% dry (45-70), 72-75% wet (45-80).
- `SimRunner --bench` writes `docs/BALL_TEST_BENCH.md`; a test fails when the file and the physics disagree.
- Also: `BallMotion.LandSpeed`; `MatchEngine.BodyContactNormal` (the body contact's geometry, now shared by the match and the bench; no change to the match).
- **Tests**: `BallTestBenchTests` (6).

## `feature/ball-stability` (stage 10)

**Ball physics, stage 10: the 20 ms step does what a step 200 times finer does.** Details: `docs/BALL_PHYSICS.md`.
- **A dead landing**: a falling ball that ended a step within 0.5 mm of the grass counted as rolling and lost its bounce (about 0.2-1% of landings). Now only a ball not moving vertically rolls.
- **The bounce inside the step**: the ball meets the grass where its path crosses it during the step, bounces there at the velocity it had then and spends the rest of the step on its new velocity (it used to bounce up to a step - 0.6 m at 30 m/s - late).
- **The trapezoid rule**: flight and rolling move the ball on the mean of the step's start and end velocities. Landings are now within 2-3 cm of the fine step (a driven shot landed 0.55 m short), bounces within 1-2% (5-10% low), rolls within 0.3% (0.7% short).
- **A bug of this stage's own, caught before it shipped**: with the bounce inside the step the kickers' aim (`KickSolver`) found the second bounce, and headed goals fell from 21% to 3% in 800 matches. The step now reports where the ball landed (`BallMotion.Landed` / `LandX` / `LandY` / `LandSpeed`) and a test checks the aim against the flight.
- **In the match** (800 distinct matches each): goals 3.03 -> 2.94 (1.4 standard errors, not conclusive; real 2.83), headed share 21.4% -> 20.6%, pass completion 79.8% -> 79.7%.
- **Tests that looked for the ball on the grass at the end of a step** (as the solver did) now use `BallMotion.Landed`: `BallFlightTests` and the wind test. One check was wrong, not just its method: TEST 7 checked a topspin ball 20 m out, where it is 4 cm above the grass (a 0.1 ms step lands it at 20.08 m); it passed before only because the old integrator had already bounced it at 19.68 m. The check is now at 18 m (assertions unchanged).
- **Another test's sample**: `UnselfishPlayersShootLess` saw no difference on its 24 matches (13.8 v 13.8 shots); over 96 the unselfish side shoots 2.9 fewer a match (about 5 standard errors), so it now looks at 96 (assertion unchanged).
- **A test's sample**: `AHeelFlickGoesBackAndTheFakeShotMovesTheBall` saw 16 of 35 fakes move the ball on its 16 matches, against a 50% bar; over 48 matches it is 66% before this stage and 60% after (a fake that loses the ball counts as not moving it, which is the run of play), so the test now looks at 48 (assertion unchanged).
- **Tests**: `BallStabilityTests` (12): against the fine step, the dead landing, the aim, no energy gained in flight, extreme balls, and the match independent of how the host cuts its time.

## `feature/ball-deformation` (stage 9)

**Ball physics, stage 9: the ball flattens.** Details: `docs/BALL_PHYSICS.md`. **The match does not change** (measured); the picture does.
- **The ball as a gas spring** (`DeformationModel`): an inflated ball stays a sphere with a flat cap (Tadrist and Darbois Texier, filmed at 4000 fps), and the air pushes back with its pressure on the flat area, so k = 2 pi R P (59 kN/m for the reference ball) and the contact lasts pi sqrt(m / k) = 8.45 ms. Bent to the measured impacts of a match ball on a rigid force plate (Iga, Nunome, Inoue and Ikegami; 9-19.5 m/s): every measured contact time within 0.1 ms, every measured flattening within 1.5 mm. 1.7 cm for a 10 m/s pass, 4.4 cm for a 30 m/s shot, 5.0 cm square onto the post at 20 m/s.
- **The frame's restitution slope** is the measured 0.0056 per m/s (B), replacing stage 8's 0.01 borrowed from tennis balls (E): 0.75 for a shot square on the post at 25 m/s. In the match (800 distinct matches each) nothing moves beyond the noise: goals 3.03 -> 3.03, woodwork 0.77 -> 0.76, headed share 21.5% -> 21.4%.
- **The contacts on screen**: the engine notes each contact (about 1,050 a match, from the ball's velocity jump between steps; it only reads the ball), and each replay frame ends with the latest one (flattening in mm, direction x100). The website, the classic page and Unity draw the ball for that frame with a flat cap on the side that faced what it hit, at the real depth. Seen from above, a bounce shows nothing (its cap is underneath).
- Changed together (the engine contract): `ReplayRecorder`, `app/src/engine/replay` and its copy in `web/src/replay`, the classic page, Unity (not compiled here: no Unity in this environment).
- **Side effect on a test**: `AHighBacklinePlaysHighAndCompactWithTheKeeperBehindIt` (six matches) saw the high back line's block 1.4 m shorter than normal, against its 1.5 m bar; over 30 matches the gap is 2.6 m with the old slope and 2.5 m with the new, so the block-length part now looks at 18 matches (assertion unchanged).
- **Tests**: `BallDeformationTests` (6), `replay.test.ts` (+4); `BallSpinReplayTests` reads the spin 4 numbers further from the frame's end; `yarn check` 134 / 134; the classic page plays a match in a browser with no page error.

## `feature/ball-collisions` (stage 8)

**Ball physics, stage 8: collisions.** Details: `docs/BALL_PHYSICS.md`. The old collisions stay available: `SimRunner --legacycollide` (`MatchConfig.Collisions = null`).
- **One impulse for every contact** (`BallPhysics.Impact`): the stage 4 bounce about any normal, against a surface that may move - restitution on the normal speed, friction on the slip at the contact point, the spin changed with it. Tested: it is the bounce for n = +z, and it conserves angular momentum about the contact point.
- **The goal frame** is two posts and a bar as capsules (round corners, a bar the ball can land on), and the ball is **swept** over each step: at 30 m/s it moves 0.6 m a step against a 0.17 m contact distance, so fast shots used to pass through the post. 0.13 of 0.82 frame contacts a match are ones the old end-of-step test missed.
- Ball on the frame: e 0.85 at the FIFA drop-test speed (the ball's own rebound on concrete), falling 0.01 per m/s (0.66 for a shot square on at 25 m/s; the old model used 0.7 for everything); friction 0.3 (E).
- **Players**: a block or a chance touch is an impulse off the body (a cylinder moving with the player), struck where the old model drew a direction - square on it comes back, at the edge it runs on, with spin. Keeper catches, parries and punches and blocked crosses stay aimed actions; the net stays a soft absorber.
- **In the match** (800 distinct matches each): goals 2.99 -> 3.03 (noise; standard error 0.06), woodwork 0.38 -> 0.77, corners 8.8 -> 9.7 (real 10.4), shots blocked 6.24 -> 6.58, **headed share of goals 18.7% -> 21.5%** (above the real 13-19%): left for the calibration round.
- **Correction to stage 7**: its goal rise (2.98 -> 3.10) was about 1.4 standard errors, not conclusive; a second, independent 800 matches gave 2.93 -> 2.99. The doc no longer calls it a real effect.
- **Side effects** (measured with both models on the same seeds): the crossing style crosses 21.2 -> 18.9 a match (a block hands the winger the ball back less often; still x1.35 normal play's), so `CrossesIntoTheBoxCrossesMore` now looks at 24 matches (six landed at x1.13). Dives are unchanged over 240 matches (26 / 19 v 24 / 15 bought / booked), but 32 matches drew none bought: `BoldKeepersStandHigherAndTheDiverGoesDownNearTheBox` now looks at 64. Assertions unchanged.
- **Tests**: `BallCollisionTests` (7). `dotnet test sim/` -> 300 / 302 before the two sample sizes above (1 h 2 m); on the final commit **Passed! 302 / 302** (1 h 10 m).

## `feature/ball-kick` (stage 7)

**Ball physics, stage 7: what the foot gives the ball.** Details: `docs/BALL_PHYSICS.md`. The old kicks stay available: `SimRunner --legacykick` (`MatchConfig.Kick = null`).
- **Spin by kind of kick** (`KickModel`), as a launch spin parameter Sp = R w / v so it holds at any speed: a curled shot 0.11-0.27 (4-10 rev/s at 26 m/s, the measured
  curve-kick band; Asai 2004 and Hong 2011 sit in the middle), a curled free kick 0.16-0.27, the outside of the foot 0.09-0.22, a curled cross or pass 0.10-0.25,
  a chip now has backspin (0.10-0.22). The old curls were a third to a half of the measured spin. The kicker's aim allows for the stronger curl (stage 3's solvers).
- **The fastest strike**: ball speed / foot speed 1.3 (the literature's 1.06-1.65); the foot strikes at 18-24 m/s by physical power, so no shot is faster than 23.4-31.2 m/s.
- **An oncoming ball**: a full strike (laces, volley, half-volley, bicycle, knuckleball, weaker foot) of a ball coming at the striker - a first-time shot, a volley - sends
  30% of its speed back on top (one-dimensional impact: the share is exactly the speed ratio minus 1, whatever the restitution). Paced strikes (side foot, curl, chip) do not.
- The speed-spin trade-off (Asai 2002) is already in the strikes (the curler is struck at about 0.8 of the laces drive's speed); tested, not modelled twice.
- **In the match**: goals 2.98 -> 3.10 a match (4 seeds x 200; real 2.83), shots 25.45 -> 25.77, on target 8.49 -> 8.78, volley and free-kick goals unchanged, headed share 17.3% -> 18.7%. The goals are left for the calibration round planned after the kick stages, not tuned in the measured physics.
- **Tests**: `BallKickTests` (6). `APlayerShootsAtAnOpenGoal` looked at 24 matches and saw none: open goals have been about 0.1 a match since the ball-physics stages, with the old kicks and the new alike (100 matches each, `--diag`); it now looks at up to 60 (the assertion is unchanged). `dotnet test sim/` -> **Passed! 295 / 295** (1 h 9 m, on a loaded machine).

## `feature/ball-spin` (stage 6)

**Ball physics, stage 6: the ball turns as the physics spins it.** Details: `docs/BALL_PHYSICS.md`. **The match does not change** (only the picture).
- The rotational physics came with stages 2-5 (a 3D angular velocity, the Magnus force, the bounce and slip impulses, the spin of rolling). The three viewers still turned
  the ball from its movement, so a curler did not turn about the vertical and a chip with backspin turned forward.
- **Replay frames** end with the free ball's angular velocity (world axes, rad per replay second × 10); 0, 0, 0 while a player has it or with the old ground model.
  The website, the classic page and Unity turn the ball about that axis in that direction; older replays and held balls fall back to the movement. The drawn rate stays
  capped (0.5 rad a frame): a real 10 rev/s would strobe on a screen.
- Changed together (the engine contract): `ReplayRecorder`, `app/src/engine/replay` and its copy in `web/src/replay`, the classic page, Unity.
- **Tests**: `BallSpinReplayTests` (2), `replay.test.ts` (3); `yarn check` 130 / 130; the classic page plays a match in a browser with no page error; `dotnet test sim/` -> **Passed! 289 / 289** (15 m 22 s).

## `feature/ball-roll` (stage 5)

**Ball physics, stage 5: the ball on the ground.** Sources, classes (A-E) and numbers: `docs/BALL_PHYSICS.md`. **This changes the match, a little.**
- **Rolling** (`BallPhysics.Roll`): a rolling ball has the spin of rolling and slows by the surface's rolling resistance a + b v (the form fitted to timing-gate data
  by the IST / Kolitzus method) plus the air drag on a rolling ball. **Slipping**: a ball that lands still slipping slides under friction μ g until it rolls,
  speed and spin changing together (angular momentum about the contact point conserved: checked).
- **Surfaces** (`GroundModel`, `MatchConfig.GrassLength`): natural dry a 0.408, b 0.167 - fitted to two measurements of three Norwegian natural pitches at
  once (Gabrielsen / NBI 2004 and Dunlop's comment, both read: velocity change over 2 m gates 0.68 m/s mean, roll about 6.4 m from the 1 m ramp): the model gives
  0.72 m/s and 6.4 m. Short, long, wet and turf profiles keep that shape, fitted to roll distances chosen inside FIFA's 4-10 m (class D). The old model rolled 4.1 m.
- **Kept as before**: ground kicks and touches start rolling (the kick model is stage 7); surface irregularities are not modelled (E: a random deflection would break
  the AI's predictions).
- **Legacy kept**: `MatchConfig.Ground = null` reproduces stage 4 to the digit.
- **Stage 4 v stage 5** (200 matches a seed, two seeds): goals 2.92 -> 2.95 (real 2.83), shots 26.1 -> 25.5 (25.4), on target 8.61 -> 8.57 (8.8), headed share
  of goals 19.0% -> 16.9% (13-19%), passes won by the opponent 69.6 -> 67.3, pass completion about 79.7%.
- **Tests**: `BallRollTests` (9). `ParkingTheBusSitsDeepAndSavesTheLegs` landed on its 8 m edge on 12 matches; on 24 it reads 24 m v 33 m (threshold unchanged). `dotnet test sim/` -> **Passed! 287 / 287** (14 m 15 s).

## `feature/ball-bounce` (stage 4)

**Ball physics, stage 4: the bounce.** Sources, classes (A-E) and numbers: `docs/BALL_PHYSICS.md`. **This changes the match, a little.**
- **Model** (`Core/GroundModel.cs`, `BallPhysics.Bounce`): an impulse at the contact point. The normal speed comes back times e(impact speed, surface); friction
  at the contact acts against the slip of the bottom of the ball (velocity + spin) up to μ times the normal impulse: if that is enough the ball grips and leaves
  rolling, otherwise it slides through; the same impulse changes the spin (I = k m R²). A ball with no spin landing at an angle leaves with topspin, backspin
  checks it, topspin kicks it on; a bounce never adds energy.
- **Surfaces**: natural dry (e 0.632: a 0.80 m rebound in the FIFA drop test, the middle of FIFA's "excellent" 0.60-1.00 m), natural wet (same e, μ 0.22: the
  angle rebound of a lightly watered natural pitch, 74-76%), football turf dry (e 0.602, 0.725 m, the middle of Quality Pro 0.60-0.85 m) and wet. Rain switches to
  the wet profile; `MatchConfig.ArtificialTurf` picks turf. The FIFA Test Manual (2024) and the Natural-Pitch Rating System (2022) were read directly; the Tennessee
  thesis (2024) through its abstract. e falls with impact speed (-0.01 per m/s): **class E**, no football measurement found (the slope is from tennis balls).
- **Checked**: the FIFA drop test rebounds inside the excellent range on grass and inside Quality Pro on turf; the FIFA angle test (50 km/h, 30°) gives 50-70% dry
  and 70-80% wet (the thesis: 56-69% and 74-76%); repeated bounces lose energy each time and the ball rolls after 3-10 bounces.
- **Legacy bounce kept**: `MatchConfig.Ground = null` / `SimRunner --legacybounce`, identical to stage 3 to the digit on both seeds.
- **Old v new bounce** (200 matches a seed, medium v medium): goals 2.88 -> 2.92 (real 2.83), shots 25.5 -> 26.1 (25.4), on target 8.31 -> 8.61 (8.8),
  headed share of goals 20.3% -> 19.0% (13-19%), passes +5, completion 79.5 -> 79.8%, volleys 5.2 -> 5.5.
- **Marking gap, measured again** (all four marking pairings, 150 matches each, two seeds): old bounce +7.7 / +3.3, new bounce +9.0 / +16.7 points (zonal over
  man-to-man) - about +5 -> +13, i.e. back to about where PR #8 left it. **Open**, for a calibration round after stage 5 (the rolling model changes passing again).
- **Tests**: `BallBounceTests` (11). Two statistical tests tipped and were made larger, thresholds unchanged: `ForwardsShootBeforeTheKeeper...` 16 -> 48 matches
  (open-goal goals: 0 in its 16 matches, 0.10 a match with either bounce over 120); `RealWorldPinTests` 40 -> 80 matches a pairing (the gap read +28.75 on 40,
  +24.4 on 80 - near the 25 limit: the test is doing its job). `dotnet test sim/` -> **Passed! 278 / 278** (12 m 41 s).

## `feature/ball-flight` (stage 3)

**Ball physics, stage 3: the flight.** Built on `feature/ball-physics` (stages 0-2, [#10](https://github.com/SoccerVision/soccer-vision/pull/10)); the sources, the classes
(A-E) and the full numbers are in `docs/BALL_PHYSICS.md`. **This changes the match.**
- **Model** (`Core/AeroModel.cs`, `Engine/BallPhysics.cs`): gravity; drag ½ ρ Cd(Re, Sp) A v² - 0.46 below Re 1.2e5 (8 m/s), the crisis between 8.5 and 13 m/s, about
  0.19 after it, plus up to 0.13 for spin; the Magnus force ½ ρ Cl(Sp) A v² along (ω × v̂), a vector force, so backspin lifts, topspin dips, sidespin curves and spin about the line of flight
  does nothing (Cl 0.09 at Sp 0.05, 0.26 at 0.2, flattening at 0.3); the angular velocity is a 3D world vector that decays at 0.08/s (was 0.25). Coefficients are the two wind-tunnel
  series and the trajectory fits of Goff and Carré 2009 (the full text was read), read from the figures by eye (±0.03 on Cd, ±0.04 on Cl) - class B/D, not a fit to our matches.
  Knuckleball: **not modelled** (no coefficient model was found; TEST 9 is not built).
- **Checked against measurements**: Re agrees with their Eq. 2 within 3%; a no-spin ball at 18 m/s and 22° lands 21.1 m away (they measured 21.9 m; their quoted launch values integrate to 21.4 m
  with Cd 0.2, so the check is ±1 m); terminal velocity 31 m/s; the Magnus force does no work; mirror-image sidespin; a Beckham-like kick curves 2.5-6.5 m over 27 m (their estimate: about 3 m).
- **Kicks aim by the curve** (`KickSolver.CurlYaw`, `CurlYawToReach`, `LoftSpeedWithSpin`): the old hand-tuned start angles (shots 0.04 + 0.02 × spin/10 rad, free kicks 0.06 rad, crosses none)
  assumed a Magnus force 2.5 times weaker. Without the solver the same physics gave goals 2.27, shots 21.6 and headed shots 1.5 a match (crosses missing their men); with it, below.
- **Legacy flight kept**: `MatchConfig.Aero = null` / `SimRunner --legacyflight` is the old model; checked **identical to the digit** (200 matches) after the solver change.
- **Old v physical flight** (`SimRunner`, 200 matches each, medium v medium, default tactics):

| | seed 700 old / physical | seed 900 old / physical | mean old -> physical | real |
|---|---|---|---|---|
| Goals | 2.61 / 2.75 | 2.69 / 3.00 | 2.65 -> 2.88 | 2.83 |
| Shots | 24.0 / 25.1 | 24.0 / 25.9 | 24.0 -> 25.5 | 25.4 |
| On target | 7.77 / 7.97 | 7.53 / 8.65 | 7.65 -> 8.31 | 8.8 |
| Headed goals, % | 15.9 / 18.4 | 17.8 / 22.2 | 16.9 -> 20.3 | 13-19 |
| Passes, completion % | 441.7 / 441.3, 79.3 / 79.4 | 438.9 / 444.7, 79.3 / 79.6 | unchanged | 80-85 |

  The direction repeats on both seeds. **Open**: the headed share of goals (20%) is a little above the real range; the penalty conversion rate moves with the sample (61-71%, 50 penalties a run).
- **Tests**: `BallFlightTests` (14: TESTS 5-8 and the checks above); `dotnet test sim/` -> **Passed! 268 / 268** (10 m 26 s), the `RealWorldPinTests` among them.

## `feature/ball-physics` (stages 0-2)

**Ball physics, step by step. Stages 0-2: the audit, the ball itself, its moment of inertia. The match does not change yet.** The
full plan, every parameter's source and class (A standardised, B measured, C derived, D assumed, E needs calibration) are in
`docs/BALL_PHYSICS.md`.
- **Stage 0, audit** of the current model (`BallPhysics.cs`): drag uses one Cd (0.25, no source) for every speed; the curl constant
  `MagnusK` (no source) implies a side-force coefficient of about 0.72 times the spin parameter, against 0.85-1.17 in the 2020 measurements
  of Asai et al. (read through the abstract only: the publisher refuses automated downloads), so the engine curls the ball about 60-85% as
  much; restitution 0.55, bounce friction 0.86, rolling resistance 0.9 m/s² and the spin decay have no source; spin is two scalars, not a 3D vector.
- **Stage 1, the ball** (`Core/BallSpec.cs`, `MatchConfig.Ball`): a configurable reference ball of 0.430 kg and 0.690 m (inside IFAB Law 2's
  410-450 g and 68-70 cm, read from the Law itself), radius 0.1098 m and area 0.03789 m² derived from it, inflation 0.85 atm **gauge**
  (the Law's 0.6-1.1 is the pressure above the air; absolute = gauge + 101,325 Pa). A ball outside the Law is reported, not refused.
- **Stage 2, moment of inertia**: I = k m R², k between a solid sphere (2/5) and a thin shell (2/3); reference 2/3 (0.003457 kg·m²),
  because no measured value for a size 5 ball was found - class E until one is. Angular velocity and momentum are 3D (`Core/Vec3.cs`).
- **Tests**: `BallSpecTests` (TEST 1 and the inertia checks), 6 / 6; `dotnet test sim/` -> **Passed! 254 / 254** (13 m 49 s).

## `feat/profile-clubs-leagues`

**Profile, clubs and leagues from Figma** (section "Profile, clubs & leagues – screens").

- **Profile**: your profile (record, recent matches, achievements, support and invite cards), edit profile (name,
  username, country, favourite club, about, privacy switches), achievements (12, all computed from saved matches),
  another manager's profile (head-to-head, private profiles respected).
- **Invite a friend**: link to copy and share, the people you invited and whether they played; `/i/<username>` landing
  that remembers the inviter for Create your team.
- **Support the game**: one-time donations through Stripe Checkout (`STRIPE_SECRET_KEY`), Supporter badge; no donor
  names or amounts are shown to anyone else.
- **Club settings** (name, short name, city, ground, crest description, kits link, delete with typed confirmation),
  **another club** (overview + squad: positions, ages, overall only), **leagues** (browse, follow, a league's table,
  fixtures and results).
- **404 and error pages**; the account menu gains My profile, Club settings, Invite a friend, Support the game;
  search results link to clubs and managers.
- DB (`drizzle/0008`, additive): profile bio / favourite club / visibility, club city / ground, `donations`,
  `league_follows`.
- Tests: `features/profile/achievements.test.ts` (5), `tests/e2e/profile.spec.ts` (5).
- **Server hardening** (`docs/server.md`): an index for every foreign key (`drizzle/0009`, 14 indexes), the
  `rls_auto_enable()` event-trigger function no longer callable through the REST API (Supabase advisor warnings
  gone), profile pages read only the records arrays of saved matches, request-level dedupe of manager / club lookups,
  CSP `form-action` allows the Stripe Checkout redirect.

## `feat/match-day`

**Match day screens from Figma** (section "Match day & more – screens").

- **/play** is a Play hub (computer / online / replay, match setup with Watch live or Instant result and opponent strength,
  recent results) → **Pre-match** (both line-ups, opponent report, mentality, Kick off) → full-screen **Live match**
  (score bar with scorers, pitch with weather and goal banners, timeline with event markers, Commentary / Stats /
  Ratings tabs, momentum, mentality + touchline instructions, Substitutions and Tactics drawers) → **Half-time**
  dialog → **Full-time report** (player of the match, stats, key moments, ratings; saved automatically).
- In-match changes are real engine plan edits (line, width, tempo, tackling) and substitutions, applied at the next
  stoppage; the saved tactics don't change.
- **/matches/[id]** replay in the same layout: scrubber, jump between key moments (← →), saved stats and ratings.
- Accent colour is blue (matches Figma); the old `match-viewer` and `replay-player` are removed.
- Not in this change: the online lobby screens (create/join, waiting room, connection lost) — separate PR.

## `fix/qa-audit`

**Fixes from the QA and UX audit** (two merged reviews: `docs/qa/2026-11-06-qa-ux-audit.md`, with a fix-status table).

- **Classic page (the default site)**: doctype, `lang`, viewport and icons - it rendered in quirks mode as a shrunken 980 px desktop page on phones; its
  phone CSS now applies. "Start match" waits visibly for the engine (a press before it was ready was dropped). The kits screen opens at the top.
  The `hidden` attribute always wins (CSS overrode it: the second set of mode buttons and "Opponent strength" showed online). Settings: no
  empty sections, Escape closes. Manager tabs: fade on the clipped edge, arrow keys. An invite link joins at once. Neutral copy for the friend.
- **App**: unsaved Tactics ask before you leave; Tactics tabs are a real tab widget; page headers no longer squeeze the title on phones; visitor
  pages have `<main>` and every page a skip link; auth forms focus the first invalid field and announce a wrong password once; 44 px
  show-password button; 24 px link targets. "Contact Us" mails `NEXT_PUBLIC_SUPPORT_EMAIL` (hidden until set; it pointed at a private repo).
  Logout lands on `/home`. The landing leads with "Create your club". `/favicon.ico`, `robots.txt`, `sitemap.xml`.
- **Tests**: `tests/e2e/qa-regressions.spec.ts` (11). `yarn check` 113 / 113; e2e 14 / 16 + 11 / 11 (the 2 failures are fixed by PR #16).

## `chore/phase3-cleanup`

**Phase 3: cleanup and debugging, no gameplay change.** Full record (fixes, checks, remaining debt): `docs/phase-3-cleanup.md`.

- **Daily e2e red since Nov 5**: `team.spec.ts` waited for the "Squad complete" celebration even when the test account already had its club; `match.spec.ts`
  still expected sign-in to land on `/play`. Both fixed.
- **Messages**: opening Messages now marks them read, so the top bar badge clears (nothing ever did).
- **Fresh lists**: messages, notifications and transfer offers are no longer cached per user; a new one shows on the next load instead of minutes later.
- **Cleanup**: unused exports removed; one `MatchSkeleton` instead of three copies.
- **Tests**: `yarn check` 113 / 113; Playwright 14 / 14 without online, online 2 / 2; engine determinism and replay tests 14 / 14.

## `chore/close-loose-ends`

**Loose ends from the earlier lists, closed; nothing in the engine's play changes.** Deferred by the user and taken off the list: the league setting, the branches in the old repo.

- **Classic page header**: no "engine demo" kicker and no static "Team A v Team B" / "4-4-2 v 4-4-2" line; the title shows the match's own team names (set when the match begins), the page
  title is "SoccerVision". The random and staged matches are still played by the engine's demo teams, whose names are "Team A" and "Team B" - a naming choice, not changed here.
- **`faces/manifest.json` 404**: the page asked for it on every load and no build ever shipped it. `yarn classic:build` now ships an empty one (or the real one if `docs/faces` has it);
  checked in a browser: no failed request, no page error.
- **`KeeperCloseDribble`**: already gone from the code (the earlier list was out of date).
- **SimRunner reference column**: the seven figures that were counted ([C]: goals 2.83, shots 25.4, on target 8.8, corners 10.4, fouls 21.5, yellows 3.44, reds 0.12; `real_leagues.py`, Premier League 2015/16-2024/25) replace the
  "PL 24/25" numbers, which had no source in the repository; the header says every other figure is an unsourced estimate. The offsides figure is marked unsourced.
- **A test that pins the engine to real data** (`RealWorldPinTests`, 160 matches: medium v medium in all four marking pairings, both seats): goals 2.4-3.2 (real 2.83; measured 2.59 on its own seeds), shots 22-28 (25.4),
  fouls 19-24 (21.5), the zonal / man-to-man gap within 25 points (measured -2.5; about +25 before the goals round), the home edge with seats and systems swapped between -5 and 30 points (measured +15; real +12).
  The ranges are wide on purpose: they catch an engine that has wandered off, not a tenth of a goal.
- **Tests**: `dotnet test sim/` -> **Passed! 248 / 248** (13 m 11 s; the three new ones included). `yarn check` in `app/`: lint, typecheck and format clean, 58 / 58.

## `feature/goals-and-marking-gap`

**The two open items of the squads round: goals 2.4-2.5 v 2.83, and zonal marking beating man-to-man by 20+ points.** They turned out to be one problem seen twice.

- **What the measurement showed** (`mark4.sh`: medium v medium in all four marking pairings, 150 matches each, two seeds; Team A is the home side): with the new squads a zonal
  defence concedes about 1.0 goals a team and a man-to-man one about 1.3, and man-to-man leaks shots (29.3 a match both ways v 23.4 for zonal v zonal; strikers' shots 16.9 v 10.9).
  Goals a match: zonal v zonal 1.9-2.0, zonal v man 2.4, man v zonal 2.5-2.7, man v man 3.05-3.15. The default match is the mixed pairing, which is why it sat at 2.4-2.5.
  Zonal beat man-to-man, seats pooled, by +23 (seed 700, 300 matches a pairing) and +26 (seed 900) points of win share.
- **Man-to-man stands tighter**: `ManMarkGapScale` 0.4 (the gap he gives his man was 1.1-4.1 m times (2 - marking skill)). Alone: gap +14 (0.6), +10 (0.3, one seed), +16.5 (0.4, seed 900).
- **Zonal areas smaller**: `ZoneRadiusScale` 0.75 (9 / 10.5 m). A man who arrives between two zones is nobody's and only the shape covers him - the real weakness of a zonal side.
  With both: gap +10.5 (seed 700) and +6.7 (seed 900). A radius of 0.6 was no better (+10.2). **Tried and dropped**: how hard a zonal defender steps to his man (`--zonalpull`, 0.7 and 1.4):
  no effect on goals (zonal v zonal 1.79 / 1.87 against 1.92) or on the gap.
- **Goals**: `SaveBase` (the save chance of a shot with no difficulty, before skill) 0.98 -> 0.90. Averaged over the four pairings, two seeds: goals 2.30 / 2.43 -> **2.74 / 2.79** (PL 2.83),
  shots 24.6 (25.4), on target 8.0 (8.8), saves 5.3-5.4, fouls 21.2-21.3 (21.5), penalty conversion 74-76% (75-80%). Steps measured on seed 900: 0.98 -> 2.43, 0.95 -> 2.55, 0.92 -> 2.73.
- **After both changes the zonal / man-to-man gap is +13.7 (seed 700) and +6.7 (seed 900): about 10 points, from about 25.** Noise is about 5 points a cell at 150 matches, so this is
  "much smaller, not zero". Man v man (2.9-3.1 goals, 26.7 shots) is still livelier than zonal v zonal (2.4-2.5 goals, 23 shots).
- **Strength spreads 1.8 / 0.45 -> 1.25 / 0.38**: the higher goal rate made the strong side win too much (strong v medium 83 / 14 / 3). Each pairing four ways, two seeds, 300 matches a seed:
  strong v weak 85.2 / 13.5 / 1.3 (real 85 / 11 / 4), strong v medium 75.7 / 20.8 / 3.8 (73 / 17 / 10), medium v weak 56.9 / 25.0 / 18.2 (54 / 27 / 19).
  **Still open**: the favourite almost never loses (1.3% and 3.8% against 4% and 10%) and the underdog scores about half what real underdogs do (0.33 v 0.63, 0.49 v 0.84).
  That is match-to-match variance, not squad strength.
- **Tests**: `dotnet test sim/` -> **Passed! 245 / 245** (13 m 32 s). The first run failed six; none was an engine regression, but each is stated:
  - the two full-back tests (`AnAttackingTeamPushesItsFullBacksUpFurther...`, `RolesChangeWhereAPlayerPlays`) tipped with any change (undoing the marking change passed them, undoing the keeper change failed them the other way round); on four matches
    the effect is noise. With 12 and 16 matches: attacking -22.4 m v cautious -28.2 m (needs +3), wing-back -7.1 m v defensive -12.5 m (needs +4) - margins of 5.8 and 5.4 m, thresholds unchanged.
  - `HardTacklersGoInMoreAndFoulMore`: 48 matches, not 24 (challenges and fouls 42.1 -> 45.0, +6.9%; threshold +4%).
  - `EachSideOfTheCornersHasItsOwnTaker`: four matches, not one (one match can have no home corner).
  - `ShotsAreStruckInAllTheDifferentWays`: 80 matches, not 16 (the outside of the boot: 0 in 16 matches, 2 in 40; the other kinds 31-137).
  - `StrongerMeansBetterPlayers`: the weak step needs 4 points, not 5 - the weak spread is now 0.38 (weak 48.2, medium 53.1, strong 70.2).
- **Open, not in this PR**: the zonal / man-to-man gap is about 10 points, not 0; the favourite loses too rarely (1.3% / 3.8% v 4% / 10%) and the underdog scores about half what real ones do;
  shots on target 8.0 (8.8).

## `feat/supabase-migration`

**From Neon to Supabase: Auth, Postgres, Storage and Realtime.** Neon Managed Auth has no Facebook provider; Supabase Auth has Google,
Facebook and Discord built in. Plan: `docs/plans/supabase-migration.md`; cutover: `docs/runbooks/supabase-cutover.md`.

- **Login**: Google, Facebook (no longer "coming soon") and Discord. Email sign-up keeps the 6-digit code, the reset link keeps its
  screens; the per-email lockout and the anti-enumeration answers are unchanged (Server Actions on `@supabase/ssr`).
  The browser client refuses password grants. A reset changes the password only on the recovery session that
  redeemed the link, not on an ordinary login. The per-email pause lives in the Server Actions. Supabase's
  password-verification hook is a paid feature, so it is not used. The sign-in form gives the same answer for a
  wrong password, an unknown address, and a pause. A password session opened after the pause began is not signed
  in. An older session stays. A password session the app did not create, including a direct Auth API grant, is not signed in.
- **Database**: Supabase Postgres through the transaction pooler (postgres-js); users are `auth.users`, display names moved to
  `profiles.display_name`; RLS on every table (the Data API can't read them). One fresh baseline migration.
- **Replays**: Supabase Storage (private `replays` bucket), read and written by the server only.
- **Live lobbies**: a database trigger broadcasts on a private channel per lobby (host and guest only); the client re-reads the lobby,
  so the lockstep still never sees a watermark without its inputs. Polling stays the fallback.
- **Moving the data**: `yarn migrate:neon-to-supabase` copies users (same ids), clubs, squads, matches and replays. Old passwords
  can't be moved: password users reset theirs once (the login screen says so).
- **CI**: e2e runs on the Supabase dev project and seeds its test accounts (`yarn e2e:seed`); the deploy migrates Supabase prod.

## `feature/new-generator-squads`

**The demo squads now come from the generator's realism round, and the engine is re-calibrated for them.** The generator (soccer-api.dev-spirit.com, PRs
[#22](https://github.com/SoccerVision/soccer-vision-players-stats/pull/22) and [#27](https://github.com/SoccerVision/soccer-vision-players-stats/pull/27)) is what the
website's "Create your team" uses too, so the engine has to play its players right.

- **Squads**: all three levels, the medium ones too, fetched from the live API (seeds 101 / 202). Checked: what the live server returns is identical, file for file, to what
  was tested. Average attribute: weak 44.1 -> 43.4, medium 55.2 -> 54.1, strong 70.3 -> 66.6.
- **Why the engine needed it**: the old squads were inflated by the old generator's bugs (values drawn once per process, stats overwritten by thresholds) and the
  engine was tuned on them. Old medium -> new: defenders' physical power 72 -> 57, midfielders' teamwork 63 -> 47, acceleration 67 -> 55, decisions 70 -> 63, aggression
  29 -> 50; keepers' one-on-one 65 -> 72. Played as it was (100 matches): goals 2.80 -> 1.99, shots 26.8 -> 20.8, fouls 20.1 -> 23.4. Each attribute shifted back explained a part
  (teamwork +0.3 goals, aggression +0.35, acceleration +0.15, decisions +0.2) - `SimRunner --adjust Stat=N` (new) shifts one attribute of every player.
- **Generator decisions 42 / 50 / 58 -> 55 / 65 / 75** ([#27](https://github.com/SoccerVision/soccer-vision-players-stats/pull/27), merged and live): still loosely tied to the level
  and spread wide; only the means, which were our design, moved to where the engine was tuned.
- **Constants** (150 matches, two seeds): `XgScale` 0.6 -> 1.0 (the chance a player sees in a shot - the new squads made too few chances), `FoulRate` 1.4 -> 1.25 (higher
  aggression), `RangeSaveScale` 0.3 -> 0.1, `CloseReachBase` 0.2 -> 0.3. Now: shots 26.2 (PL 25.4), on target 8.6 (8.8), fouls 21.0 (21.5), yellows 4.0 (3.44), corners 10.7 (10.4).
  **Goals 2.4-2.5 (PL 2.83): not closed** - the keepers save 6.3 of 8.6 shots on target (real 6.0 of 8.8); no save constant moved it further. Noted as open.
- **Winger cut-ins**: 3 stars is the generator's typical weaker foot (63% of normal players), so "a two-footed winger cuts in too" now needs 4+ stars (it was 3+, the old level 2).
  Without it every winger cut in (18 winger shots a match, 0.3 crosses); now 5.6 shots and 7.0 crosses.
- **Strong / weak spreads 1.2 / 0.5 -> 1.8 / 0.45, measured fairly**: each pairing played four ways - each level with both colours, in both seats - because Team A (zonal marking)
  is far better than Team B (man-to-man) with these squads: the same strong side wins 92% as Team A and 71% as Team B. Before, the strong side was always Team A, which hid it.
  Two seeds, 800 matches a pairing: strong v weak 81 / 17 / 3 (real 85 / 11 / 4), strong v medium 72 / 23 / 5 (73 / 17 / 10), medium v weak 53 / 29 / 19 (54 / 27 / 19).
  The zonal / man-to-man gap itself (20 points) is not real-football sized; it is open, for a round of its own.
- **Tests changed** (each kept its point; the old squads' particular players no longer carry it):
  - `FasterPlayersCoverMoreGround`: stamina pinned at 80 for both runs - with the new squads distance was set by fatigue (fast 6844 m v slow 6802 m).
  - `EveryInvertedWingerShoots...`: 2.5 cut-in shots a match (the new inverted winger: 2.9; the "three" was our design).
  - `CutInWingersCurl...`: counts every cut-in shot (the diagnostics), and kind 8 (curled) as well as kind 2 for the flank check - the new wingers curl most of theirs.
  - `IndividualInstructionsChange...`: the right midfielder's traits off - he drew Unselfish, which hardly shoots from range whatever he is told.
  - `AHighPressTakesTheBallBack`: 48 matches, not 24 (the press gains +2.0 / +2.3 points over 120 matches, two seeds; 24 gave +0.7).
  - `CornersAreVariedAndSwingBothWays`: both swings must happen; it no longer asks for more in-swingers (40 v 52 with these takers' feet).
  - `app/src/features/team/lineup.test.ts` (the website's port of `build_teams.py`): the new Red squad's XI, bench and attributes, worked out by `build_teams.py` itself.

## `feature/weak-foot-stars`

**The weaker foot in stars, 1 to 5.** How other games show it (read through a search): EA FC rates the weaker foot in 1-5 stars on the player card
([guide](https://www.magicgameworld.com/?p=128993)); Football Manager keeps a hidden 1-20 number and shows a word (very weak ... very strong,
[guide](https://sortitoutsi.net/content/67409/training-a-players-weaker-foot-guide)); for Football Manager Pro / Top Eleven no description was found. We use stars,
as the team generator already does.

- **The squads** (`tools/teams/build_teams.py`): each foot in stars, the strong one 5, the other the generator's `weak_foot_stars`. The medium squads were saved before
  the generator had stars and carry only its 1-3 level: it becomes stars the way the generator's own 138 players show it - level 1 = 1 star (9%) or 2 (91%), 2 = 3,
  3 = 4 (87%) or 5 (13%) - fixed by the player's id. All the other values of every player are unchanged (checked field by field). The six squads now hold 1 to 5 stars
  (1: 4 players, 2: 57, 3: 44, 4: 12, 5 - two-footed: 3).
- **The engine** keeps its tuning: it weighs the stars on its old 1-3 scale (`PlayerAttributes.FootLevel`: 1 / 1.2 / 2 / 2.75 / 3), so a 2-star foot plays as the old "1",
  3 stars as "2", 4-5 as "3", with a little difference inside each group. Passing on the weaker foot, the weak-foot shot's pace and accuracy, the trivela, the winger who
  cuts inside, and the traits read the stars. 150 matches, seeds 500 and 900, before / after: goals 2.75 / 2.70 and 2.74 / 2.94 (noise, in opposite directions), results and shots the same.
- **The page**: the player card shows the weak foot as stars (gold filled, the rest dim) instead of "2/3"; the foot colours are green for 4-5 stars, yellow for 3, orange for 1-2.
  The hover card and the Unity HUD read the new scale (two-footed = 5 and 5).
- **Tests**: `GeneratedTeamsTests.TheWeakerFootIsRatedInStarsOneToFive` (the strong foot 5, every star value present, someone two-footed, the scale the engine weighs them on);
  the left back's left foot is 5; "some use the weaker foot" is 3 stars or more (the old 2 of 3). `BoardAndDeadBallTests.APlayerShootsAtAnOpenGoal` played six matches
  for a thing that happens about 0.2 times a match (60 matches, seeds 60 and 3000: 10 and 16 before this round, 16 and 14 after - no fewer), so six saw none about a
  quarter of the time; it now plays up to 24 until one is seen.

## `feature/passing-tempo`

**Passes in the match facts, tiki-taka at Barcelona's real pace, and the game 5% slower.**

- **Real passing, counted** (`tools/calibration/passing_tempo.py`, [StatsBomb open data](https://github.com/statsbomb/open-data); open play only - throw-ins, goal kicks,
  corners, free kicks and kick-offs left out):

  | per team per match              | Premier League 2015/16 (380 matches) | Guardiola's Barcelona, La Liga 2010/11 (33) | its opponents |
  | ------------------------------- | ------------------------------------ | ------------------------------------------- | ------------- |
  | passes                          | 434                                  | 738                                         | 275           |
  | completed                       | 77.5%                                | 88.5%                                       | 71.2%         |
  | mean length                     | 19.0 m                               | 16.7 m                                      | 19.7 m        |
  | passes a possession run         | 4.35                                 | 7.12                                        | 3.10          |
  | seconds between passes in a run | 3.60                                 | 3.09                                        | 3.52          |
  | share of the match's passes     | -                                    | 72.5%                                       | 27.5%         |

  (2009/10 and 2011/12 Barcelona: 616 and 702 passes, 68% and 70.5% of the match's passes.) The 747 passes a match quoted by fan sites for 2010/11 is confirmed.

- **Why the engine's pass count looked low**: the match is 2,430 sim seconds of football at true speed shown as 90 clock minutes (2,309 at the new pace), so anything counted
  over time is about 2.3 times lower than in a real match - the distance covered was already scaled for this, the passes were not. Scaled, an ordinary team makes about 460 passes
  (real 434). The share between the teams was already right: a possession side v an ordinary one 72% of the passes (Barcelona 72.5%).
- **Passes in the match facts**: two new rows, _Passes_ and _Pass accuracy_, shown as over a real 90 minutes (`ReplayRecorder.PassesAs90`; the stats row is 4 numbers longer - the
  demo page and the online swap read the new length).
- **Tiki-taka at Barcelona's pace** (`MatchEngine.PatientTouch` = 0.6): a team that chose to keep the ball (possession, short passing) takes a touch and looks up before it moves
  the ball on, outside the final third. Barcelona pass 14% faster than a Premier League side in a run (3.09 v 3.60 s); the engine's possession side passed 29% faster than an
  ordinary one, now 13% (60 matches, seed 500: 4.58 -> 5.59 clock s). Possession side v an ordinary one: 61.9% -> 67.3% of the time, 482 -> 450 passes (scaled about 1,050 -
  still above Barcelona's 738), shots 13.8 -> 11.0. Larger values (1.0-2.0) were tried: fewer passes, but the share of the ball went past Barcelona's and the shots fell further.
- **The game 5% slower** (`MatchConfig.MotionSpeed` 2.025 -> 1.924; the Unity controller had the old 2.25, now the same): the players and the ball move 5% slower on screen; the
  clock and the real length of a half are unchanged. Goals a match 2.99 -> 2.75 (150 matches, seed 500; real Premier League 2.83), fouls and cards about the same.
- **Fix: the clock and the real time drifted apart** (`MatchEngine.Advance` / `Step`): both were summed as floats, about 60,000 small steps a match; at the new pace a half
  ended about 2 real seconds too early for its clock (637.5 s for 639.5-648.7). Both are now summed in double (the public values stay floats). Found by
  `FullMatchTests.MatchReachesFullTimeWithCorrectDurations`.
- **Test changed**: `FullMatchTests.ManyMatchesStayHealthy` asked every team for more than 100 passes - counted in compressed time. A side a goal up from the first
  minute now sits off (the score round) and made 99 (39% of the ball). It now asks for more than 150 as over 90 minutes (that side: 232); the fewest open-play
  passes by a Premier League side in a 2015/16 match was 183 (StatsBomb). `TacticalIntelligenceTests.LongThrows...` failed once (1 long throw in 12 matches) and
  passes since the clock fix; over 60 matches the long throws went from 34 (old pace) to 43 (new pace) - not fewer.
- **Not done**: an ordinary team passes faster than a real one in true-speed seconds (2.8 v 3.6 s) and builds up in fewer passes per shot (15 v 34 real, unscaled) - that is the
  pace of the whole engine, a separate round.

## `feature/possession-quality`

**Possession now follows the score, and a random match keeps each team's style.** Item 2 of the list of wrong statements ("possession near 50% in every pairing") was
fixed for style and quality in the possession round; this round checks what was left against real match data, not against articles.

- **Real data, counted** (`tools/calibration/possession_tiers.py`, [StatsBomb open data](https://github.com/statsbomb/open-data): every match of the 2015/16 Premier League and
  La Liga, 380 each; possession as each side's share of the passes - how Opta works out the published figure - and as its share of the time on the ball, which agree within a point):

  |                                                 | Premier League 2015/16 | La Liga 2015/16  |
  | ----------------------------------------------- | ---------------------- | ---------------- |
  | top two v bottom three                          | 53.8%                  | 60.4%            |
  | top two v 7th-14th                              | 49.9%                  | 62.1%            |
  | 7th-14th v bottom three                         | 52.9%                  | 50.4%            |
  | 7th-14th v 7th-14th, match to match (10% - 90%) | 36 - 64                | 38 - 62          |
  | a goal down / level / a goal up                 | 55.7 / 50 / 44.3       | 56.7 / 50 / 43.3 |
  | two or more down / up                           | 52.9 / 47.1            | 51.6 / 48.4      |

  One season a league, and the tier cells are small (12 to 48 matches): possession follows a club's style more than its table place (Leicester won the 2015/16 league with 42%).

- **What the engine did** (`SimRunner --poss`, which now also prints the pass share, the share by the score and the teams' passing quality; `--random` plays the game's random
  match - both managers pick a random plan and change the tactics during the match): with the default tactics, possession by level was too strong (strong v weak 62%, medium v weak 60%)
  because both teams play the same style; the score did nothing (a goal down: 49.5%); and in a random match every team played nearly the default style after six minutes.
- **The score** (`TeamAgent.RefreshEff`, `MatchEngine.Patience`): a team a goal up plays a little deeper, presses less and is less patient on the ball; a team a goal down the other way;
  less (0.4 of it) two or more apart; nothing at level scores. `TeamAgent.ScoreEffect` (1.3) and `MatchEngine.ScorePatience` (1.0) were set on 100-match runs and confirmed on 150 matches,
  seed 2000: a goal down **57.9%** with the default tactics, **53.7%** in a random match (real 56-57); two down 55.4% / 52.0% (real 52-53). Both teams refresh their tactics at every goal.
  With `--scoreeffect 0` the match is the same as before to the last digit.
- **Fix: the computer manager kept wiping its own plan.** Every four minutes it built its tactics from the default ones and only then answered the game, so a random plan's styles
  (passing, defence, attack, special) were gone at the first check. It now starts from the plan it kicked off with. Also: protecting a lead late it no longer switches to short passing
  (a side in front has less of the ball; it still slows the restarts and the keeper takes his time). The live match (the computer against you) starts from the default tactics, as before.
- **Random match, after** (150 matches, seed 2000): strong v weak **55.0%** (real 54-60), medium v weak **51.3%** (real 50-53), medium v medium match to match 43 - 56 (real 36 - 64:
  still narrower - real clubs differ more in style than the random plans do; not changed).
- **Results, before / after** (default tactics, Team A at home, 150 matches each, seed 500, the same matches; one seat only): strong v weak 95 / 5 / 0 -> 93 / 7 / 1, goals 3.90 - 0.18 -> **3.05** - 0.24
  (real, both seats pooled: 2.82 - 0.63 - a team in front no longer piles them on); strong v medium 87 / 11 / 3 -> 87 / 11 / 2; medium v weak 81 / 14 / 5 -> 80 / 14 / 6;
  medium v medium 60 / 19 / 21 -> 63 / 23 / 14. Win rates within the noise of 150 matches (about 4 points); goals a match 2.75-4.08 -> 2.91-3.29.
- **Not done**: the pass share. A side's share of the passes runs far ahead of its share of the time (strong v weak 78% of the passes, 62% of the time; real: the two agree), because the
  engine's matches have about 410 passes - the weaker side hardly strings passes together. That is the pass count, a separate round.
- **The high press against the score**: with the score in play a high press takes the ball back less: +1.1 / +1.3 points of possession over the same side without it
  (120 matches, seeds 3000 and 3100; about +1.8 before) - the side behind now pushes up as well. Tried and not kept: a pressing side keeping its press when in front (no difference),
  more weight for the opposing press in a team's patience (no difference with the test's press, which is at the cap). The press itself is unchanged. Test changed:
  `PossessionStyleTests.AHighPressTakesTheBallBack` asked for +1.5 points on 24 matches (52.0 v 50.7 now); it asks for +0.8. The size of the press's effect is our design, not a measured number.
- **Fix: a card at the final whistle.** A second yellow given at the whistle (the card for a foul where the referee had played the advantage) left the player on the pitch with no red:
  the referee was still walking to him with the card when the match ended (seed 6301, 93rd minute). At the end of every period a card on its way is now shown at once, and the red after a second
  yellow follows straight away (`ResolveCardsBeforeTheWhistle`). Test changed: `InjuryAndShotTests.DirectRedCards...` asked for the red to come strictly later on the clock than the
  second yellow - at the whistle the clock is stopped, so it now accepts the same second (the red still comes after the yellow in the match's events).
- **Tests**: `ScoreEffectTests` - a side a goal up plays deeper and presses less, a goal down the other way, level again as before; a side a goal down has 52-63% of the ball
  (24 matches); the computer manager keeps a counter-attacking plan 20 minutes in.

## `feature/phone-tabs`

**On a phone the page is no longer one endless scroll (it was about 4,950 px).** A narrow screen (760 px or less) shows one part at a time:

- **Page tabs** under the pitch: _Manager_ / _Commentary_ / _Match facts_.
- **Team Tactics** has its own three tabs: _Formation_ / _Squad_ / _Instructions_ (the formation board, the whole squad table, the sliders and style
  buttons - they were stacked one under the other).
- The manager's own tab row (Team Tactics, Player Instruction, Corners, ...) is one scrolling row instead of three lines.
- The squad table fits the screen width, so the Rating column is no longer behind a sideways scroll; the IN / OUT cards fade out at the right edge to
  show that they go on.
- Tablet and desktop are unchanged (the tab rows are hidden above 760 px). CSS, template and a few lines of JS - no engine change.
  Measured on a 390 px screen: the manager view is about 3,000 px, the commentary view about 1,300 px.

## `feature/feedback-ui-ref-switch`

**The referee, the long ball to a free man, and the phone.**

- **The referee shows a card in front of the player.** He used to hold it up from wherever he was. Now, with play stopped, he goes to the player (a little faster than his
  running pace) and shows it a metre and a half from him, and stays with him while he holds it up; the restart waits for him. Over 60 matches 186 of 203 cards were shown beside
  the player; the rest he showed after about 8 seconds of walking (the player kept moving away) - a longer walk made the red after a second yellow come too late. The red after a second yellow comes the usual interval after he has shown the yellow (if he is still walking to the player, it waits for him).
  Tests changed: `ContactAndOfficialsTests` checked the card was up at the very moment of the foul - it now accepts "shown, or the referee on his way to the player with it";
  `ManagerTests.TheChosenCornerTaker...` and `RatingsAndSkillsTests.TheManagersCornerSetUp...` played one match (seed 7) and needed a home corner in it - since the referee change
  all of seed 7's corners go the other way, so they take the first match from seed 7 that has one; `SkillMovesTests.Nutmegs...` counted the rare dummy for a shot on 16 matches
  (0.06-0.19 a match depending on tiny changes; 0.31 on 64, 0.44 before these rounds) - it now plays 48.
  `TacticStyleTests.ParkingTheBus...` measured the back line on 6 matches and landed on its 8 m edge (26 v 34); over 60 matches the gap is 10 m (24 v 34), so it now takes 12 matches.
- **At a goal kick (or the keeper holding the ball) the referee waits in midfield**, about 42 m from the kicking side's goal and 11 m off the middle on his diagonal, where the long ball
  comes down - not on the edge of the box. IFAB's guidance: the referee should be "close enough to see play without interfering with play" and "usually use a wide diagonal system"
  ([IFAB, Positioning, movement and teamwork](https://www.theifab.com/laws/latest/guidelines/positioning-movement-and-teamwork/); the guidance gives no position for the referee at a
  goal kick - this one is ours). Tests: `RefereeTests` (cards beside the player, 90%+; the referee within 25 m of halfway at goal kicks, 80%+).
- **A switch of play to a free man is taken down, not headed away.** A lofted pass from a team-mate, with no opponent within 2.5 m, is now controlled (chest, thigh, or a step back to let
  it drop) instead of headed on; before, 10.4 switches a match reached the man and were headed (and usually lost), now 1.1 (the ones where somebody was on him) - 9.4 are taken down
  (`ChestControlHeight = 2.4`, diagnostics `control.chest*`, `switch.headed`).
- **Phone: a finger can drag players.** Before, dragging was mouse and pen only (a finger had to tap twice). Now on the formation board a finger drags at once (the board does not scroll);
  on a card or a row of the squad table the finger rests a moment (about a quarter of a second) and then drags - a quick swipe still scrolls the cards or the page. Checked in a phone
  browser (touch events): a board swap, a sheet-row drag, and a swipe that moves nothing.
- **No odd artefacts**: a double tap or long press no longer selects text or pops up the copy menu, images do not drag, a tap leaves no grey flash, the page does not zoom on a double
  tap (pinch zoom still works); the game code, the links and the inputs stay selectable.
- The keeper-distance work: the extra appetite for shots in the box now stops at 18 m (`MidRangeShotAppetite` for 11-18 m), so the "long shots more / less" instruction still makes
  a player shoot from distance about twice as often as the other (the test asked for that and failed with 11-22 m). Keeper 3-6 m from the shooter on 10.9% of foot shots (real 8.6).

## `feature/match-stats`

**The match statistics, checked against ten Premier League seasons (counted) and StatsBomb (shot mechanics).** 200 matches on each of two seeds (4242 and 9001); real
numbers from `tools/calibration/real_leagues.py` (football-data.co.uk, 3,800 matches) and `tools/calibration/compare.py` (StatsBomb open data, 194 matches).

| per match                      | before | now (seed 4242 / 9001) | real                            |
| ------------------------------ | ------ | ---------------------- | ------------------------------- |
| goals                          | 2.70   | 2.91 / 2.91            | 2.83                            |
| shots                          | 30.1   | 26.3 / 26.6            | 25.4                            |
| shots on target                | 11.0   | 9.4 / 9.6              | 8.8                             |
| saves                          | 8.4    | 6.6 / 6.8              | about 6 (on target minus goals) |
| xG (what the shots were worth) | 3.81   | 2.65 / 2.82            | about the goals                 |
| fouls                          | 16.5   | 21.2 / 21.3            | 21.5                            |
| yellow cards                   | 2.6    | 3.4 / 3.6              | 3.44                            |
| penalties                      | 0.27   | 0.27 / 0.33            | 0.26 (StatsBomb)                |
| goals from the six-yard box    | 11%    | 20%                    | 21% (StatsBomb)                 |

**Why, and what changed** (each found by measuring first; the diagnostics now count shots and their outcome by zone and kind - `shot.zh.*`, `goal.zh.*`, `outcome.*`):

- **Tap-ins were saved.** A shot from inside the six-yard box scored 11% of the time (real: 31%), and keepers saved 44% of them. A keeper reached as far (3.2 m) against a shot
  from 4 m as against one from 20 m. Now his reach follows the time he has: a shot that is on him in a quarter of a second or less (about his reaction time) leaves him 20% of
  his reach (what his body covers), rising to his full dive at 0.6 s (`CloseReachBase = 0.2`).
- **Players saw chances that were not there.** The chance a player weighs before shooting added up to 3.8 goals a match while 2.7 went in: he shot 40% too often. His estimate is
  now scaled to what such shots actually score in the engine (`XgScale = 0.72`): fewer shots, and the shown xG matches the goals.
- **Long shots**: the extra save chance for a shot from range (up to +12 points) was cut to 30% of it (`RangeSaveScale = 0.3`).
- **Fouls**: a challenge outside the box is a foul 1.4 times as often (`FoulRate = 1.4`); inside the box it is unchanged (the penalties were already right).
- Tried and left: a lower appetite for shots inside the box (0.5-0.8 of it) changed nothing - a shot there is chosen on its chance, not on the appetite (`BoxShotAppetite` stays 1).

**Tests changed**: `FinishingTests` asked for at least 8 saves a match - the engine's own old number, not the real one; it now asks for 5-8.5 (real: about 6). `ManagerTests.FiveChanges...`
assumed no injury substitution before half-time in the match it plays (seed 12); with more fouls there is one, so it now takes the first match from seed 12 that has none (the rules it tests are unchanged).

**Keepers standing 3-6 m from the shooter (second pass)**: real shots (StatsBomb, foot, open play) have the keeper under 3 m / 3-6 / 6-10 / 10+ m away in 3.9 / 8.6 / 16.4 / 71.1% of the
cases; the engine had 4.9 / 17.2 / 19.0 / 58.9. The cause was not the keeper but where the shots came from: new diagnostics (`shot.dist.*`, `shot.keeperDepth.*`) show the engine's foot
shots came 29% from 6-11 m and 35% from 11-22 m (real open-play shots, headers included: 24% and 47%), and the keeper stood where keepers stand (0-4 m off his line for a shot from
range, 4+ m off only on 5% of close shots). Players reached 6-8 m and shot from there instead of from the edge of the box. Now a player shoots from 11-22 m three times as readily
(`MidRangeShotAppetite = 3`) and the chance he weighs is scaled to 0.6 (`XgScale`, was 0.72) so the total does not run away. Over three more seeds (200 matches each): keeper
distance 3.7 / 11.7 / 15.3 / 69.4% (real 3.9 / 8.6 / 16.4 / 71.1); goals from outside the box 9-12% (real 13%); goals 2.8-3.0; shots 28.4 (**up** from 26.3 in the first pass; real 25.4).
The 3-6 m share is still above the real one. **Correction**: the first pass said the xG statistic "matches the goals" - it did because the scaled estimate was being summed; the statistic
(and the momentum and commentary values) now use the unscaled chance, which was always about 20-30% above the goals in this engine (3.4-3.7 against 2.8-3.0): it is not a calibrated xG.

**Keepers 3-6 m from the shooter (third pass).** What was left of the 3-6 m shots: two thirds came after the shooter had carried the ball more than 6 m (new diagnostic
`shot.k36.*`: how he got there, how far he carried it, whether the keeper was charging) - he ran on at the keeper until the keeper was on top of him. Making a run towards the keeper
worth less changed nothing (the keeper is far when the run starts and comes out during it; tried and taken out). What works: when the keeper rushes out at him from 6-11 m, a player
shoots twice as readily (`ShootBeforeKeeper = 2`) - he shoots or chips before the angle is gone. Three seeds, 200 matches each: keeper 3-6 m away on 9.2 / 10.7 / 10.2% of foot shots
(real 8.6%; first 17.2%, then 11.7%); under 3 m 3.1-3.5% (real 3.9); 6-10 m 18.4% (real 16.4); 10 m+ 68-69% (real 71). Goals 2.83-2.95; shots 28.7-29.0 (real 25.4: still 13% too many).

**Still off, not changed here**: red cards 0.22-0.28 a match (Premier League 0.12; Ligue 1, La Liga, Serie A 0.23-0.30) - more fouls brought more second yellows; goals from outside
the box 9% (real 13%) and fewer shots from there (6.5 against 9.7); headed goals 18% (real 14%); keepers still stand 3-6 m from the shooter twice as often as real ones.

## `feature/league-calibration`

**Checked against eight real leagues, and the home advantage measured properly.** Real results, 10 seasons (2015/16 - 2024/25) of
[football-data.co.uk](https://www.football-data.co.uk/data.php): Premier League, Ligue 1, Eredivisie, Bundesliga, La Liga, Serie A, Primeira Liga, Scottish Premiership.

- **Home advantage is real and the engine has it.** Real: home wins 43-45%, draws 23-26%, away wins 28-33% in every league (the Premier League 44 / 23 / 32, goals 1.55 - 1.28); in
  2020/21, with empty stadiums, the edge vanished (38% home, 40% away). The earlier "58 / 25 / 17" in our matches was not home advantage but Team A being the better side (zonal v
  man-to-man marking); `SimRunner --swap` puts the same two squads in the other seats, and pooling both seats (600 matches each):

  | the same tactic for both teams  | home win | draw | away win | goals home - away | home edge               |
  | ------------------------------- | -------- | ---- | -------- | ----------------- | ----------------------- |
  | default (A zonal, B man-to-man) | 41%      | 23%  | 36%      | 1.42 - 1.29       | +5 points, +0.12 goals  |
  | both zonal, normal tackling     | 41%      | 31%  | 28%      | 1.06 - 0.86       | +12 points, +0.20 goals |
  | both man-to-man, get stuck in   | 46%      | 22%  | 32%      | 1.75 - 1.43       | +13 points, +0.32 goals |
  | both counter-attack, direct     | 46%      | 21%  | 34%      | 1.75 - 1.39       | +12 points, +0.36 goals |

  (Real: +12 points, +0.27 goals.) Nothing to change; only the default pairing, where the two sides are not the same team, shows less.

- **Are France and the Netherlands different?** Mostly not. The champion of a season: wins 72-80% of its matches, 2.35-2.54 points a game, in every league (Premier League 75%,
  Ligue 1 74%, Eredivisie 77%, Primeira Liga 80%); the bottom three win 15-22%. Single clubs over ten seasons: Paris SG 73% (goals 2.52 - 0.85), Ajax 71%, PSV 72%, Man City 70%,
  Celtic 75%, Benfica 75%. A little more top-heavy in the Netherlands, Portugal and Scotland, not in France.
- **The three tiers, from the table** (strong = the top two, medium = 7th-14th, weak = the bottom three; the stronger side's win / draw / loss %, both seats; counted by
  `tools/calibration/real_leagues.py`, which anyone can run again):

  |                         | strong v weak                      | strong v medium               | medium v weak                 |
  | ----------------------- | ---------------------------------- | ----------------------------- | ----------------------------- |
  | Premier League          | 89 / 8 / 3                         | 72 / 16 / 11                  | 61 / 24 / 15                  |
  | Ligue 1                 | 81 / 14 / 5                        | 68 / 20 / 12                  | 55 / 25 / 21                  |
  | Eredivisie              | 88 / 8 / 4                         | 78 / 15 / 7                   | 53 / 27 / 20                  |
  | Bundesliga              | 78 / 16 / 7                        | 71 / 17 / 12                  | 50 / 29 / 20                  |
  | La Liga                 | 84 / 12 / 3                        | 69 / 18 / 13                  | 54 / 29 / 16                  |
  | Serie A                 | 83 / 13 / 3                        | 70 / 20 / 10                  | 58 / 25 / 18                  |
  | Primeira Liga           | 90 / 8 / 2                         | 82 / 13 / 5                   | 47 / 31 / 22                  |
  | **all seven (counted)** | **85 / 11 / 4, goals 2.82 - 0.63** | **73 / 17 / 10, 2.31 - 0.84** | **54 / 27 / 19, 1.69 - 0.98** |

  (An earlier version of this table was made with a slightly different "medium" for the 18-team leagues, and its last row was averaged by hand, not counted: it said 84 / 71 / 55. The
  numbers above are the counted ones; the changes are 1-3 points.)

  The gap is not symmetric: the top is further above the middle than the middle is above the bottom. With one spread for both (0.8) the engine had 82 / 62 / 64. Now the strong squads go
  120% of the way from the medium one to the generator's excellent squad and the weak ones 50% (`TeamStrengthScaling.StrongSpread / WeakSpread`, attributes capped at 99):
  strong v weak 79%, strong v medium 63%, medium v weak 56% (150 matches a seat, both seats pooled; real: 85 / 73 / 54). The middle is right; the top two are under (the engine's
  strong squad also scores fewer goals than a real top side: 1.8 against 2.3 for strong v medium - not changed here).

- Not built, offered: a league setting (Premier League, Ligue 1, Eredivisie, ...) that sets these gaps from the table above - the differences between the leagues are small (the table).

## `feature/possession-realism`

**Possession now follows the style, the quality of the passers and the press.** Before: both teams got the ball the same number of times (about 85 each) and kept it
about 10 seconds, whoever they were - a park-the-bus team had 50% of the ball, a strong team against a weak one 52%, and the possession style, short passing and
a high press moved it by 2-5 points. Real football: the most possession-heavy side of a Premier League season has about 64-70% of the ball, and a match above
80% has happened only a handful of times (Liverpool 83.1% against Sheffield United in 2024 is the record,
[StatMuse](https://statmuse.com/fc/ask/most-possession-in-a-premier-league-game-by-a-team), [Khel Now](https://khelnow.com/football/top-five-instances-teams-ball-possession)).

- **Patience** (`Patience()` in `MatchEngine.OnBall.Passing.cs`): how much a team wants to keep the ball = its style (+1 possession, +0.5 short passing, -0.5 direct
  passing, -0.5 counter-attack, -0.5 park the bus) + how much better its passers are than the other team's (the starting eleven's mean of passing, ball control, composure
  and decisions; nothing for a gap under 4 points, up to +-1.8) - how hard the other side presses (a high press -0.6, and -2 for every point of pressing intensity above the
  default). A safe pass in a team's own half is worth `0.025 x patience` more (fading to nothing at the halfway line; negative patience counts half): the ball is
  recycled in the back and the middle, and in the other half the chance decides. A team on its defaults is at 0, so **ordinary matches are exactly as before** (the same 100
  matches, seed 500: goals 1.86 - 0.90, shots 18.61 - 12.16, possession 50.94%, before and after, to the last digit).
- **A possession side still shoots**: its risk premium used to make shots cost more as well as passes ("keeping the ball: no gambles"); a shot now costs what it always did, and
  the shot tendency of a patient team is raised by 1.2 x its style patience.
- **Read from the articles** ([FIFA research: possession and winning](https://www.fifatrainingcentre.com/en/environment/fifa-research/high-performance/possession-analysis/part-1-1-the-relationship-between-possession-and-winning.php),
  [why possession stats mislead](https://the-footballanalyst.com/why-possession-stats-might-be-the-most-misleading-metric-in-soccer/),
  [possession value](https://the-footballanalyst.com/possession-value-pv-measuring-the-real-impact-of-every-action/),
  [verticality](https://the-footballanalyst.com/verticality-in-possession-football-tactics-explained/),
  [possession style](https://www.soccerhelp.com/terms/soccer-possession-style.shtml)): possession is not winning (at the 2022 men's World Cup the teams with less of the ball won 26
  games to 23 and took 1.45 points a game to 1.31; Spain had 82% and lost to Japan, Leicester won the league on 42%) - so a possession side here gets **no better chances** than the
  others (xG 1.5 to 1.4 against a default side, 1.5 to 1.4 against a counter-attacking one), only the ball; a safe sideways or backward pass has almost no _value_ (PV) - the retention
  bonus is the style's, a deliberate price for control, and it stops at the halfway line; vertical play is faster and riskier (the direct and counter styles lose the ball sooner);
  and a possession style is played "only as far up the field as the skill allows" - the quality term.
- **What it does** (80 matches, seed 900, medium squads; home v away):

  | home v away                                                                    | home possession | passes (home / away) | pass completion | shots       | xG        | goals     | possession match to match (min - max) |
  | ------------------------------------------------------------------------------ | --------------- | -------------------- | --------------- | ----------- | --------- | --------- | ------------------------------------- |
  | default v default                                                              | 50.7%           | 210 / 197            | 80% / 77%       | 18.2 / 11.8 | 2.6 / 1.2 | 2.0 / 0.7 | 44 - 57                               |
  | possession + short passing v default                                           | **65.4%**       | 588 / 147            | 93% / 74%       | 13.8 / 12.4 | 1.5 / 1.4 | 1.1 / 0.8 | 58 - 73                               |
  | possession + short v counter-attack + direct                                   | **71.4%**       | 695 / 115            | 94% / 70%       | 13.8 / 11.2 | 1.5 / 1.4 | 1.0 / 1.3 | 63 - 80                               |
  | possession + short + high press + aggression v park the bus + counter + direct | 60.9%           | 339 / 116            | 89% / 75%       | 28.3 / 4.9  | 3.0 / 0.5 | 2.3 / 0.5 | 53 - 71                               |
  | default v possession + short + high press + aggression                         | 40.7%           | 129 / 364            | 70% / 87%       | 18.4 / 16.7 | 2.9 / 0.8 | 1.8 / 0.5 | 28 - 47                               |
  | high press (aggression 0.8) v default                                          | 53.8%           | 209 / 148            | 79% / 71%       | 19.3 / 11.6 | 2.6 / 1.4 | 2.2 / 0.9 | 49 - 59                               |

  A top possession side now passes 600-700 times and completes 93-94% (a top real side: 700-900 passes, about 90%). Matches reach 80% possession (the tail), a typical
  possession side 65-72%. A park-the-bus team still keeps the ball more than a real one would (time wasting is part of that style in the engine; not changed).
  **Not changed but seen**: the counter-attack style is strong in the engine - against a default side it takes 2.6 xG to 1.4 on 45% of the ball; that was so before this round.

- **Quality**: strong and weak squads (see the team strength round) now also differ in how much of the ball they have: strong v weak 61%, medium v weak 61%, strong v medium 54%
  (both seats, 200 matches each; real football: top v bottom roughly 60-65%). Because keeping the ball costs the stronger side some attacks, the strong and weak squads are
  moved 80% of the way from the medium one instead of 60% (then re-fitted to eight leagues, see `feature/league-calibration`: strong 120%, weak 50%) and the results stay where the real data are:

  | stronger v weaker (both seats, 200 matches each) | stronger wins | draw | loses | goals       | real football                                       |
  | ------------------------------------------------ | ------------- | ---- | ----- | ----------- | --------------------------------------------------- |
  | strong v weak                                    | 82%           | 14%  | 4%    | 2.55 - 0.32 | top four v bottom three 80%, 2.6 - 0.6; top two 89% |
  | medium v weak                                    | 64%           | 21%  | 15%   | 1.91 - 0.62 | big favourite 68%, 2.14 - 0.86                      |
  | strong v medium                                  | 62%           | 19%  | 19%   | 1.88 - 0.78 | clear favourite 57%, 1.84 - 1.04                    |

- **Tests** (`PossessionStyleTests`): two ordinary teams share the ball (46-55%); a possession side keeps it (60-78%) and shoots at least 80% as often as its opponent; a counter side
  against a possession side has 20-40%; a high press takes the ball back (more than 1.5 points above the plain side); a strong team has more than 54% against a weak one with no
  tactic. `SimRunner --poss` prints all of this (per team: possession, passes, completion, tackles won, interceptions, shots, goals, xG, possessions, seconds per possession,
  the spread of possession).

## `feature/team-strength`

**Weak, medium and strong teams** (squads from the team generator API, <https://soccer-api.dev-spirit.com/docs>, v2).

- **Medium is the squad the game always had** - nothing measured before changes (`GeneratedTeams.cs` is untouched except one word: its player helper is
  now `internal`). **Weak** and **Strong** are the same two teams (seeds 101 and 202) made at the API's `weaker` and `excellent` level
  (`data/teams/api/`, built into `GeneratedTeamLevels.cs` with `build_teams.py --levels-dir data/teams/api`). The same builder picks the eleven and
  the bench exactly as it did for the medium squads. The API's own `normal` level is not used: it is the same as ours (below).
- **Where to choose**: "Play the computer" asks _Your team_ and _Opponent_ (weak / medium / strong); in the developer view the gear has _Home squad_ and
  _Away squad_ (changing one replays the match). Online games are always medium. SimRunner: `--home-strength weak|medium|strong --away-strength ...`;
  the engine: `DemoTeams.Red(seed, TeamStrength)`, the WASM `Start(..., homeStrength, awayStrength)`. SimRunner also prints home win / draw / away win %.
- **How the squads compare** (the eleven that start, mean attribute out of 100; speed; finishing of midfielders and forwards):

  | squad                                 | all      | defenders | midfielders | forwards | keeper | speed | finishing |
  | ------------------------------------- | -------- | --------- | ----------- | -------- | ------ | ----- | --------- |
  | weak (API _weaker_)                   | 44.8     | 43.4      | 45.5        | 44.7     | 47.5   | 52    | 48        |
  | **medium (ours)**                     | **57.1** | 55.7      | 59.0        | 55.8     | 57.5   | 67    | 65        |
  | medium (API _normal_, for comparison) | 56.1     | 55.5      | 56.4        | 55.1     | 59.7   | 64    | 64        |
  | strong (API _excellent_)              | 71.2     | 68.8      | 73.0        | 70.5     | 74.6   | 85    | 83        |

  Our own squads are within about 1 point of the API's normal level everywhere, so "medium" is what the API calls normal.

- **How far apart the teams are - checked against real football.** The generator's weaker / excellent squads, used as they are, made the stronger side win
  94% of the time (a medium team against a weak one) - far more than the real game. Real numbers, from 10 Premier League seasons (3,800 matches,
  <https://www.football-data.co.uk/englandm.php>, results and Pinnacle closing odds with the margin taken out):

  | real Premier League, 2015/16 - 2024/25 | favourite wins | draw | favourite loses | goals fav - underdog      |
  | -------------------------------------- | -------------- | ---- | --------------- | ------------------------- |
  | all matches                            | home 44%       | 23%  | away 32%        | 1.55 - 1.28 (home - away) |
  | clear favourite (55-60% in the odds)   | 57%            | 22%  | 21%             | 1.84 - 1.04               |
  | big favourite (60-70%)                 | 68%            | 19%  | 12%             | 2.14 - 0.86               |
  | huge favourite (80%+)                  | 88%            | 7%   | 5%              | 2.93 - 0.60               |
  | top four at home v bottom three        | 80%            | 14%  | 6%              | 2.59 - 0.62               |
  | top two (either seat) v bottom three   | 89%            | 8%   | 3%              |                           |

  So each weak / strong squad is moved only **60%** of the way from the medium squad towards the generator's (`TeamStrengthScaling.Spread = 0.6`, the same
  player in the same slot; tried 0.3 / 0.45 / 0.6 over 100 matches per pairing and both seats). The stronger side's results, both seats pooled
  (150 matches per seat, seed 500; the home edge cancels out; with Spread 0.6, before the possession round - see that round for the final 0.8 table):

  | stronger v weaker | stronger wins | draw | loses | goals stronger - weaker | real football                                       |
  | ----------------- | ------------- | ---- | ----- | ----------------------- | --------------------------------------------------- |
  | strong v weak     | 81%           | 12%  | 6%    | 2.57 - 0.42             | top four v bottom three 80%, 2.6 - 0.6; top two 89% |
  | medium v weak     | 69%           | 17%  | 14%   | 2.25 - 0.68             | big favourite 68%, 2.14 - 0.86                      |
  | strong v medium   | 60%           | 18%  | 22%   | 1.78 - 0.81             | clear favourite 57%, 1.84 - 1.04                    |

  All nine pairings (home v away; goals home - away; shots home - away; home win / draw / away win %):

  | home v away                               | goals           | shots           | result %         |
  | ----------------------------------------- | --------------- | --------------- | ---------------- |
  | weak v weak                               | 1.58 - 0.75     | 12.9 - 6.4      | 59 / 23 / 18     |
  | weak v medium                             | 0.89 - 1.49     | 8.2 - 15.6      | 25 / 25 / 51     |
  | weak v strong                             | 0.69 - 1.83     | 5.4 - 21.3      | 13 / 21 / 66     |
  | medium v weak                             | 3.00 - 0.46     | 24.9 - 4.6      | 87 / 9 / 3       |
  | **medium v medium (baseline, unchanged)** | **1.89 - 0.87** | **18.4 - 12.4** | **58 / 25 / 17** |
  | medium v strong                           | 1.23 - 1.31     | 14.5 - 17.6     | 37 / 23 / 40     |
  | strong v weak                             | 3.30 - 0.15     | 31.4 - 2.2      | 95 / 4 / 1       |
  | strong v medium                           | 2.25 - 0.39     | 25.3 - 8.3      | 81 / 13 / 7      |
  | strong v strong                           | 1.62 - 0.82     | 19.6 - 15.2     | 55 / 29 / 17     |

  Possession stayed near 50% in every pairing - which is **not** how real football works (a team's share of the ball follows its style and its quality; see the possession round, which changes it). It was written here as if it were realistic; it was a limit of the engine. **Correction (see `feature/league-calibration`)**: the note that stood here - that the home edge between equal teams was bigger than the real one - was wrong. 58 / 25 / 17 was Team A's own edge over Team B (zonal v man-to-man marking, a better squad), not home advantage; with the two squads swapped between the seats the home edge is the real size (+12 points of win share, +0.2 to +0.3 goals).

- Tests: `TeamStrengthTests` (valid squads at every strength; medium unchanged; each step at least 5 attribute points; the stronger side scores more and wins more than it loses, 48
  matches per pairing, both seats; the biggest mismatch stays between 62% and 92%, not a massacre). Verified in the browser: choosing the strengths in the page reaches the engine and the squads that
  come back differ (speed 84 v 50 with strong at home and weak away).

## `fix/landscape-layout`

**A phone on its side: the pitch fills the screen's height and the controls sit beside it.** Before (a live match in a 1000 x 375 frame): a small pitch in the middle with big empty
margins, the title cut off at the top and the controls (speed, LIVE, the slider) below the fold. Now, in a short landscape window (640 px or less): the pitch takes the full height
(left column), the controls are a rail on the right (mode buttons, sound, speed, the LIVE badge and who plays, the slider, the clock), the title is gone (the scoreboard on the pitch
already says who plays) and the manager's panels follow below at full width. Checked at 1000 x 375, 844 x 390 and 1280 x 600 (a small laptop window); portrait, tablet and desktop are
unchanged. CSS only.

## `fix/layout-responsive`

**The page checked at four sizes (phone upright / on its side, tablet, desktop) and the ugly parts fixed.**

- **Line-up card on an upright phone**: the logo sat on top of the team name, the title wrapped into the logo, the little pitch lay over the list
  and the list was cut off. Now (in any narrow pitch box): list on the left, little pitch on the right, all eleven names visible, nothing overlaps.
- **Phone on its side**: the pitch was taller than the screen. It now fits the screen height (title shrinks to one slim line).
- **Manager panel on a phone**: "THE MANAGER'S PICK" no longer breaks into three lines; the text goes under it.
- Tablet and desktop are unchanged. CSS only - no engine change.
- Full suite on the integrated preview branch (before this round): Passed 229 / 229.

## `fix/player-size-by-height`

**Smaller player circles, sized by the player's height.**

- The minimum player circle is a little smaller (5.4 CSS px radius instead of 6.2), so they crowd each other less on a phone.
- A player's circle now follows his height: 180 cm is the base size, each 10 cm more or less is about 6% bigger or smaller (165 cm: 91%,
  195 cm: 109%, capped at 88%..112%). Numbers are fitted to the circle as before. Drawing only - no engine change, no effect on results.

## `fix/mobile-pitch-resolution`

**A sharp pitch on a phone, and shirt numbers that fit their circle.**

- **The blur**: the page lowers its drawing quality when it thinks the device is slow, and it judged that by the time _between_ frames. A phone
  in low-power mode (or a busy browser) shows 30 frames a second, which looked like "slow" - so the pitch was drawn at 1x pixel density on a 3x
  screen. It now judges by how long _the page_ takes to draw a frame (a real slowdown still reduces effects and frame rate), and the pitch never
  drops below 1.5x pixel density.
- **The numbers**: a shirt number had a minimum size of 8 canvas pixels, while a player is a 3 px speck on a phone-width pitch (the whole pitch is
  ~370 px wide), so the number spilled out of the circle. Now a player is never smaller than 6.2 CSS pixels in radius (about 12 px across - they
  overlap a little more on a phone, but can be read), and the number is sized to fit inside the circle (a three-digit number is narrower).
- **Correction**: the first diagnosis (low-power mode) was a guess from the colour of the battery icon, and it was wrong for the phone in question.
  The cause on that phone is not confirmed, so the resolution no longer depends on the speed ladder at all: it stays at the device's pixel density
  (up to 2x) always; a slow device sheds effects and frame rate instead. The developer view (or `#perf` in the address) shows a status line with
  what the pitch is really drawn at: `pitch 744x563 (2x) · quality 0 · 20 fps · 2.2 ms`.
- Checked at 420 px wide, 3x density, with 30 frames a second forced: before, the pitch canvas had 1 pixel per CSS pixel and the numbers were
  unreadable; now it has 2 per CSS pixel and each number is inside its circle.

## `feature/corner-routine-attack-tab`

**The corner routine is in the Attack tab.** The right- and left-side routine choosers (varied, near post, far post, penalty spot, pulled
back, short) were in Corners -> Takers, with the list of who takes them; the routine is where the corner is aimed, so it now sits in
Corners -> Attack, above the set-up of who stands where. Takers keeps the taker lists and the zones. Checked in a browser: both
selectors are in the Attack tab, none is left in Takers, and a changed routine is kept after the redraw.

## `feature/board-drag-circle`

**Dragging a player on the formation board**: the circle now comes along under the mouse - his shirt colours, his number and his name, the
size it has on the board, centred on the pointer - so it is plain that he is really being carried (it used to be a small grey label
hanging off the cursor). The place he left shows a faint, dashed circle (it was a half-faded dot with its label). Checked in a browser:
the carried circle is centred on the pointer to the pixel, the left-behind dot is at 30% with a dashed outline, and it is gone on release.

## `feature/crowd-sounds`

**The crowd, properly.** What the crowd does for each event now lives in one place, `web/src/audio/crowdReactions.ts`, and is checked
without a browser (`npm run check:audio` in `web/`: 26 scenarios, plus a loudness check for every reaction).

- **The applause could not be heard at all**: the recording is very quiet (peak 0.12, a thirteenth of the others) and played at a
  further-reduced level. It is now lifted (x10) - every reaction that uses it was silent before: a saved shot, a shot turned round the
  post, the woodwork, a blocked shot, a draw at full time.
- **The visitors score**: their fans celebrate loudly but **from one side only** (hard to one side, dull and far, gain 0.1 -> 0.55; it
  was inaudible) and the whole rest of the ground boos (louder than the celebration), then the crowd goes quiet.
- **The opponent fouls / is carded**: a foul on our player is jeered, a card for the visitors is applauded, a red card for them is a
  roar and jeers; a card or free kick against us still angers the home end.
- **A shot by the visitors**: the crowd swells (holds its breath); our keeper's save, a block by our defender, a shot turned round the
  post for a corner and a shot off the woodwork get applause (relief); a home penalty is cheered.
- **The final whistle**: a home win is cheered, a defeat booed, a draw applauded - **at the whistle**. Before, the reaction was tied to the
  FullTime event, which the engine emits after the whole walk-off (30-40 seconds later). The engine now marks the final whistle (Detail
  1; a whistle after ninety minutes that is followed by extra time or penalties is not marked; a shoot-out result still comes with the
  FullTime event). `FinalWhistleTests` checks that the crowd is told the result exactly once, in league, cup and league cup.
- Checked in a real browser (Playwright): all seven recordings decode, nine events in a row produce the expected reactions, no errors.
  I cannot listen to it myself - the levels were checked by measurement (`MEASURED_LEVEL`), not by ear.

## `feature/attack-rebounds-2`

**Rebounds, the second round - a rebound shot now converts as often as a real one.** The engine takes as many rebound shots as reality (1.1 a
match against 1.34) but scored half as many (about 10% against 21%). New counters (`rebound.*`) showed why, over 150 matches: the follow-up
shots **died in the crowd** - 54% were blocked (22% of all shots are) and 35% missed - and the keeper saved three in four of those on target
(reality: about three in five), although their xG (0.18) said they should convert at 18%. Three changes, all for the follow-up shot only
(the same team's shot within 3 s, within 14 m of goal):

- **A gap through the defenders**: each defender is, 55% of the time, caught moving or unsighted for a follow-up shot, so he does not get a leg
  in the way (decided once per shot and per defender, from a hash - no random numbers drawn). Blocked: 54% -> about 30% of rebound shots.
- **A cleaner finish**: the aim error is 35% smaller (he is already set, the keeper and the defenders are still recovering). Misses 35% -> 27%.
- **The keeper is still getting up**: his chance to stop it is x0.7 (he has just made a save).
  200-plus matches on three seeds: rebound goals **0.14 -> 0.23-0.27 a match** (real 0.28), rebound conversion **10% -> 20-26%** (real 21%),
  share of open-play goals 5% -> 9-10% (real 12%); goals 2.64-2.79 a match (was 2.6-2.7), shots 29.7-30.2 (unchanged), first-time goals 53-56%.
  Tried first: only the gap (blocks fell, but the shots were saved or missed instead - goals did not move); only the sharper finish (same).
  Tests: the full suite passed 227 of 228; the one failure, `IndividualInstructionsChangeHowAPlayerPlays` (one player's long shots, 3.8 against 1.8 where
  it needs more than 4.1), averaged only four matches - too few for something this rare, so any change in the engine could tip it. It now
  averages sixteen and passes.

## `feature/teams-api`

**The team generator's new address**: the squads came from `soccer-simulation.onrender.com`, which no longer answers.
`tools/teams/fetch_teams.py` fetches squads from the new API (<https://soccer-api.dev-spirit.com/docs>, v2 - same seed, same squad)
in the format `build_teams.py` reads (the skill groups v2 returns as null are left out); `build_teams.py` takes `--dir` and `--out`.
The squads the game uses now are not changed - that changes every match and the tests' measurements, so it is a separate step
(`fetch_teams.py --out data/teams red-rovers:SEED blue-falcons:SEED`, then `build_teams.py`, then the full suite). Tried: two squads
of 23 (seeds 101 and 202) build into 20-man squads. Noted: `GeneratedTeams.cs` in the repository was not made by the present
`build_teams.py` (it lacks the `Order` list the script writes) - that was so before.

## `feature/attack-rebounds`

**The rebound is hit first time**: a ball that comes back off the keeper, the post or a defender a moment after his team's shot, within
17 m of goal - the man who followed it in hits it at once (55-95% of the time, by his finishing and composure) instead of taking a touch
while the keeper gets up. 200 matches, two seeds: goals **2.53** a match (real 2.53), foot goals hit first time **54-58%** (real 55%),
rebounds converted about **11%** (was about 8%; real 21%), rebounds 5-6% of open-play goals (real 12%). Tried and taken out again: a
keeper slower to get up after a save (the follow-up shots rarely reach him - most are blocked or miss - and goals fell).

**Corners, looked into (and left as they are)**: new counters (`header.corner.*`, `header.corner.nearestDefender.*`) show that at a
corner nearly every header is a free one (7.5 a match free, 0.3 contested) although a defender is within 2 m of the header two times in
three. Three changes were tried and taken out again because they changed nothing measurable (200 matches each): a stronger defender in
his own box in the air, markers tight between their man and the landing point, a defender at the shoulder making it a contested
header. The corner problem is in how the delivery and the runs meet (the ball reaches the attacker before any defender can), not in the
duel - the next thing to look at. Also still open: shots, about 30 a match against 25 in reality - a higher price on a missed shot
barely changed them (30.6 -> 29.7); the engine's attacks reach the box more often than real ones (it plays far more directly: about
410 passes a match, real 850-1000).

## `fix/online-connect`

**Online play with a six-digit code, kits of your own, and a connection that comes back.** After two hours of picking line-ups the
match did not kick off: the direct connection between the two browsers had dropped without a word, and there was no way back
without a new invite. Now:

- **Start screen**: "Play the computer" or "Play a friend online". Online, you pick only your own kits - your kit, a second kit (worn
  if your friend's shirt looks like yours) and your goalkeeper - and then **Create a game** (you get a six-digit code, or a link with
  it) or **Join a game** (type in your friend's code). The two find each other through the free PeerJS broker (0.peerjs.com): it only
  passes the handshake; the match goes directly between the two browsers, encrypted.
- **The kits**: the host keeps his kit; the friend wears his own, or his second kit if his looks like the host's (or, if both do, a
  shirt that clashes with neither); each keeper takes a colour that clashes with no shirt. Both sides work it out the same way and
  both see the two kits before the match ("your friend's shirt looked like yours: he wears his second kit").
- **A connection that drops comes back**: a heartbeat every 2 s; after 10 s of silence a red banner says so ("reconnecting...",
  "waiting for him to come back"), the friend's page reconnects by itself, and the host sends him everything he missed (the match
  carries on on the host's engine; the friend catches up and his "ready" and his team are sent again). Tested with two browsers: a
  15 s silence before the match and a closed connection in the middle of it - the match kicked off and both clocks agree afterwards.
- If the code service cannot be reached the page falls back to the invite link and reply code.

**Online play: no more endless "Connecting..."**. The two browsers connect directly (WebRTC) and the page said nothing when they could
not reach each other. Now: the invite and the reply code wait (up to 12 s) for the internet address the STUN server reports - the old
4 s limit could leave only the home-network address in the code, and a friend elsewhere could then never connect - and warn if there
is none; four STUN servers instead of one provider; the page says what the connection is doing ("looking for a way through the two
networks") and, after 30 s or when it fails, why (mobile data, work or school networks) and what to try; a reply code for an invite
that was already used, or a page that was reloaded, gets its own message; both players are told to keep the page open. A TURN relay
for networks that cannot connect directly can be given in the address (`?turn=turn:host:3478,user,password`).

## `feature/player-names`

**Realistic player names from the Onomaverse names datasets** (<https://onomaverse.com/datasets>, CC BY 4.0 - the notice is in
`THIRD_PARTY_NOTICES.md` and in the code as `NameAttribution`).

- `tools/names/build_onomaverse_names.py` reads three files of the release (given-name frequency, surname frequency, name gender
  inference) and writes `Simulation/Names/OnomaverseNameData.cs`: for **37 countries** the most common **male** first names (up to 300) and surnames (up to 500) with their frequency weights - Spain, Germany, France, Italy, England, Brazil, Argentina, Portugal,
  the Netherlands, Croatia, Belgium, Switzerland, Austria, Denmark, Sweden, Norway, Poland, Czechia, Ireland, the USA, Turkey,
  Mexico, Colombia, Uruguay, Chile, Peru, Bolivia, Costa Rica, Panama, Nigeria, Ghana, Cameroon, Egypt, South Africa, Canada,
  Finland and Russia. A name recorded for both sexes counts with the share of its bearers that are male (Andrea in Italy: yes; Ana,
  Maria: no). Names are copied as the dataset writes them, accents and special characters kept (João, Müller, Željko).
- **Local names only**: a name that is far more at home in another cultural group of countries is left out (Mohamed in Spain, Singh
  in England, Yilmaz in Germany, Nguyen in the Netherlands) - neighbours that share names keep them (Antonio in Spain, Jan in the
  Netherlands); the dataset's own origin label (Arabic, Sanskrit, Vietnamese...) is a second check. Women's surname forms become
  the men's (Kowalska -> Kowalski, Ivanova -> Ivanov), a woman's given name in a surname list is dropped, a surname that is one of
  the country's common first names is dropped (no Ivan Ivan), the small words are lower case (van der Linden, da Silva), the Irish
  O' and the Mc spellings are put right (O'Brien, McDonald), the Italian D' (D'Angelo), and the long tail of rare "names" is cut.
- **Accents checked on the internet**: the dataset writes many names without their accents (Jose, Garcia, Goncalves).
  `tools/names/restore_accents.py` puts back an accent only where Wikipedia confirms it - English Wikipedia (a redirect from the
  plain spelling, or an article in a name category of the language) **and** the Wikipedia of the language itself (es / pt / fr / it),
  plus the spelling rules (a Spanish accent where the stress rule wants one: Hernán yes, Hernándo no; the same for Portuguese) and
  each language's own letters (no Cantù in Mexico). 216 names: José, García, López, Álvarez, Núñez, Martín, Raúl (Spanish), Antônio,
  Flávio... (Brazil), Luís, Vítor (Portugal), François, Étienne, Hervé (France), Dessì (Italy). Every change is printed for a read
  through; four were taken out by hand (Baldé, Florín, Gállego, Chicó). Names Wikipedia does not confirm stay as they are.
- `NameBank` (the tables by country - ISO code or nationality), `WeightedNameList` (first names and surnames apart, picked in
  proportion to their frequency), `PlayerNameGenerator` (a full name per player from one country, never the same full name twice in
  a database, `Reserve` for the players that already exist, never the same first name and surname, the same seed the same names),
  `NationMix` (`League("ES")`: mostly Spanish, some foreigners), `NameSquad(team, mix)` (names and nationality only).
- **Nothing existing changed**: the demo squads are still named by `IsraeliNames` (a test fixes the names of a seed).
- Left out because the dataset's names for them are mostly not their own: Greece, Morocco, Algeria, Japan; too few names: Hungary,
  Bulgaria, Ecuador, Serbia; not in the dataset at all: Ukraine, Senegal, Ivory Coast, Mali, Romania, Slovakia, Bosnia, Paraguay,
  Venezuela, Australia, South Korea. Thin countries (Norway, Czechia, Russia, Sweden, Denmark, Finland...) say so in their note.
  Not Onomaverse: the Croatian surnames (the release's 29 are nearly all not Croatian) - a short list of common Croatian surnames.
- A sample of 20 names per country: `tools/names/SAMPLE_NAMES.md`. Tests: `NameGenerationTests` (16).

## `feature/attack-calibration`

**Phase 0 of the attack redesign: real football as the yardstick.** `tools/calibration` (see its README): `statsbomb_targets.py`
reads 194 matches of complete 2015/16 league seasons (Premier League, La Liga, Serie A, Ligue 1 - 40 each - and the 34 Bundesliga
matches there are) from the **StatsBomb open data** (<https://github.com/hudl/open-data>; data: StatsBomb) and writes
`targets.json`; `compare.py` puts the engine next to it. Where the engine stands (100 matches) against the real thing:

|                                                             | engine             | real              |
| ----------------------------------------------------------- | ------------------ | ----------------- |
| goals / shots / on target a match                           | 2.94 / 35.3 / 14.0 | 2.53 / 24.8 / 8.3 |
| foot goals in open play hit first time                      | **15%**            | **55%**           |
| open-play goals with no pass before them                    | **46%**            | 14%               |
| open-play goals from a rebound                              | 2.5%               | 12%               |
| open-play goals after a ground pass or cut-back             | 17%                | 30%               |
| foot shots with the keeper under 3 m away                   | **18.5%**          | 3.9%              |
| goals from the six-yard box / the rest of the box / outside | 26 / 62 / 12%      | 21 / 66 / 13%     |
| headers / penalties of all goals                            | 20 / 8%            | 14 / 7%           |

The engine's goals come too often from a player carrying the ball on to the keeper and too seldom from a first-time finish after a
pass or from a rebound - the next phases are about that.

**Phases 1-2, first round** (measured against the same 194 matches):

- **The first-time finish**: a pass into the box (now up to 20 m out and 17 m wide of the middle) at a good angle is hit first time
  far more often - a finisher is set more readily (35-90%, by finishing and composure), the shot is weighed much higher while he is
  set and carrying it on is not an option then. Foot goals in open play hit first time: 15% -> **46%** (real 55%).
- **Carrying it on to the keeper is worth less**: the shot a dribble leads to is discounted more (x0.72, was 0.85) and by every
  defender near where he would shoot from (x0.8 each). Shots with the keeper under 3 m away 18.5% -> 10% of foot shots (real 3.9%).
- **The spill**: a hard shot, a wet ball or unsure hands - the keeper can only beat it down in front of him (6-34% of his parries),
  and the man who followed it in has a second chance. Goals from rebounds 2.5% -> 5% (real 12%).
- **The measurement made fair**: a goal counts as set up by the team's last pass in that possession, however long the scorer carried
  it after (as StatsBomb counts it; it was only within 4 s), and a first-time finish is counted as one (a flag was cleared before it
  was read). Open-play goals with no pass before them: **18%** (real 14%); after a ground pass or cut-back 28% (real 30%).
- Still to do: too many shots (35 a match, real 25) - from the six-yard box (4.7 against 1.7), after crosses (7.6 against 3.1) and
  after through balls (4.5 against 0.8); rebounds are shot as often as in reality but scored half as often.

**Phases 1-2, second round - fewer, better shots** (200 matches, two seeds, against the same 194 matches):

- **A shot that does not go in gives the ball away**: that cost is now weighed in the box too (it was only outside it), so a 4%
  chance from a tight angle is not worth more than keeping the ball for a better one (half the cost when he is set for a first-time
  finish). Outside the box the cost of a poor shot is higher as well.
- **The edge of the box is a place to have a go from** (17-30 m: x1.4): shots from outside the box 7.0 -> **9.2** a match (real 9.7).
- **Running on to a keeper who is coming out**: the shot it leads to is worth about half when the keeper would be within 4.5 m.
- **The header in the box**: not every header there is at goal - with a defender all over him he may only get a flick-on, and from
  a hopeless angle he heads it down for a team-mate.
- **The through ball counted right**: a pass played into space for a runner counts as a ground pass, not a through ball (as
  StatsBomb counts it); crosses are delivered with the crosser's skill (`0.35 + 0.4 x crossing`), a little less accurately, and are
  weighed a little lower.
- Where it stands now: goals **2.45-2.80** a match (real 2.53), shots **30-32** (35.6; real 24.8), shots on target 11.7-12.2 (8.3),
  headers 15-18% of goals (14%), outside-box goals 7-11% (13%), foot goals hit first time 42-43% (55%). Still to do: shots from the
  six-yard box (4.3-4.6 a match, real 1.7) and with the keeper under 3 m away (14.5%, real 3.9%) - that is where the forwards stand
  (phase 3, off-ball positioning) - and rebounds (3-5% of goals, real 12%).

**Phase 3, first round - where the attackers stand** (100 matches):

- **Box roles when the cross is on**: each man attacks his own zone - the near post just outside the six-yard box, the far post,
  beyond the far post (the far winger), the cut-back zone at the edge of the box (the ball-side midfielder), and the **late run**:
  the other midfielder waits at the edge for the second ball and only arrives at the penalty spot when the carrier is near the
  by-line. A lone forward goes between the near post and the spot. Before, the forwards stood 6 m out, on the keeper.
- **Crosses go to those zones** (near post, spot, far post, beyond the far post - in front of the six-yard box, not into it); the
  near- and far-post corners land a metre further out, where the keeper cannot just come and take them.
- **A forward does not stand on the keeper**: in open play he waits at most 8 m from the line (a run in behind still goes further).
- **Space**: attackers in the last third keep 4.5 m from each other - two men in one spot are marked by one defender.
- **Following the shot in**: more of the attackers read the rebound (30-80% by off the ball, was 20-60%).
- New counters to find out where the six-yard shots come from (`shot.six.<assist>.<how>`, `shot.after.<restart>.<zone>`): a third
  of them come from corners, a quarter are headers from open-play crosses, a fifth are a player carrying it into the six-yard box.
- Honest result: the shape changed and headers came down to **14.5%** of goals (real 14.3%), but the totals barely moved - shots
  32 a match (real 24.8), from the six-yard box 4.2 (1.7), with the keeper under 3 m 15% of foot shots (3.9%), rebounds 3.6% of
  goals (12%). Next: corners (5.2 shots a match within 7 s of one - real about 4 in the whole possession), the carry into the six-yard
  box, and the second ball after a save.

**Phase 3, second round - not on to the keeper** (200 matches, two seeds):

- New counters (`shot.k3.*`) showed what the shots with the keeper under 3 m away were: two in three against a keeper standing
  still on his line - the forward had carried it right up to him (from the side of the six-yard box, along the by-line), or took a
  touch on to him after a pass.
- **A carry that ends beside the keeper** (within 5 m of where he will be, and nearer to him than now) is weighed at an eighth to a
  quarter of its worth (a good dribbler goes round him a little more often): the man that close shoots or squares it now.
- Foot shots with the keeper under 3 m away: **15% -> 3.5%** (real 3.9%); shots from the six-yard box 4.2 -> **2.2-2.4** a match
  (real 1.7); goals 2.17-2.76 a match (real 2.53); foot goals hit first time 47-53% (real 55%).

**Two tactics made clearer** (they had slipped under their tests with the new attack): parking the bus keeps the back four home
even with the ball (no full-back overlapping - the bunker is kept for when the ball is lost); and with a man in front of him the
individual instruction "dribble more / less" counts double (that is a take-on). The default teams play as before (the same seed gives
the same 100 matches: 2.92 goals, 30.4 shots).

**Phase 5 - the test**: `AttackCalibrationTests` holds the attack against the StatsBomb targets over 24 matches - goals 1.6-3.6, shots
16-40, six-yard shots under 3.5, outside-box shots over 22% of the shots, foot shots with the keeper under 3 m away under 8%, foot goals
hit first time over 30%, and 5-45% of decisions going to a near-equal option.

**Phase 4 - natural variation** (200 matches, two seeds):

- **His own leanings**: every player is a little more of a shooter, a dribbler, a passer or a crosser than the next man with the
  same numbers (x0.88-1.12 on what that option is worth to him, fixed for the match). It is taken from his own make-up - the
  phases that already give his running its wobble - so it uses no random draw and a replayed match stays the same.
- **A good decision is not always the same decision**: among the options that are almost as good as the best he chooses with a
  soft-max - a clear-headed player nearly always the best, a poor decision-maker more loosely, a creative one or a showboat a
  little more freely. Options that are clearly worse are never chosen this way (only by misreading them, as before). About one
  decision in four now goes to a near-equal option (most are which way to carry it).
- Nothing got worse: goals **2.52** a match over the 200 (real 2.53), pass completion 78.5-78.8% (was 79.2%), foot goals hit first
  time **50-52%** (real 55%), foot shots with the keeper under 3 m away **3.9-4.5%** (real 3.9%).

## `fix/card-during-play` - cards, goals, the walk off

- **Fix: a card was shown while the play went on.** In 40 matches, 34 of 114 cards (30%) were shown with the ball in play - and a red
  card sent the player off the pitch in the middle of an attack. Two causes:
  - A foul played on with advantage: the card was given 2.5 seconds later "without stopping play" (the code said it approximated the
    next stoppage). Now the card waits for the next real stoppage - the ball out, a foul, a goal, the end of the half - and is shown
    then (before, a goal or the whistle in those 2.5 seconds could lose it altogether).
  - A booking for simulation (the diver): the card was given with the play going on. Now the referee stops the play, books the
    diver with the play stopped, and gives an indirect free kick to the other side (Law 12).
    Over 40 matches: 118 cards, none shown while the play went on. Test: `CardTimingTests`.
- **Goals that look like football** (researched: in the Premier League 2024-25 11.5% of the goals came from outside the box, the
  lowest share on record; in the World Cup 2018 about six in ten from open play; headers 13-19%). Measured over 100 matches with a
  new goal breakdown (zone, finish, the pass before it, a rebound, how far the keeper was):
  - **The open goal was far too generous**: a keeper more than 2.4 m off the line of the shot counted as beaten - a third of them
    were standing in their own goal - and the shot was then rolled in and saved only a quarter as often: 0.91 "open-goal" goals a
    match, 36% of all goals. Now the goal is open only if the keeper cannot get to the line of the shot (with his dive) before the
    ball does, or he is down: 0.17-0.20 a match.
  - **Shots from range**: the further out a shot is struck, the likelier the save (up to +12%): goals from outside the box 18% ->
    7-9%.
  - **The keepers saved too much once the free goals were gone** (87% of the shots on target, against about 70% in the top leagues):
    the save is a little less likely across the board. Goals a match 2.77-2.87 over two samples of 100 (the real range 2.7-3.0),
    headers 18.8%.
  - Still to do: the shots are too many (35 a match against 26) and the saves with them (11.5 against 5-6).
- **The walk off**: at half time the players and the officials walk off to the tunnel before the break, and for the second half they
  come back out of it and jog to their places. At full time the match does not just stop: the winners celebrate together in the
  middle, go to the far side to applaud their fans and then walk off; the losers trudge off with their heads down; after a draw both
  walk off. (Only what the viewer sees; the clock has stopped.)
- **The keeper's gloves are gone for good**: the dive is the stretched disc alone (no arms with round ends), and the keeper's
  gestures have no "hands" on them (they read as gloves).

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
  - _Momentum after a goal_: the test measured the level afterwards, which depends on where the team started; it now
    measures the swing the goal causes.
  - _Cut-in curlers_: the traits and the finishing rule had halved them (21 -> 7 per 12 matches). The winger who is cutting inside
    now keeps running at the full-back (the finishing rule leaves his dribble alone) and shoots a little more readily; about 15
    curlers in 24 matches (0.6 a match); the test counts 24 matches.
  - _The diver_: 16 matches instead of 6 (a rare event).
- **Reorganisation, no change in behaviour - proved, not assumed.**
  - _A fingerprint tool_: `SimRunner --golden` plays 20 fixed matches (leagues, the staged match as the page runs it, rain,
    snow and wind, night, cup and league cup with shoot-outs, random plans, computer managers) through the replay recorder and
    hashes every frame, event and statistics row. The fingerprint is `21195E340097B26F` before the cleanup, after it and after
    each step below (the cut-in change above is a change of behaviour, made after them: the fingerprint from then on is `5662DC3522CDEFF8`).
  - _The engine_: the four largest files, 1100-1800 lines each, split by topic into files of 130-620 lines (moves of whole
    methods with their comments, nothing rewritten): `OnBall` (decision, `.Passing`, `.Dribbling`, `.Shooting`), `TeamAI`
    (shape, `.Marking`, `.Pressing`, `.Players`, `.Movement`), `SetPieces` (start, `.Layout`, `.Execution`), `Contacts`
    (control and headers, `.Goalkeeper`, `.Tackles`). The map is in TECHNICAL_OVERVIEW.
  - _The demo page_: the 3300-line `match-template.html` is `template.html` (the markup) plus `css/NN-topic.css` (10 files) and
    `js/NN-topic.js` (25 files, one per topic); the build puts them back together at the `@include` lines, and the assembled
    page is identical to the old template byte for byte.
  - TECHNICAL_OVERVIEW brought up to date (the frame is 157 numbers, the file map, the fingerprint, the page's parts).

## `feature/online-play`

- **Play a friend online** (user mode). After the kits: _Play the computer_ or _Invite a friend_. The host gets an invite
  link and sends it (WhatsApp, mail...); the friend opens it, picks his own kit and keeper (nothing that looks like the
  host's kit or keeper is offered) and gets a reply code to send back; the host pastes it and the two are connected. Both
  pick their eleven, formation and instructions; the match kicks off when both have pressed _Start match_.
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

- **Twelve more traits** (Traits tab): _attacks every high ball_, _holds his position_, _roams_, _runs in behind_, _presses
  relentlessly_, _saves his energy_, _leader_, _big-game player_, _fragile_, _draws fouls_, and for keepers _bold_ (off his
  line, claims crosses) and _stays on his line_. Never two that contradict (holds position / roams or runs in behind,
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
  1. _Cuts inside and shoots_ - from the flank he always comes inside, and shoots more after the cut.
  2. _Loves to dribble_ - runs with the ball far more often.
  3. _Sets up his team-mates_ - fewer shots, looks for the better-placed man.
  4. _Outside of the foot_ - the trivela instead of the weaker foot.
  5. _Uses his weaker foot_ - does not shift onto the strong foot, and his weaker one is a grade better.
  6. _Likes tricks_ - backheels, dummies, fake shots and backheel finishes more often.
  7. _Goes in hard_ - only for an aggressive player (70+; the trait gives him it): more tackles and slides, more fouls.
  8. _Shoots from distance_ - has a go from range.
  9. _Switches the play_ - values the long ball to the other flank.
  10. _Back to goal_ - holds it up, stronger in the body, lays it off to the runners.
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
  - **Defence**: _Balanced_; _High press_ (wins the ball high, a high line and hard pressing, but tires the legs ~40%
    faster); _Offside trap_ (the whole line takes a step up together to leave the forwards offside - 5 caught offside a
    match instead of 1 - and now and then one man misses the step, so the trap fails); _Park the bus_ (a deep bunker, narrow, one
    forward left up, wasting time - the back line 19 m from goal instead of 33, and the legs stay fresh).
  - **Attack**: _Mixed_; _Crosses into the box_ (the wide men deliver from everywhere wide, more men in the box - 17
    crosses a match instead of 9); _Runs in behind_ (through balls and runs, 20 through balls instead of 11); _Long
    shots_ (from 20-30 m, 19 shots from 18 m+ instead of 8).
  - **Extra tactic**: _Possession_ (short passing, no gambles, 4.7 long balls instead of 9); _Time wasting_ (restarts
    taken 1.9x slower, the keeper and the carrier take their time, the clock runs); _Counter-attacks_ (win it in your own
    half and break at once, everybody forward, through balls); _Relentless pressure_ (everybody up round the box,
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

  | View                      | Demo page  | React |
  | ------------------------- | ---------- | ----- |
  | Sunny day                 | 24 → 46    | 60    |
  | Night                     | 44 → 56-60 | 59    |
  | Snow                      | 20 → 43    | 46    |
  | Rain on a neglected pitch | 30 → 33-42 | –     |

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

  | Measure      | Result           |
  | ------------ | ---------------- |
  | Goals        | 3.34 (2.97)      |
  | Shots        | 33.9 (25.9)      |
  | Yellow cards | 4.2 (4.2)        |
  | Red cards    | 0.15 (0.10-0.15) |
  | Offsides     | 3.2 (3.4)        |
  | Added time   | 3.4 + 4.0 min    |

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
  - _Shielding the ball out_: a ball rolling out of play for our restart (goal kick, throw-in), with an opponent
    chasing it, is not played. The defender who gets there first puts his body between the ball and the chaser and
    escorts it out.
    - The chaser is held off according to physical strength.
    - Commentary: "... shields it out from ... for a goal kick".
  - _Shoulder to shoulder_: two opponents racing side by side for a loose ball lean into each other. The stronger,
    taller and more determined one holds his line and gets there first; the other is knocked off stride.
    - Rarely, it's a careless push (a foul).
    - Commentary: "... muscles ... off the ball".
- **Pressure, cover, balance** (diagonal defending): when the ball carrier is pressed in or near our half, the nearest
  team-mate takes up a position about 6 m diagonally behind the presser, on the inside (cover). The far-side full-back
  tucks in so there is no gap through the middle (balance).
- **A disciplined back line**:
  - Each team has a _defensive chemistry_ score, built from its defenders' Teamwork, Positioning and Decisions.
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

  | Measure                                        | Result                |
  | ---------------------------------------------- | --------------------- |
  | Goals                                          | 3.27 (2.97)           |
  | Shots                                          | 31.6 (25.9)           |
  | Offsides                                       | 3.8 (3.4)             |
  | Pass completion                                | 80% (80-85%)          |
  | Yellow cards                                   | 4.2 (4.2)             |
  | Through balls                                  | 28, of which 9 arrive |
  | Passes into space                              | 17, about half arrive |
  | Interceptions where the defender read the pass | 20                    |
  | Balls shielded out                             | 3                     |
  | Shoulder duels won                             | 28                    |
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
  - _Zonal_: each defender and midfielder owns an area around his place in the block (about 9 m for defenders, 10.5 m
    for midfielders), which moves with the ball. He picks up the most dangerous opponent who enters it and stays
    goal-side, but never leaves his zone. When the runner moves on, he is handed over to the next zone.
    `MarkingSwitches` counts the hand-overs.
  - _Man-to-man_: when the ball is lost, the four defenders take the four most advanced opponents and the midfielders
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

  | Measure           | This version | Real world | Before |
  | ----------------- | ------------ | ---------- | ------ |
  | Goals             | 3.01         | 2.97       |        |
  | Fouls             | 21.9         | 22.1       |        |
  | Yellow cards      | about 4.4    | 4.2        |        |
  | Red cards         | 0.16         | 0.10–0.15  |        |
  | Tackle attempts   | 106          |            | 108    |
  | – of which slides | about 10     |            |        |

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

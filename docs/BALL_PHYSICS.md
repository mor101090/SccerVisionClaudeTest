# Ball physics

The ball model of the match engine: what it is, where every number comes from, and how sure we are. Every parameter in one table:
[`BALL_PARAMETERS.md`](BALL_PARAMETERS.md); the test bench's results: [`BALL_TEST_BENCH.md`](BALL_TEST_BENCH.md). Built in stages (one branch and
PR each, the full engine suite before each merge). Units are SI throughout: kg, m, s, N, rad, Pa.

**Classes** used for every parameter:

| | Meaning |
|---|---|
| **A** | Officially standardised (IFAB Laws of the Game, SI definitions) |
| **B** | Experimentally measured, with a source that was read |
| **C** | Mathematically derived from A/B/D values |
| **D** | Approximation or simulation assumption (a chosen value, no measurement behind it) |
| **E** | Unknown, needs calibration |

A number read only through a search result or an abstract (the full text was not available) is marked **secondary**, never B.

## Sources and what was actually read

| Source | What was read | Used for |
|---|---|---|
| Iga, Nunome, Inoue, Ikegami, [Basic mechanical analysis of soccer ball impact](https://ojs.ub.uni-konstanz.de/cpa/article/view/5637/5130), ISBS conference proceedings (Nagoya University) | **The text itself** (the PDF, its Table 1). A size 5 match ball (Molten Pelada 405, 22 cm, 426 g, 0.9 bar) fired at a Kistler force plate on a steel pedestal, filmed at 5000 frames a second, five speeds x five trials: in 9.05 / 11.36 / 13.68 / 16.27 / 19.46 m/s, out 7.21 / 8.90 / 10.56 / 12.25 / 14.40 m/s; contact 8.5 / 8.3 / 7.9 / 7.7 / 7.4 ms; peak flattening 2.45 / 3.13 / 3.57 / 4.03 / 4.64 cm at 3.4-3.7 ms | Stage 9: the flattening, the contact time and the frame's restitution slope (B) |
| Tadrist, Darbois Texier, [Open questions on the impact of an inflated ball](https://arxiv.org/abs/1708.01282), arXiv 1708.01282, 2017 | **The text itself** (the PDF). Inflated balls filmed at 4000 frames a second on marble: the ball stays a spherical cap, "no increase in radius"; the gas pushes back with the pressure on the flat area, so F = 2 pi R P x (a linear spring) and the contact time is pi sqrt(m / 2 pi R P), independent of the speed for small flattening; a real ball stiffens at large flattening, so the contact shortens with the speed | Stage 9: the gas-spring model (C from A) and the flat-capped drawing |
| [IFAB Law 2, The Ball](https://www.theifab.com/laws/latest/the-ball/) | The page itself: circumference 68-70 cm, 410-450 g "in weight at the start of the match", pressure "0.6-1.1 atmosphere (600-1,100 g/cm²) at sea level" | Class A limits in `BallSpec` |
| Goff and Carré, [Trajectory analysis of a soccer ball](https://doi.org/10.1119/1.3197187), *Am. J. Phys.* 77(11):1020-1027, 2009 | **The full text** (a PDF copy, read page by page, figures included). Equations of motion, Re = vD/ν with ν = 1.54e-5 m²/s and D = 0.218 m, mass 0.424 kg, A = 0.0375 m², ρ = 1.2 kg/m³; Fig. 6 (wind-tunnel Cd(Re) of two series), Fig. 7 (Cd(Sp)), Fig. 8 (CL(Sp), wind tunnel and their own trajectory points), their measured no-spin range of 21.9 m at 18 m/s and 22° (Cd 0.2) and 0.17 at 19.4 m/s, the spin decay estimate, the Beckham estimate | **Class B**: the source of the flight tables. Values read from the figures by eye (±0.03 on Cd, ±0.04 on CL) |
| FIFA, [Test Manual I](https://www.sporteimpianti.it/wp-content/uploads/2024/04/FIFA-TTM-I-Layout-v8.pdf) (football turf, April 2024) | **The text itself.** Test balls are inflated to rebound 1.45 ± 0.03 m on concrete from 2.0 m. Method 2024-01: vertical rebound of a ball dropped from 2.00 m (underside). Method 2024-02: ball roll from a 1 m, 45° ramp. Method 2024-14: angle ball rebound = speed after / before for a ball fired at 50 ± 2 km/h and 30 ± 2°, with under 3 rev/s of spin | **Class A** test methods; stage 4 (and 5) |
| FIFA, [Natural-Pitch Rating System](https://digitalhub.fifa.com/m/58aa765dd3e85f26/original/FIFA-natural-pitch-rating-system_EN.pdf) (May 2022) | **The text itself.** "Excellent" natural pitch: vertical ball rebound 0.60-1.00 m (satisfactory 0.50-0.59 or 1.01-1.10), ball roll 4.0-10.0 m | **Class A** ranges; stage 4 (and 5) |
| The English FA, conditions of use for artificial pitches (through a search, secondary) | FIFA Quality turf: vertical rebound 0.60-1.00 m (Quality Pro 0.60-0.85 m), angle ball rebound 45-70% dry, 45-80% wet, ball roll 4-10 m | Secondary; stage 4 |
| J. Thomas, [Soccer Ball Interactions on Wet and Dry Turfgrasses at Various Mowing Heights](https://trace.tennessee.edu/utk_gradthes/11814), MSc thesis, University of Tennessee, 2024 | **The abstract only** (the full text is embargoed until 2027): angle ball rebound 56% (Kentucky bluegrass) and 69% (bermudagrass) dry, 74-76% lightly irrigated (63% for one); vertical rebound changed by 7 cm or less dry v wet and with mowing height; lower mowing gives a longer roll | Class B (abstract); stage 4 |
| M. Gabrielsen, [Ball Roll](https://www.isss-sportsurfacescience.org/downloads/documents/A7ZD60AH55_Gabrielsen_Ball_Roll.pdf.pdf), Norwegian Building Research Institute, ISSS Technical Meeting 2004 | **The text itself.** The 1 m ramp delivers the ball at 3.2 m/s; the UEFA / NBI study (2003) measured six turf systems and three natural pitches with timing gates 2 m apart starting 0.5 m from the ramp; the roll distance trend BRD = 4.3404 × VC^-0.9789 (R² 0.87); the IST / Kolitzus method fits dec = a + b v ("the deceleration decreases the slower the ball roll") | **Class B**: the source of the rolling model |
| J. Dunlop, [comment on the NBI report](https://www.isss-sportsurfacescience.org/filemanager_net/files/pdf/comment_on_ball_roll.pdf) (ISSS, 2004) | **The text itself.** For the natural pitches of the study the velocity change was 0.55-0.87 m/s, mean 0.68 (95% CI 0.62-0.74); "heavy slow northern European grasses"; "rarely in soccer play does the ball roll to a stop" | Class B; stage 5 |
| A British study of pitch testing (1985 standards), through a search | 90% of natural-pitch deceleration values 0.45-1.5 m/s² (the source the engine's old comment quoted without a reference) | Secondary; stage 5 |
| Asai, Kimachi, Liu, Koido, Nakayama, Goff, Hong, [Measurements of the Flight Trajectory of a Spinning Soccer Ball and the Magnus Force Acting on It](https://www.mdpi.com/2504-3900/49/1/88), *Proceedings* 49(1):88, 2020 | **Not the full text** (the publisher refuses automated downloads). Title and authors from Crossref; from the abstract through a search: time-averaged side-force coefficients 0.21, 0.24, 0.29 at time-averaged spin parameters 0.18, 0.22, 0.34 (spin parameter Sp = Rω/U), vertical spin axes, the side force falls as the ball slows | Secondary: the audit below; stage 3 |
| Goff, Hong, Asai, [Influence of Surface Properties on Soccer Ball Trajectories](https://www.mdpi.com/2504-3900/49/1/143), *Proceedings* 49(1):143, 2020 | **Not the full text.** From search snippets: the drag coefficient depends on speed and spin; the drag crisis (a sharp fall of Cd at a critical Reynolds number) differs between balls (Jabulani, Brazuca, a smooth ball of the same size; the smooth ball has the highest critical Reynolds number); panel texture, seam width and depth matter | Secondary, qualitative only: stage 3 |
| Paudel, Yan, [High-fidelity simulations of full-flight soccer ball aerodynamics using a monolithic overset approach](https://link.springer.com/article/10.1007/s00466-025-02705-2), *Computational Mechanics*, 2025 | The abstract (Crossref): fully coupled CFD of a spinning free kick; spin rate drives the lateral deviation, kick speed the range and flight time; a smoother ball shows a stronger Magnus effect; compared with an ODE model using empirical drag coefficients | Qualitative: why a coefficient model (not CFD) is the right tool in a game; no numbers taken |
| Asai et al. 2004, the infront curve kick (Sheffield Hallam repository, [record](https://shura.shu.ac.uk/2206/)) | **Not the full text** (the repository is behind a bot wall); through a search: foot 24.2 m/s, ball 27.1 m/s, 7.8 rev/s | Class B (secondary): stage 7 |
| Hong et al. 2011, through a later *Scientific Reports* article ([srep06067](https://link.springer.com/content/pdf/10.1038/srep06067.pdf); authors and title not checked) | **A search summary**: curve shots 26.0 m/s with 7.2 rev/s about the vertical; curve kicks 4-10 rev/s, knuckleballs 0-2 rev/s | Class B (secondary): stage 7 |
| Asai et al. 2002, [The curve kick of a football I: impact with the foot](https://shura.shu.ac.uk/2127) | **The record only**: a trade-off between ball speed and spin as the foot strikes further off centre | Qualitative: stage 7 |
| Lees and Nolan 1998 (*J. Sports Sci.*), through later papers; an [ISBS 2008 paper](https://ojs.ub.uni-konstanz.de/cpa/article/view/1999/1867) | **Not the review itself**: ball speed / foot speed is the index of a good strike, 1.06-1.65 in the literature by where the foot is measured; the ISBS study: about 1.40 for instep and side-foot alike | Class B (secondary): the ratio 1.3 of stage 7 |
| A 2025 *College Physics* paper on a three-line pendulum for sports balls ([abstract](https://dxwl.bnu.edu.cn/EN/10.16854/j.cnki.1000-0712.240047)) | Only that it measured footballs' moments of inertia and compared them with the thin-shell value; **the numbers were not seen** | Why the inertia factor stays class E |

## Stage 0 - the model before this work (audit)

`Engine/BallPhysics.cs` (one integrator shared by the live match and every AI prediction: `KickSolver`, `GroundRollTable`,
`BallTrajectory`, so a plan always agrees with what the ball does), constants in `Core/MatchConfig.cs`. Semi-implicit Euler at a
fixed 0.02 s step; the ball moves in real units in sim seconds (the clock is compressed separately: `MotionSpeed`).

| Parameter | Value | What it is | Class |
|---|---|---|---|
| `Pitch.BallRadius` | 0.11 m | Geometric radius for ground and post contact | C (≈ 0.690 / 2π = 0.1098; within 0.2 mm) |
| `Gravity` | 9.81 m/s² | | C (standard 9.80665; local g 9.78-9.83) |
| `AirDragK` | 0.0133 1/m | ½ ρ Cd A / m with ρ = 1.2 kg/m³, Cd = 0.25, R = 0.11 m, m = 0.43 kg | C from **Cd = 0.25, D**: one constant for every speed, so no drag crisis and no spin dependence |
| `MagnusK` | 0.0042 | Lateral acceleration = k · spin · speed (spin in rad/s) | **D, no source**. It implies a side-force coefficient Cs ≈ 0.72 · Sp (2 m k / (ρ A R)); the 2020 measurements (secondary) give Cs / Sp ≈ 0.85-1.17, so the engine's curl is roughly 60-85% of the measured one |
| `SideSpin`, `TopSpin` | 2 scalars, rad/s | Spin about the vertical axis and a "dip" term | D: not a 3D angular velocity; no backspin lift; topspin is zeroed while rolling |
| `SpinDecayPerSecond` | 0.25 1/s | Exponential spin decay in flight | D |
| `GroundRestitution` | 0.55 | Vertical speed kept on a bounce | D, no source; one value for every speed and angle |
| `BounceFriction` | 0.86 | Horizontal speed kept on a bounce | D; no spin coupling (a bounce does not change or use the spin) |
| Bounce threshold | 1.1 m/s | Below it the ball starts to roll | D |
| `RollingDecel` | 0.9 m/s² | Constant rolling resistance (μr ≈ 0.092) | D: the comment's "natural turf roll tests 0.45-1.5 m/s²" has no source in the repository |
| `GrassLinearDrag` | 0.14 1/s | Speed-proportional grass drag | D |
| `PostRestitution`, `NetRestitution` | 0.7, 0.12 | Rebound off the woodwork and the net | D |
| Weather multipliers | `ApplyConditions`, `PitchSurface` | Wet, snow, mud, puddles | D |

The engine's match statistics (goals, shots, passing, set pieces) were calibrated on this model. **Every later stage that changes the
ball changes them**, so each one is re-measured against the real figures (`RealWorldPinTests`, `SimRunner`) before it is merged.

## Stage 1 - the ball (done)

`Core/BallSpec.cs`, `MatchConfig.Ball`. Test: `BallSpecTests` (TEST 1).

| Parameter | Reference value | Class |
|---|---|---|
| Mass | 0.430 kg (Law: 0.410-0.450) | D inside an A range |
| Circumference | 0.690 m (Law: 0.680-0.700) | D inside an A range |
| Equivalent radius R = C / 2π | 0.1098 m | C |
| Projected area A = π R² | 0.03789 m² | C |
| Inflation pressure | 0.85 atm = 86.1 kPa **gauge** (above the surrounding air) | D inside an A range |
| Law's pressure range | 58.8-111.5 kPa gauge (the Law's "0.6 atmosphere" and "600 g/cm²" differ by 3%; the wider reading is kept) | A |
| Absolute pressure inside | gauge + 101,325 Pa = 187.5 kPa | C |

A ball outside Law 2 is allowed (for experiments) and reported by `LawTwoViolations()`; a non-physical one (mass ≤ 0, a negative
pressure) is refused.

## Stage 2 - moment of inertia (done)

I = k m R², k configurable between a solid sphere (2/5) and a thin shell (2/3). A real ball (cover, panels, lining layers, bladder, air)
has most of its mass near the surface, so k is close to but a little under 2/3. **No published measurement of a size 5 ball was read**,
so the reference uses k = 2/3 (I = 0.003457 kg·m², class D) and k stays class E until a measurement is found. Angular velocity and
angular momentum are 3D vectors (`Vec3`): L = I ω (parallel, a sphere is isotropic), E = ½ I ω², Δω = τ dt / I. A 30 m/s shot with
10 rev/s of spin carries about 3.5% of its energy in the spin.

The engine's ball still integrates the two scalar spins of stage 0; the 3D angular velocity replaces them in stage 3 (flight) and
stage 6 (spin), where it starts to change the ball's path.

## Stage 3 - flight (done)

`Core/AeroModel.cs`, `MatchConfig.Aero` / `AirDensity` / `AirDynamicViscosity`, `BallPhysics.FlightAcceleration`. Tests: `BallFlightTests` (TESTS 5-8 and the
checks around them). `MatchConfig.Aero = null` (`SimRunner --legacyflight`) is the model of stage 0, bit for bit, kept for comparison.

**Forces** (v: velocity relative to the air, so wind works as before): gravity m g; drag ½ ρ Cd(Re, Sp) A v² against v; Magnus ½ ρ Cl(Sp) A v² along
(ω × v̂) / |ω × v̂|. That direction is perpendicular to both the spin and the flight: backspin lifts, topspin dips, sidespin curves, and spin about the line of
flight (ω ∥ v) does nothing. Re = ρ v D / μ, Sp = R |ω⊥| / v (ω⊥: the spin perpendicular to the flight). The angular velocity is a 3D world vector; the kicks still
set side spin and top spin (relative to the direction they fly in) and the first step turns them into the vector once.

| Parameter | Value | Class and source |
|---|---|---|
| Air density | 1.2 kg/m³ | D (Goff and Carré use 1.2; the standard atmosphere at 15 °C is 1.225) |
| Air dynamic viscosity | 1.85e-5 Pa·s | B (Goff and Carré's ν = 1.54e-5 m²/s × 1.2) |
| Cd0(Re) | 0.46 below Re 1.2e5, the crisis between 1.2e5 and 1.8e5 (v 8.5-13 m/s), 0.19 after it (0.18 at 3e5, 0.20 at 5e5) | **B/D**: the two wind-tunnel series of Goff and Carré Fig. 6 read by eye (Asai et al. 2007: 0.46 to Re 1.5e5, down to 0.14-0.15 at 2.3-3e5, 0.19 at 4.5e5; Carré et al. 2005 scale ball: crisis at 0.9-1.3e5, then 0.20-0.25); the profile is chosen **between** them. Their own trajectory fits: 0.2 (Re 2.6e5) and 0.17 (2.2-2.8e5). Balls differ by ±0.05 |
| Drag added by spin | +0.45 per unit of Sp, at most +0.13 | D: a line through Fig. 7 (Cd 0.16-0.18 at Sp 0.05, 0.29 at 0.31); capped because beyond Sp 0.31 there is no measurement (the paper's trajectory fits use 0.27-0.30 up to Sp 0.71) |
| Cl(Sp) | 0 / 0.09 / 0.17 / 0.23 / 0.26 / 0.29 / 0.30 / 0.31 at Sp 0 / 0.05 / 0.10 / 0.15 / 0.20 / 0.30 / 0.50 / 0.71 | **B**: mean of the wind-tunnel points of Fig. 8 (Sp up to 0.3) and the paper's trajectory estimates (0.27-0.30 at Sp 0.24-0.25, 0.29 at 0.49, 0.31 at 0.71; ±30%); ±0.04. It flattens near 0.3. The same function is used for the side force (the paper argues both come from the same physics) |
| Spin decay | exp(-0.08 t) | B, secondary: 4-8% lost by the apex, about 10% by landing (Goff and Carré, citing James and Haake 2008); the engine had 0.25 |
| Knuckleball (no spin, seam-driven side force) | **not modelled** | **E**: the paper's model "ignores knuckle-ball effects due to seam orientations"; no coefficient model was found (the CFD paper's abstract gives none). TEST 9 is therefore not built; `Ball.ShotKnuckle` stays a flag that only makes the keeper's job harder |

**Checked against measurements** (what a coefficient cannot fake):
- Re = ρ v D / μ reproduces Goff and Carré's Eq. 2 (Re × 1e-5 = v / 7 m/s) within 3%.
- A no-spin ball launched at 18 m/s and 22° from 0.85 m lands 21.1 m away; they measured 21.9 m (their launch values are quoted as approximate and integrating
  their own stated numbers with Cd = 0.2 gives 21.4 m: the check is ±1 m, not a fit).
- A falling ball reaches about 31 m/s (terminal velocity sqrt(2 m g / ρ A Cd)).
- The Magnus force does no work (energy conserved with the drag off, about any spin axis); drag only loses energy; sidespin to the left and to the right are mirror images;
  a Beckham-like kick (36 m/s, 63 rad/s, Sp 0.19) is 2.5-6.5 m off line after 27 m (the paper's back-of-the-envelope estimate: about 3 m with a coefficient of 0.2; the wind-tunnel value at Sp 0.19 is 0.25).

**What it does to the engine's ball**: at low spin the engine's old Magnus constant implied Cl ≈ 0.72 × Sp, the measured table about 1.8 × Sp (Sp 0.05), i.e. 2.5 times the curl for the
same spin, and the old drag constant (Cd 0.25) was too high at shot speeds (Cd 0.18-0.19 at 20-30 m/s) and far too low below 10 m/s (0.46).

**The solver change that goes with it**: the engine's kicks aimed off by a hand-tuned angle to let a curl bring the ball back (shots 0.04 + 0.02 × spin/10 rad, free kicks 0.06 rad, crosses
and corners not at all). With 2.5 times the curl those angles missed the target by metres. A kicker now aims by solving the same integrator the flight uses
(`KickSolver.CurlYaw`, `CurlYawToReach`, `LoftSpeedWithSpin`): the start line and the speed for the curved path. Without this, the same physics gave goals 2.27 (from 2.61),
shots 21.6 (24.0) and headed shots 1.5 (3.7) a match, because the crosses missed their men.

**Match statistics, old flight v physical flight** (`SimRunner`, 200 matches, seed 700, same teams):

| | old flight | physical flight | real |
|---|---|---|---|
| Goals | 2.61 | 2.75 | 2.83 |
| Shots | 24.0 | 25.1 | 25.4 |
| On target | 7.77 | 7.97 | 8.8 |
| Headed shots | 3.67 | 4.29 | - |
| Headed goals, % of goals | 15.9 | 18.4 | 13-19 |
| Crosses (all) | 31.2 | 30.6 | - |
| Passes | 441.7 | 441.3 | - |
| Pass completion % | 79.3 | 79.4 | 80-85 |

## Stage 4 - the bounce (done)

`Core/GroundModel.cs`, `MatchConfig.Ground` / `ArtificialTurf`, `BallPhysics.Bounce`. Tests: `BallBounceTests` (TESTS 2, 3, 13 and the ground side of 6-7).
`MatchConfig.Ground = null` (`SimRunner --legacybounce`) is the old bounce, bit for bit.

**Model**: an impulse at the contact point. Normal: v_n' = e(v_n) v_n. Tangential: the slip of the bottom of the ball u = v_t + ω × r (r = -R ẑ); friction
acts against it with at most μ (1 + e) m v_n; the impulse that stops the slip is m |u| k / (1 + k) (k from I = k m R²). If friction can give that much, the ball
grips and leaves rolling; otherwise it slides through. The same impulse changes the spin by (r × J) / I. So a ball with no spin landing at an angle leaves with
topspin, backspin checks it (less speed along the ground), topspin kicks it on, and spin about the vertical axis is untouched. A bounce never adds energy (checked
over 200 random impacts). e = sqrt(h_rebound / h_drop) for the drop test.

| Parameter | Value | Class and source |
|---|---|---|
| Drop-test impact speed | 6.26 m/s (sqrt(2 g × 2.00 m)) | C from the FIFA method |
| e, natural grass | 0.632 at 6.26 m/s: a 0.80 m rebound, the middle of FIFA's "excellent" 0.60-1.00 m | D inside an A range |
| e, football turf | 0.602: 0.725 m, the middle of FIFA Quality Pro 0.60-0.85 m | D inside an A range |
| e by impact speed | -0.01 per m/s above 6.26 m/s (+0.01 below), clamped 0.2-0.95 | **E**: no measurement for a football found; the slope is borrowed from pressurised tennis balls on a rigid plate (0.8 at 3 m/s to 0.6 at 20 m/s, Dignall 2005) |
| e, wet | the same as dry | B: Thomas 2024 found 7 cm or less of difference |
| μ, dry (grass and turf) | 0.6 | D: anything above about 0.45 grips in the FIFA angle test; the result (about 60%) is inside the dry range and between the thesis' 56% and 69% |
| μ, natural wet | 0.22 | C: reproduces the thesis' 74-76% when lightly irrigated |
| μ, turf wet | 0.25 | D: inside FIFA's wet range (45-80%) |
| Snow | e × 0.55 (0.3 / 0.55, the old ratio) | D, as before |
| Mud, puddles (local) | e × (1 - 0.45 mud - 0.75 water), μ × (1 - 0.15 mud - 0.35 water) | D, as before |
| Below 1.1 m/s of normal impact the ball stops bouncing and rolls | | D, as before |
| Ball pressure | not in the bounce yet | E: the FIFA ball is set to 1.45 m on concrete (e 0.85); a law for e(pressure) was not found |

**Checked**: the FIFA drop test gives a rebound inside the excellent range on grass and inside Quality Pro on turf; the higher the drop, the smaller the fraction
that comes back; the FIFA angle test gives 50-70% dry and 70-80% wet (the thesis: 56-69% and 74-76%); a dry bounce grips (the contact point stops slipping);
repeated bounces lose energy every time and the ball is rolling after 3-10 bounces and under 5 s.

**Match statistics, old bounce v new bounce** (both with the physical flight; `SimRunner`, 200 matches a seed, medium v medium; the old bounce reproduces stage 3 to the digit):

| | seed 700 old / new | seed 900 old / new | mean old -> new | real |
|---|---|---|---|---|
| Goals | 2.75 / 2.90 | 3.00 / 2.94 | 2.88 -> 2.92 | 2.83 |
| Shots | 25.1 / 26.7 | 25.9 / 25.4 | 25.5 -> 26.1 | 25.4 |
| On target | 7.97 / 8.86 | 8.65 / 8.36 | 8.31 -> 8.61 | 8.8 |
| Headed goals, % | 18.4 / 19.2 | 22.2 / 18.7 | 20.3 -> 19.0 | 13-19 |
| Passes, completion % | 441 / 449, 79.4 / 79.9 | 445 / 448, 79.6 / 79.8 | +5, 79.5 -> 79.8 | 80-85 |
| Volleys | 4.97 / 5.39 | 5.36 / 5.63 | 5.2 -> 5.5 | - |

## Stage 5 - the ball on the ground (done)

`GroundModel.RollA` / `RollB`, `BallPhysics.Roll` / `RollingDeceleration`, `MatchConfig.GrassLength`. Tests: `BallRollTests` (TEST 4, TEST 13, the slip-to-roll
transition). With `MatchConfig.Ground = null` the old ground model runs, bit for bit.

**Model**: a rolling ball has the spin of rolling (ω = ẑ × v / R) and slows by the surface's rolling resistance a + b v plus the air drag, which on a rolling ball
slows the spin too (1/(1+k) of the free value). A ball that lands still slipping at the contact (a bounce the friction could not finish, or a soft landing with
spin) slides: friction μ g works on the slip, changing speed and spin together, until the bottom of the ball stops slipping; the angular momentum about the
contact point is conserved meanwhile (checked: it ends at (v0 + k R ω0)/(1 + k), 3/5 of the speed for a thin shell with no spin). Ground kicks, touches and
deflections are still treated as rolling from the start (stage 7 did not change that). Spin about the vertical dies at 3/s as before (D). Surface irregularities
are not modelled (E): a random deflection would break the AI's predictions, which use the same integrator.

| Profile | a (m/s²) | b (1/s) | Roll from the 1 m ramp (3.2 m/s) | Class |
|---|---|---|---|---|
| Natural, dry (normal cut) | 0.408 | 0.167 | 6.4 m; velocity change over the 2 m gates 0.72 m/s | **C from B**: fits both Norwegian measurements at once (mean ΔV 0.68, 95% CI 0.62-0.74; the study's trend gives 6.4 m for it) |
| Natural, dry, short cut | 0.343 | 0.140 | 7.5 m | D (the thesis: a lower cut rolls further) |
| Natural, dry, long | 0.480 | 0.196 | 5.5 m | D |
| Natural, wet | 0.281 | 0.115 | 9.0 m | D (the old model's wet pitch rolled about 1.4 times as far) |
| Football turf, dry / wet | 0.320 / 0.264 | 0.131 / 0.108 | 8.0 / 9.5 m | D, inside FIFA Quality 4-10 m |
| Snow | 1.8 | 0.35 | | D, as before |
| Worn pitch | + 0.35 × (1 - maintenance) on a | | | D, as before |
| Old model | 0.9 | 0.14 (+ the old air drag) | 4.1 m | D: a heavy, slow pitch at FIFA's lower limit |

All the profiles keep the measured shape b / a and are inside FIFA's "excellent" 4.0-10.0 m. At 3 m/s the dry natural profile decelerates the ball 1.0 m/s²,
inside the British 0.45-1.5 m/s². The passers aim with a roll table built from the same deceleration (checked: arrival speed and time agree with the live ball).

**Match statistics, stage 4 v stage 5** (200 matches a seed, medium v medium; the old ground model reproduces stage 4 to the digit):

| | seed 700 | seed 900 | mean | real |
|---|---|---|---|---|
| Goals | 2.90 -> 3.07 | 2.94 -> 2.82 | 2.92 -> 2.95 | 2.83 |
| Shots | 26.7 -> 25.4 | 25.4 -> 25.5 | 26.1 -> 25.5 | 25.4 |
| On target | 8.86 -> 8.64 | 8.36 -> 8.50 | 8.61 -> 8.57 | 8.8 |
| Headed goals, % | 19.2 -> 17.5 | 18.7 -> 16.3 | 19.0 -> 16.9 | 13-19 |
| Passes won by the opponent | 70.0 -> 68.0 | 69.3 -> 66.7 | 69.6 -> 67.3 | - |
| Pass completion % | 79.9 -> 79.7 | 79.8 -> 79.7 | about 79.7 | 80-85 |

## Stage 6 - spin, shown as the physics has it (done)

The rotational physics itself came with stages 2-5: a 3D angular velocity (stage 2), the Magnus force from it and its decay in flight (stage 3), the friction impulse that
changes it at a bounce (stage 4), the slip that turns into rolling and the spin of rolling (stage 5). What stage 6 adds is that the viewers show it.

**Before**: the three viewers (the website, the classic page, Unity) turned the ball from its movement - rolling on the ground, "a gentler spin" in the air - so a
curler did not turn about the vertical, a chip with backspin turned forward, and a ball in the air spun whatever it was really doing. **Now**: every replay frame ends with the
free ball's angular velocity (world axes, radians per replay second × 10: the physical spin times the match pace) and the viewers turn the ball about that axis, in that
direction. While a player has the ball (or with the old ground model, which has no rolling spin) the frame carries 0, 0, 0 and the viewers fall back to the movement; so do
replays saved before this (their frames end earlier). A ball struck along the ground this very step shows the spin of rolling (the rolling model has not run on it yet).

**Kept as a choice**: the drawn rate is capped at 0.5 rad a drawn frame. A real shot spins at about 10 rev/s, which on a 60 Hz screen would strobe (the eye would see
a random or a backwards turn); the axis and the direction are the physics' own.

Changed together, as the engine contract requires: `ReplayRecorder` (frame length + 3), `app/src/engine/replay` (`replay.ts` reads `ball.w`, `ball3d.ts` turns by it,
`drawPitch.ts` passes it), the same files in `web/src/replay`, the classic page (`00-core.js`, `03-ball.js`) and Unity (`Ball3DTexture.Spin`, `BallView.Render`,
`MatchController`). The online guest view's frame swap copies the new numbers as they are (spin is not mirrored: the guest sees the same pitch).

Tests: `BallSpinReplayTests` (each frame ends with the free ball's physical spin; a held ball and the old ground model write 0, 0, 0; every rolling ball turns),
`app/src/engine/replay/replay.test.ts` (where the spin sits in the frame, read in rad per replay second, absent from older replays).

## Stage 7 - what the foot gives the ball (done)

`Core/KickModel.cs`, `MatchConfig.Kick` (null: the kicks before this stage, `SimRunner --legacykick`).

**Spin by kind of kick.** Each curled kick draws its spin as a launch spin parameter Sp = R w / v, so a profile holds at any kick speed. The kicker's
draw (the same random number as before, so the old model is untouched) and his skill pick a point in the range, half each:

| Kick | Sp at launch | At 26 m/s | Class |
|---|---|---|---|
| Curled shot (inside of the foot) | 0.11-0.27 | 4.1-10.2 rev/s | C from B: Hong 4-10 rev/s at 26 m/s; Asai's 7.8 rev/s at 27.1 m/s (Sp 0.20) and Hong's mean (0.19) sit in it |
| Outside of the foot | 0.09-0.22 | 3.4-8.3 rev/s | D (no measurement found) |
| Curled free kick | 0.16-0.27 | 6.0-10.2 rev/s | C from B (the upper half: a specialist with time) |
| Curled cross or pass | 0.10-0.25 | (at the pass's speed) | D |
| Chip (backspin) | 0.10-0.22, by skill | (at 9-15 m/s: 1.3-4.8 rev/s) | D (no measurement found) |
| Knuckleball | unchanged: about 0.06 rev/s | | inside Hong's 0-2 rev/s |
| Laces drive, side foot, others | unchanged (a random 8 rad/s of side spin) | | E |

The old model's curls were 9-17 rad/s for a curled shot (about 2 rev/s, Sp 0.07) and 15-28 rad/s for a free kick: a third to a half of what was measured. The
error in the spin is kept as a share of it (19%, the old model's 2.5 rad/s on its 13 rad/s; class E). The kicker allows for the stronger curl (stage 3's solvers aim
off by exactly what it brings back), so a curled shot bends further but is not aimed worse.

**The fastest strike.** Ball speed / foot speed at impact is 1.3 (class C from B: the literature's instep values are 1.06-1.65 by where the foot is measured; Asai's
curve kick 1.12, the ISBS study 1.40). A player's foot strikes at 18 m/s plus 6 m/s for the full physical-power attribute (class D: elite instep kicks are struck at
about 20-24 m/s), so the fastest ball is 23.4-31.2 m/s. A shot is never faster than that.

**The speed-spin trade-off** (Asai 2002) is already in the strikes: the engine's curler is struck at 19-24 m/s, its laces drive at 23-33 m/s (about 0.8 of the
speed: the measured ratios 1.12 v 1.3-1.4 give 0.80-0.86 for the same foot). It is tested, not modelled twice.

**An oncoming ball.** One-dimensional impact of an effective foot mass M (restitution e) on the ball m: v_out = U (1 + e) M / (M + m) - u (e M - m) / (M + m).
With the measured ratio r = (1 + e) M / (M + m), the second coefficient is exactly r - 1 = 0.3 whatever e is (class C). So a full strike (laces, volley,
half-volley, bicycle, knuckleball, the weaker foot) of a ball that comes at the striker - a first-time shot, a volley - sends 30% of that ball's speed back on top;
one running away from the foot takes it off. Strikes the kicker paces (side foot, curl, chip) are not maxed and do not take it. The part of the oncoming velocity
across the line of the kick is not modelled (the kicker's aim allows for it).

**Not modelled**: the contact point and friction as such (the impact model is one-dimensional; Asai's FEM curve of spin against offset is not reproduced, the measured
spin ranges stand in for it), the contact time (stage 9), backspin on lofted long balls and clearances (they keep no spin), headers' speed (the head is not a foot).

**In the match** (four seeds x 200 matches, old kicks v new):

| Per match (mean of 4 seeds) | Old kicks | New kicks | Real (PL) |
|---|---|---|---|
| Goals | 2.98 (3.07, 2.82, 3.00, 3.02) | 3.10 (3.22, 2.96, 3.09, 3.12) | 2.83 [C] |
| Shots | 25.45 | 25.77 | 25.4 [C] |
| Shots on target | 8.49 | 8.78 | 8.8 [C] |
| Headed goals, % of goals | 17.3 | 18.7 | 13-19 |
| Volley goals, % of goals | 14.5 | 14.9 | n/a |
| Free-kick goals | 0.11 | 0.11 | ~0.05-0.1 |
| Woodwork | 0.33 | 0.38 | ~0.6-0.8 (est.) |

Goals rise by 0.12 a match (4%; the seeds' spread puts that at about 1.4 standard errors, which is not conclusive: a second, independent 800 matches measured
during stage 8 gave 2.93 -> 2.99, +0.07, 0.8 standard errors; so a small rise at most): more shots on target, a few more hit the
woodwork (the stronger curl brings more of them to the corners). The engine now scores 3.10 a match against the real 2.83; it was 2.95 after stage 5. The
calibration round planned after the kick stages (with the marking gap) is where that is brought back - not by tuning the measured physics.

Tests: `BallKickTests` (the curve kick spins as measured; the spin scales with the speed; the fastest strike; the coupling equals the impact model for any e; the
speed-spin trade-off in the strikes; both kick models deterministic and the new one wired in).

## Stage 8 - collisions (done)

`Core/CollisionModel.cs`, `BallPhysics.Impact`, `MatchConfig.Collisions` (null: the collisions before this stage, `SimRunner --legacycollide`).

**One impulse for every contact.** `BallPhysics.Impact` is the bounce of stage 4 about any contact normal n, against a surface that may move (a player
running into the ball): the ball's normal speed relative to the surface is turned back times e; friction at the contact point r = -R n works on the slip
u = v + w x r, up to mu x the normal impulse, and changes the spin by (r x J) / I. With n = +z it is the bounce exactly (tested), and the ball's angular
momentum about the contact point is conserved through it (tested): the spin and the speed change together, as the physics says.

**The goal frame.** Two posts and the bar are capsules (segments with the posts' 6 cm radius), so the corners where they meet are round and a ball can
land on top of the bar. The old test looked at the end of each 0.02 s step only: a ball at 30 m/s moves 0.6 m a step while the contact distance is 0.17 m,
so a fast shot could pass through a post. The ball's path over the step is now sampled finer than the contact distance and the first touch refined by
bisection; the ball is put there, takes the impulse and spends the rest of the step on its new velocity. In the match, 0.13 of the 0.82 frame contacts a
match are ones the end-of-step test would have missed.

| Parameter | Value | Class |
|---|---|---|
| Ball on the frame, e at 6.26 m/s | 0.85: FIFA sets the ball's pressure so that it rebounds 1.45 m on concrete from 2.0 m (Test Manual I, 2024); a steel or aluminium post is as rigid as concrete for the ball | C from A |
| e falls with the impact speed | 0.01 per m/s (0.66 for a shot square on at 25 m/s; the old model: 0.7 for every impact). **Stage 9 replaced it with the measured 0.0056 (B, below)** | E: the ground model's analogy with tennis balls; the football studies found (Sheffield Hallam, Loughborough, *Sports Engineering* 2022) could not be read |
| Friction ball / post | 0.3 | E |
| Body radius (trunk and legs as one cylinder) | 0.2 m (only the direction of the normal depends on it) | D |
| Ball on a body, e | 0.2-0.55 for a block, 0.25-0.5 for a chance touch (the old model's drawn speed shares, now restitutions) | D |
| Friction ball / body | 0.4 | E |

**Players.** A block or an unintended touch (`BlockShot`, `Deflect`; the keeper's knock-down too) is an impulse off the player's body: a vertical cylinder
moving with him, struck at a lateral offset drawn where the old model drew a direction (the same random numbers, in the same order). Square on, the ball
comes back the way it came; at the edge it runs on, a little slowed, with spin from the friction. The normal tilts up by up to 30 degrees (a shin, thigh or
chest facing up) where the old model drew a pop-up. The old deflections kept the ball's direction within 75 degrees whatever the touch.

**Kept as they were** (and why): the keeper's catches, parries and punches are aimed actions (he chooses where to push the ball: round the post, over the
bar, away from danger), with speeds of 0.3-0.5 of the shot's, which is what a soft, yielding hand gives (class D); a blocked cross is a leg swung at the
ball towards the line, also aimed; the net stays a soft absorber (e 0.12, class D).

**In the match** (800 distinct matches each: seeds 1, 1001, 2001 and 3001 x 200; standard error of the goals' mean about 0.06):

| Per match | Before stage 7 | Stage 7 | Stage 8 | Real (PL) |
|---|---|---|---|---|
| Goals | 2.93 | 2.99 | 3.03 | 2.83 [C] |
| Shots | 25.53 | 25.73 | 26.46 | 25.4 [C] |
| Shots on target | 8.45 | 8.66 | 8.82 | 8.8 [C] |
| Shots blocked | 6.21 | 6.24 | 6.58 | 6-7 |
| Woodwork | 0.37 | 0.38 | 0.77 | ~0.6-0.8 (est.) |
| Corners | 9.23 | 8.79 | 9.68 | 10.4 [C] |
| Headed goals, % of goals | 17.6 | 18.7 | 21.5 | 13-19 |
| Pass completion % | 79.8 | 80.0 | 79.7 | 80-85 |

The woodwork count doubles: the posts no longer let fast shots through, and the round corners and the top of the bar now take the ball too. Blocks
that come back off a body give more second shots and more corners. Goals move by 0.04 (noise). **The headed share of goals rises to 21.5%, above the
real 13-19%** (the four seeds give 19.6-23.1%, so this looks real): balls lifted off a body and rebounds off the frame fall in the box. It is left for the
calibration round with the goals and the marking gap, not tuned in the collision physics.

Two tactical side effects, measured with the old collisions and the new on the same seeds (diagnostics compared counter by counter):
- **The crossing style crosses less**: 21.2 -> 18.9 crosses a match (40 matches; normal play 13.8 -> 13.9). A block no longer sends the ball back to
  the winger as often (crosses blocked 12.0 -> 10.4, attempts 29.1 -> 26.0, the rebound xG falls by a third), so he gets fewer second tries. The style
  still crosses x1.35 more than normal play (x1.54 before). `CrossesIntoTheBoxCrossesMore` now looks at 24 matches; on its six it landed at x1.13.
- **Dives**: none bought in the 32 matches of `BoldKeepersStandHigherAndTheDiverGoesDownNearTheBox`, but over 240 matches the two models agree
  (26 bought / 19 booked v 24 / 15). It was the sample, not the physics; the test now looks at 64 matches.

Tests: `BallCollisionTests` (the impact is the bounce about any normal; angular momentum about the contact point is conserved; the frame takes more from a
harder hit and nothing is created; a 32 m/s shot at the post is always caught, which the old test missed; the bar's top and its corners are round; a glancing
hit on the post spins the ball; both collision models deterministic and the new one wired in).

## Stage 9 - deformation (done)

**The model.** An inflated ball pressed onto a surface stays a sphere with a flat cap; it does not bulge sideways (Tadrist and Darbois Texier
filmed it at 4000 frames a second). The air pushes back with its pressure on the flat area, A = 2 pi R x for a flattening x, so the ball is a
spring of stiffness **k = 2 pi R P** (P the gauge pressure the Law sets) and touches for **pi sqrt(m / k)**, whatever the speed. For the
reference ball (430 g, 69 cm, 0.85 atm) that is 59 kN/m and 8.45 ms. The measured impacts on a rigid force plate (Iga, Nunome, Inoue and
Ikegami: a match ball at 0.9 bar, five speeds from 9 to 19.5 m/s) agree with it: the spring gives 8.2 ms for their ball, they measured
8.5-7.4 ms, and at 13.7 m/s the spring's peak flattening is the measured one to the millimetre. A real ball stiffens as it flattens, so the
contact shortens and the flattening grows a little slower than the spring says; the two exponents are fitted to the five measured impacts.

| Quantity | Value | Class |
|---|---|---|
| Stiffness | k = 2 pi R P: 59 kN/m for the reference ball (41-77 kN/m over the Law's 0.6-1.1 atm) | C from A (the Law's pressure, the model of Tadrist and Darbois Texier) |
| Contact time | pi sqrt(m / k) x (v / 11.2 m/s)^-0.19: 9.5 ms at 6 m/s, 8.2 ms at 13 m/s, 7.3 ms at 25 m/s; each measured impact within 0.1 ms | C (the spring), B (the exponent and reference: the fit to the five impacts) |
| Peak flattening | sqrt(m / k) x dv / 1.77 x (dv / 22 m/s)^-0.15, dv the velocity change (in + out): 1.7 cm for a 10 m/s pass from rest, 4.4 cm for a 30 m/s shot from rest, 5.0 cm for a ball that hits the post square at 20 m/s (it comes back at 15.5); each measured impact within 1.5 mm | C (the spring), B (the exponent 0.85, the measured restitution 0.77) |
| Deepest flattening drawn | 0.6 R (6.6 cm): beyond about two thirds of the radius the ball ripples; the measured ones stay below 0.45 R | D |
| A contact | a jump in the ball's velocity of more than 2 m/s between two 20 ms steps (flight alone gives at most about 0.4 m/s a step) | D |
| **The frame's restitution slope** | **e falls 0.0056 per m/s** of impact speed (the measured 0.797 at 9.05 m/s to 0.740 at 19.46 m/s, a straight line within 0.003): 0.75 for a shot square on the post at 25 m/s | **B** (stage 8 used 0.01, E) |

The level at the FIFA drop speed stays 0.85 (C from A: FIFA's test ball); the measured ball would have been 0.81 there, within the spread of
real match balls. The ground's slope (stage 4, 0.01 per m/s, E) is not changed: on grass the turf loses energy too, and no measurement on
grass at match speeds was found.

**What it changes.** The contact (8 ms) is shorter than the engine's step (20 ms), so the impulses stay instantaneous and the deformation does not
change the play. Only the frame's slope does: a post hit at shot speed now gives back more of its speed (0.75 rather than 0.66 at 25 m/s).
The engine notes every contact (about 1,050 a match: passes, traps, kicks, bounces, the frame, bodies; a catch and a throw are left out, the
hands are soft and unmeasured) with its flattening and direction (18 mm on average).

| Per match (800 distinct matches each: seeds 1, 1001, 2001, 3001 x 200) | Stage 8 slope (0.01) | Measured slope (0.0056) | Real |
|---|---|---|---|
| Goals | 3.03 | 3.03 | 2.83 |
| Shots | 26.46 | 26.33 | 25.4 |
| On target | 8.82 | 8.83 | 8.8 |
| Woodwork | 0.77 | 0.76 | |
| Corners | 9.68 | 9.68 | 10.4 |
| Headed goals, % of goals | 21.5 | 21.4 | 13-19 |
| Pass completion, % | 79.7 | 79.8 | |

Nothing moves beyond the noise (goals' standard error is about 0.06): the frame is hit under once a match, and the slope only matters for the
hardest hits. The stage 8 column reproduces stage 8's own figures exactly, as it should.

One test landed on its edge with the new random course: the high back line's block length on six matches (1.4 m shorter than normal against a
1.5 m bar). Over 30 matches the gap is 2.6 m with the old slope and 2.5 m with the new one; the test now looks at 18.

**What you see.** Each replay frame ends with the latest contact since the previous frame (flattening in mm, direction x100); the website, the
classic page and Unity draw the ball for that frame with a flat cap on the side that faced what it hit, at the real depth (a 30 m/s shot: a
flat cap 40% of the radius deep). Seen from above, a bounce (pushed straight up, the cap underneath) shows nothing, which is what a camera
above the pitch would see. The real contact lasts 8 ms; it is held for one drawn frame (about 17-40 ms), the shortest a screen can show.

Tests: `BallDeformationTests` (the spring's contact time and the five measured ones; the five measured flattenings; a harder ball flattens less
and touches shorter; the frame's restitution falls as the measured ball's did; the engine notes the contacts and the replay frame carries
them; the deformation does not change the match), `replay.test.ts` (the contact read from the frame, absent from older replays, the flat
side and its depth, nothing from above for a bounce).

## Stage 10 - stability (done)

The ball's integrator was measured against itself at a step 200 times finer (0.1 ms, the reference), for a drop, a long ball, a driven shot, a
chip and rolls. Three faults showed, all fixed:

| Fault | Effect at the game's 20 ms step (before) | Fix | After |
|---|---|---|---|
| A falling ball that ended a step within 0.5 mm of the grass counted as rolling | its vertical speed was zeroed: it died on the grass without a bounce (about 0.2% of landings at 11 m/s, 1% of slow drops; every landing at a fine step) | only a ball not moving vertically (under 0.05 m/s) rolls | always bounces |
| The bounce was taken at the end of the step | up to a step late (0.6 m at 30 m/s), at the speed the ball would have had a step later, and the rest of the step's rise lost | the ball meets the grass at the fraction of the step its path crosses it, bounces there at the velocity it had then, and spends the rest of the step on its new velocity | at the touch |
| The ball moved on its velocity at the end of each step (semi-implicit Euler) | it fell a little early every step: a 30 m/s driven shot landed 0.55 m (a whole step) short, a 25 m/s long ball 0.37 m; bounces 5-10% low; rolls 0.7% short | it moves on the mean of the step's start and end velocities (the trapezoid rule, exact for gravity alone); a rolling ball that stops during a step moves only until it stops | landings within 2-3 cm, bounces within 1-2% (a chip's 2.3%), rolls within 0.3% |

**A bug of this stage's own, caught by the match comparison.** With the bounce inside the step, the ball is already back in the air at the end
of that step. The kickers' aim (`KickSolver.CarryDistance` / `LandingPoint`, used for every lofted pass and cross) looked for the ball on the
grass at the end of a step, so it found the second bounce: every lofted ball came down short of its aim, and in 800 matches headed goals fell
from 21% to 3% of goals and pass completion from 79.8% to 77.1%. The step now reports where the ball landed (`BallMotion.Landed`, `LandX`,
`LandY`, `LandSpeed`) and the solvers use it; a test checks the solver against the flight.

**Determinism.** The match does not depend on how the host cuts its time: a frame at a time (1/60 s), a quarter second or a whole second
gives the same match, step for step (tested). The engine stays on single-precision floats and its own random stream, so the same build gives
the same match on every machine; the browser and the server run the same WebAssembly build. (A Unity build compiles the same C# with
IL2CPP; its maths library may round a transcendental function differently in the last bit, so a Unity replay of a match recorded in the
browser is not guaranteed to be identical - the engine contract for a future Unity view is to play the recorded frames, not re-simulate.)

| Per match (800 distinct matches each: seeds 1, 1001, 2001, 3001 x 200) | Stage 9 | Stage 10 | Real |
|---|---|---|---|
| Goals | 3.03 | 2.94 | 2.83 |
| Shots | 26.33 | 26.45 | 25.4 |
| On target | 8.83 | 8.81 | 8.8 |
| Woodwork | 0.81 | 0.76 | |
| Corners | 9.68 | 9.56 | 10.4 |
| Headed goals, % of goals | 21.4 | 20.6 | 13-19 |
| Pass completion, % | 79.8 | 79.7 | |

Goals fall by 0.09 (about 1.4 standard errors: not conclusive), towards the real 2.83; nothing else moves beyond the noise.

Tests: `BallStabilityTests` (the game's step lands and bounces where the fine step does, for a drop, a long ball, a driven shot and a chip; a
falling ball just above the grass still bounces; rolls stop where the fine step stops them; the kickers' aim agrees with the flight; flight in
still air never gains energy over 400 random launches with spin; extreme balls - 60 m/s with 300 rad/s of spin from 30 m up - stay finite,
never go under the grass and come to rest; the match does not depend on how the host cuts its time).

## Stage 11 - test bench (done)

`BallTestBench` runs the specification's TESTS 2-14 on the engine's own physics and records, for every scenario, the speed in and out, the
spin in and out, the flight time, the distance, the bounce height, the energy lost and the response. The full table is
[`docs/BALL_TEST_BENCH.md`](BALL_TEST_BENCH.md), written by `SimRunner --bench`; a test fails when the file and the physics disagree, so the
table is always the physics as it is. TEST 1 is `BallSpecTests`; TEST 9 (the knuckleball) is listed as not built (stage 3: no model).

FIFA's own tests are run the way FIFA runs them (class A ranges), and the bench meets them:

| FIFA test | The bench | FIFA's range |
|---|---|---|
| Vertical rebound from 2.00 m, natural grass | 0.76 m | 0.60-1.00 m ("excellent") |
| Vertical rebound from 2.00 m, football turf | 0.69 m | 0.60-0.85 m (Quality Pro) |
| Ball roll off the 1 m, 45 degree ramp (3.2 m/s at the bottom) | 6.4 m | 4.0-10.0 m |
| Angle ball rebound (50 km/h, 30 degrees), dry | keeps 59-60% of its speed | 45-70% (turf) |
| Angle ball rebound, wet | keeps 72-75% | 45-80% (turf) |

What the bench shows, read row by row:
- **Drops and bounces** (TESTS 2-3): the rebound ratio falls from 0.66 (0.5 m) to 0.56 (5 m), as the impact-speed dependence of e says; each
  bounce keeps 38-46% of the height before it.
- **Shots** (TEST 5): 30 m/s at 15 degrees carries 35 m in 1.48 s and lands at 21 m/s; at 45 degrees drag holds it to 56 m (no drag: 92 m).
- **Spin** (TESTS 6-8): backspin of Sp 0.1-0.2 carries a 25 m/s, 20 degree kick 42 m against 32.5 m without; topspin brings it down at
  19-23 m. Sidespin curls a 26 m/s kick 3.75 m at 4 rev/s and 5.0 m at 10 rev/s: the curl grows more slowly than the spin because a faster
  spin also raises the drag (the spin-dependent drag of stage 3), so the ball arrives slower and lower.
- **The frame** (TESTS 10-11, 14): square on, a 25 m/s ball comes back at 0.73 of its speed; 12 cm off the post's centre it leaves at 82
  degrees with 15 rev/s of spin; off the bar's underside it is driven down at 84 degrees. The post is exactly symmetric; the bar to within
  0.15 m/s (gravity pulls the same way on both sides of it). No shot from 20 to 50 m/s passes through the post, from any of 20 step phases.
- **Players** (TEST 12): the foot sends a still ball at 23-31 m/s by power; a 10 m/s oncoming ball adds 3 m/s. A shot striking a body square
  on keeps 40% of its speed; near the body's edge it runs on at 54-60% and turns 117-134 degrees.
- **Surfaces** (TEST 13): a 10 m/s roll goes 24 m on long grass, 27 m on normal, 31 m on short, 33 m on dry turf, 36-37 m wet; wet grounds
  give back more speed in the angle rebound (they grip less).

Tests: `BallTestBenchTests` (the doc is the bench's output; TESTS 2-14 are all there; FIFA's ranges; no energy created and the frame symmetric;
spin acts as the Magnus force says; nothing passes through the post).

## Stage 12 - every parameter classified (done)

[`BALL_PARAMETERS.md`](BALL_PARAMETERS.md) lists every parameter of the ball's physics - the ball, the air, flight, the ground, the foot,
collisions, deformation, the integrator - with its value, its class and its source, in one place. Of 62 rows, 5 are A, 11 B, 11 C, 28 D and 7 E.
Most D values are choices inside an A or B range (the ball's mass in the Law's range, the ground's e in FIFA's). The E list is what needs
calibration, in order of how much it matters to the match: the ground's restitution slope (the ball's own share is now measured, the turf's is
not), the spin of a laces drive, ball-post and ball-body friction, the kick spin error, the inertia factor (a D value with no measurement), the
knuckleball (a model, not a number) and pressure in the bounce. `BallParameterTests` fails when a field of a model class has no row there.

## Plan for the next stages

| Stage | What | Changes the match? |
|---|---|---|
| 3 | **Done** (above). Flight: gravity, drag with Cd(Re, spin) and a drag crisis, Magnus as a vector force; knuckle side force not modelled (no coefficient model) | Yes: shots, crosses, long balls |
| 4 | **Done** (above). Bounce: impulse model, e(v_n, surface), Coulomb friction at the contact point, spin in and out | Yes, a little |
| 5 | **Done** (above). Rolling: slip → roll transition, rolling resistance a + b v per pitch profile (short / normal / long / wet / dry / turf) | Yes, a little |
| 6 | **Done** (above). 3D ω through flight, bounce and roll (stages 2-5); the viewers show the real spin | No (the picture only) |
| 7 | **Done** (above). Kicks: measured spin per kick type, foot speed cap, an oncoming ball's share; contact point and friction not modelled as such | Yes |
| 8 | **Done** (above). Collisions: the frame as capsules with a swept test, impulses with restitution, friction and spin against the frame and players' bodies; keeper parries stay aimed | Yes |
| 9 | **Done** (above). Deformation: the ball as a gas spring (k = 2 pi R P) fitted to measured impacts: contact time, flattening, the frame's measured restitution slope; the viewers draw the flat cap | A little (the frame's slope) |
| 10 | **Done** (above). Stability: the ground touch inside the step, the trapezoid rule, the dead landing fixed; determinism tested | A little (goals 3.03 -> 2.94, noise-level) |
| 11 | **Done** (above). Test bench: TESTS 2-14 with the recorded quantities, FIFA's tests run as FIFA runs them, `docs/BALL_TEST_BENCH.md` | No |
| 12 | **Done** (above). Every parameter classified A-E in one table, [`BALL_PARAMETERS.md`](BALL_PARAMETERS.md), with the list of what needs calibration | No |

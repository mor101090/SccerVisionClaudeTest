# Ball physics

The ball model of the match engine: what it is, where every number comes from, and how sure we are. Built in stages (one branch and
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
| [IFAB Law 2, The Ball](https://www.theifab.com/laws/latest/the-ball/) | The page itself: circumference 68-70 cm, 410-450 g "in weight at the start of the match", pressure "0.6-1.1 atmosphere (600-1,100 g/cm²) at sea level" | Class A limits in `BallSpec` |
| Goff and Carré, [Trajectory analysis of a soccer ball](https://doi.org/10.1119/1.3197187), *Am. J. Phys.* 77(11):1020-1027, 2009 | **The full text** (a PDF copy, read page by page, figures included). Equations of motion, Re = vD/ν with ν = 1.54e-5 m²/s and D = 0.218 m, mass 0.424 kg, A = 0.0375 m², ρ = 1.2 kg/m³; Fig. 6 (wind-tunnel Cd(Re) of two series), Fig. 7 (Cd(Sp)), Fig. 8 (CL(Sp), wind tunnel and their own trajectory points), their measured no-spin range of 21.9 m at 18 m/s and 22° (Cd 0.2) and 0.17 at 19.4 m/s, the spin decay estimate, the Beckham estimate | **Class B**: the source of the flight tables. Values read from the figures by eye (±0.03 on Cd, ±0.04 on CL) |
| FIFA, [Test Manual I](https://www.sporteimpianti.it/wp-content/uploads/2024/04/FIFA-TTM-I-Layout-v8.pdf) (football turf, April 2024) | **The text itself.** Test balls are inflated to rebound 1.45 ± 0.03 m on concrete from 2.0 m. Method 2024-01: vertical rebound of a ball dropped from 2.00 m (underside). Method 2024-02: ball roll from a 1 m, 45° ramp. Method 2024-14: angle ball rebound = speed after / before for a ball fired at 50 ± 2 km/h and 30 ± 2°, with under 3 rev/s of spin | **Class A** test methods; stage 4 (and 5) |
| FIFA, [Natural-Pitch Rating System](https://digitalhub.fifa.com/m/58aa765dd3e85f26/original/FIFA-natural-pitch-rating-system_EN.pdf) (May 2022) | **The text itself.** "Excellent" natural pitch: vertical ball rebound 0.60-1.00 m (satisfactory 0.50-0.59 or 1.01-1.10), ball roll 4.0-10.0 m | **Class A** ranges; stage 4 (and 5) |
| The English FA, conditions of use for artificial pitches (through a search, secondary) | FIFA Quality turf: vertical rebound 0.60-1.00 m (Quality Pro 0.60-0.85 m), angle ball rebound 45-70% dry, 45-80% wet, ball roll 4-10 m | Secondary; stage 4 |
| J. Thomas, [Soccer Ball Interactions on Wet and Dry Turfgrasses at Various Mowing Heights](https://trace.tennessee.edu/utk_gradthes/11814), MSc thesis, University of Tennessee, 2024 | **The abstract only** (the full text is embargoed until 2027): angle ball rebound 56% (Kentucky bluegrass) and 69% (bermudagrass) dry, 74-76% lightly irrigated (63% for one); vertical rebound changed by 7 cm or less dry v wet and with mowing height; lower mowing gives a longer roll | Class B (abstract); stage 4 |
| Asai, Kimachi, Liu, Koido, Nakayama, Goff, Hong, [Measurements of the Flight Trajectory of a Spinning Soccer Ball and the Magnus Force Acting on It](https://www.mdpi.com/2504-3900/49/1/88), *Proceedings* 49(1):88, 2020 | **Not the full text** (the publisher refuses automated downloads). Title and authors from Crossref; from the abstract through a search: time-averaged side-force coefficients 0.21, 0.24, 0.29 at time-averaged spin parameters 0.18, 0.22, 0.34 (spin parameter Sp = Rω/U), vertical spin axes, the side force falls as the ball slows | Secondary: the audit below; stage 3 |
| Goff, Hong, Asai, [Influence of Surface Properties on Soccer Ball Trajectories](https://www.mdpi.com/2504-3900/49/1/143), *Proceedings* 49(1):143, 2020 | **Not the full text.** From search snippets: the drag coefficient depends on speed and spin; the drag crisis (a sharp fall of Cd at a critical Reynolds number) differs between balls (Jabulani, Brazuca, a smooth ball of the same size; the smooth ball has the highest critical Reynolds number); panel texture, seam width and depth matter | Secondary, qualitative only: stage 3 |
| Paudel, Yan, [High-fidelity simulations of full-flight soccer ball aerodynamics using a monolithic overset approach](https://link.springer.com/article/10.1007/s00466-025-02705-2), *Computational Mechanics*, 2025 | The abstract (Crossref): fully coupled CFD of a spinning free kick; spin rate drives the lateral deviation, kick speed the range and flight time; a smoother ball shows a stronger Magnus effect; compared with an ODE model using empirical drag coefficients | Qualitative: why a coefficient model (not CFD) is the right tool in a game; no numbers taken |
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

## Plan for the next stages

| Stage | What | Changes the match? |
|---|---|---|
| 3 | **Done** (above). Flight: gravity, drag with Cd(Re, spin) and a drag crisis, Magnus as a vector force; knuckle side force not modelled (no coefficient model) | Yes: shots, crosses, long balls |
| 4 | **Done** (above). Bounce: impulse model, e(v_n, surface), Coulomb friction at the contact point, spin in and out | Yes, a little |
| 5 | Rolling: slip → roll transition, rolling resistance μr per pitch profile (short / long / wet / dry / artificial), spin decay | Yes: every pass |
| 6 | Spin: 3D ω through flight, bounce and roll; torques; the renderer shows the real spin | Yes |
| 7 | Kicks: foot speed, contact point and friction → ball velocity and spin per kick type | Yes |
| 8 | Collisions: posts, crossbar, players, keeper hands with impulses, continuous detection | Yes |
| 9 | Deformation: a contact-time / compression model for impact losses; visual squash from the same numbers | Small |
| 10 | Stability: fixed step, continuous collision against posts and ground, determinism kept | No (safety) |
| 11 | Test bench: TESTS 2-14 with the recorded quantities (initial / final speed, spin, flight time, distance, bounce height, energy loss) | No |

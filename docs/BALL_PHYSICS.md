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

## Plan for the next stages

| Stage | What | Changes the match? |
|---|---|---|
| 3 | Flight: gravity, drag with Cd(Re, spin) and a drag crisis, Magnus as a vector force ½ ρ A C_L(Sp) v² (ω̂ × v̂), knuckle side-force fluctuation only with a coefficient model | Yes: shots, crosses, long balls |
| 4 | Bounce: impulse model with restitution e(v_n, surface) and Coulomb friction at the contact point, spin in and out | Yes |
| 5 | Rolling: slip → roll transition, rolling resistance μr per pitch profile (short / long / wet / dry / artificial), spin decay | Yes: every pass |
| 6 | Spin: 3D ω through flight, bounce and roll; torques; the renderer shows the real spin | Yes |
| 7 | Kicks: foot speed, contact point and friction → ball velocity and spin per kick type | Yes |
| 8 | Collisions: posts, crossbar, players, keeper hands with impulses, continuous detection | Yes |
| 9 | Deformation: a contact-time / compression model for impact losses; visual squash from the same numbers | Small |
| 10 | Stability: fixed step, continuous collision against posts and ground, determinism kept | No (safety) |
| 11 | Test bench: TESTS 2-14 with the recorded quantities (initial / final speed, spin, flight time, distance, bounce height, energy loss) | No |

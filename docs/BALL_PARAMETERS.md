# Ball physics parameters

Every parameter of the ball's physics in one table, with its value, its class and where it comes from (ball physics stage 12). The detail and
the sources' full references are in [`BALL_PHYSICS.md`](BALL_PHYSICS.md); what the physics does with them, scenario by scenario, is in
[`BALL_TEST_BENCH.md`](BALL_TEST_BENCH.md). A test (`BallParameterTests`) fails when a parameter of the code is missing from this page.

**Classes.** **A** officially standardised (the Laws of the Game, FIFA's test methods and ranges). **B** experimentally measured (a published
measurement, read in the source itself unless marked secondary). **C** mathematically derived (from A or B by physics). **D** an approximation
or a simulation assumption (a choice, usually inside an A or B range). **E** unknown: no measurement was found; the value needs calibration.
An estimated coefficient is never presented as a measured one: a value fitted to a measurement is C, a value chosen inside a range is D.

## The ball (`BallSpec`)

| Parameter | Value | Class | Source |
|---|---|---|---|
| `IfabMinMassKg`, `IfabMaxMassKg` | 0.410-0.450 kg | A | IFAB Law 2 |
| `IfabMinCircumferenceM`, `IfabMaxCircumferenceM` | 0.680-0.700 m | A | IFAB Law 2 |
| `IfabMinGaugePressurePa`, `IfabMaxGaugePressurePa` | 58.8-111.5 kPa gauge | A | IFAB Law 2 ("0.6-1.1 atmosphere (600-1,100 g/cm²)"; the wider reading of the two units) |
| `StandardAtmospherePa`, `TechnicalAtmospherePa` | 101,325 Pa; 98,066.5 Pa | A | the units' definitions |
| `MassKg` | 0.430 kg | D inside A | the middle of the Law's range |
| `CircumferenceM` | 0.690 m (radius 0.1098 m, C) | D inside A | the middle of the Law's range |
| `GaugePressurePa` | 86.1 kPa (0.85 atm) | D inside A | the middle of the Law's range |
| `InertiaFactor` | 2/3 (I = k m R² = 0.003457 kg·m²) | D (E until measured) | a thin shell; a real ball is a little under it; no measurement of a size 5 ball was read |
| `SolidSphereInertiaFactor`, `ThinShellInertiaFactor` | 2/5, 2/3 | C | the bounds of k (a solid sphere, a thin shell) |

## The air and the step (`MatchConfig`)

| Parameter | Value | Class | Source |
|---|---|---|---|
| `Gravity` | 9.81 m/s² | A | standard gravity (9.80665) to three figures |
| `AirDensity` | 1.2 kg/m³ | D | Goff and Carré use 1.2; the standard atmosphere at 15 °C is 1.225 |
| `AirDynamicViscosity` | 1.85e-5 Pa·s | B | Goff and Carré's kinematic viscosity 1.54e-5 m²/s × 1.2 |
| `FixedStep` | 0.02 s | D | the engine's step; stage 10 measured it against a 0.1 ms step: landings within 2-3 cm, bounces within 1-2%, rolls within 0.3% |
| `AirDragK` | 0.0133 | D | the old flight model (`--legacyflight` only) |

## Flight (`AeroModel`)

| Parameter | Value | Class | Source |
|---|---|---|---|
| Cd0(Re) table | 0.46 below Re 1.2e5, the drag crisis between 1.2e5 and 1.8e5, 0.18-0.20 after it | B/D | the two wind-tunnel series in Goff and Carré 2009, Fig. 6, read by eye; the profile is chosen between them (balls differ by ±0.05) |
| `DragPerSpinParameter` | +0.45 per unit of Sp | D | a line through Goff and Carré Fig. 7 (wind tunnel, Sp up to 0.31) |
| `MaxSpinDrag` | +0.13 at most | D | no measurement beyond Sp 0.31 |
| Cl(Sp) table | 0 / 0.09 / 0.17 / 0.23 / 0.26 / 0.29 / 0.30 / 0.31 at Sp 0 / 0.05 / 0.1 / 0.15 / 0.2 / 0.3 / 0.5 / 0.71 | B | the mean of Goff and Carré's Fig. 8 wind-tunnel points and trajectory estimates; the same coefficient for the side force |
| `SpinDecayPerSecond` | 0.08 /s | B (secondary) | Goff and Carré, citing James and Haake 2008: 4-8% of the spin lost by the apex, about 10% by landing |
| `DragScale`, `LiftScale` | 1, 1 | D | calibration hooks for another ball; unused |
| Knuckleball side force | not modelled | E | no coefficient model of the seam-driven force was found; TEST 9 is not built |

## The ground (`GroundModel`)

| Parameter | Value | Class | Source |
|---|---|---|---|
| `ReferenceImpactSpeed` | 6.26 m/s | C from A | FIFA's vertical rebound test: sqrt(2 g × 2.00 m) |
| `Restitution`, natural grass | 0.632 (a 0.80 m rebound from 2 m; 0.76 m with the air's drag) | D inside A | the middle of FIFA's "excellent" 0.60-1.00 m |
| `Restitution`, football turf | 0.602 (0.725 m; 0.69 m with drag) | D inside A | the middle of FIFA Quality Pro 0.60-0.85 m |
| `Restitution`, wet | the same as dry | B | Thomas 2024 (abstract): 7 cm or less of difference |
| `RestitutionPerMetrePerSecond` | -0.01 per m/s of impact speed | E | borrowed from pressurised tennis balls (Dignall 2005); the ball's own measured loss on a rigid plate is 0.0056 per m/s (stage 9), the turf's share is unmeasured |
| `Friction`, dry grass and turf | 0.6 | D inside A | anything above about 0.45 grips in FIFA's angle test: 60% kept, inside 45-70% |
| `Friction`, natural wet | 0.22 | C from B | reproduces Thomas 2024's 74-76% angle rebound |
| `Friction`, turf wet | 0.25 | D inside A | inside FIFA's wet 45-80% |
| `RollA`, `RollB`, natural dry | 0.408 m/s², 0.167 /s (deceleration a + b v) | C from B | fits both Norwegian (NBI / UEFA 2003) measurements at once: 6.4 m off the FIFA ramp |
| `RollA`, `RollB`, other profiles | short 0.343 / 0.140, long 0.480 / 0.196, wet 0.281 / 0.115, turf 0.320 / 0.131 (wet 0.264 / 0.108) | D inside A | scaled from the dry profile; every one rolls 4-10 m off the ramp (FIFA) |
| `FittedRoll` | 5.5-9.5 m | D inside A | the ramp distance each profile was set to |
| Snow, mud, puddles, worn pitch | multipliers on e, μ and the roll | D | kept from the old model |
| Below 1.1 m/s of normal impact the ball stops bouncing | | D | |
| Ball pressure in the bounce | not modelled | E | no law for e(pressure) was found |

## The foot (`KickModel`)

| Parameter | Value | Class | Source |
|---|---|---|---|
| `Curler` | Sp 0.11-0.27 (4.1-10.2 rev/s at 26 m/s) | C from B | Hong et al. 2011 (4-10 rev/s at 26 m/s); Asai et al. 2004's 7.8 rev/s at 27.1 m/s sits in it |
| `FreeKick` | Sp 0.16-0.27 | C from B | the upper half of the curve-kick range |
| `OutsideFoot` | Sp 0.09-0.22 | D | no measurement found |
| `CurledPass` | Sp 0.10-0.25 | D | the curve-kick range at a pass's speed |
| `ChipBackspin` | Sp 0.10-0.22 | D | no measurement found |
| `SpinErrorFraction` | 0.19 of the spin (standard deviation) | E | the old model's 2.5 rad/s on 13 rad/s, kept as a relative error |
| `BallFootSpeedRatio` | 1.3 | C from B | the literature's 1.06-1.65 (Asai's curve kick 1.12, an ISBS 2008 study 1.40) |
| `FootSpeedBase`, `FootSpeedPerPower` | 18 m/s + 6 m/s × power | D | elite instep kicks are struck at about 20-24 m/s |
| The spin of a laces drive, a side foot and the other kicks | a random 8 rad/s of side spin | E | unchanged from the old model |

## Collisions (`CollisionModel`)

| Parameter | Value | Class | Source |
|---|---|---|---|
| `FrameRestitution` | 0.85 at the reference speed | C from A | FIFA sets the ball to rebound 1.45 m from 2.0 m on concrete: e = sqrt(1.45 / 2.0); a post is as rigid as concrete for the ball |
| `ReferenceImpactSpeed` | 6.26 m/s | C from A | the FIFA drop's impact speed |
| `RestitutionPerMetrePerSecond` | -0.0056 per m/s | B | Iga, Nunome, Inoue and Ikegami: a match ball on a rigid force plate, e 0.80 -> 0.74 from 9 to 19.5 m/s |
| `FrameFriction` | 0.3 | E | no measurement of ball on a painted post found |
| `BodyRadius` | 0.2 m | D | the trunk and legs as one cylinder (only the normal's direction depends on it) |
| `BodyFriction` | 0.4 | E | |
| Body restitution | 0.2-0.55 for a block, 0.25-0.5 for a chance touch | D | the old model's drawn speed shares, read as restitutions |
| Post radius | 0.06 m (12 cm across) | D inside A | Law 1: posts and bar no more than 12 cm wide; the widest allowed |

## Deformation (`DeformationModel`)

| Parameter | Value | Class | Source |
|---|---|---|---|
| Stiffness k = 2πRP | 59 kN/m (41-77 over the Law's pressures) | C from A | Tadrist and Darbois Texier 2017: the gas pushes on the flat cap's area |
| `CompressionExponent` | 0.85 | B | the fit to Iga et al.'s five measured flattenings (each within 1.5 mm) |
| `ReferenceVelocityChange` | 22 m/s | B | the same fit |
| `MeasuredRestitution` | 0.77 | B | Iga et al.'s mean (0.80-0.74) |
| `ContactTimeExponent` | -0.19 | B | the fit to Iga et al.'s five measured contact times (each within 0.1 ms) |
| `ContactTimeReferenceSpeed` | 11.2 m/s | B | the same fit |
| `MaxCompressionFraction` | 0.6 of the radius | D | beyond about two thirds the ball ripples (Tadrist and Darbois Texier); the measured ones stay under 0.45 |
| `ContactThreshold` | 2 m/s of velocity change between steps | D | flight alone changes it by at most about 0.4 m/s a step |

## Integration (`BallPhysics`, stage 10)

| Parameter | Value | Class | Source |
|---|---|---|---|
| Integrator | the trapezoid rule (mean of the step's start and end velocities); the ground touch found inside the step | C | exact for gravity alone; measured against a 0.1 ms step |
| Rolling vertical speed | under 0.05 m/s | D | far below the slowest bounce |

## Summary

| Class | Rows | What it covers |
|---|---|---|
| A | 5 | the Laws' ball (mass, size, pressure ranges); the pressure units; gravity |
| B | 11 | viscosity; the lift table and spin decay; the drag-crisis table (B/D); wet-ground restitution; the ball's impact loss on a rigid plate; the flattening and contact-time fits (Iga et al.) |
| C | 11 | derived from A or B: the inertia bounds, the drop speed, the frame's e, the gas spring, the dry-grass roll fit, wet-grass friction, the curl and free-kick spin ranges, the ball / foot speed ratio, the integrator |
| D | 28 | choices inside an A or B range (mass, size, pressure, the inertia factor, ground e and μ, roll profiles, the post's width) and the simulation's own assumptions (the step, air density, spin drag, body geometry and restitution, thresholds) |
| E | 7 | the ground's e slope, ball / post and ball / body friction, the kick spin error, the drive's spin, the knuckleball, pressure in the bounce |

(62 rows; a row that groups several fields counts once. The inertia factor is D for its value and E for its truth: no measurement was found.)

**What needs calibration (the E list), in order of how much it matters to the match.** The ground's restitution slope (every bounce; the ball's
own share is now measured, the turf's is not); the spin of a laces drive (most shots); ball-post and ball-body friction (the angle a ball
leaves a post or a body at); the kick spin error; and, though its value is a D, the inertia factor (the spin every contact gives); the knuckleball (a model, not a number);
pressure in the bounce. None is presented as measured; each one's row says what it was borrowed from or chosen inside.

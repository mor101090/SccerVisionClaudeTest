using System;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// How the pitch takes a bouncing ball: the coefficient of restitution of the normal impact and the friction at the contact point. One
    /// profile per kind of surface; see docs/BALL_PHYSICS.md for the classes (A standardised, B measured, C derived, D approximation, E unknown).
    ///
    /// Sources (read directly):
    /// - FIFA Test Manual I (2024), Test Method 2024-01: the ball is dropped from 2.00 m (to its underside) and the rebound height measured;
    ///   before testing the ball's pressure is set so that it rebounds 1.45 m on concrete from 2.0 m (e = sqrt(1.45/2.0) = 0.85 at 6.26 m/s).
    ///   Test Method 2024-14: the angle ball rebound is the ratio of the ball's speed after to before an impact at 50 km/h and 30 degrees, with
    ///   less than 3 rev/s of spin.
    /// - FIFA Natural-Pitch Rating System (2022): an "excellent" natural pitch rebounds the ball 0.60-1.00 m (e 0.55-0.71); 0.50-0.59 and
    ///   1.01-1.10 m are "satisfactory".
    /// - FIFA Quality for football turf (through the English FA's conditions of use, secondary): 0.60-1.00 m (Quality Pro 0.60-0.85 m); angle
    ///   ball rebound 45-70% dry, 45-80% wet.
    /// - J. Thomas, MSc thesis, University of Tennessee 2024 (abstract): on natural turfgrass the angle ball rebound was 56% (Kentucky bluegrass)
    ///   and 69% (bermudagrass) dry, 74-76% lightly irrigated; the vertical rebound changed by 7 cm or less between dry and wet.
    /// </summary>
    public sealed class GroundModel
    {
        public string Name;
        /// <summary>Coefficient of restitution of the normal impact at <see cref="ReferenceImpactSpeed"/> (the FIFA drop test, 6.26 m/s).</summary>
        public float Restitution;
        /// <summary>The impact speed of the FIFA vertical rebound test: sqrt(2 g 2.00 m) = 6.26 m/s (class C).</summary>
        public float ReferenceImpactSpeed = 6.26f;
        /// <summary>
        /// How much the restitution falls per m/s of normal impact speed above the reference (and rises below it). A ball bounces less
        /// from a harder impact; no measurement for a football was found (pressurised tennis balls fall from about 0.8 at 3 m/s to 0.6 at
        /// 20 m/s on a rigid plate, Dignall 2005), so this is class E by analogy, 0.01 per m/s.
        /// </summary>
        public float RestitutionPerMetrePerSecond = 0.01f;
        /// <summary>
        /// Coulomb friction coefficient at the contact during a bounce. Dry grass grips the ball (any value above about 0.45 does for the FIFA
        /// angle test, giving about 60%, in the middle of the dry range and between the thesis' 56% and 69%); a wet surface lets it slide:
        /// 0.22 reproduces the thesis' 74-76% (class C from B).
        /// </summary>
        public float Friction;

        /// <summary>
        /// Rolling resistance of a rolling ball, as a deceleration a + b v (m/s², v in m/s), on top of the air drag (stage 5). The linear form is the
        /// one the IST / Kolitzus method fits to timing-gate data ("the deceleration decreases the slower the ball rolls"; Gabrielsen, Norwegian Building
        /// Research Institute, ISSS 2004). The air drag on a rolling ball is counted separately (it slows the spin too: 1/(1+k) of the free value).
        /// </summary>
        public float RollA, RollB;
        /// <summary>The roll distance this profile was fitted to: a ball leaving the FIFA / EN 12234 ramp (1 m drop) reaches the surface at 3.2 m/s (Gabrielsen) and rolls this far.</summary>
        public float FittedRoll;

        public GroundModel(string name, float restitution, float friction, float rollA = 0.408f, float rollB = 0.167f, float fittedRoll = 6.4f)
        {
            Name = name;
            Restitution = restitution;
            Friction = friction;
            RollA = rollA;
            RollB = rollB;
            FittedRoll = fittedRoll;
        }

        /// <summary>The rolling resistance at this speed (m/s²), without the air drag.</summary>
        public float RollingResistance(float speed) => RollA + RollB * speed;

        /// <summary>The restitution of a normal impact at this speed (clamped to 0.2-0.95).</summary>
        public float RestitutionAt(float normalSpeed)
            => MathUtil.Clamp(Restitution - RestitutionPerMetrePerSecond * (normalSpeed - ReferenceImpactSpeed), 0.2f, 0.95f);

        /// <summary>The rebound height (ball underside) of the FIFA drop test from 2.00 m on this surface, ignoring the air: e^2 x 2.00 m.</summary>
        public float DropTestRebound => Restitution * Restitution * 2.00f;

        public GroundModel With(string name, float? restitution = null, float? friction = null, float? rollA = null, float? rollB = null, float? fittedRoll = null)
            => new GroundModel(name, restitution ?? Restitution, friction ?? Friction, rollA ?? RollA, rollB ?? RollB, fittedRoll ?? FittedRoll)
               { ReferenceImpactSpeed = ReferenceImpactSpeed, RestitutionPerMetrePerSecond = RestitutionPerMetrePerSecond };

        // Rolling (stage 5). Measured anchor: three natural grass fields in Norway (NBI for UEFA, 2003; "heavy slow northern European grasses",
        // Dunlop's comment): the velocity change over 2 m timing gates from the 1 m ramp averaged 0.68 m/s (0.55-0.87; 95% CI 0.62-0.74), and the
        // roll distance trend of the same study gives about 6.4 m for it. a = 0.408, b = 0.167 reproduce both (6.40 m and 0.72 m/s, with the air
        // drag of a rolling ball added): class C from B. The FIFA Natural-Pitch Rating System calls 4.0-10.0 m excellent; the old engine rolled 4.1 m.
        // Other profiles keep the measured shape (b / a) and are fitted to a roll distance chosen inside FIFA's ranges (class D).

        /// <summary>Natural grass, dry, a normal match cut: the Norwegian measurements (roll 6.4 m from the 1 m ramp). e 0.632, grips (mu 0.6).</summary>
        public static readonly GroundModel NaturalDry = new GroundModel("natural, dry", 0.632f, 0.6f, 0.408f, 0.167f, 6.4f);
        /// <summary>Natural grass, dry, cut short (the thesis: a lower cut rolls further): 7.5 m (class D).</summary>
        public static readonly GroundModel NaturalDryShort = new GroundModel("natural, dry, short", 0.632f, 0.6f, 0.343f, 0.140f, 7.5f);
        /// <summary>Natural grass, dry, long: 5.5 m (class D).</summary>
        public static readonly GroundModel NaturalDryLong = new GroundModel("natural, dry, long", 0.632f, 0.6f, 0.480f, 0.196f, 5.5f);
        /// <summary>Natural grass, wet: the same vertical rebound, sliding friction 0.22 (the thesis' 74-76% angle rebound), a faster roll: 9.0 m (class D; the old model's wet pitch rolled about 1.4 times as far).</summary>
        public static readonly GroundModel NaturalWet = new GroundModel("natural, wet", 0.632f, 0.22f, 0.281f, 0.115f, 9.0f);
        /// <summary>Football turf, dry: e 0.602 (0.725 m, the middle of FIFA Quality Pro 0.60-0.85 m), grips; roll 8.0 m (class D, inside FIFA Quality 4-10 m).</summary>
        public static readonly GroundModel ArtificialDry = new GroundModel("artificial, dry", 0.602f, 0.6f, 0.320f, 0.131f, 8.0f);
        /// <summary>Football turf, wet: the same rebound, sliding friction 0.25, roll 9.5 m (class D).</summary>
        public static readonly GroundModel ArtificialWet = new GroundModel("artificial, wet", 0.602f, 0.25f, 0.264f, 0.108f, 9.5f);
    }
}

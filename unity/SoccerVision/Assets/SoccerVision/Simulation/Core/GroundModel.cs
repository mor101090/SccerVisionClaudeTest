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

        public GroundModel(string name, float restitution, float friction)
        {
            Name = name;
            Restitution = restitution;
            Friction = friction;
        }

        /// <summary>The restitution of a normal impact at this speed (clamped to 0.2-0.95).</summary>
        public float RestitutionAt(float normalSpeed)
            => MathUtil.Clamp(Restitution - RestitutionPerMetrePerSecond * (normalSpeed - ReferenceImpactSpeed), 0.2f, 0.95f);

        /// <summary>The rebound height (ball underside) of the FIFA drop test from 2.00 m on this surface, ignoring the air: e^2 x 2.00 m.</summary>
        public float DropTestRebound => Restitution * Restitution * 2.00f;

        public GroundModel With(string name, float? restitution = null, float? friction = null)
            => new GroundModel(name, restitution ?? Restitution, friction ?? Friction) { ReferenceImpactSpeed = ReferenceImpactSpeed, RestitutionPerMetrePerSecond = RestitutionPerMetrePerSecond };

        /// <summary>Natural grass, dry: e 0.632 (a 0.80 m rebound, the middle of FIFA's "excellent" 0.60-1.00 m, class D in an A range); grips (mu 0.6, class D above the grip threshold).</summary>
        public static readonly GroundModel NaturalDry = new GroundModel("natural, dry", 0.632f, 0.6f);
        /// <summary>Natural grass, wet: the same vertical rebound (the thesis: 7 cm or less of difference), sliding friction 0.22 (class C from the thesis' 74-76%).</summary>
        public static readonly GroundModel NaturalWet = new GroundModel("natural, wet", 0.632f, 0.22f);
        /// <summary>Football turf (artificial), dry: e 0.602 (0.725 m, the middle of FIFA Quality Pro's 0.60-0.85 m, class D in an A range); grips.</summary>
        public static readonly GroundModel ArtificialDry = new GroundModel("artificial, dry", 0.602f, 0.6f);
        /// <summary>Football turf, wet: the same rebound, sliding friction 0.25 (class D: inside the wet angle rebound range 45-80%).</summary>
        public static readonly GroundModel ArtificialWet = new GroundModel("artificial, wet", 0.602f, 0.25f);
    }
}

using System;
using System.Collections.Generic;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// The physical ball: a configurable size 5 football in SI units (kg, m, Pa). The reference ball is one chosen point inside the
    /// IFAB ranges, not a claim that every ball is the same. Every value says where it comes from (see docs/BALL_PHYSICS.md):
    /// A = officially standardised, B = experimentally measured, C = mathematically derived, D = an approximation or simulation
    /// assumption, E = unknown and needing calibration.
    /// </summary>
    public sealed class BallSpec
    {
        // ---- Law 2, The Ball (IFAB, https://www.theifab.com/laws/latest/the-ball/) - class A -------------------------------
        /// <summary>Law 2: at least 410 g at the start of the match (the Law says "weight"; it is a mass).</summary>
        public const float IfabMinMassKg = 0.410f;
        public const float IfabMaxMassKg = 0.450f;
        /// <summary>Law 2: circumference 68-70 cm.</summary>
        public const float IfabMinCircumferenceM = 0.680f;
        public const float IfabMaxCircumferenceM = 0.700f;
        /// <summary>
        /// Law 2: "0.6-1.1 atmosphere (600-1,100 g/cm²) at sea level". This is the inflation pressure a ball gauge reads, i.e. the
        /// pressure ABOVE the surrounding air (gauge pressure), not the absolute pressure inside the ball. The Law gives two units
        /// that differ by about 3%: 0.6 standard atmospheres = 60.8 kPa, 600 g/cm² = 58.8 kPa. The wider reading is kept here.
        /// </summary>
        public const float IfabMinGaugePressurePa = 0.600f * TechnicalAtmospherePa; // 58.8 kPa
        public const float IfabMaxGaugePressurePa = 1.100f * StandardAtmospherePa;   // 111.5 kPa

        /// <summary>One standard atmosphere, Pa (by definition, class A).</summary>
        public const float StandardAtmospherePa = 101325f;
        /// <summary>One kgf/cm² (a "technical atmosphere"), Pa (by definition, class A).</summary>
        public const float TechnicalAtmospherePa = 98066.5f;

        /// <summary>The moment-of-inertia factor of a solid uniform sphere, I = 2/5 m R² (class C).</summary>
        public const float SolidSphereInertiaFactor = 0.4f;
        /// <summary>The moment-of-inertia factor of an ideal thin spherical shell, I = 2/3 m R² (class C).</summary>
        public const float ThinShellInertiaFactor = 2f / 3f;

        /// <summary>Mass, kg. Reference 0.430 (class D: a chosen value inside the IFAB range).</summary>
        public readonly float MassKg;
        /// <summary>Circumference, m. Reference 0.690 (class D: a chosen value inside the IFAB range).</summary>
        public readonly float CircumferenceM;
        /// <summary>Inflation pressure above the surrounding air, Pa. Reference 0.85 atm = 86.1 kPa, the middle of the Law's range (class D).</summary>
        public readonly float GaugePressurePa;
        /// <summary>
        /// k in I = k m R². A real ball is a cover, panels, lining layers, a bladder and air, with most of its mass near the surface:
        /// closer to a shell than to a solid sphere, but the layers have thickness, so k is a little under 2/3. No published measured
        /// value for a size 5 ball was found (see docs/BALL_PHYSICS.md), so the reference uses the thin-shell 2/3 (class D) and the
        /// value is configurable between the two ideal models (class E until measured).
        /// </summary>
        public readonly float InertiaFactor;

        public BallSpec(float massKg, float circumferenceM, float gaugePressurePa, float inertiaFactor)
        {
            if (!(massKg > 0f)) throw new ArgumentException("the ball's mass must be positive");
            if (!(circumferenceM > 0f)) throw new ArgumentException("the ball's circumference must be positive");
            if (!(gaugePressurePa >= 0f)) throw new ArgumentException("the ball's gauge pressure cannot be negative");
            if (!(inertiaFactor >= SolidSphereInertiaFactor - 1e-6f && inertiaFactor <= ThinShellInertiaFactor + 1e-6f))
                throw new ArgumentException("the inertia factor must lie between a solid sphere (2/5) and a thin shell (2/3)");
            MassKg = massKg;
            CircumferenceM = circumferenceM;
            GaugePressurePa = gaugePressurePa;
            InertiaFactor = inertiaFactor;
        }

        /// <summary>The reference ball: 0.430 kg, 0.690 m, 0.85 atm gauge, thin-shell inertia.</summary>
        public static readonly BallSpec Reference = new BallSpec(0.430f, 0.690f, 0.85f * StandardAtmospherePa, ThinShellInertiaFactor);

        /// <summary>Equivalent spherical radius, m: R = C / 2π (class C). For the reference ball 0.1098 m.</summary>
        public float RadiusM => CircumferenceM / (2f * MathF.PI);
        /// <summary>Projected (frontal) area for drag and lift, m²: A = π R² (class C).</summary>
        public float CrossSectionAreaM2 => MathF.PI * RadiusM * RadiusM;
        /// <summary>Absolute pressure inside the ball at sea level, Pa (gauge + one standard atmosphere, class C).</summary>
        public float AbsolutePressurePa => GaugePressurePa + StandardAtmospherePa;
        /// <summary>Moment of inertia about any axis through the centre, kg·m² (I = k m R², class C from the chosen k).</summary>
        public float MomentOfInertia => InertiaFactor * MassKg * RadiusM * RadiusM;

        /// <summary>Angular momentum L = I ω, kg·m²/s (a sphere's inertia is the same about every axis, so L is parallel to ω).</summary>
        public Vec3 AngularMomentum(Vec3 angularVelocity) => angularVelocity * MomentOfInertia;
        /// <summary>Angular velocity from angular momentum, rad/s.</summary>
        public Vec3 AngularVelocity(Vec3 angularMomentum) => angularMomentum / MomentOfInertia;
        /// <summary>Rotational kinetic energy ½ I ω², J.</summary>
        public float RotationalEnergy(Vec3 angularVelocity) => 0.5f * MomentOfInertia * angularVelocity.LengthSq;
        /// <summary>Translational kinetic energy ½ m v², J.</summary>
        public float TranslationalEnergy(Vec3 velocity) => 0.5f * MassKg * velocity.LengthSq;
        /// <summary>The change in angular velocity from a torque τ (N·m) acting for dt seconds: Δω = τ dt / I.</summary>
        public Vec3 SpinChangeFromTorque(Vec3 torque, float dt) => torque * (dt / MomentOfInertia);

        /// <summary>Where this ball is outside Law 2 (empty: a legal ball). A non-legal ball is allowed for experiments; it is reported, not refused.</summary>
        public List<string> LawTwoViolations()
        {
            var v = new List<string>();
            if (MassKg < IfabMinMassKg || MassKg > IfabMaxMassKg) v.Add($"mass {MassKg:0.000} kg outside 0.410-0.450");
            if (CircumferenceM < IfabMinCircumferenceM || CircumferenceM > IfabMaxCircumferenceM) v.Add($"circumference {CircumferenceM:0.000} m outside 0.680-0.700");
            if (GaugePressurePa < IfabMinGaugePressurePa || GaugePressurePa > IfabMaxGaugePressurePa) v.Add($"gauge pressure {GaugePressurePa / 1000f:0.0} kPa outside {IfabMinGaugePressurePa / 1000f:0.0}-{IfabMaxGaugePressurePa / 1000f:0.0}");
            return v;
        }
    }
}

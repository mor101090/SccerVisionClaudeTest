using System;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// How far the ball flattens in an impact and for how long it touches (ball physics stage 9). See docs/BALL_PHYSICS.md for the classes
    /// (A standardised, B measured, C derived, D approximation, E unknown).
    ///
    /// An inflated ball pressed onto a surface stays a sphere with a flat cap (Tadrist and Darbois Texier 2017, filmed at 4000 frames a second:
    /// "the ball remains a spherical cap and no increase in radius has been noticed"). The air pushes back with its pressure on the flat area,
    /// A = 2 pi R x for a flattening x, so the ball is a spring of stiffness k = 2 pi R P (P the inflation, gauge, pressure) and touches for
    /// pi sqrt(m / k): 8.4 ms for the reference ball (class C from A: the Law's ball). The measured impacts on a rigid force plate (Iga, Nunome,
    /// Inoue and Ikegami: a match ball at 0.9 bar fired at 9-19.5 m/s, filmed at 5000 frames a second) agree: contact 8.5-7.4 ms, peak
    /// flattening 2.45-4.64 cm. A real ball stiffens as it flattens, so the contact shortens and the flattening grows a little slower than the
    /// linear spring says: the exponents below are fitted to those five measured impacts (class B).
    ///
    /// The match uses impulses (the contact, 8 ms, is shorter than the engine's 20 ms step), so none of this changes the play: it is the size of
    /// the squash the viewers draw, and the contact time is recorded for the test bench.
    /// </summary>
    public sealed class DeformationModel
    {
        /// <summary>
        /// The measured peak flattening grows as (velocity change)^0.85 (Iga et al.: 2.45 cm at a 16.3 m/s change, 4.64 cm at 33.9 m/s;
        /// the fit is within 0.1 cm at all five speeds). Class B.
        /// </summary>
        public float CompressionExponent = 0.85f;
        /// <summary>
        /// The velocity change at which the linear spring and the measurements agree, m/s (class B: the fit's own reference; with the
        /// measured restitution 0.77 it is a 12.4 m/s impact).
        /// </summary>
        public float ReferenceVelocityChange = 22f;
        /// <summary>The restitution of those measured impacts (0.80-0.74, mean 0.77), which turns a velocity change into an impact speed (class B).</summary>
        public float MeasuredRestitution = 0.77f;
        /// <summary>The measured contact time shortens as (impact speed)^-0.19 (8.5 ms at 9.05 m/s, 7.4 ms at 19.46 m/s). Class B.</summary>
        public float ContactTimeExponent = -0.19f;
        /// <summary>The impact speed at which the linear spring's contact time holds, m/s (class B: the fit of the five measured contact times, each within 0.1 ms).</summary>
        public float ContactTimeReferenceSpeed = 11.2f;
        /// <summary>
        /// The deepest flattening shown, a fraction of the radius: beyond about two thirds the ball is no longer a flat-capped sphere (it
        /// ripples, Tadrist and Darbois Texier), and the measured flattenings stay below 0.45 R. Class D.
        /// </summary>
        public float MaxCompressionFraction = 0.6f;
        /// <summary>
        /// The smallest velocity change between two steps the engine takes for a contact, m/s: flight alone (gravity and drag on a 35 m/s
        /// shot) changes the velocity by at most about 0.4 m/s a step, and a carried ball by less (class D).
        /// </summary>
        public float ContactThreshold = 2f;

        /// <summary>The ball's stiffness as a gas spring, N/m: k = 2 pi R P (class C from A).</summary>
        public static float Stiffness(BallSpec ball) => 2f * MathF.PI * ball.RadiusM * ball.GaugePressurePa;

        /// <summary>sqrt(m / k), s: the spring's time scale.</summary>
        public static float TimeScale(BallSpec ball) => MathF.Sqrt(ball.MassKg / MathF.Max(1f, Stiffness(ball)));

        /// <summary>The linear spring's contact time, pi sqrt(m / k), s: independent of the speed.</summary>
        public static float LinearContactTime(BallSpec ball) => MathF.PI * TimeScale(ball);

        /// <summary>The contact time of an impact at this normal speed, s (the linear spring's, shortened as measured).</summary>
        public float ContactTime(BallSpec ball, float normalSpeed)
        {
            float v = MathF.Max(0.5f, normalSpeed);
            return LinearContactTime(ball) * MathF.Pow(v / ContactTimeReferenceSpeed, ContactTimeExponent);
        }

        /// <summary>
        /// The peak flattening, m, of an impact that changed the ball's velocity by <paramref name="velocityChange"/> (a bounce, a kick, the frame):
        /// the linear spring's sqrt(m / k) times the impact speed, bent to the measured exponent, and capped.
        /// </summary>
        public float Compression(BallSpec ball, float velocityChange)
        {
            if (!(velocityChange > 0f)) return 0f;
            float linear = TimeScale(ball) * velocityChange / (1f + MeasuredRestitution);
            float x = linear * MathF.Pow(velocityChange / ReferenceVelocityChange, CompressionExponent - 1f);
            return MathF.Min(x, MaxCompressionFraction * ball.RadiusM);
        }

        /// <summary>The reference deformation (the values above).</summary>
        public static DeformationModel Reference => new DeformationModel();
    }
}

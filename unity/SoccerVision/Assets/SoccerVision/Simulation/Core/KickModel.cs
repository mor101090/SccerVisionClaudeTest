using System;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// What the foot gives the ball (ball physics stage 7): how much spin each kind of kick puts on it, how fast a player's foot can
    /// strike, and what a ball that is already moving adds. See docs/BALL_PHYSICS.md for the classes (A standardised, B measured,
    /// C derived, D approximation, E unknown).
    ///
    /// Spin is given as the spin parameter at launch, Sp = R w / v (the quantity the Magnus coefficient depends on), so a profile holds
    /// at any kick speed.
    ///
    /// Sources (secondary: the abstracts and other papers' summaries of them; the full texts are behind paywalls or bot walls):
    /// - Asai et al. 2004, the infront curve kick: foot 24.2 m/s, ball 27.1 m/s, 7.8 rev/s (49 rad/s): Sp = 0.20.
    /// - Hong et al. 2011 (as a later Scientific Reports article, srep06067, summarises it): curve shots 26.0 m/s and 7.2 rev/s (Sp 0.19); curve kicks spin 4-10 rev/s,
    ///   knuckleballs 0-2 rev/s.
    /// - Asai et al. 2002: a trade-off between ball speed and spin as the foot strikes further off the centre of the ball.
    /// - Lees and Nolan 1998 (through later papers): the ratio of ball speed to foot speed is the index of a good strike, 1.06-1.65 in the
    ///   literature depending on where the foot speed is measured; an ISBS 2008 study found about 1.40 for instep and side-foot alike.
    /// </summary>
    public sealed class KickModel
    {
        /// <summary>A range of launch spin parameters for one kind of kick (the kicker's draw picks a point inside it).</summary>
        public readonly struct SpinRange
        {
            public readonly float Min, Max;
            public SpinRange(float min, float max) { Min = min; Max = max; }
            /// <summary>The spin (rad/s) for a draw u in [0, 1] at this ball speed and radius.</summary>
            public float Spin(float u, float speed, float radius) => (Min + (Max - Min) * MathUtil.Clamp01(u)) * speed / radius;
        }

        /// <summary>
        /// A curled shot with the inside of the foot: Sp 0.11-0.27 is Hong et al.'s 4-10 rev/s at their 26 m/s; Asai's and Hong's means
        /// (0.20, 0.19) sit in it (class C from B).
        /// </summary>
        public SpinRange Curler = new SpinRange(0.11f, 0.27f);
        /// <summary>The outside of the foot (trivela): no measurement found; a little less than the inside (class D).</summary>
        public SpinRange OutsideFoot = new SpinRange(0.09f, 0.22f);
        /// <summary>A curled free kick: the same strike, taken by a specialist with time to set it up: the upper half of the curve-kick range (class C from B).</summary>
        public SpinRange FreeKick = new SpinRange(0.16f, 0.27f);
        /// <summary>A curled cross or pass: the same inside-foot strike at lower speed; the whole curve-kick range (class D).</summary>
        public SpinRange CurledPass = new SpinRange(0.10f, 0.25f);
        /// <summary>A chipped shot: struck under the ball, backspin (class D: no measurement of a chip's spin found; less than a curve kick's because the contact is short).</summary>
        public SpinRange ChipBackspin = new SpinRange(0.10f, 0.22f);

        /// <summary>
        /// How far a curled kick's spin is from what the kicker meant (standard deviation, a fraction of the spin): the old model's
        /// 2.5 rad/s on its 13 rad/s, kept as a relative error (class E).
        /// </summary>
        public float SpinErrorFraction = 0.19f;

        /// <summary>
        /// Ball speed / foot speed at impact. The literature's instep values are 1.06-1.65 depending on the foot point measured; 1.3 is a
        /// middle value (Asai's curve kick 1.12, the ISBS study 1.40), class C from B.
        /// </summary>
        public float BallFootSpeedRatio = 1.3f;
        /// <summary>The foot speed of the weakest striker of the ball (m/s) and what the full physical-power attribute adds (class D: elite instep kicks are struck at about 20-24 m/s).</summary>
        public float FootSpeedBase = 18f, FootSpeedPerPower = 6f;

        /// <summary>
        /// The share of an oncoming ball's speed that a full strike sends back (one-dimensional impact of an effective foot mass M with
        /// restitution e: v_out = U (1 + e) M / (M + m) - u (e M - m) / (M + m); with the ratio r = (1 + e) M / (M + m) the second
        /// coefficient is exactly r - 1, whatever e is). Class C from the measured ratio.
        /// </summary>
        public float IncomingCoupling => BallFootSpeedRatio - 1f;

        /// <summary>The fastest this player's foot can strike (m/s), from his physical power (0-100).</summary>
        public float FootSpeed(int physicalPower) => FootSpeedBase + FootSpeedPerPower * MathUtil.A(physicalPower);

        /// <summary>The fastest ball this player can strike from a still ball (m/s).</summary>
        public float MaxBallSpeed(int physicalPower) => FootSpeed(physicalPower) * BallFootSpeedRatio;

        /// <summary>
        /// What a moving ball adds to a full strike in this direction (m/s): the part of its velocity coming back at the kicker, times the
        /// coupling. A ball running away from the foot takes the same share off.
        /// </summary>
        public float IncomingBonus(Vec2 kickDirection, Vec2 incomingVelocity) => -IncomingCoupling * Vec2.Dot(kickDirection.Normalized, incomingVelocity);

        /// <summary>The reference foot (the values above).</summary>
        public static KickModel Reference => new KickModel();
    }
}

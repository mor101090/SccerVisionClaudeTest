using System;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// The throw-in as a throw (polish round 2): a release speed that comes from the thrower, a release angle and backspin from the
    /// biomechanics literature, and a flight that is the ball's physics. How far a man can throw it decides whether a long throw into
    /// the box is on, and the long-throw specialist (<see cref="PlayerTrait.LongThrowSpecialist"/>) is the man who can.
    /// </summary>
    public sealed partial class MatchEngine
    {
        // Measured (class B): male players release a throw-in at 12-19 m/s and 22-40 degrees (laboratory studies summarised by Linthorne
        // and Everett 2006, Sports Biomechanics 5(2); arXiv physics/0601149); a typical throw: 13.4 m/s at 32.1 degrees from 2.23 m, 17.0 m;
        // the optimum angle for a long throw about 30 degrees; backspin 0.4-1.3 rev/s (Bray and Kerwin 2004, cited there).

        /// <summary>The height (m) the ball leaves the hands at (class B: 2.23 m measured, about 2.3 m typical).</summary>
        public static float ThrowReleaseHeight = 2.23f;
        /// <summary>The release angle of a long throw (rad; class B: about 30 degrees is the optimum the measured player reached, 32.1 his preferred).</summary>
        public static float LongThrowReleaseAngle = 0.54f;
        /// <summary>The backspin a throw leaves the hands with (rev/s; class B: 0.4-1.3 measured, the middle).</summary>
        public static float ThrowBackspinRevPerSecond = 0.85f;
        /// <summary>The release speed of the weakest thrower (m/s; class B: the bottom of the measured 12-19).</summary>
        public static float ThrowSpeedMin = 12f;
        /// <summary>The best release speed of a player who is not a long-throw specialist (m/s; class D inside the measured range).</summary>
        public static float ThrowSpeedOrdinaryMax = 16.5f;
        /// <summary>The release speed of the best long-throw specialist (m/s; class B: the top of the measured 12-19).</summary>
        public static float ThrowSpeedMax = 19f;
        /// <summary>The height (m) a long throw is aimed to come down to at the near post: a man's head (class D).</summary>
        public static float LongThrowTargetHeight = 1.9f;
        /// <summary>How far (m) the long-throw specialist walks over to take a throw on the other side of the attack (class D).</summary>
        public static float LongThrowWalk = 40f;
        /// <summary>How often a throw that can reach the box goes long: the specialist / anyone else who can reach (class D; the rest are taken
        /// short, when the defence expects the long one).</summary>
        public static float LongThrowChanceSpecialist = 0.8f, LongThrowChanceOther = 0.4f;
        /// <summary>The spread of a release (class B: the measured player's release angle varied by 1.6 degrees and his speed by 0.3 m/s, one standard deviation).</summary>
        public static float ThrowAngleSpread = 0.028f, ThrowSpeedSpread = 0.3f;

        /// <summary>The fastest a player can release a throw-in (m/s): his physical power within the measured range; a specialist at the top of it.</summary>
        public static float MaxThrowSpeed(PlayerProfile p)
        {
            float power = MathUtil.A(p.Attributes.PhysicalPower);
            return p.Has(PlayerTrait.LongThrowSpecialist)
                ? ThrowSpeedOrdinaryMax + (ThrowSpeedMax - ThrowSpeedOrdinaryMax) * power
                : ThrowSpeedMin + (ThrowSpeedOrdinaryMax - ThrowSpeedMin) * power;
        }

        private static float ThrowTopSpin => -ThrowBackspinRevPerSecond * MathUtil.TwoPi;

        /// <summary>
        /// How far (m, along the ground) a throw released at <paramref name="speed"/> and <paramref name="angle"/> carries before it comes down to
        /// <paramref name="height"/> (still air, the throw's backspin, the stage 3 flight).
        /// </summary>
        public static float ThrowCarry(float speed, float angle, float height, MatchConfig c)
        {
            c = c.StillAir;
            var b = new BallMotion { Z = ThrowReleaseHeight, Vx = speed * MathF.Cos(angle), Vz = speed * MathF.Sin(angle), TopSpin = ThrowTopSpin };
            float dt = c.FixedStep;
            for (int i = 0; i < 600; i++)
            {
                float px = b.X, pz = b.Z;
                BallPhysics.Step(ref b, c, dt);
                if (b.Landed) return b.LandX; // (the ground inside the step: where it first touched)
                if (b.Vz < 0f && b.Z <= height)
                {
                    float f = pz > b.Z ? (pz - height) / (pz - b.Z) : 1f;
                    return px + (b.X - px) * MathUtil.Clamp01(f);
                }
            }
            return b.X;
        }

        /// <summary>The release speed that carries a throw at <paramref name="angle"/> to <paramref name="distance"/>, coming down to <paramref name="height"/>.</summary>
        public static float ThrowSpeedFor(float distance, float angle, float height, MatchConfig c)
        {
            float lo = 4f, hi = 30f;
            for (int i = 0; i < 20; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (ThrowCarry(mid, angle, height, c) < distance) lo = mid; else hi = mid;
            }
            return hi;
        }

        /// <summary>How far a player can throw a long throw to a man's head (m).</summary>
        public float LongThrowReach(PlayerAgent p) => ThrowCarry(MaxThrowSpeed(p.Profile), LongThrowReleaseAngle, LongThrowTargetHeight, Config);

        /// <summary>
        /// Where a long throw is aimed: as deep into the box as the thrower can reach, 6 m out from goal - the near corner of the six-yard box
        /// (9 m from the middle) at best, the box at least 5 m inside its side line (15 m from the middle) or not at all (class D). From the
        /// touchline that is 19-25 m: a specialist reaches the six-yard box (16.5-19 m/s: 22-28 m to a man's head), a strong ordinary thrower
        /// the wide part of the box. Real throw-ins into the box: 1.43 a match, median 24.9 m (tools/calibration/throw_ins.py).
        /// </summary>
        public static float LongThrowTargetLateral = 9f, LongThrowTargetWidest = 15f, LongThrowTargetForward = 46.5f;

        /// <summary>The point of the box this man aims a long throw at from <paramref name="spot"/>, or false if he cannot reach the box.</summary>
        private bool LongThrowTarget(PlayerAgent p, Vec2 spot, out Vec2 target)
        {
            var team = p.Team;
            float side = MathF.Sign(team.Lateral(spot));
            float reach = LongThrowReach(p);
            float df = LongThrowTargetForward - team.Forward(spot);
            float across = reach * reach - df * df; // (how far across the pitch he reaches at that depth)
            target = default;
            if (across <= 0f) return false;
            float lateral = MathF.Max(LongThrowTargetLateral, Pitch.HalfWidth - MathF.Sqrt(across) + 0.5f);
            if (lateral > LongThrowTargetWidest) return false;
            target = team.ToWorld(LongThrowTargetForward, side * lateral);
            return true;
        }

        /// <summary>Can this man throw it from <paramref name="spot"/> to the near post?</summary>
        private bool CanThrowIntoBox(PlayerAgent p, Vec2 spot) => p != null && LongThrowTarget(p, spot, out _);

        /// <summary>
        /// Releases a long throw at <paramref name="target"/>: the speed that carries it there at the long-throw angle, no faster than the man
        /// can throw (if he cannot reach, it falls short - that is the throw), with the measured spread of a release.
        /// </summary>
        private void ReleaseLongThrow(PlayerAgent taker, Vec2 from, Vec2 target)
        {
            float d = Vec2.Distance(from, target);
            float angle = LongThrowReleaseAngle + _rng.Gaussian(0f, ThrowAngleSpread);
            float need = ThrowSpeedFor(d, angle, LongThrowTargetHeight, Config);
            float max = MaxThrowSpeed(taker.Profile);
            float speed = MathF.Min(need, max) + _rng.Gaussian(0f, ThrowSpeedSpread);
            Vec2 dir = (target - from).Normalized.Rotate(_rng.Gaussian(0f, 0.035f));
            if (CollectDiagnostics)
            {
                Count("throw.long." + (taker.Has(PlayerTrait.LongThrowSpecialist) ? "specialist" : "other") + (need > max ? ".short" : ""));
                Count("throw.long.speedTenths", (int)(speed * 10f));
                Count("throw.long.distanceTenths", (int)(d * 10f));
            }
            KickBall(taker, from, dir * (speed * MathF.Cos(angle)), speed * MathF.Sin(angle), 0f, ThrowTopSpin, KickKind.Restart, null, target, true, true, ThrowReleaseHeight);
        }
    }
}

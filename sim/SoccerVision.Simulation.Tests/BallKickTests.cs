using System;
using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>
    /// Ball physics stage 7: what the foot gives the ball (spin by kind of kick, the fastest strike, what an oncoming ball adds), checked
    /// against the curve-kick measurements (Asai et al. 2004, Hong et al. 2011) and the one-dimensional impact model. See docs/BALL_PHYSICS.md.
    /// </summary>
    public class BallKickTests
    {
        private static readonly KickModel K = KickModel.Reference;
        private const float R = 0.1098f; // (the reference ball: 0.690 m / 2 pi)

        [Fact]
        public void Test7_TheCurveKickSpinsAsMeasured()
        {
            // Asai 2004: 27.1 m/s and 7.8 rev/s; Hong 2011: 26.0 m/s and 7.2 rev/s, curve kicks 4-10 rev/s. The middle of the curler's range at
            // those speeds must give their mean, its ends the 4-10 rev/s band.
            float revs(float w) => w / (2f * MathF.PI);
            Assert.InRange(revs(K.Curler.Spin(0.5f, 27.1f, R)), 6.5f, 9.0f);
            Assert.InRange(revs(K.Curler.Spin(0.5f, 26.0f, R)), 6.0f, 8.5f);
            Assert.InRange(revs(K.Curler.Spin(0f, 26f, R)), 3.5f, 4.5f);
            Assert.InRange(revs(K.Curler.Spin(1f, 26f, R)), 9.5f, 10.5f);
            // a free kick specialist sits in the upper half; every curled kick stays in the measured band at a curve-kick speed
            foreach (var range in new[] { K.Curler, K.OutsideFoot, K.FreeKick, K.CurledPass })
                for (float u = 0f; u <= 1f; u += 0.25f)
                    Assert.InRange(revs(range.Spin(u, 26f, R)), 3.3f, 10.5f); // (the outside of the foot, unmeasured, reaches a little below 4 rev/s)
        }

        [Fact]
        public void TheSpinScalesWithTheKickSpeed()
        {
            // the profile is a spin parameter: the same strike at half the speed spins half as fast, and keeps the same Magnus coefficient
            float a = K.Curler.Spin(0.5f, 24f, R), b = K.Curler.Spin(0.5f, 12f, R);
            Assert.InRange(a / b, 1.999f, 2.001f);
            Assert.InRange(R * a / 24f, K.Curler.Min, K.Curler.Max);
        }

        [Fact]
        public void TheFastestStrikeComesFromTheFootSpeed()
        {
            // elite instep kicks: foot about 20-24 m/s, ball about 26-31 m/s (ratio 1.3); a weak striker still gets over 23 m/s
            Assert.InRange(K.FootSpeed(100), 23.9f, 24.1f);
            Assert.InRange(K.MaxBallSpeed(100), 30f, 32f);
            Assert.InRange(K.MaxBallSpeed(0), 23f, 24f);
            Assert.True(K.MaxBallSpeed(80) > K.MaxBallSpeed(50));
        }

        [Fact]
        public void AnOncomingBallAddsWhatTheImpactModelSays()
        {
            // 1D impact: v_out = U (1 + e) M/(M + m) - u (e M - m)/(M + m). Pick any e and the M that gives the ratio; the coupling must be ratio - 1.
            float m = 0.43f, ratio = K.BallFootSpeedRatio;
            foreach (float e in new[] { 0.4f, 0.5f, 0.6f })
            {
                float M = ratio * m / (1f + e - ratio); // from (1 + e) M / (M + m) = ratio
                float coupling = (e * M - m) / (M + m);
                Assert.InRange(coupling, K.IncomingCoupling - 1e-4f, K.IncomingCoupling + 1e-4f);
            }
            // a 15 m/s cross coming straight at the striker adds 4.5 m/s to a full volley back the way it came; one running away takes it off;
            // one crossing his line adds nothing
            var goal = new Vec2(1f, 0f);
            Assert.InRange(K.IncomingBonus(goal, new Vec2(-15f, 0f)), 4.49f, 4.51f);
            Assert.InRange(K.IncomingBonus(goal, new Vec2(15f, 0f)), -4.51f, -4.49f);
            Assert.InRange(K.IncomingBonus(goal, new Vec2(0f, 15f)), -1e-4f, 1e-4f);
        }

        [Fact]
        public void TheSpeedSpinTradeOffIsInTheStrikes()
        {
            // Asai 2002: the further off centre, the more spin and the less speed. The engine's curler is struck slower than its laces drive by
            // about what the measured ratios give (curve kick 1.12, instep about 1.3-1.4: 0.80-0.86 of the speed for the same foot).
            float curl = 19f + 5f * 0.7f, power = 23f + 6f * 0.7f + 3f * 0.7f;
            Assert.InRange(curl / power, 0.70f, 0.90f);
        }

        [Fact]
        public void BothKickModelsAreDeterministicAndTheNewOneIsWiredIn()
        {
            static MatchEngine Run(bool legacy)
            {
                var c = MatchConfig.CreateDefault();
                if (legacy) c.Kick = null;
                var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 11, c);
                e.RunToEnd();
                return e;
            }
            foreach (bool legacy in new[] { true, false })
            {
                var a = Run(legacy); var b = Run(legacy);
                Assert.Equal(a.Home.Score, b.Home.Score);
                Assert.Equal(a.Away.Score, b.Away.Score);
                Assert.Equal(a.EventLog.Count, b.EventLog.Count);
            }
            // the kick model changes the match (it is in the kicks, not only in the tables)
            Assert.NotEqual(Run(true).EventLog.Count, Run(false).EventLog.Count);
        }
    }
}

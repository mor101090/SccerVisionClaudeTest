using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    public class PhysicsTests
    {
        private static readonly MatchConfig Config = MatchConfig.CreateDefault();

        [Fact]
        public void DroppedBallBouncesLowerAndSettles()
        {
            var b = new BallMotion { Z = 2f };
            float lastPeak = 2f, peak = 0f;
            int bounces = 0;
            bool rising = false;
            for (int i = 0; i < 2000; i++)
            {
                float before = b.Vz;
                BallPhysics.Step(ref b, Config, Config.FixedStep);
                if (before < 0f && b.Vz > 0f) { bounces++; rising = true; peak = b.Z; }
                if (rising && b.Vz > 0f) peak = System.MathF.Max(peak, b.Z);
                if (rising && b.Vz <= 0f)
                {
                    Assert.True(peak < lastPeak, "each bounce must be lower");
                    lastPeak = peak;
                    rising = false;
                }
            }
            Assert.InRange(bounces, 1, 10);
            Assert.False(b.IsAirborne);
            Assert.Equal(Pitch.BallRadius, b.Z, 3);
        }

        [Fact]
        public void GroundPassSlowsAndStops()
        {
            var b = new BallMotion { Z = Pitch.BallRadius, Vx = 15f };
            for (int i = 0; i < 5000 && b.HorizontalSpeed > 0f; i++) BallPhysics.Step(ref b, Config, Config.FixedStep);
            Assert.Equal(0f, b.HorizontalSpeed, 3);
            Assert.InRange(b.X, 20f, 60f); // a firm 15 m/s pass rolls a few tens of metres on grass
        }

        [Fact]
        public void RollTableAgreesWithTheIntegrator()
        {
            var table = new GroundRollTable(Config);
            foreach (float v0 in new[] { 6f, 12f, 20f })
            {
                var b = new BallMotion { Z = Pitch.BallRadius, Vx = v0 };
                float t = 0f;
                for (int i = 0; i < 100; i++) { BallPhysics.Step(ref b, Config, Config.FixedStep); t += Config.FixedStep; }
                Assert.InRange(table.DistanceAfterTime(v0, t), b.X - 0.3f, b.X + 0.3f);
                Assert.InRange(table.SpeedAfterDistance(v0, b.X), b.HorizontalSpeed - 0.3f, b.HorizontalSpeed + 0.3f);
            }
            float kick = table.KickSpeedFor(20f, 6f);
            Assert.InRange(table.SpeedAfterDistance(kick, 20f), 5.7f, 6.3f);
            float tt = table.TimeToDistance(kick, 20f);
            float matched = table.KickSpeedForTime(20f, tt, 1f);
            Assert.InRange(matched, kick - 0.3f, kick + 0.3f);
        }

        [Fact]
        public void LoftedKickLandsOnTarget()
        {
            foreach (float d in new[] { 15f, 30f, 50f })
            {
                float v = KickSolver.LoftSpeedFor(d, 0.5f, Config, out float flight);
                float carry = KickSolver.CarryDistance(v, 0.5f, Config, out float flight2);
                Assert.InRange(carry, d - 0.6f, d + 0.6f);
                Assert.InRange(flight, 0.5f, 4f);
                Assert.Equal(flight, flight2, 2);
            }
        }

        [Fact]
        public void ShotSolverHitsRequestedHeight()
        {
            var horizontal = new Vec2(25f, 0f);
            float vz = KickSolver.VerticalSpeedFor(horizontal, Pitch.BallRadius, 20f, 1.5f, Config);
            float z = KickSolver.HeightAtDistance(horizontal, Pitch.BallRadius, vz, 20f, Config);
            Assert.InRange(z, 1.35f, 1.65f);
        }

        [Fact]
        public void RngIsDeterministic()
        {
            var a = new Rng(42);
            var b = new Rng(42);
            for (int i = 0; i < 1000; i++) Assert.Equal(a.NextUInt(), b.NextUInt());
            var c = new Rng(43);
            Assert.NotEqual(new Rng(42).NextUInt(), c.NextUInt());
        }

        [Fact]
        public void ClockTextFollowsFootballConventions()
        {
            var clock = new MatchClock { Period = MatchPeriod.FirstHalf, Seconds = 30f };
            Assert.Equal("1'", clock.MinuteText);
            clock.Seconds = 2699f;
            Assert.Equal("45'", clock.MinuteText);
            clock.Seconds = 2700f + 75f;
            Assert.Equal("45+2'", clock.MinuteText);
            clock.Period = MatchPeriod.SecondHalf;
            clock.Seconds = 2700f + 61f;
            Assert.Equal("47'", clock.MinuteText);
            clock.Seconds = 5400f + 10f;
            Assert.Equal("90+1'", clock.MinuteText);
        }
    }
}

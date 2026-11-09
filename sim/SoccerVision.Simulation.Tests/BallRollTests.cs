using System;
using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>
    /// Ball physics stage 5: the ball on the ground (rolling resistance a + b v per surface, the slip of a landing ball dying into rolling, the
    /// spin of rolling). Checked against the ball-roll measurements of the Norwegian Building Research Institute (ISSS 2004) and FIFA's ranges,
    /// and against the conservation of angular momentum about the contact point. See docs/BALL_PHYSICS.md.
    /// </summary>
    public class BallRollTests
    {
        private const float R = Pitch.BallRadius;

        private static MatchConfig Config(GroundModel g)
        {
            var c = MatchConfig.CreateDefault();
            c.Ground = g;
            return c;
        }

        /// <summary>A ball rolling (no slip) along +X from this speed: how far it goes, and its speed after 0.5 m and 2.5 m (the UEFA / NBI timing gates).</summary>
        private static (float Distance, float GateA, float GateB, float Time) RollOut(float v0, MatchConfig c)
        {
            var b = new BallMotion { Z = R, Vx = v0, Wy = v0 / c.Ball.RadiusM, HasWorldSpin = true };
            float t = 0f, gateA = -1f, gateB = -1f;
            for (int i = 0; i < 20000 && b.HorizontalSpeed > 0f; i++)
            {
                BallPhysics.Step(ref b, c, c.FixedStep);
                t += c.FixedStep;
                if (gateA < 0f && b.X >= 0.5f) gateA = b.HorizontalSpeed;
                if (gateB < 0f && b.X >= 2.5f) gateB = b.HorizontalSpeed;
            }
            return (b.X, gateA, gateB, t);
        }

        [Fact]
        public void Test4_TheRampTestRollsAsFarAsTheMeasuredNaturalPitches()
        {
            // the 1 m ramp delivers the ball at 3.2 m/s (Gabrielsen 2004); three Norwegian natural pitches: velocity change over 2 m gates 0.68 m/s
            // (95% CI 0.62-0.74), a roll of about 6.4 m by the study's own trend
            var r = RollOut(3.2f, Config(GroundModel.NaturalDry));
            Assert.InRange(r.Distance, 6.2f, 6.6f);
            Assert.InRange(r.GateA - r.GateB, 0.62f, 0.80f);
            // and every natural profile is inside FIFA's "excellent" 4.0-10.0 m; turf inside FIFA Quality 4-10 m
            foreach (var g in new[] { GroundModel.NaturalDryShort, GroundModel.NaturalDryLong, GroundModel.NaturalWet, GroundModel.ArtificialDry, GroundModel.ArtificialWet })
                Assert.InRange(RollOut(3.2f, Config(g)).Distance, 4.0f, 10.0f);
            // the old model rolled about 4.1 m: a heavy, slow pitch
            Assert.InRange(RollOut(3.2f, Config(null)).Distance, 3.8f, 4.4f);
        }

        [Fact]
        public void Test4_EachProfileRollsTheDistanceItWasFittedTo()
        {
            foreach (var g in new[] { GroundModel.NaturalDry, GroundModel.NaturalDryShort, GroundModel.NaturalDryLong, GroundModel.NaturalWet, GroundModel.ArtificialDry, GroundModel.ArtificialWet })
                Assert.InRange(RollOut(3.2f, Config(g)).Distance, g.FittedRoll - 0.25f, g.FittedRoll + 0.25f);
        }

        [Fact]
        public void TheDecelerationFallsAsTheBallSlows()
        {
            // the IST / Kolitzus method fits dec = a + b v to timing-gate data: "the deceleration decreases the slower the ball rolls"
            var c = Config(GroundModel.NaturalDry);
            float last = 0f;
            foreach (float v in new[] { 0.5f, 2f, 5f, 10f, 15f })
            {
                float d = BallPhysics.RollingDeceleration(c, v);
                Assert.True(d > last, $"{d:0.00} m/s² at {v} m/s");
                last = d;
            }
            // the British 1985 study: 90% of natural pitches decelerate the ball 0.45-1.5 m/s² (at test speeds of a few m/s)
            Assert.InRange(BallPhysics.RollingDeceleration(c, 3f), 0.45f, 1.5f);
        }

        [Fact]
        public void Test13_ShortGrassRollsFurtherThanLongAndWetFurtherThanDry()
        {
            float Roll(GroundModel g) => RollOut(5f, Config(g)).Distance;
            Assert.True(Roll(GroundModel.NaturalDryShort) > Roll(GroundModel.NaturalDry));
            Assert.True(Roll(GroundModel.NaturalDry) > Roll(GroundModel.NaturalDryLong));
            Assert.True(Roll(GroundModel.NaturalWet) > Roll(GroundModel.NaturalDry));
            Assert.True(Roll(GroundModel.ArtificialWet) > Roll(GroundModel.ArtificialDry));
            // the weather picks the profile
            var c = MatchConfig.CreateDefault(); c.GrassLength = -1; c.ApplyConditions();
            Assert.Same(GroundModel.NaturalDryShort, c.Ground);
        }

        [Fact]
        public void ARollingBallStopsAndStaysStopped()
        {
            var c = Config(GroundModel.NaturalDry);
            var b = new BallMotion { Z = R, Vx = 12f, Wy = 12f / c.Ball.RadiusM, HasWorldSpin = true };
            for (int i = 0; i < 3000; i++) BallPhysics.Step(ref b, c, c.FixedStep);
            Assert.Equal(0f, b.HorizontalSpeed);
            float x = b.X;
            for (int i = 0; i < 100; i++) BallPhysics.Step(ref b, c, c.FixedStep);
            Assert.Equal(x, b.X);
            Assert.InRange(MathF.Abs(b.Wy), 0f, 1e-4f);
        }

        [Fact]
        public void ARollingBallHasTheSpinOfRolling()
        {
            var c = Config(GroundModel.NaturalDry);
            var b = new BallMotion { Z = R, Vx = 6f, Vy = -3f };
            for (int i = 0; i < 10; i++) BallPhysics.Step(ref b, c, c.FixedStep);
            // contact point at rest: v + w x (-R z) = 0
            Assert.InRange(b.Vx - c.Ball.RadiusM * b.Wy, -1e-4f, 1e-4f);
            Assert.InRange(b.Vy + c.Ball.RadiusM * b.Wx, -1e-4f, 1e-4f);
        }

        [Fact]
        public void ASlidingBallEndsRollingAtTheSpeedAngularMomentumAllows()
        {
            // With no rolling resistance and no air, only friction at the contact acts, and it has no moment about the contact point: the angular
            // momentum about it, m v R + I w (topspin positive), is conserved, so the ball ends rolling at v = (v0 + k R w0) / (1 + k)
            // - 3/5 of the speed for a thin shell with no spin, more with topspin, less with backspin.
            var c = MatchConfig.CreateDefault();
            c.Aero = null; c.AirDragK = 0f;
            c.Ground = GroundModel.NaturalWet.With("frictional, no resistance", rollA: 0f, rollB: 0f);
            float k = c.Ball.InertiaFactor, rr = c.Ball.RadiusM;
            foreach (float w0 in new[] { 0f, 40f, -40f })
            {
                var b = new BallMotion { Z = R, Vx = 10f, Wy = w0, HasWorldSpin = true, Sliding = true };
                int steps = 0;
                while (b.Sliding && steps++ < 2000) BallPhysics.Step(ref b, c, c.FixedStep);
                Assert.False(b.Sliding);
                float expected = (10f + k * rr * w0) / (1f + k);
                Assert.InRange(b.Vx, expected * 0.99f, expected * 1.01f);
            }
        }

        [Fact]
        public void AChipWithBackspinChecksOnLandingAndThenRolls()
        {
            // airborne -> landing -> bounces -> slip -> rolling -> rest, all in one flight
            var c = Config(GroundModel.NaturalDry);
            var b = new BallMotion { Z = R + 0.001f, Vx = 14f, Vz = 9f, Wy = -60f, HasWorldSpin = true };
            bool wasAirborne = false, landed = false, rolled = false;
            int bounces = 0;
            for (int i = 0; i < 6000; i++)
            {
                float vz = b.Vz;
                BallPhysics.Step(ref b, c, c.FixedStep);
                if (b.IsAirborne) wasAirborne = true;
                if (vz < 0f && b.Vz > 0f) bounces++;
                if (wasAirborne && !b.IsAirborne) landed = true;
                if (landed && !b.Sliding && b.Vz == 0f && b.HorizontalSpeed > 0f) rolled = true;
                if (b.HorizontalSpeed == 0f && b.Vz == 0f && !b.IsAirborne) break;
            }
            Assert.True(wasAirborne && landed && rolled, "the phases");
            Assert.InRange(bounces, 1, 10);
            Assert.Equal(0f, b.HorizontalSpeed);
        }

        [Fact]
        public void ThePassersRollTableAgreesWithTheLiveBall()
        {
            var c = Config(GroundModel.NaturalDry);
            var table = new GroundRollTable(c);
            foreach (float d in new[] { 8f, 20f, 35f })
            {
                float v0 = table.KickSpeedFor(d, 4f);
                var b = new BallMotion { Z = R, Vx = v0 };
                float t = 0f;
                while (b.X < d && t < 20f) { BallPhysics.Step(ref b, c, c.FixedStep); t += c.FixedStep; }
                Assert.InRange(b.HorizontalSpeed, 3.6f, 4.4f);                      // it arrives at the speed it was meant to
                Assert.InRange(t, table.TimeToDistance(v0, d) - 0.1f, table.TimeToDistance(v0, d) + 0.1f);
            }
        }
    }
}

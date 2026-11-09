using System;
using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>
    /// Ball physics stage 10: stability. The game's 20 ms step against a 0.1 ms one (the reference), a falling ball that ends a step just above
    /// the grass, energy in flight, extreme inputs, and a match that does not depend on how the host cuts its time. See docs/BALL_PHYSICS.md.
    /// </summary>
    public class BallStabilityTests
    {
        private static readonly MatchConfig C = MatchConfig.CreateDefault();
        private const float Fine = 0.0001f;

        /// <summary>Flies a ball to its first bounce: where it bounced, when, and how high it rose after it.</summary>
        private static (float x, float t, float apex) FirstBounce(BallMotion b, float dt)
        {
            float t = 0f, x = float.NaN, when = float.NaN, apex = 0f, prevVz = b.Vz;
            bool bounced = false;
            while (t < 10f)
            {
                float x0 = b.X;
                BallPhysics.Step(ref b, C, dt);
                t += dt;
                if (!bounced && prevVz < 0f && b.Vz > 0f)
                {
                    bounced = true;
                    x = x0; when = t; // (where the step that bounced began: within one step of the touch at either dt)
                }
                else if (bounced)
                {
                    apex = MathF.Max(apex, b.Z);
                    if (b.Vz < 0f && b.Z <= Pitch.BallRadius + 0.001f) break;
                }
                prevVz = b.Vz;
            }
            return (x, when, apex);
        }

        public static TheoryData<string, float, float, float, float> Launches => new TheoryData<string, float, float, float, float>
        {
            // name, speed (m/s), elevation (rad), backspin about y (rad/s, + = topspin for +x), start height
            { "drop from 2 m", 0f, 0f, 0f, 2f },
            { "long ball 25 m/s at 35 deg", 25f, 0.61f, 15f, 0f },
            { "driven shot 30 m/s at 8 deg", 30f, 0.14f, 0f, 0f },
            { "chip 14 m/s at 50 deg with backspin", 14f, 0.87f, -25f, 0f },
        };

        [Theory]
        [MemberData(nameof(Launches))]
        public void TheGameStepLandsAndBouncesWhereAFineStepDoes(string name, float speed, float elevation, float spinY, float height)
        {
            var m = new BallMotion
            {
                Z = Pitch.BallRadius + height, Vx = speed * MathF.Cos(elevation), Vz = speed * MathF.Sin(elevation),
                Wy = spinY, HasWorldSpin = true,
            };
            var game = FirstBounce(m, C.FixedStep);
            var fine = FirstBounce(m, Fine);
            // the step that bounced starts within one step's travel of the touch, at both steps
            float travel = speed * MathF.Cos(elevation) * C.FixedStep;
            Assert.InRange(game.x, fine.x - travel - 0.05f, fine.x + 0.05f);
            Assert.InRange(game.t, fine.t - C.FixedStep - 0.001f, fine.t + C.FixedStep + 0.001f);
            // and it rises as high after the bounce: within 3% (before stage 10: 5-10% low)
            Assert.InRange(game.apex, fine.apex * 0.97f, fine.apex * 1.03f);
            Assert.False(string.IsNullOrEmpty(name));
        }

        [Fact]
        public void AFallingBallThatEndsAStepJustAboveTheGrassStillBounces()
        {
            // 0.3 mm above the grass and falling at 5 m/s: before stage 10 it counted as rolling and lost its bounce
            var b = new BallMotion { Z = Pitch.BallRadius + 0.0003f, Vx = 3f, Vz = -5f };
            BallPhysics.Step(ref b, C, C.FixedStep);
            Assert.True(b.Vz > 1f, "it bounces: " + b.Vz);
            // and at a fine step, where every landing passes through that last half millimetre, it bounces too
            var f = new BallMotion { Z = 1.5f, Vz = 0f };
            float top = 0f;
            bool down = false;
            for (int i = 0; i < 20000; i++)
            {
                BallPhysics.Step(ref f, C, Fine);
                if (f.Vz < 0f) down = true;
                if (down && f.Vz > 0f) top = MathF.Max(top, f.Z);
            }
            Assert.True(top > 0.5f, "a 1.4 m drop bounces back up at a fine step: " + top);
        }

        [Theory]
        [InlineData(4f)]
        [InlineData(10f)]
        [InlineData(20f)]
        public void ARollingBallStopsWhereAFineStepStopsIt(float speed)
        {
            static float Stop(float v, float dt)
            {
                var b = new BallMotion { Z = Pitch.BallRadius, Vx = v };
                for (float t = 0f; t < 60f && b.Vx > 0.01f; t += dt) BallPhysics.Step(ref b, C, dt);
                return b.X;
            }
            float game = Stop(speed, C.FixedStep), fine = Stop(speed, Fine);
            // within 0.4% (before stage 10: about 0.7% short)
            Assert.InRange(game, fine * 0.996f, fine * 1.004f);
        }

        [Fact]
        public void FlightInStillAirNeverGainsEnergy()
        {
            // drag takes energy and the Magnus force does no work: kinetic + potential energy must not grow from one step to the next
            var rng = new Rng(7);
            float g = C.Gravity;
            for (int k = 0; k < 400; k++)
            {
                float v = rng.Range(5f, 35f), el = rng.Range(0.05f, 1.1f), az = rng.Range(-3.1f, 3.1f);
                var b = new BallMotion
                {
                    Z = Pitch.BallRadius + rng.Range(0f, 3f), Vx = v * MathF.Cos(el) * MathF.Cos(az), Vy = v * MathF.Cos(el) * MathF.Sin(az), Vz = v * MathF.Sin(el),
                    Wx = rng.Range(-60f, 60f), Wy = rng.Range(-60f, 60f), Wz = rng.Range(-60f, 60f), HasWorldSpin = true,
                };
                float E(BallMotion m) => 0.5f * (m.Vx * m.Vx + m.Vy * m.Vy + m.Vz * m.Vz) + g * m.Z;
                for (int i = 0; i < 400 && b.Z > Pitch.BallRadius + 0.3f || i < 2; i++)
                {
                    float before = E(b);
                    BallPhysics.Step(ref b, C, C.FixedStep);
                    if (b.Z <= Pitch.BallRadius + 0.3f) break; // (the ground is the bounce's business)
                    Assert.True(E(b) <= before + 1e-3f * before + 1e-3f, $"launch {k} step {i}: {before} -> {E(b)}");
                }
            }
        }

        [Fact]
        public void ExtremeBallsStayFiniteAndComeToRest()
        {
            var cases = new[]
            {
                new BallMotion { Z = 30f, Vx = 60f, Vy = -20f, Vz = 25f, Wx = 300f, Wy = -300f, Wz = 300f, HasWorldSpin = true },
                new BallMotion { Z = Pitch.BallRadius, Vx = 0.001f },
                new BallMotion { Z = Pitch.BallRadius + 0.00001f, Vz = -0.00001f },
                new BallMotion { Z = Pitch.BallRadius, Vx = 45f, Wz = 500f, SideSpin = 40f, TopSpin = -40f },
            };
            foreach (var start in cases)
            {
                var b = start;
                for (int i = 0; i < 6000; i++)
                {
                    BallPhysics.Step(ref b, C, C.FixedStep);
                    Assert.True(float.IsFinite(b.X) && float.IsFinite(b.Y) && float.IsFinite(b.Z) && float.IsFinite(b.Vx) && float.IsFinite(b.Vz) && float.IsFinite(b.Wx) && float.IsFinite(b.Wz));
                    Assert.True(b.Z >= Pitch.BallRadius - 1e-4f, "never under the grass: " + b.Z);
                }
                Assert.True(b.Speed < 0.05f, "comes to rest in two minutes: " + b.Speed);
            }
        }

        [Fact]
        public void TheMatchDoesNotDependOnHowTheHostCutsItsTime()
        {
            // the browser advances by a frame (1/60 s), the server by a quarter second, a test by whole seconds: one match
            static MatchEngine Run(float realStep)
            {
                var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 21, MatchConfig.CreateDefault());
                e.RunToEnd(realStep);
                return e;
            }
            var a = Run(0.25f);
            foreach (float cut in new[] { 1f / 60f, 1f, 0.3f })
            {
                var b = Run(cut);
                Assert.Equal(a.Home.Score, b.Home.Score);
                Assert.Equal(a.Away.Score, b.Away.Score);
                Assert.Equal(a.EventLog.Count, b.EventLog.Count);
                Assert.Equal(a.StepCount, b.StepCount);
            }
        }
    }
}

using System;
using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>
    /// Ball physics stage 4: the bounce (an impulse at the contact point: restitution by impact speed and surface, Coulomb friction, spin in
    /// and out). Checked against the FIFA test methods and requirements where they exist (Test Manual I 2024, Natural-Pitch Rating System 2022)
    /// and against conservation laws elsewhere. See docs/BALL_PHYSICS.md.
    /// </summary>
    public class BallBounceTests
    {
        private static MatchConfig Config(GroundModel g)
        {
            var c = MatchConfig.CreateDefault();
            c.Ground = g;
            return c;
        }

        /// <summary>Drops the ball from this height (its underside) and returns the height (underside) of the next apex.</summary>
        private static float DropRebound(float height, MatchConfig c)
        {
            float r = Pitch.BallRadius;
            var b = new BallMotion { Z = height + r };
            bool bounced = false;
            float apex = 0f;
            for (int i = 0; i < 2000; i++)
            {
                float vzBefore = b.Vz;
                BallPhysics.Step(ref b, c, c.FixedStep);
                if (!bounced && vzBefore < 0f && b.Vz > 0f) bounced = true;
                if (bounced) { apex = MathF.Max(apex, b.Z - r); if (b.Vz < 0f) break; }
            }
            return apex;
        }

        [Fact]
        public void Test2_TheFifaDropTestReboundsInsideTheExcellentRange()
        {
            // FIFA Test Method 2024-01: dropped from 2.00 m (underside); a natural pitch rated "excellent" rebounds it 0.60-1.00 m
            float natural = DropRebound(2.00f, Config(GroundModel.NaturalDry));
            Assert.InRange(natural, 0.60f, 1.00f);
            // football turf: FIFA Quality Pro 0.60-0.85 m
            float turf = DropRebound(2.00f, Config(GroundModel.ArtificialDry));
            Assert.InRange(turf, 0.60f, 0.85f);
            // the vertical rebound hardly changes when the grass is wet (Thomas 2024: 7 cm or less)
            Assert.InRange(MathF.Abs(DropRebound(2.00f, Config(GroundModel.NaturalWet)) - natural), 0f, 0.07f);
        }

        [Fact]
        public void Test2_AHarderImpactBouncesRelativelyLower()
        {
            var c = Config(GroundModel.NaturalDry);
            float last = 2f;
            foreach (float h in new[] { 0.5f, 1f, 2f, 4f, 8f })
            {
                float ratio = DropRebound(h, c) / h;
                Assert.True(ratio < last, $"rebound ratio {ratio:0.000} from {h} m");
                last = ratio;
            }
        }

        [Fact]
        public void Test3_RepeatedBouncesLoseEnergyAndTheBallComesToRest()
        {
            var c = Config(GroundModel.NaturalDry);
            float r = Pitch.BallRadius;
            var b = new BallMotion { Z = 3f + r, Vx = 2f };
            int bounces = 0;
            float lastApex = 3f, apex = 0f, t = 0f;
            bool rising = false;
            for (int i = 0; i < 1000; i++)
            {
                float vz = b.Vz;
                BallPhysics.Step(ref b, c, c.FixedStep);
                t += c.FixedStep;
                if (vz < 0f && b.Vz > 0f) { bounces++; rising = true; apex = 0f; }
                if (rising) apex = MathF.Max(apex, b.Z - r);
                if (rising && b.Vz < 0f)
                {
                    Assert.True(apex < lastApex * 0.6f, $"bounce {bounces}: {apex:0.000} m after {lastApex:0.000} m");
                    lastApex = apex;
                    rising = false;
                }
                if (b.Z <= r + 1e-4f && b.Vz == 0f) break; // rolling: done bouncing
            }
            Assert.True(b.Vz == 0f && b.Z <= r + 1e-4f, "still bouncing after 20 s");
            Assert.InRange(bounces, 3, 10);
            Assert.True(t < 5f, $"bounced for {t:0.0} s");
        }

        private static (float Ratio, BallMotion After) AngleRebound(MatchConfig c, Vec3 spin)
        {
            // FIFA Test Method 2024-14: 50 km/h at 30 degrees to the surface, under 3 rev/s of spin; the result is the speed after / before
            float v = 50f / 3.6f, a = 30f * MathF.PI / 180f, r = Pitch.BallRadius;
            var b = new BallMotion { Z = r + 0.02f, Vx = v * MathF.Cos(a), Vz = -v * MathF.Sin(a), Wx = spin.X, Wy = spin.Y, Wz = spin.Z, HasWorldSpin = true };
            float before = b.Speed;
            for (int i = 0; i < 20; i++)
            {
                before = b.Speed;
                float vz = b.Vz;
                BallPhysics.Step(ref b, c, c.FixedStep);
                if (vz < 0f && b.Vz > 0f) break;
            }
            return (b.Speed / before, b);
        }

        [Fact]
        public void TheFifaAngleReboundFallsInTheRangeAndWetGrassSlides()
        {
            var dry = AngleRebound(Config(GroundModel.NaturalDry), Vec3.Zero);
            var wet = AngleRebound(Config(GroundModel.NaturalWet), Vec3.Zero);
            // FIFA: dry 45-70%, wet 45-80%; Thomas 2024 on natural grass: 56% and 69% dry, 74-76% lightly irrigated
            Assert.InRange(dry.Ratio, 0.50f, 0.70f);
            Assert.InRange(wet.Ratio, 0.70f, 0.80f);
            Assert.True(wet.Ratio > dry.Ratio + 0.08f);
            var turf = AngleRebound(Config(GroundModel.ArtificialDry), Vec3.Zero);
            Assert.InRange(turf.Ratio, 0.45f, 0.70f);
        }

        [Fact]
        public void ADryBounceGripsAndSendsTheBallOnWithTopspin()
        {
            var c = Config(GroundModel.NaturalDry);
            var (_, after) = AngleRebound(c, Vec3.Zero);
            // travelling +X, topspin is w along +Y; gripped: the bottom of the ball does not slip (v_x = R w_y)
            Assert.True(after.Wy > 10f, $"w_y = {after.Wy:0.0} rad/s");
            Assert.InRange(after.Vx - c.Ball.RadiusM * after.Wy, -0.02f, 0.02f); // (the rest of that step's flight: drag and spin decay)
        }

        [Fact]
        public void Test6And7_BackspinChecksTheBallAndTopspinKicksItOn()
        {
            var c = Config(GroundModel.NaturalDry);
            var none = AngleRebound(c, Vec3.Zero).After;
            var back = AngleRebound(c, new Vec3(0f, -60f, 0f)).After;
            var top = AngleRebound(c, new Vec3(0f, 60f, 0f)).After;
            Assert.True(back.Vx < none.Vx - 1f, $"after the bounce: backspin {back.Vx:0.0} m/s, none {none.Vx:0.0} m/s");
            Assert.True(top.Vx > none.Vx + 1f, $"after the bounce: topspin {top.Vx:0.0} m/s, none {none.Vx:0.0} m/s");
            // the vertical part is about the same: friction acts along the ground (the last centimetres of flight differ a little with the spin)
            Assert.InRange(back.Vz - none.Vz, -0.1f, 0.1f);
        }

        [Fact]
        public void SidespinAboutTheVerticalSurvivesTheBounce()
        {
            var c = Config(GroundModel.NaturalDry);
            var after = AngleRebound(c, new Vec3(0f, 0f, 40f)).After;
            Assert.InRange(after.Wz, 39f, 40.01f); // (only the flight's own decay takes it)
        }

        [Fact]
        public void ABounceNeverAddsEnergy()
        {
            var c = Config(GroundModel.NaturalDry);
            var ball = c.Ball;
            var rng = new Random(7);
            for (int n = 0; n < 200; n++)
            {
                var b = new BallMotion
                {
                    Z = Pitch.BallRadius, Vx = (float)(rng.NextDouble() * 40 - 20), Vy = (float)(rng.NextDouble() * 40 - 20), Vz = -(float)(1.5 + rng.NextDouble() * 15),
                    Wx = (float)(rng.NextDouble() * 160 - 80), Wy = (float)(rng.NextDouble() * 160 - 80), Wz = (float)(rng.NextDouble() * 160 - 80), HasWorldSpin = true,
                };
                float E(BallMotion m) => ball.TranslationalEnergy(new Vec3(m.Vx, m.Vy, m.Vz)) + ball.RotationalEnergy(new Vec3(m.Wx, m.Wy, m.Wz));
                float before = E(b);
                BallPhysics.Bounce(ref b, c);
                Assert.True(E(b) <= before * 1.0001f, $"energy {before:0.00} -> {E(b):0.00} J");
            }
        }

        [Fact]
        public void Test13_TheSurfacesDiffer()
        {
            float nat = DropRebound(2f, Config(GroundModel.NaturalDry)), turf = DropRebound(2f, Config(GroundModel.ArtificialDry));
            Assert.True(turf < nat, "football turf takes a little more out of the vertical bounce");
            // snow (through the weather) all but kills the bounce
            var snow = MatchConfig.CreateDefault();
            snow.Conditions.Weather = Weather.Snow;
            snow.ApplyConditions();
            Assert.True(DropRebound(2f, snow) < 0.5f * nat);
            // rain switches the ground to its wet profile
            var rain = MatchConfig.CreateDefault();
            rain.Conditions.Weather = Weather.Rain;
            rain.ApplyConditions();
            Assert.Same(GroundModel.NaturalWet, rain.Ground);
            var art = MatchConfig.CreateDefault();
            art.ArtificialTurf = true;
            art.ApplyConditions();
            Assert.Same(GroundModel.ArtificialDry, art.Ground);
        }

        [Fact]
        public void TheLegacyBounceIsStillThere()
        {
            var c = Config(null);
            float h = DropRebound(2f, c);
            Assert.InRange(h, 0.50f, 0.62f); // e 0.55 on every impact: 0.55^2 x 2 m = 0.605 m (less a little air)
        }
    }
}

using System;
using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>
    /// Ball physics, TEST 1 (ball specifications) and stage 2 (moment of inertia). The reference ball must be a legal size 5 ball
    /// (IFAB Law 2) and every derived quantity must follow from its definition. See docs/BALL_PHYSICS.md.
    /// </summary>
    public class BallSpecTests
    {
        private static readonly BallSpec Ball = BallSpec.Reference;

        [Fact]
        public void Test1_TheReferenceBallIsTheChosenOneAndLegal()
        {
            Assert.Equal(0.430f, Ball.MassKg, 6);
            Assert.Equal(0.690f, Ball.CircumferenceM, 6);
            Assert.Empty(Ball.LawTwoViolations());
            Assert.Same(Ball, MatchConfig.CreateDefault().Ball);
        }

        [Fact]
        public void Test1_RadiusAndAreaFollowFromTheCircumference()
        {
            Assert.InRange(Ball.RadiusM, 0.10981f, 0.10983f);                 // 0.690 / 2π = 0.10982 m
            Assert.Equal(Ball.CircumferenceM, 2f * MathF.PI * Ball.RadiusM, 5);
            Assert.InRange(Ball.CrossSectionAreaM2, 0.03788f, 0.03790f);       // π R² = 0.03789 m²
            // the engine's geometric radius (Pitch.BallRadius, used for contact with the ground and the posts) is within 0.5 mm of it
            Assert.True(MathF.Abs(Pitch.BallRadius - Ball.RadiusM) < 0.0005f, $"Pitch.BallRadius {Pitch.BallRadius} v {Ball.RadiusM}");
        }

        [Fact]
        public void Test1_PressureIsGaugePressureAndTheLawsRangeIsKept()
        {
            // 0.85 atm above the surrounding air; inside the ball the absolute pressure is that plus one atmosphere
            Assert.Equal(86126f, Ball.GaugePressurePa, 0);
            Assert.Equal(Ball.GaugePressurePa + 101325f, Ball.AbsolutePressurePa, 0);
            Assert.InRange(BallSpec.IfabMinGaugePressurePa, 58800f, 58900f);   // 600 g/cm² (the lower of the Law's two readings of 0.6)
            Assert.InRange(BallSpec.IfabMaxGaugePressurePa, 111400f, 111500f); // 1.1 standard atmospheres
        }

        [Fact]
        public void Test1_AnIllegalBallIsReportedNotRefused()
        {
            var light = new BallSpec(0.380f, 0.690f, 0.85f * BallSpec.StandardAtmospherePa, BallSpec.ThinShellInertiaFactor);
            Assert.Contains(light.LawTwoViolations(), s => s.StartsWith("mass"));
            var flat = new BallSpec(0.430f, 0.690f, 0.3f * BallSpec.StandardAtmospherePa, BallSpec.ThinShellInertiaFactor);
            Assert.Contains(flat.LawTwoViolations(), s => s.StartsWith("gauge pressure"));
            Assert.Throws<ArgumentException>(() => new BallSpec(0f, 0.69f, 80000f, 0.6f));
        }

        [Fact]
        public void Stage2_MomentOfInertiaIsConfigurableBetweenTheTwoIdealModels()
        {
            float mr2 = Ball.MassKg * Ball.RadiusM * Ball.RadiusM;
            Assert.Equal(2f / 3f * mr2, Ball.MomentOfInertia, 7);                // reference: thin shell, 0.003457 kg·m²
            Assert.InRange(Ball.MomentOfInertia, 0.003456f, 0.003458f);
            var solid = new BallSpec(0.430f, 0.690f, 86126f, BallSpec.SolidSphereInertiaFactor);
            Assert.Equal(0.4f * mr2, solid.MomentOfInertia, 7);
            Assert.Throws<ArgumentException>(() => new BallSpec(0.430f, 0.690f, 86126f, 0.3f));
            Assert.Throws<ArgumentException>(() => new BallSpec(0.430f, 0.690f, 86126f, 0.7f));
        }

        [Fact]
        public void Stage2_AngularMomentumAndEnergyAreThreeDimensional()
        {
            var w = new Vec3(3f, -4f, 12f);                                       // |ω| = 13 rad/s about a tilted axis
            var l = Ball.AngularMomentum(w);
            Assert.Equal(13f * Ball.MomentOfInertia, l.Length, 6);
            Assert.Equal(0f, Vec3.Cross(l, w).Length, 6);                        // parallel: a sphere's inertia is the same about every axis
            var back = Ball.AngularVelocity(l);
            Assert.Equal(w.X, back.X, 5); Assert.Equal(w.Y, back.Y, 5); Assert.Equal(w.Z, back.Z, 5);
            Assert.Equal(0.5f * Ball.MomentOfInertia * 169f, Ball.RotationalEnergy(w), 6);
            // a torque of 0.01 N·m about Z for 0.1 s adds τ dt / I rad/s about Z
            var dw = Ball.SpinChangeFromTorque(new Vec3(0f, 0f, 0.01f), 0.1f);
            Assert.Equal(0.001f / Ball.MomentOfInertia, dw.Z, 4);
            Assert.Equal(0f, dw.X, 6);
            // a shot at 30 m/s with 10 rev/s of spin: the spin carries a few per cent of the energy (shell 2/3 m R²)
            float trans = Ball.TranslationalEnergy(new Vec3(30f, 0f, 0f)), rot = Ball.RotationalEnergy(new Vec3(0f, 0f, 20f * MathF.PI));
            Assert.InRange(rot / trans, 0.03f, 0.05f);
        }
    }
}

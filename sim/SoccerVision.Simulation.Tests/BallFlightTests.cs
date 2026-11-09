using System;
using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>
    /// Ball physics stage 3: the flight model (drag by Reynolds number and spin, vector Magnus force, 3D spin). Where a measured
    /// result exists (Goff and Carre, Am. J. Phys. 77 (2009) 1020) the model is checked against it; the rest are checks of direction,
    /// symmetry and conservation that no choice of coefficient can fake. See docs/BALL_PHYSICS.md.
    /// </summary>
    public class BallFlightTests
    {
        private const float R = Pitch.BallRadius;

        private sealed record Flight(float Range, float Lateral, float Apex, float Time, float EndSpeed, BallMotion End, BallMotion Impact);

        /// <summary>Flies a ball from (0, 0, z0) at this speed and elevation along +X, with this world spin, to its first ground contact.</summary>
        private static Flight Fly(float speed, float elevationDeg, Vec3 spin, float z0 = 0.85f, MatchConfig c = null)
        {
            c ??= MatchConfig.CreateDefault();
            float el = elevationDeg * MathF.PI / 180f;
            var b = new BallMotion { X = 0f, Y = 0f, Z = z0, Vx = speed * MathF.Cos(el), Vz = speed * MathF.Sin(el), Wx = spin.X, Wy = spin.Y, Wz = spin.Z, HasWorldSpin = true };
            float t = 0f, apex = z0;
            var impact = b;
            for (int i = 0; i < 5000; i++)
            {
                impact = b;
                BallPhysics.Step(ref b, c, c.FixedStep);
                t += c.FixedStep;
                apex = MathF.Max(apex, b.Z);
                if (b.Z <= R + 1e-4f && t > 0.05f) break;
            }
            return new Flight(b.X, b.Y, apex, t, b.Speed, b, impact);
        }

        private static float EnergyPerMass(BallMotion b, MatchConfig c) => 0.5f * b.Speed * b.Speed + c.Gravity * b.Z;

        [Fact]
        public void TheReynoldsNumberAgreesWithGoffAndCarre()
        {
            // Goff and Carre (2009), Eq. 2: Re x 1e-5 = v / (7 m/s) for their ball (D = 0.218 m, nu = 1.54e-5 m²/s)
            var c = MatchConfig.CreateDefault();
            foreach (float v in new[] { 7f, 14f, 21f, 28f })
            {
                float re = AeroModel.ReynoldsNumber(c.AirDensity, v, 2f * c.Ball.RadiusM, c.AirDynamicViscosity);
                Assert.InRange(re * 1e-5f, v / 7f * 0.97f, v / 7f * 1.03f);
            }
        }

        [Fact]
        public void TheDragCoefficientFallsThroughTheCrisisBetween9And17MetresPerSecond()
        {
            var a = AeroModel.Reference;
            var c = MatchConfig.CreateDefault();
            float Re(float v) => AeroModel.ReynoldsNumber(c.AirDensity, v, 2f * c.Ball.RadiusM, c.AirDynamicViscosity);
            // before the crisis: about 0.45 (both wind-tunnel series in Goff and Carre Fig. 6); after it 0.15-0.25
            Assert.InRange(a.Cd0(Re(6f)), 0.42f, 0.48f);
            Assert.InRange(a.Cd0(Re(25f)), 0.15f, 0.23f);
            Assert.True(a.Cd0(Re(8f)) > 2f * a.Cd0(Re(20f)), "the ball drags more than twice as hard just before the crisis as after it");
            // monotone through the crisis
            float last = a.Cd0(Re(8f));
            for (float v = 8.5f; v <= 16f; v += 0.5f) { float cd = a.Cd0(Re(v)); Assert.True(cd <= last + 1e-6f, $"Cd rose at {v} m/s"); last = cd; }
            // spin adds drag (Fig. 7): about +0.45 per unit of the spin parameter
            Assert.InRange(a.Cd(Re(25f), 0.3f) - a.Cd(Re(25f), 0f), 0.12f, 0.15f);
        }

        [Fact]
        public void Test5_ThePowerfulShotAgreesWithAMeasuredNoSpinTrajectory()
        {
            // Goff and Carre: launched at v0 about 18 m/s, about 22 degrees, from about 0.85 m, no spin: measured range 21.9 m (a constant Cd of 0.2
            // fits, they say). The launch values are quoted as approximate: 0.1 m/s is 0.25 m of range, and integrating their own stated numbers with
            // Cd = 0.2 gives 21.4 m, so the check is +-1 m of the measurement, not a fit.
            var f = Fly(18f, 22f, Vec3.Zero);
            Assert.InRange(f.Range, 20.9f, 22.9f);
            // the drag is real: a vacuum flight from the same height would carry about 2 m further
            float vx = 18f * MathF.Cos(22f * MathF.PI / 180f), vz = 18f * MathF.Sin(22f * MathF.PI / 180f);
            float vac = vx * (vz + MathF.Sqrt(vz * vz + 2f * 9.81f * (0.85f - R))) / 9.81f;
            Assert.True(f.Range < vac - 1.5f, $"range {f.Range:0.0} m v vacuum {vac:0.0} m");
        }

        [Fact]
        public void TheTerminalVelocityOfAFallingBallIsAbout30MetresPerSecond()
        {
            var c = MatchConfig.CreateDefault();
            var b = new BallMotion { X = 0f, Y = 0f, Z = 500f };
            for (int i = 0; i < 20000 && b.Z > 5f; i++) BallPhysics.Step(ref b, c, c.FixedStep);
            Assert.InRange(-b.Vz, 28f, 34f); // sqrt(2 m g / (rho A Cd)) with Cd about 0.19: 31 m/s
        }

        [Fact]
        public void Test6_BackspinLiftsTheBallAndTopspinDipsIt()
        {
            // travelling +X: topspin is w along +Y (the top of the ball moves forward), backspin along -Y
            float w = 60f; // rad/s, Sp = 0.20 at 33 m/s
            var none = Fly(30f, 15f, Vec3.Zero);
            var back = Fly(30f, 15f, new Vec3(0f, -w, 0f));
            var top = Fly(30f, 15f, new Vec3(0f, w, 0f));
            Assert.True(back.Apex > none.Apex + 0.3f, $"apex: backspin {back.Apex:0.00} m, none {none.Apex:0.00} m");
            Assert.True(back.Time > none.Time, "backspin hangs longer");
            Assert.True(top.Apex < none.Apex - 0.3f, $"apex: topspin {top.Apex:0.00} m, none {none.Apex:0.00} m");
            Assert.True(top.Time < none.Time, "topspin drops sooner");
            Assert.True(top.Range < none.Range && none.Range < back.Range + 12f, "topspin shortens the flight");
            // pure back- or topspin makes no sideways force
            Assert.InRange(MathF.Abs(back.Lateral), 0f, 0.01f);
            Assert.InRange(MathF.Abs(top.Lateral), 0f, 0.01f);
        }

        [Fact]
        public void Test7_TopspinDropsTheBallBelowTheNoSpinPath()
        {
            // (the ground behaviour of topspin is in BallBounceTests; here the flight: the ball is already lower 12 m out, and by more further on)
            float HeightAt(float x, Vec3 w)
            {
                var c = MatchConfig.CreateDefault();
                var b = new BallMotion { Z = 0.5f, Vx = 25f * MathF.Cos(0.35f), Vz = 25f * MathF.Sin(0.35f), Wx = w.X, Wy = w.Y, Wz = w.Z, HasWorldSpin = true };
                for (int i = 0; i < 400 && b.X < x; i++) BallPhysics.Step(ref b, c, c.FixedStep);
                return b.Z;
            }
            // (both points before the topspin ball comes down, so the bounce plays no part)
            float none15 = HeightAt(12f, Vec3.Zero), top15 = HeightAt(12f, new Vec3(0f, 70f, 0f));
            float none25 = HeightAt(20f, Vec3.Zero), top25 = HeightAt(20f, new Vec3(0f, 70f, 0f));
            Assert.True(top25 > Pitch.BallRadius + 0.05f, $"the topspin ball is still in the air 20 m out ({top25:0.00} m)");
            Assert.True(top15 < none15 - 0.25f, $"12 m out: topspin {top15:0.00} m, none {none15:0.00} m");
            Assert.True(none25 - top25 > none15 - top15, $"the gap widens: {none15 - top15:0.00} m at 12 m, {none25 - top25:0.00} m at 20 m");
        }

        [Fact]
        public void Test8_SidespinCurvesTheBallToTheSideItSpinsToAndIsSymmetric()
        {
            // counter-clockwise from above (w along +Z) with the ball travelling +X: the force points to +Y (the ball's left)
            var left = Fly(30f, 8f, new Vec3(0f, 0f, 60f), z0: R + 0.001f);
            var right = Fly(30f, 8f, new Vec3(0f, 0f, -60f), z0: R + 0.001f);
            Assert.True(left.Lateral > 1.0f, $"left lateral {left.Lateral:0.00} m (range {left.Range:0.0})");
            Assert.True(right.Lateral < -1.0f, $"right lateral {right.Lateral:0.00} m");
            Assert.InRange(left.Lateral + right.Lateral, -0.02f, 0.02f);                 // mirror images
            Assert.InRange(left.Range - right.Range, -0.02f, 0.02f);
            // the curve grows with the spin, but less than in proportion: the Magnus coefficient flattens near 0.3 beyond Sp 0.3 (Goff and Carre Fig. 8)
            var more = Fly(30f, 8f, new Vec3(0f, 0f, 120f), z0: R + 0.001f);
            Assert.True(more.Lateral > left.Lateral && more.Lateral < 2f * left.Lateral, $"lateral {left.Lateral:0.00} m at 60 rad/s, {more.Lateral:0.00} m at 120");
        }

        [Fact]
        public void AFreeKickLikeBecksHamsCurvesAboutAsMuchAsGoffAndCarreEstimated()
        {
            // Goff and Carre: Beckham's 2001 free kick left his foot at about 36 m/s with about 63 rad/s of spin (Sp 0.19), flew 27 m, and curved
            // about 3 m (a rough estimate that gives a side-force coefficient of 0.2; the wind-tunnel value at Sp 0.19 is about 0.25).
            var f = Fly(36f, 12f, new Vec3(0f, 0f, 63f), z0: R + 0.001f);
            // measure the sideways deviation when the ball has gone 27 m along the pitch
            var c = MatchConfig.CreateDefault();
            var b = new BallMotion { Z = R + 0.001f, Vx = 36f * MathF.Cos(12f * MathF.PI / 180f), Vz = 36f * MathF.Sin(12f * MathF.PI / 180f), Wz = 63f, HasWorldSpin = true };
            float t = 0f;
            while (b.X < 27f && t < 3f) { BallPhysics.Step(ref b, c, c.FixedStep); t += c.FixedStep; }
            Assert.InRange(b.Y, 2.5f, 6.5f);
            Assert.True(f.Range > 27f);
        }

        [Fact]
        public void SpinAboutTheLineOfFlightMakesNoSidewaysForce()
        {
            // a rifled ball: w along the velocity; the Magnus force is zero (w x v = 0)
            // (gravity off, or the falling ball tilts away from its own spin axis and is no longer rifled)
            var c = MatchConfig.CreateDefault();
            c.Gravity = 0f;
            var b = new BallMotion { Z = 1f, Vx = 25f, Wx = 80f, HasWorldSpin = true };
            for (int i = 0; i < 25; i++) BallPhysics.Step(ref b, c, c.FixedStep);
            Assert.InRange(MathF.Abs(b.Y), 0f, 1e-4f);
            Assert.InRange(MathF.Abs(b.Z - 1f), 0f, 1e-4f);
        }

        [Fact]
        public void TheMagnusForceDoesNoWork()
        {
            // with the drag switched off the only forces are gravity and the Magnus force, which is perpendicular to the velocity: the
            // mechanical energy (per unit mass) is conserved by any spin, about any axis
            var c = MatchConfig.CreateDefault();
            c.Aero = new AeroModel { DragScale = 0f };
            foreach (var w in new[] { new Vec3(0f, 70f, 0f), new Vec3(0f, 0f, 70f), new Vec3(30f, -40f, 50f) })
            {
                var b = new BallMotion { Z = 2f, Vx = 25f, Vy = 3f, Vz = 8f, Wx = w.X, Wy = w.Y, Wz = w.Z, HasWorldSpin = true };
                float e0 = EnergyPerMass(b, c);
                for (int i = 0; i < 40; i++) BallPhysics.Step(ref b, c, c.FixedStep);
                Assert.InRange(EnergyPerMass(b, c) / e0, 0.995f, 1.005f);
            }
        }

        [Fact]
        public void DragAloneOnlyLosesEnergy()
        {
            var c = MatchConfig.CreateDefault();
            var b = new BallMotion { Z = 2f, Vx = 25f, Vz = 8f };
            float last = EnergyPerMass(b, c);
            for (int i = 0; i < 60; i++)
            {
                BallPhysics.Step(ref b, c, c.FixedStep);
                float e = EnergyPerMass(b, c);
                Assert.True(e < last + 1e-3f, "energy rose");
                last = e;
            }
        }

        [Fact]
        public void TheSpinDecaysExponentiallyAtTheMeasuredRate()
        {
            var c = MatchConfig.CreateDefault();
            var b = new BallMotion { Z = 3f, Vx = 20f, Vz = 6f, Wz = 60f, HasWorldSpin = true };
            for (int i = 0; i < 50; i++) BallPhysics.Step(ref b, c, c.FixedStep); // one second
            // Goff and Carre: 4-8% lost by the apex, about 10% by landing (about 1.5 s): 0.07-0.1 1/s
            Assert.InRange(b.Wz / 60f, 0.90f, 0.95f);
        }

        [Fact]
        public void AKickSpinGivenAsSideAndTopSpinBecomesTheWorldVectorOnce()
        {
            // the engine's kicks still set the two scalars; the first physical step turns them into the world vector
            var c = MatchConfig.CreateDefault();
            var b = new BallMotion { Z = 1f, Vx = 0f, Vy = 20f, Vz = 3f, SideSpin = 10f, TopSpin = 40f };
            BallPhysics.Step(ref b, c, c.FixedStep);
            Assert.True(b.HasWorldSpin);
            Assert.Equal(0f, b.SideSpin); Assert.Equal(0f, b.TopSpin);
            // travelling +Y, topspin is about the axis z x y = -X
            Assert.True(b.Wx < -35f && MathF.Abs(b.Wy) < 1f, $"w = ({b.Wx:0.0}, {b.Wy:0.0}, {b.Wz:0.0})");
            Assert.InRange(b.Wz, 9f, 10f);
        }

        [Fact]
        public void TheLegacyFlightIsStillThereAndStillDifferent()
        {
            var legacy = MatchConfig.CreateDefault(); legacy.Aero = null;
            var a = Fly(18f, 22f, Vec3.Zero, c: legacy);
            var b = Fly(18f, 22f, Vec3.Zero);
            Assert.NotEqual(a.Range, b.Range);
            Assert.InRange(a.Range, 18f, 23f); // the old model (Cd 0.25 at every speed) still lands where it did
        }
    }
}

using System;
using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>
    /// Ball physics stage 8: collisions. The impulse (restitution, friction, spin) against any normal; the goal frame as capsules found by a
    /// swept test; a ball striking a player's body. See docs/BALL_PHYSICS.md.
    /// </summary>
    public class BallCollisionTests
    {
        private static readonly MatchConfig C = MatchConfig.CreateDefault();

        [Fact]
        public void TheImpactIsTheBounceAboutAnyNormal()
        {
            // the same landing through Bounce (ground up) and through Impact with n = +z: the same velocity and spin
            var a = new BallMotion { X = 0f, Y = 0f, Z = Pitch.BallRadius, Vx = 12f, Vy = 3f, Vz = -5f, Wx = 4f, Wy = -20f, Wz = 7f, HasWorldSpin = true };
            var b = a;
            BallPhysics.Bounce(ref a, C);
            float e = C.Ground.RestitutionAt(5f);
            BallPhysics.Impact(ref b, C, Vec3.UnitZ, e, C.Ground.Friction, Vec3.Zero);
            Assert.InRange(b.Vx, a.Vx - 1e-4f, a.Vx + 1e-4f);
            Assert.InRange(b.Vy, a.Vy - 1e-4f, a.Vy + 1e-4f);
            Assert.InRange(b.Vz, a.Vz - 1e-4f, a.Vz + 1e-4f);
            Assert.InRange(b.Wx, a.Wx - 1e-3f, a.Wx + 1e-3f);
            Assert.InRange(b.Wy, a.Wy - 1e-3f, a.Wy + 1e-3f);
            Assert.InRange(b.Wz, a.Wz - 1e-3f, a.Wz + 1e-3f);
        }

        [Fact]
        public void TheImpactConservesAngularMomentumAboutTheContactPoint()
        {
            // friction acts at the contact point, so the ball's angular momentum about that point (m (centre - contact) x v + I w) does not change in the
            // tangential directions; tried on a slanted normal
            var n = new Vec3(-0.6f, 0.48f, 0.64f).Normalized;
            var m = new BallMotion { Vx = 20f, Vy = -6f, Vz = -9f, Wx = 30f, Wy = -10f, Wz = 45f, HasWorldSpin = true };
            float R = C.Ball.RadiusM, k = C.Ball.InertiaFactor;
            Vec3 L(BallMotion b) { var r = R * n; return Vec3.Cross(r, new Vec3(b.Vx, b.Vy, b.Vz)) + k * R * R * new Vec3(b.Wx, b.Wy, b.Wz); }
            var before = L(m);
            BallPhysics.Impact(ref m, C, n, 0.7f, 0.3f, Vec3.Zero);
            var after = L(m);
            // (r x n = 0: the normal impulse has no moment about the contact point either)
            Assert.InRange((after - before).Length, 0f, 1e-3f * before.Length + 1e-4f);
        }

        [Fact]
        public void TheFrameTakesMoreFromAHarderHit()
        {
            var cm = CollisionModel.Reference;
            Assert.InRange(cm.FrameRestitutionAt(6.26f), 0.849f, 0.851f);           // the FIFA drop test
            Assert.InRange(cm.FrameRestitutionAt(25f), 0.65f, 0.67f);               // a shot square on the post
            Assert.True(cm.FrameRestitutionAt(30f) < cm.FrameRestitutionAt(10f));
            // nothing is created: the ball leaves slower than it came, whatever the spin it brought
            var m = new BallMotion { Vx = 25f, Wz = 60f, HasWorldSpin = true };
            float e0 = 0.5f * C.Ball.MassKg * 625f + C.Ball.RotationalEnergy(new Vec3(0f, 0f, 60f));
            BallPhysics.Impact(ref m, C, new Vec3(-0.8f, 0.6f, 0f), cm.FrameRestitutionAt(20f), cm.FrameFriction, Vec3.Zero);
            float e1 = C.Ball.TranslationalEnergy(new Vec3(m.Vx, m.Vy, m.Vz)) + C.Ball.RotationalEnergy(new Vec3(m.Wx, m.Wy, m.Wz));
            Assert.True(e1 < e0);
        }

        /// <summary>A match on an empty pitch whose ball is sent at the goal frame (the players are kept far away).</summary>
        private static BallMotion ShootAtFrame(Vec3 from, Vec3 velocity, MatchConfig c, out int woodwork)
        {
            var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 3, c);
            var m = new BallMotion { X = from.X, Y = from.Y, Z = from.Z, Vx = velocity.X, Vy = velocity.Y, Vz = velocity.Z };
            var b = e.TestHitFrame(m, 0.6f, out woodwork);
            return b;
        }

        [Fact]
        public void AFastShotCannotPassThroughThePost()
        {
            // 32 m/s straight at the right post's centre, along the goal line's normal: a 0.02 s step moves it 0.64 m, more than twice the contact
            // distance (0.17 m). The old end-of-step test is lucky or not; the swept test always finds the post.
            float postX = Pitch.HalfLength + Pitch.PostRadius, postY = Pitch.GoalHalfWidth + Pitch.PostRadius;
            int hits = 0, legacyHits = 0;
            for (int i = 0; i < 20; i++)
            {
                float start = postX - 6f - 0.031f * i;   // (the phase of the steps against the post)
                var b = ShootAtFrame(new Vec3(start, postY, 1f), new Vec3(32f, 0f, 0f), C, out int w);
                if (w > 0 && b.Vx < 0f) hits++;
                var legacy = MatchConfig.CreateDefault(); legacy.Collisions = null;
                ShootAtFrame(new Vec3(start, postY, 1f), new Vec3(32f, 0f, 0f), legacy, out int lw);
                if (lw > 0) legacyHits++;
            }
            Assert.Equal(20, hits);
            Assert.True(legacyHits < 20, "the old test caught every one: the case is not a tunnelling case");
        }

        [Fact]
        public void TheCrossbarAndItsCornersAreRound()
        {
            float postX = Pitch.HalfLength + Pitch.PostRadius, top = Pitch.GoalHeight + Pitch.PostRadius;
            // dropping straight onto the top of the bar's middle: it bounces up
            var b = ShootAtFrame(new Vec3(postX, 0f, top + 1.5f), new Vec3(0f, 0f, -4f), C, out int w);
            Assert.True(w > 0 && b.Vz > 0f, "dropped on the bar: " + b.Vz);
            // into the corner where the bar meets the post, diagonally: sent back with both an outward and a downward part
            var c = ShootAtFrame(new Vec3(postX - 3f, Pitch.GoalHalfWidth + Pitch.PostRadius + 0.08f, top + 0.08f), new Vec3(20f, 0f, 0f), C, out int w2);
            Assert.True(w2 > 0 && c.Vx < 0f, "the corner sends it back: " + c.Vx);
        }

        [Fact]
        public void APostGivesASpinningBallSpinAndTakesSome()
        {
            // a ball hitting the post a glancing blow picks up spin about the vertical axis from the friction
            float postX = Pitch.HalfLength + Pitch.PostRadius, postY = Pitch.GoalHalfWidth + Pitch.PostRadius;
            var b = ShootAtFrame(new Vec3(postX - 4f, postY - 0.12f, 1f), new Vec3(25f, 0f, 0f), C, out int w);
            Assert.True(w > 0);
            Assert.True(MathF.Abs(b.Wz) > 5f, "spin about the vertical after a glancing hit: " + b.Wz);
        }

        [Fact]
        public void BothCollisionModelsAreDeterministicAndTheNewOneIsWiredIn()
        {
            static MatchEngine Run(bool legacy)
            {
                var c = MatchConfig.CreateDefault();
                if (legacy) c.Collisions = null;
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
            Assert.NotEqual(Run(true).EventLog.Count, Run(false).EventLog.Count);
        }
    }
}

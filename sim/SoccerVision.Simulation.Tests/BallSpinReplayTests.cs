using System;
using SoccerVision.Replay;
using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>
    /// Ball physics stage 6: the viewers turn the ball by its physical spin. Each replay frame ends with the free ball's angular velocity (world
    /// axes, rad per replay second x10); a ball a player has, or the old ground model, records 0, 0, 0 and the viewers fall back to the
    /// movement. See docs/BALL_PHYSICS.md.
    /// </summary>
    public class BallSpinReplayTests
    {
        [Fact]
        public void EachFrameEndsWithTheFreeBallsPhysicalSpin()
        {
            var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 4242, MatchConfig.CreateDefault());
            var rec = new ReplayRecorder(e, 10);
            int free = 0, moving = 0, movingSpinning = 0, held = 0, n = ReplayRecorder.FrameLength;
            float k = 10f * e.Config.MotionSpeed;
            for (int i = 0; i < 3000 && !e.IsFinished; i++)
            {
                var chunk = rec.Step(1);
                if (chunk.FrameCount != 1) continue;
                Assert.Equal(n, chunk.Frames.Length);
                int wx = chunk.Frames[n - 3], wy = chunk.Frames[n - 2], wz = chunk.Frames[n - 1];
                if (e.Ball.IsFree && !e.Ball.InHands)
                {
                    free++;
                    var m = e.Ball.M;
                    var w = !m.HasWorldSpin && !m.IsAirborne ? new Vec3(-m.Vy / e.Config.Ball.RadiusM, m.Vx / e.Config.Ball.RadiusM, m.SideSpin) : m.AngularVelocity;
                    Assert.Equal((int)MathF.Round(w.X * k), wx);
                    Assert.Equal((int)MathF.Round(w.Y * k), wy);
                    Assert.Equal((int)MathF.Round(w.Z * k), wz);
                    if (e.Ball.M.Speed > 0.5f && !e.Ball.M.IsAirborne) { moving++; if (wx != 0 || wy != 0 || wz != 0) movingSpinning++; }
                }
                else
                {
                    held++;
                    Assert.True(wx == 0 && wy == 0 && wz == 0, "a ball a player has records no spin");
                }
            }
            Assert.True(free > 100 && held > 100, $"free {free}, held {held}");
            // a ball rolling along the ground always turns (the spin of rolling); one in the air may have none (a lofted pass struck without spin)
            Assert.True(movingSpinning > 0.95 * moving, $"a rolling ball spins in {movingSpinning} of {moving} frames");
        }

        [Fact]
        public void TheOldGroundModelRecordsNoSpin()
        {
            var c = MatchConfig.CreateDefault();
            c.Ground = null;
            var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 4242, c);
            var rec = new ReplayRecorder(e, 10);
            int n = ReplayRecorder.FrameLength;
            for (int i = 0; i < 300; i++)
            {
                var chunk = rec.Step(1);
                if (chunk.FrameCount != 1) continue;
                Assert.True(chunk.Frames[n - 3] == 0 && chunk.Frames[n - 2] == 0 && chunk.Frames[n - 1] == 0);
            }
        }
    }
}

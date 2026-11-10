using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SoccerVision.Simulation;
using Xunit;
using Xunit.Abstractions;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>
    /// The goal net (polish round 8): a ball that reaches the back, a side or the roof of the net is slowed over the net's give - not stopped
    /// dead in one step - comes back off it at a few percent of its speed at most, never goes through it, and ends at rest in the goal.
    /// </summary>
    public class GoalNetTests
    {
        private readonly ITestOutputHelper _out;
        public GoalNetTests(ITestOutputHelper output) { _out = output; }

        private const float R = Pitch.BallRadius;

        private (List<BallMotion> path, BallMotion end) Shoot(BallMotion start, float seconds = 3f)
        {
            var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 3, MatchConfig.CreateDefault());
            e.Ball.InGoal = true; // (a goal: the ball is in the net)
            var path = new List<BallMotion>();
            var end = e.TestHitFrame(start, seconds, out _, m => path.Add(m));
            return (path, end);
        }

        [Theory]
        [InlineData(30f, 0.4f)]
        [InlineData(20f, 1.2f)]
        [InlineData(10f, 0.2f)]
        public void AShotIntoTheBackOfTheNetIsSlowedOverTheGiveOfTheNet(float speed, float height)
        {
            float back = Pitch.HalfLength + Pitch.GoalDepth - R;
            var (path, end) = Shoot(new BallMotion { X = Pitch.HalfLength - 1f, Y = 0.5f, Z = height, Vx = speed, Vz = 0.5f });
            int first = path.FindIndex(m => m.X > back);
            Assert.True(first >= 0, "the ball reaches the back of the net");
            float deepest = 0f, rebound = 0f; int slowing = 0;
            for (int i = first; i < path.Count; i++)
            {
                deepest = MathF.Max(deepest, path[i].X - back);
                if (path[i].X > back && path[i].Vx > 0f) slowing++;
                if (path[i].X <= back) rebound = MathF.Max(rebound, -path[i].Vx); // (off the net again: how fast it comes back out)
            }
            float impact = path[Math.Max(0, first - 1)].Vx;
            _out.WriteLine($"{speed} m/s: into the net {deepest:0.00} m, slowed over {slowing} steps (0.02 s), back off it at {rebound:0.00} m/s ({100 * rebound / impact:0.0}%); at rest at x {end.X - Pitch.HalfLength:0.00} m behind the line, height {end.Z:0.00}, speed {end.Speed:0.00}");
            Assert.True(deepest > 0.02f && deepest <= MatchEngine.NetMaxGive + 0.01f, "the net gives, but not further than its give");
            Assert.True(slowing >= (speed >= 20f ? 2 : 1), "the ball is slowed over the net's give, not stopped dead in one step");
            Assert.True(rebound < 0.05f * impact, "it hardly comes back off the net");
            Assert.InRange(end.X, Pitch.HalfLength, back + 0.01f);
            Assert.True(end.Z < R + 0.02f && end.Speed < 0.3f, "it ends at rest in the bottom of the net");
            foreach (var m in path) Assert.True(m.X <= back + MatchEngine.NetMaxGive + 0.01f, "never through the net");
        }

        [Fact]
        public void TheSideNettingAndTheRoofGiveToo()
        {
            float side = Pitch.GoalHalfWidth - R, roof = Pitch.GoalHeight - R;
            var (path, end) = Shoot(new BallMotion { X = Pitch.HalfLength + 0.3f, Y = 1f, Z = 0.5f, Vx = 4f, Vy = 18f, Vz = 0f });
            float outSide = 0f; foreach (var m in path) outSide = MathF.Max(outSide, m.Y - side);
            var (path2, end2) = Shoot(new BallMotion { X = Pitch.HalfLength + 0.3f, Y = 0f, Z = 1.5f, Vx = 3f, Vz = 12f });
            float outRoof = 0f; foreach (var m in path2) outRoof = MathF.Max(outRoof, m.Z - roof);
            _out.WriteLine($"side netting: out {outSide:0.00} m, at rest y {end.Y:0.00}; roof: up {outRoof:0.00} m, at rest z {end2.Z:0.00}");
            Assert.True(outSide > 0.02f && outSide <= MatchEngine.NetMaxGive + 0.01f);
            Assert.True(outRoof > 0.02f && outRoof <= MatchEngine.NetMaxGive + 0.01f);
            Assert.True(MathF.Abs(end.Y) <= side + 0.01f && end.Speed < 0.3f, "back inside the goal, at rest");
            Assert.True(end2.Z < R + 0.02f && end2.Speed < 0.3f, "down off the roof onto the grass in the goal");
        }

        [Fact]
        public void AfterAGoalTheBallStaysInTheNetAndDoesNotFreeze()
        {
            long goals = 0, moving = 0, through = 0;
            float back = Pitch.HalfLength + Pitch.GoalDepth - R + MatchEngine.NetMaxGive + 0.01f;
            Parallel.For(0, 16, i =>
            {
                var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 4400 + (ulong)i, MatchConfig.CreateDefault());
                var prevPhase = e.Phase;
                while (!e.IsFinished)
                {
                    e.Advance(0.02f);
                    if (e.Phase == MatchPhase.GoalCelebration && prevPhase != MatchPhase.GoalCelebration)
                    {
                        // the ball has just gone in: follow it for 1.5 s
                        Interlocked.Increment(ref goals);
                        var start = e.Ball.Pos;
                        bool moved = false;
                        for (int k = 0; k < 75 && e.Phase == MatchPhase.GoalCelebration; k++)
                        {
                            e.Advance(0.02f);
                            if (Vec2.Distance(e.Ball.Pos, start) > 0.05f) moved = true;
                            if (MathF.Abs(e.Ball.Pos.Y) < Pitch.GoalHalfWidth && MathF.Abs(e.Ball.Pos.X) > back) Interlocked.Increment(ref through);
                        }
                        if (moved) Interlocked.Increment(ref moving);
                    }
                    prevPhase = e.Phase;
                }
            });
            _out.WriteLine($"goals {goals}: the ball went on moving into the net {moving}; through the net {through}");
            Assert.True(goals > 10);
            Assert.Equal(0, through);
            Assert.True(moving >= goals * 0.9, "the ball does not freeze when the goal is scored");
        }
    }
}

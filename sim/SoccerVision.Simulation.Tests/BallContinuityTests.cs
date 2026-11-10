using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SoccerVision.Simulation;
using Xunit;
using Xunit.Abstractions;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>
    /// One ball at a time (polish round 7): out of play, the match ball never jumps where it can be seen - it rolls in from an attendant
    /// out of view (behind the goal line for a goal kick or a corner) or it is the same ball, fetched by the thrower; the ball that went out
    /// is never shown next to the match ball.
    /// </summary>
    public class BallContinuityTests
    {
        private readonly ITestOutputHelper _out;
        public BallContinuityTests(ITestOutputHelper output) { _out = output; }

        private static bool InView(Vec2 p) => MathF.Abs(p.X) <= Pitch.HalfLength + 4f && MathF.Abs(p.Y) <= Pitch.HalfWidth + 3f;

        [Fact]
        public void TheBallNeverJumpsInViewAndThereIsOneBallAtATime()
        {
            long jumps = 0, twoBalls = 0, goalKickBalls = 0, goalKickFromBehind = 0, samples = 0;
            var total = new ConcurrentDictionary<string, int>();
            var where = new ConcurrentBag<string>();
            Parallel.For(0, 24, i =>
            {
                ulong seed = 7300 + (ulong)i;
                var e = new MatchEngine(DemoTeams.Red(seed), DemoTeams.Blue(seed), seed, MatchConfig.CreateDefault()) { CollectDiagnostics = true };
                Vec2 prev = e.Ball.Pos;
                bool wasStoppage = false;
                while (!e.IsFinished)
                {
                    e.Advance(0.1f);
                    var r = e.CurrentRestart;
                    bool stoppage = e.Phase == MatchPhase.Stoppage && r != null
                                    && (r.Type == RestartType.ThrowIn || r.Type == RestartType.CornerKick || r.Type == RestartType.GoalKick);
                    Vec2 now = e.Ball.Pos;
                    if (stoppage && wasStoppage)
                    {
                        Interlocked.Increment(ref samples);
                        // a jump of more than 2 m in a tenth of a second where it can be seen (the throw-in lifted overhead is a few decimetres)
                        if (Vec2.Distance(prev, now) > 2f && InView(prev) && InView(now))
                        {
                            Interlocked.Increment(ref jumps);
                            where.Add($"{r.Type} {prev} -> {now}");
                        }
                        if (e.DeadBallActive && Vec2.Distance(new Vec2(e.DeadBall.X, e.DeadBall.Y), now) < 1f) Interlocked.Increment(ref twoBalls);
                        if (r.Type == RestartType.GoalKick && !Pitch.InsideField(prev, 0f) && Pitch.InsideField(now, 0f))
                        {
                            Interlocked.Increment(ref goalKickBalls);
                            if (MathF.Abs(prev.X) > Pitch.HalfLength) Interlocked.Increment(ref goalKickFromBehind);
                        }
                    }
                    prev = now;
                    wasStoppage = stoppage;
                }
                Assert.Equal(0, e.RecoveredErrors);
                foreach (var kv in e.Diagnostics) if (kv.Key.StartsWith("out.retrieved") || kv.Key.StartsWith("out.newBall") || kv.Key.StartsWith("restart.forced")) total.AddOrUpdate(kv.Key, kv.Value, (_, v) => v + kv.Value);
            });
            int Sum(string prefix) => total.Where(kv => kv.Key.StartsWith(prefix)).Sum(kv => kv.Value);
            _out.WriteLine($"{samples} samples at throw-ins, corners and goal kicks: jumps in view {jumps}, the old ball beside the match ball {twoBalls}");
            _out.WriteLine($"throw-ins with the same ball {Sum("out.retrieved") - Sum("out.retrieved.pickedUp")} (picked up {Sum("out.retrieved.pickedUp")}), new balls: " +
                           string.Join(", ", total.Where(kv => kv.Key.StartsWith("out.newBall.")).OrderBy(kv => kv.Key).Select(kv => $"{kv.Key.Substring(12)} {kv.Value}")));
            _out.WriteLine($"goal kicks: the new ball came onto the pitch from behind the goal line {goalKickFromBehind} of {goalKickBalls}; restarts forced {Sum("restart.forced")}");
            foreach (var w in where.Take(5)) _out.WriteLine("  jump: " + w);
            Assert.True(samples > 1000);
            Assert.Equal(0, jumps);
            Assert.Equal(0, twoBalls);
            Assert.True(Sum("out.retrieved.pickedUp") > 0, "a throw-in is taken with the ball that went out when it stops within reach");
            Assert.True(goalKickBalls > 10 && goalKickFromBehind == goalKickBalls, "the goal kick's new ball comes from behind the goal line");
        }
    }
}

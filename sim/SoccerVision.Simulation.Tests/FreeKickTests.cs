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
    /// Direct free kicks (polish round 6): the kinds of strike, a wall lined up on the near post that jumps as the ball is struck (a low
    /// shot goes under it), a keeper near the middle of his goal, a little to the far side, and free kicks scored about as often as real ones.
    /// </summary>
    public class FreeKickTests
    {
        private readonly ITestOutputHelper _out;
        public FreeKickTests(ITestOutputHelper output) { _out = output; }

        [Fact]
        public void TheWallTheKeeperAndTheStrikesAtADirectFreeKick()
        {
            var total = new ConcurrentDictionary<string, int>();
            long walls = 0, wallOnPost = 0, keepers = 0, keeperNearMiddle = 0, shots = 0, goals = 0;
            Parallel.For(0, 192, i =>
            {
                ulong seed = 8800 + (ulong)i;
                var e = new MatchEngine(DemoTeams.Red(seed), DemoTeams.Blue(seed), seed, MatchConfig.CreateDefault()) { CollectDiagnostics = true };
                e.RestartExecuting += r =>
                {
                    if (r.Type != RestartType.DirectFreeKick || r.Wall.Count < 2 || MathF.Abs(r.Spot.Y) < 3f) return;
                    var att = r.Team;
                    float nearSide = MathF.Sign(r.Spot.Y);
                    // the wall's end man on the line from the ball to just outside the near post
                    Vec2 post = new Vec2(Pitch.HalfLength * att.AttackGoalSign, nearSide * (Pitch.GoalHalfWidth + MatchEngine.WallOutsidePost));
                    Vec2 dir = (post - r.Spot).Normalized;
                    float best = r.Wall.Min(w => MathF.Abs(Vec2.Cross(dir, w.ShapeTarget - r.Spot)));
                    Interlocked.Increment(ref walls);
                    if (best < 0.35f) Interlocked.Increment(ref wallOnPost);
                    var gk = att.Opponent.Goalkeeper;
                    if (gk != null)
                    {
                        Interlocked.Increment(ref keepers);
                        float across = gk.ShapeTarget.Y * nearSide; // + towards the wall's post
                        if (across <= 0f && across > -1f) Interlocked.Increment(ref keeperNearMiddle);
                    }
                };
                e.RunToEnd();
                Assert.Equal(0, e.RecoveredErrors);
                Interlocked.Add(ref shots, e.Home.Stats.FreeKickShots + e.Away.Stats.FreeKickShots);
                Interlocked.Add(ref goals, e.Home.Stats.FreeKickGoals + e.Away.Stats.FreeKickGoals);
                foreach (var kv in e.Diagnostics) if (kv.Key.StartsWith("fk.")) total.AddOrUpdate(kv.Key, kv.Value, (_, v) => v + kv.Value);
            });
            int Sum(string prefix) => total.Where(kv => kv.Key.StartsWith(prefix)).Sum(kv => kv.Value);
            _out.WriteLine($"free kicks at goal {shots} in 192 matches, goals {goals} ({100.0 * goals / Math.Max(1, shots):0.0}%, real 6.2%), blocked {Sum("fk.blocked")} ({100.0 * Sum("fk.blocked") / Math.Max(1, shots):0}%, real 34%)");
            _out.WriteLine($"strikes: curled {Sum("fk.style.Curled")}, driven {Sum("fk.style.Driven")}, placed {Sum("fk.style.Placed")}, under the wall {Sum("fk.style.Under")} (went under it {Sum("fk.wall.under")}), knuckleball {Sum("fk.style.Knuckle")}");
            _out.WriteLine($"walls off centre {walls}: lined up on the near post {wallOnPost}; keeper near the middle, to the far side: {keeperNearMiddle} of {keepers}");
            Assert.True(shots > 30, "direct free kicks at goal are taken");
            foreach (var s in new[] { "Curled", "Driven", "Placed", "Under", "Knuckle" }) Assert.True(Sum("fk.style." + s) > 0, $"{s} free kicks happen");
            Assert.True(Sum("fk.wall.under") > 0, "a low shot goes under the jumping wall");
            Assert.True(walls > 10 && wallOnPost == walls, "the wall is lined up on the near post");
            Assert.True(keepers > 10 && keeperNearMiddle == keepers, "the keeper stands near the middle, a little to the far side");
            Assert.InRange(100.0 * goals / shots, 1.0, 15.0); // (real 6.2%; about 180 free kicks here, so a broad band)
        }
    }
}

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
    /// Running with the ball as touches (polish round 5): the ball rolls ahead between touches - short with a man near, longer into space - is
    /// never let run out of play by its carrier and never wanders off; a kick is struck with the ball at the foot; knock-and-runs happen.
    /// </summary>
    public class DribbleTouchTests
    {
        private readonly ITestOutputHelper _out;
        public DribbleTouchTests(ITestOutputHelper output) { _out = output; }

        [Fact]
        public void TheBallRollsAheadBetweenTouchesAndStaysWithItsCarrier()
        {
            long samples = 0, ahead = 0, far = 0, outside = 0;
            var total = new ConcurrentDictionary<string, int>();
            Parallel.For(0, 16, i =>
            {
                ulong seed = 9700 + (ulong)i;
                var e = new MatchEngine(DemoTeams.Red(seed), DemoTeams.Blue(seed), seed, MatchConfig.CreateDefault()) { CollectDiagnostics = true };
                long s = 0, a = 0, f = 0, o = 0;
                while (!e.IsFinished)
                {
                    e.Advance(0.1f);
                    var p = e.Ball.Owner;
                    if (p == null || e.Ball.InHands || e.Phase != MatchPhase.Live || p.Vel.Length < 3f) continue;
                    s++;
                    float d = Vec2.Distance(e.Ball.Pos, p.Pos);
                    if (d > 1.0f) a++;
                    if (d > 4f) f++;
                    if (!Pitch.InsideField(e.Ball.Pos, 0.05f)) o++;
                }
                Assert.Equal(0, e.RecoveredErrors);
                Interlocked.Add(ref samples, s); Interlocked.Add(ref ahead, a); Interlocked.Add(ref far, f); Interlocked.Add(ref outside, o);
                foreach (var kv in e.Diagnostics) if (kv.Key.StartsWith("dribble.") || kv.Key.StartsWith("contact.far.kick.")) total.AddOrUpdate(kv.Key, kv.Value, (_, v) => v + kv.Value);
            });
            int Sum(string prefix) => total.Where(kv => kv.Key.StartsWith(prefix)).Sum(kv => kv.Value);
            _out.WriteLine($"running with the ball: {samples} samples, the ball more than a metre ahead {100.0 * ahead / samples:0.0}%, more than 4 m {far}, outside the field {outside}");
            _out.WriteLine($"touches: close {Sum("dribble.touch.Close")}, progressive {Sum("dribble.touch.Progressive")}; knock-and-runs {Sum("dribble.knockOn") - Sum("dribble.knockOnWon")} (won {Sum("dribble.knockOnWon")}); passes or shots struck out of reach {Sum("contact.far.kick.Pass.") + Sum("contact.far.kick.Shot.")}");
            Assert.True(samples > 1000);
            Assert.True(ahead > samples / 20, "the ball runs ahead of a running carrier (touches), not glued to him");
            Assert.True(far < samples / 200, "a carried ball does not wander off");
            Assert.Equal(0, outside);
            Assert.True(Sum("dribble.touch.Close") > 0 && Sum("dribble.touch.Progressive") > 0, "both touch styles happen");
            Assert.True(Sum("dribble.knockOn") > 0, "knock-and-runs happen");
            Assert.True(Sum("contact.far.kick.Pass.") + Sum("contact.far.kick.Shot.") < 16, "a pass or a shot is struck with the ball at the foot");
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using SoccerVision.Simulation;
using Xunit;
using Xunit.Abstractions;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>
    /// Open-play crosses against real ones (polish round 10; StatsBomb Premier League 2015/16, tools/calibration/crosses.py and cross_outcomes.py):
    /// 28% are completed and only 1.8% are stopped by the other side's first action within 10 yards of the crosser; crosses are 13.3% of open-play shots
    /// and 18.5% of open-play goals. The crosser does not hit a ball into a man standing in front of him, and a man level with him or behind cannot get a leg to it.
    /// Corners (tools/calibration/corners.py, corner_outcomes.py): 0.406 shots and 0.0348 goals a corner, 43% of the shots headers; the defenders have the first touch 56% of the time.
    /// </summary>
    public class CrossTests
    {
        private readonly ITestOutputHelper _out;
        public CrossTests(ITestOutputHelper output) { _out = output; }

        [Fact]
        public void CrossesAreNotBlockedAtTheCrosserAndCompleteAsOftenAsRealOnes()
        {
            var total = new ConcurrentDictionary<string, double>();
            const int matches = 96;
            Parallel.For(0, matches, i =>
            {
                ulong seed = 12100 + (ulong)i;
                var e = new MatchEngine(DemoTeams.Red(seed), DemoTeams.Blue(seed), seed, MatchConfig.CreateDefault()) { CollectDiagnostics = true };
                e.RunToEnd();
                Assert.Equal(0, e.RecoveredErrors);
                foreach (var kv in e.Diagnostics) if (kv.Key.StartsWith("cross.") || kv.Key.StartsWith("shot.assist.")) total.AddOrUpdate(kv.Key, kv.Value, (_, v) => v + kv.Value);
            });
            double T(string k) => total.TryGetValue(k, out var v) ? v : 0;
            double kicked = T("cross.open.kicked"), near = T("cross.open.stoppedNear"), mate = T("cross.open.first.mate");
            double shots = total.Where(kv => kv.Key.StartsWith("shot.assist.")).Sum(kv => kv.Value) - T("shot.assist.CrossSetPiece") - T("shot.assist.Restart");
            _out.WriteLine($"open-play crosses {kicked / matches:0.0} a match; stopped within 10 yd of the crosser {100 * near / kicked:0.0}% (real 1.8%); completed {100 * mate / kicked:0.0}% (real 28%); " +
                           $"shots after an open-play cross {100 * T("shot.assist.Cross") / shots:0.0}% of open-play shots (real 13.3%)");
            _out.WriteLine($"called off at the kick (the lane closed) {T("cross.abortedAtKick"):0}, blocked {T("cross.blocked") / matches:0.00} a match");
            Assert.True(kicked / matches > 9, "crosses are still played");
            Assert.True(near / kicked < 0.05, $"crosses stopped next to the crosser {100 * near / kicked:0.0}% (real 1.8%; 10.5% before the round)");
            Assert.InRange(mate / kicked, 0.20, 0.36);   // real 28%
            Assert.InRange(T("shot.assist.Cross") / shots, 0.08, 0.19); // real 13.3%
            Assert.True(T("cross.abortedAtKick") > 0, "a crosser who sees the lane close at the kick thinks again");
        }

        [Fact]
        public void CornersAreWonByTheDefendersMoreOftenAndHeadedGoalsAreARealShare()
        {
            var total = new ConcurrentDictionary<string, double>();
            const int matches = 96;
            double goals = 0, headGoals = 0, corners = 0;
            var lockObj = new object();
            Parallel.For(0, matches, i =>
            {
                ulong seed = 12300 + (ulong)i;
                var e = new MatchEngine(DemoTeams.Red(seed), DemoTeams.Blue(seed), seed, MatchConfig.CreateDefault()) { CollectDiagnostics = true };
                e.RunToEnd();
                Assert.Equal(0, e.RecoveredErrors);
                foreach (var kv in e.Diagnostics) if (kv.Key.StartsWith("corner.")) total.AddOrUpdate(kv.Key, kv.Value, (_, v) => v + kv.Value);
                lock (lockObj)
                {
                    goals += e.Home.Stats.Goals + e.Away.Stats.Goals;
                    headGoals += e.Home.Stats.HeaderGoals + e.Away.Stats.HeaderGoals;
                    corners += e.Home.Stats.Corners + e.Away.Stats.Corners;
                }
            });
            double T(string k) => total.TryGetValue(k, out var v) ? v : 0;
            double first = total.Where(kv => kv.Key.StartsWith("corner.first.") && !kv.Key.StartsWith("corner.first.shot.")).Sum(kv => kv.Value);
            double defFirst = total.Where(kv => kv.Key.StartsWith("corner.first.def.") || kv.Key.StartsWith("corner.first.gk.")).Sum(kv => kv.Value);
            double shots = T("corner.shot.head") + T("corner.shot.foot");
            double cornerGoals = T("corner.goal.head") + T("corner.goal.foot");
            _out.WriteLine($"corners {corners / matches:0.0} a match; the defenders (or the keeper) first to it {100 * defFirst / first:0.0}% of the time (real: 56% of the first actions, duels counted); shots a corner {shots / corners:0.000} (real 0.406), {100 * T("corner.shot.head") / shots:0}% headers (real 43%); " +
                           $"goals a corner {cornerGoals / corners:0.0000} (real 0.0348); headed goals {100 * headGoals / goals:0.0}% of all goals (real 14.3%, 20% before the round)");
            Assert.True(defFirst / first > 0.33, "the defenders get the first touch at a corner more often than before (26%)");
            Assert.InRange(shots / corners, 0.28, 0.48);        // real 0.406
            Assert.InRange(cornerGoals / corners, 0.012, 0.045); // real 0.0348
            Assert.True(headGoals / goals < 0.19, $"headed goals {100 * headGoals / goals:0.0}% of the goals (real 14.3%; 20-22% before the round)");
        }
    }
}

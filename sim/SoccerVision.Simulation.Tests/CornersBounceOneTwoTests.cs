using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>Varied corners (routines, in- and out-swingers), finishes that bounce, plenty of one-twos, labels without '#'.</summary>
    public class CornersBounceOneTwoTests
    {
        private static List<MatchEngine> Play(ulong from, int n)
        {
            var engines = new MatchEngine[n];
            Parallel.For(0, n, i =>
            {
                engines[i] = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), from + (ulong)i, MatchConfig.CreateDefault()) { CollectDiagnostics = true };
                engines[i].RunToEnd();
            });
            return engines.ToList();
        }

        private static int Diag(IEnumerable<MatchEngine> es, string prefix) => es.Sum(e => e.Diagnostics.Where(kv => kv.Key.StartsWith(prefix)).Sum(kv => kv.Value));

        [Fact]
        public void CornersAreVariedAndSwingBothWays()
        {
            var es = Play(3100, 8);
            Assert.True(Diag(es, "corner.near") > 0 && Diag(es, "corner.far") > 0 && Diag(es, "corner.middle") > 0 && Diag(es, "corner.edge") > 0 && Diag(es, "corner.short") > 0,
                "every routine: near " + Diag(es, "corner.near") + " far " + Diag(es, "corner.far") + " middle " + Diag(es, "corner.middle") + " edge " + Diag(es, "corner.edge") + " short " + Diag(es, "corner.short"));
            int inswing = es.Sum(e => e.Diagnostics.Where(kv => kv.Key.StartsWith("corner.") && kv.Key.EndsWith(".in")).Sum(kv => kv.Value));
            int outswing = es.Sum(e => e.Diagnostics.Where(kv => kv.Key.StartsWith("corner.") && kv.Key.EndsWith(".out")).Sum(kv => kv.Value));
            Assert.True(inswing > outswing && outswing > 0, $"in-swingers {inswing}, out-swingers {outswing}");
            Assert.Contains(es.SelectMany(e => e.EventLog), x => x.Type == MatchEventType.Cross && x.Text.Contains("out-swinger"));
        }

        [Fact]
        public void SomeFinishesBounceBeforeTheGoal()
        {
            var es = Play(3200, 6);
            Assert.True(Diag(es, "shot.bouncer") >= 6 * 2, "bouncing finishes " + Diag(es, "shot.bouncer"));
        }

        [Fact]
        public void EachTeamPlaysAtLeastTenOneTwosAMatch()
        {
            var es = Play(3300, 8);
            double home = es.Average(e => e.Home.Stats.OneTwos), away = es.Average(e => e.Away.Stats.OneTwos);
            Assert.True(home >= 10 && away >= 10, $"one-twos per match: home {home:0.0}, away {away:0.0}");
            double done = es.Average(e => e.Home.Stats.OneTwosCompleted + e.Away.Stats.OneTwosCompleted);
            Assert.True(done >= 14, $"completed one-twos per match {done:0.0}");
        }

        [Fact]
        public void CommentaryNamesPlayersByNumberWithoutAHash()
        {
            var e = Play(3400, 1)[0];
            Assert.DoesNotContain(e.EventLog, x => x.Text.Contains("#"));
            Assert.Contains(e.EventLog, x => x.Type == MatchEventType.Pass && System.Text.RegularExpressions.Regex.IsMatch(x.Text, @"^\d+ \w"));
        }
    }
}

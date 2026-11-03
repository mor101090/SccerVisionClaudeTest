using System;
using System.Linq;
using System.Threading.Tasks;
using SoccerVision.Simulation;
using Xunit;
using Xunit.Abstractions;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>
    /// The inverted winger (his strong foot on the inside): a match brings him at least three shots after cutting inside; and when the
    /// manager's instruction makes a winger an inverted one he does it whatever his traits say - an unselfish one too.
    /// </summary>
    public class CutInWingerTests
    {
        private readonly ITestOutputHelper _out;
        public CutInWingerTests(ITestOutputHelper output) { _out = output; }

        private static MatchEngine[] Play(Func<TeamSetup> red, int n, ulong from = 9700)
        {
            var es = new MatchEngine[n];
            Parallel.For(0, n, i =>
            {
                es[i] = new MatchEngine(red(), DemoTeams.Blue(), from + (ulong)i, MatchConfig.CreateDefault()) { CollectDiagnostics = true };
                es[i].RunToEnd();
            });
            return es;
        }

        [Fact]
        public void EveryInvertedWingerShootsAtLeastThreeTimesAMatchAfterCuttingInAndTheInstructionBeatsTheTrait()
        {
            // the home side as it is: its inverted wingers
            var es = Play(() => DemoTeams.Red(), 16);
            var perMan = es.SelectMany(e => e.Diagnostics.Where(kv => kv.Key.StartsWith("cutin.T0.N"))).GroupBy(kv => kv.Key).ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value) / 16.0);
            _out.WriteLine("cut-in shots a match: " + string.Join(", ", perMan.Select(kv => kv.Key + "=" + kv.Value.ToString("0.0"))));
            Assert.True(perMan.Values.Any(v => v >= 3.0), "no inverted winger shoots three times a match after cutting in");

            // the instruction: an unselfish winger with the inverted-winger role cuts in and shoots just the same
            TeamSetup Instructed()
            {
                var t = DemoTeams.Red();
                for (int i = 0; i < 11; i++)
                    if (t.Formation.KindAt(i) == PositionKind.Winger || t.Formation.Slots[i].IsWideMidfielder)
                    {
                        t.Tactics.Orders[i].Role = PlayerRole.InvertedWinger;
                        var p = t.Starters[i];
                        p.Traits = PlayerTrait.Unselfish;
                    }
                return t;
            }
            var ins = Play(Instructed, 16);
            double shots = ins.Average(e => e.Home.Stats.CutInShots);
            _out.WriteLine($"with the instruction (unselfish wingers): {shots:0.0} cut-in shots a match for the two wingers");
            Assert.True(shots >= 5.0, $"the instructed wingers did not cut in and shoot: {shots:0.0} a match");
            Assert.All(ins.Concat(es), e => Assert.Equal(0, e.RecoveredErrors));
        }
    }
}

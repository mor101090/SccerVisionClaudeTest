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
    /// Combinations (polish round 4), against StatsBomb's Premier League 2015/16 (tools/calibration/combinations.py): 24.2 one-twos a match,
    /// 3.24 immediate and 20.95 delayed (a carry between the passes); the return 89% along the ground.
    /// </summary>
    public class CombinationTests
    {
        private readonly ITestOutputHelper _out;
        public CombinationTests(ITestOutputHelper output) { _out = output; }

        [Fact]
        public void OneTwosAreMostlyDelayedAndTheReturnMostlyAlongTheGround()
        {
            var total = new ConcurrentDictionary<string, int>();
            Parallel.For(0, 48, i =>
            {
                ulong seed = 9500 + (ulong)i;
                var e = new MatchEngine(DemoTeams.Red(seed), DemoTeams.Blue(seed), seed, MatchConfig.CreateDefault()) { CollectDiagnostics = true };
                e.RunToEnd();
                Assert.Equal(0, e.RecoveredErrors);
                foreach (var kv in e.Diagnostics)
                    if (kv.Key.StartsWith("onetwo.") || kv.Key.StartsWith("thirdman.")) total.AddOrUpdate(kv.Key, kv.Value, (_, v) => v + kv.Value);
            });
            double n = 48;
            double Sum(string prefix) => total.Where(kv => kv.Key.StartsWith(prefix)).Sum(kv => kv.Value) / n;
            double immediate = Sum("onetwo.return.immediate."), delayed = Sum("onetwo.return.delayed."), lofted = Sum("onetwo.return.immediate.lofted") + Sum("onetwo.return.delayed.lofted");
            double completed = Sum("onetwo.ok") - Sum("onetwo.ok.other"), returns = immediate + delayed;
            _out.WriteLine($"a match: one-twos completed {completed:0.0}; returns {returns:0.0} - immediate {immediate:0.0}, delayed {delayed:0.0}, lofted {lofted:0.0}; third-man runs {Sum("thirdman.run"):0.0}");
            // real 24.2 a match (before this round 38 here, nearly all of them first time)
            Assert.InRange(completed, 12.0, 32.0);
            // real: 13% immediate (before this round 77% here)
            Assert.InRange(immediate / returns, 0.05, 0.35);
            // real: 11% of the returns in the air (low or high)
            Assert.InRange(lofted / returns, 0.03, 0.25);
            Assert.True(Sum("thirdman.run") > 0, "third-man runs happen");
        }
    }
}

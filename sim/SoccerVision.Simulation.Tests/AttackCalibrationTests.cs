using System;
using System.Linq;
using System.Threading.Tasks;
using SoccerVision.Simulation;
using Xunit;
using Xunit.Abstractions;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>
    /// Phase 5 of the attack redesign: the engine's attacking play held against real football. The targets come from 194 matches
    /// of the StatsBomb open data (tools/calibration/targets.json, data: StatsBomb); the bands here are wide enough for 24
    /// matches, and narrow enough to catch the old habits coming back - the forward carrying it on to the keeper, the six-yard box
    /// full of shots, no shots from the edge of the box, every finish a controlled one, every decision the same.
    /// </summary>
    public class AttackCalibrationTests
    {
        private readonly ITestOutputHelper _out;
        public AttackCalibrationTests(ITestOutputHelper output) { _out = output; }

        [Fact]
        public void TheAttackLooksLikeRealFootball()
        {
            var es = new MatchEngine[48]; // (24 until polish round 1: a 24-match mean of goals moves by about 0.35 on its own)
            Parallel.For(0, es.Length, i =>
            {
                ulong seed = 4400 + (ulong)i;
                es[i] = new MatchEngine(DemoTeams.Red(seed), DemoTeams.Blue(seed), seed, MatchConfig.CreateDefault()) { CollectDiagnostics = true };
                es[i].RunToEnd();
            });
            double Sum(string prefix) => es.Sum(e => e.Diagnostics.Where(kv => kv.Key.StartsWith(prefix)).Sum(kv => (double)kv.Value));
            double n = es.Length;

            double goals = es.Sum(e => e.Home.Score + e.Away.Score) / n;
            double shots = Sum("shot.zone.") / n;
            double six = Sum("shot.zone.six") / n, outside = Sum("shot.zone.out") / n;
            double keeperClose = Sum("shot.keeper.a<3") / Math.Max(1, Sum("shot.keeper."));
            double ftGoals = Sum("goal.how.volley") + Sum("goal.how.firstTime");
            double firstTime = ftGoals / Math.Max(1, ftGoals + Sum("goal.how.controlled"));
            double decisions = es.Sum(e => e.Diagnostics.Where(kv => kv.Key.StartsWith("decide.") && kv.Key != "decide.variation" && kv.Key.Count(c => c == '.') == 1).Sum(kv => (double)kv.Value)
                                         + e.Diagnostics.Where(kv => kv.Key.StartsWith("decide.Pass.")).Sum(kv => (double)kv.Value));
            double variation = Sum("decide.variation") / Math.Max(1, decisions);
            _out.WriteLine($"goals {goals:0.00}, shots {shots:0.0} (six-yard {six:0.00}, outside {outside:0.0}), keeper under 3 m {keeperClose:P1}, first-time goals {firstTime:P0}, decisions varied {variation:P0}");

            // real (StatsBomb): 2.53 goals, 24.8 shots, 1.7 of them from the six-yard box, 9.7 from outside the box
            Assert.InRange(goals, 1.6, 3.6);
            Assert.InRange(shots, 16, 40);
            Assert.True(six < 3.5, $"six-yard shots {six:0.00} a match (real 1.7)");
            Assert.True(outside > 0.22 * shots, $"outside-box shots {outside:0.0} of {shots:0.0} (real 39%)");
            // real: 3.9% of foot shots are struck with the keeper under 3 m away; 55% of open-play foot goals are hit first time
            Assert.True(keeperClose < 0.08, $"foot shots with the keeper under 3 m away {keeperClose:P1} (real 3.9%)");
            Assert.True(firstTime > 0.3, $"foot goals hit first time {firstTime:P0} (real 55%)");
            // natural variation: some decisions go to a near-equal option, most to the best one
            Assert.InRange(variation, 0.05, 0.45);
        }
    }
}

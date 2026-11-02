using System;
using System.Linq;
using System.Threading.Tasks;
using SoccerVision.Simulation;
using Xunit;
using Xunit.Abstractions;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>
    /// Reading the game (MatchEngine.TeamAI.Intelligence.cs): the presser split, the tempo of the moment, deep completions.
    /// </summary>
    public class IntelligenceTests
    {
        private readonly ITestOutputHelper _out;
        public IntelligenceTests(ITestOutputHelper output) { _out = output; }

        private static MatchEngine[] Play(ulong from, int n)
        {
            var es = new MatchEngine[n];
            Parallel.For(0, n, i =>
            {
                es[i] = new MatchEngine(DemoTeams.Red(from + (ulong)i), DemoTeams.Blue(from + (ulong)i), from + (ulong)i, MatchConfig.CreateDefault()) { CollectDiagnostics = true };
                es[i].RunToEnd();
            });
            return es;
        }

        [Fact]
        public void TheSupportSplitsThePresserTheTempoChangesAndBallsArePlayedIntoTheDeepZone()
        {
            var es = Play(9100, 8);
            double D(string k) => es.Average(e => e.Diagnostics.GetValueOrDefault(k));
            double split = D("support.split"), same = D("support.sameSide");
            double share = split / Math.Max(1, split + same);
            double deep = es.Average(e => e.Home.Stats.DeepCompletions + e.Away.Stats.DeepCompletions) / 2.0;
            _out.WriteLine($"presser split {share:P0} of {split + same:0} support choices; tempo: accelerate {D("tempo.accelerate"):0}, calm {D("tempo.calm"):0}; deep completions per team {deep:0.0}");
            // the second man takes the other side of the presser far more often than not (not always: the space decides too)
            Assert.True(share > 0.6, $"split share {share:P0}");
            // both moments happen in a match: going after a regain, calming down under a press
            Assert.True(D("tempo.accelerate") > 0 && D("tempo.calm") > 0);
            // top leagues: a handful of deep completions per team a match
            Assert.InRange(deep, 2, 25);
            Assert.All(es, e => Assert.Equal(0, e.RecoveredErrors));
        }
    }
}

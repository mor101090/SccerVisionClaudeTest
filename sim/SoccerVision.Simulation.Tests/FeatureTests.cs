using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    public class FeatureTests
    {
        private static MatchEngine[] PlayMany(ulong first, int n)
        {
            var engines = new MatchEngine[n];
            Parallel.For(0, n, i =>
            {
                engines[i] = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), first + (ulong)i);
                engines[i].RunToEnd();
            });
            return engines;
        }

        [Fact]
        public void OffsidesHappenInMostMatches()
        {
            var engines = PlayMany(700, 16);
            int withOffside = engines.Count(e => e.EventLog.Any(x => x.Type == MatchEventType.Offside));
            double average = engines.Average(e => e.Home.Stats.Offsides + e.Away.Stats.Offsides);
            Assert.True(withOffside >= 13, $"only {withOffside}/16 matches had an offside");
            Assert.InRange(average, 2.0, 7.0);
        }

        [Fact]
        public void GoalkeepersRushOutOfTheArea()
        {
            var engines = PlayMany(800, 12);
            double average = engines.Average(e => e.Home.Stats.Sorties + e.Away.Stats.Sorties);
            Assert.True(average >= 2.0, $"average sorties {average}");
        }

        [Fact]
        public void GoalCelebrationsVary()
        {
            var kinds = new HashSet<CelebrationKind>();
            for (ulong seed = 900; seed < 940 && kinds.Count < 5; seed++)
            {
                var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), seed);
                while (!e.IsFinished)
                {
                    e.Advance(0.25f);
                    if (e.Phase == MatchPhase.GoalCelebration) kinds.Add(e.Celebration);
                }
            }
            Assert.True(kinds.Count >= 4, "celebrations seen: " + string.Join(",", kinds));
        }
    }
}

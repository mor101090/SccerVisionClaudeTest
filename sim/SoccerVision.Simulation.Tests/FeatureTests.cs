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
        public void HeadersVolleysAndCutInsideShotsHappen()
        {
            var engines = PlayMany(1100, 16);
            int headers = engines.Sum(e => e.EventLog.Count(x => x.Type == MatchEventType.Header));
            int headedShots = engines.Sum(e => e.Home.Stats.Headers + e.Away.Stats.Headers);
            int volleys = engines.Sum(e => e.Home.Stats.Volleys + e.Away.Stats.Volleys);
            int cutIns = engines.Sum(e => e.Home.Stats.CutInShots + e.Away.Stats.CutInShots);
            Assert.True(headers >= 16 * 10, "headers " + headers);
            Assert.True(headedShots >= 16, "headed shots " + headedShots);
            Assert.True(volleys >= 16, "volleys " + volleys);
            Assert.True(cutIns >= 16, "cut-inside shots " + cutIns);
            Assert.Contains(engines.SelectMany(e => e.EventLog), x => x.Type == MatchEventType.Shot && x.Detail == 1 && x.Text.Contains("volleys"));
        }

        [Fact]
        public void GoalCelebrationsVaryAndOnlyAFewJoinIn()
        {
            var kinds = new Dictionary<CelebrationKind, int>();
            var joiners = new List<int>();
            for (ulong seed = 900; seed < 916; seed++)
            {
                var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), seed);
                bool inCelebration = false;
                while (!e.IsFinished)
                {
                    e.Advance(0.25f);
                    bool now = e.Phase == MatchPhase.GoalCelebration;
                    if (now && !inCelebration && e.CelebrationJoiners.Count + 1 > 0)
                    {
                        kinds[e.Celebration] = kinds.GetValueOrDefault(e.Celebration) + 1;
                        joiners.Add(e.CelebrationJoiners.Count);
                    }
                    inCelebration = now;
                }
            }
            int goals = joiners.Count;
            Assert.True(goals >= 25, "goals " + goals);
            Assert.True(kinds.Count >= 5, "celebrations seen: " + string.Join(",", kinds.Keys));
            Assert.True(kinds.GetValueOrDefault(CelebrationKind.Huddle) <= goals * 0.25, "huddles " + kinds.GetValueOrDefault(CelebrationKind.Huddle) + "/" + goals);
            Assert.True(joiners.Average() <= 3.0, "average joiners " + joiners.Average());
            Assert.All(joiners, j => Assert.InRange(j, 0, 6));
        }

        [Fact]
        public void KeeperReleaseIsNeverChargedDown()
        {
            // Regression: at a corner the keeper caught the ball, kicked it out of his hands into an opponent
            // standing next to him and it rebounded into the net (measured 64 charge-downs, 8 goals per 100 matches).
            var engines = PlayMany(9000, 20);
            Assert.Equal(0, engines.Sum(e => e.KeeperChargeDownGoals));
            Assert.True(engines.Sum(e => e.KeeperChargeDowns) <= 1, "charge-downs " + engines.Sum(e => e.KeeperChargeDowns));
        }

        [Fact]
        public void WhistlesFollowIfabGuidance()
        {
            foreach (var e in PlayMany(400, 6))
            {
                var log = e.EventLog;
                var whistles = log.Where(x => x.Type == MatchEventType.Whistle).ToList();
                Assert.Single(whistles, w => w.Whistle == WhistleKind.HalfTime);
                Assert.Single(whistles, w => w.Whistle == WhistleKind.FullTime);
                for (int i = 0; i < log.Count; i++)
                {
                    var x = log[i];
                    // A clear goal needs no whistle to stop play.
                    if (x.Type == MatchEventType.Goal)
                        Assert.False(i + 1 < log.Count && log[i + 1].Type == MatchEventType.Whistle && log[i + 1].ClockSeconds == x.ClockSeconds, "whistle on a goal at " + x.Minute);
                    // Offside: a light whistle straight after the flag.
                    if (x.Type == MatchEventType.Offside)
                        Assert.True(i + 1 < log.Count && log[i + 1].Type == MatchEventType.Whistle && log[i + 1].Whistle == WhistleKind.Light, "offside without whistle at " + x.Minute);
                    // Penalties are given with the long, strong blast.
                    if (x.Type == MatchEventType.PenaltyAwarded)
                        Assert.Contains(log.Take(i).Reverse().Take(4), w => w.Type == MatchEventType.Whistle && w.Whistle == WhistleKind.Long);
                }
                // Restart whistles only for kick-offs, penalties and free kicks that waited for one: far fewer than all restarts.
                int restartWhistles = whistles.Count(w => w.Whistle == WhistleKind.Short);
                int kickOffs = log.Count(x => x.Type == MatchEventType.KickOff);
                int restarts = kickOffs + e.Home.Stats.ThrowIns + e.Away.Stats.ThrowIns + e.Home.Stats.GoalKicks + e.Away.Stats.GoalKicks
                               + e.Home.Stats.Corners + e.Away.Stats.Corners + e.Home.Stats.FreeKicks + e.Away.Stats.FreeKicks;
                Assert.True(restartWhistles >= kickOffs, "kick-offs need the whistle");
                Assert.True(restartWhistles < restarts * 0.4, $"{restartWhistles} restart whistles for {restarts} restarts");
            }
        }

        [Fact]
        public void HeadersAreTimedLeaps()
        {
            var engines = new MatchEngine[6];
            Parallel.For(0, engines.Length, i =>
            {
                engines[i] = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 300 + (ulong)i) { CollectDiagnostics = true };
                engines[i].RunToEnd();
            });
            int Get(string k) => engines.Sum(e => e.Diagnostics.GetValueOrDefault(k));
            int apex = Get("header.jump.apex"), air = Get("header.jump.airborne"), ground = Get("header.jump.ground");
            Assert.True(apex + air + ground > 100, "high headers " + (apex + air + ground));
            Assert.True(apex + air >= 0.8 * (apex + air + ground), $"apex {apex} airborne {air} ground {ground}");
        }
    }
}

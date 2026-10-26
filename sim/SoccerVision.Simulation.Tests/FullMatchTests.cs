using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>
    /// The base requirement: every demo match reaches 90 minutes (plus added time) without errors, with
    /// 10 real minutes per half and a 2 minute break.
    /// </summary>
    public class FullMatchTests
    {
        private static MatchEngine Play(ulong seed, MatchConfig config = null, float realStep = 0.25f)
        {
            var engine = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), seed, config);
            engine.RunToEnd(realStep);
            return engine;
        }

        [Fact]
        public void DefaultTimingMatchesTheDesign()
        {
            var c = MatchConfig.CreateDefault();
            Assert.Equal(600f, c.HalfRealSeconds);
            Assert.Equal(120f, c.HalftimeRealSeconds);
            Assert.Equal(4.5f, c.ClockSecondsPerRealSecond, 3);
        }

        [Theory]
        [InlineData(1UL)]
        [InlineData(2UL)]
        [InlineData(3UL)]
        [InlineData(42UL)]
        [InlineData(2026UL)]
        public void MatchReachesFullTimeWithCorrectDurations(ulong seed)
        {
            var e = Play(seed);
            var c = e.Config;
            Assert.True(e.IsFinished);
            Assert.Equal(MatchPeriod.FullTime, e.Clock.Period);
            Assert.Equal(0, e.RecoveredErrors);
            Assert.Equal(0, e.RuleViolations);

            var added = e.EventLog.Where(x => x.Type == MatchEventType.AddedTime).Select(x => (int)x.Value).ToList();
            Assert.Equal(2, added.Count);
            Assert.InRange(added[0], c.MinAddedMinutesFirstHalf, c.MaxAddedMinutes);
            Assert.InRange(added[1], c.MinAddedMinutesSecondHalf, c.MaxAddedMinutes);

            // Each half: 600 real seconds for 45 clock minutes, plus the added minutes, plus at most the
            // end-of-half grace (an attack may finish) - all at 4.5 clock seconds per real second.
            float perMinute = 60f / c.ClockSecondsPerRealSecond;
            float grace = c.EndOfHalfGraceClockSeconds / c.ClockSecondsPerRealSecond + 2f;
            Assert.InRange(e.FirstHalfRealSeconds, c.HalfRealSeconds + added[0] * perMinute - 0.5f, c.HalfRealSeconds + added[0] * perMinute + grace);
            Assert.InRange(e.SecondHalfRealSeconds, c.HalfRealSeconds + added[1] * perMinute - 0.5f, c.HalfRealSeconds + added[1] * perMinute + grace);
            Assert.Equal(c.HalftimeRealSeconds, e.HalftimeRealSeconds, 2);

            Assert.True(e.Clock.Seconds >= 2f * c.RegulationHalfClockSeconds + added[1] * 60f - 1f);
        }

        [Fact]
        public void PeriodsHappenInOrder()
        {
            var e = Play(7);
            var sequence = e.EventLog
                .Where(x => x.Type == MatchEventType.KickOff || x.Type == MatchEventType.HalfTime || x.Type == MatchEventType.SecondHalfStart || x.Type == MatchEventType.FullTime)
                .Select(x => x.Type)
                .ToList();
            Assert.Equal(MatchEventType.KickOff, sequence.First());
            Assert.Equal(MatchEventType.FullTime, sequence.Last());
            int ht = sequence.IndexOf(MatchEventType.HalfTime);
            Assert.True(ht > 0);
            Assert.Equal(MatchEventType.SecondHalfStart, sequence[ht + 1]);
            Assert.Equal(MatchEventType.KickOff, sequence[ht + 2]);
            Assert.Single(sequence, t => t == MatchEventType.HalfTime);
            Assert.Single(sequence, t => t == MatchEventType.FullTime);

            // Teams kick off one half each, and after conceding.
            var kickoffs = e.EventLog.Where(x => x.Type == MatchEventType.KickOff).ToList();
            var firstHalfKickOff = kickoffs.First(x => x.Period == MatchPeriod.FirstHalf);
            var secondHalfKickOff = kickoffs.First(x => x.Period == MatchPeriod.SecondHalf);
            Assert.NotEqual(firstHalfKickOff.Team, secondHalfKickOff.Team);
            int goals = e.Home.Score + e.Away.Score;
            Assert.Equal(2 + goals, kickoffs.Count + GoalsWithoutKickOff(e));
        }

        // A goal scored when the half is over is not followed by a kick-off.
        private static int GoalsWithoutKickOff(MatchEngine e)
        {
            int n = 0;
            var log = e.EventLog;
            for (int i = 0; i < log.Count; i++)
            {
                if (log[i].Type != MatchEventType.Goal) continue;
                for (int j = i + 1; j < log.Count; j++)
                {
                    if (log[j].Type == MatchEventType.KickOff) break;
                    if (log[j].Type == MatchEventType.HalfTime || log[j].Type == MatchEventType.FullTime) { n++; break; }
                }
            }
            return n;
        }

        [Fact]
        public void ScoreMatchesGoalEvents()
        {
            for (ulong seed = 10; seed < 16; seed++)
            {
                var e = Play(seed);
                int home = e.EventLog.Count(x => x.Type == MatchEventType.Goal && x.Team == 0);
                int away = e.EventLog.Count(x => x.Type == MatchEventType.Goal && x.Team == 1);
                Assert.Equal(home, e.Home.Score);
                Assert.Equal(away, e.Away.Score);
                Assert.Equal(e.Home.Score, e.Home.Stats.Goals);
            }
        }

        [Fact]
        public void ClockNeverGoesBackwardsWithinAHalf()
        {
            var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 99);
            float last = 0f;
            var period = e.Clock.Period;
            while (!e.IsFinished)
            {
                e.Advance(0.5f);
                if (e.Clock.Period != period)
                {
                    period = e.Clock.Period;
                    last = e.Clock.Seconds;
                    continue;
                }
                Assert.True(e.Clock.Seconds >= last - 1e-3f, $"clock went back {last} -> {e.Clock.Seconds}");
                last = e.Clock.Seconds;
            }
        }

        [Fact]
        public void ResultIsIndependentOfFrameRate()
        {
            // Unity renders at a variable frame rate; the fixed-step simulation must give the same match.
            var a = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 123);
            var b = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 123);
            var rng = new System.Random(5);
            a.RunToEnd(0.25f);
            while (!b.IsFinished) b.Advance(1f / 60f + (float)rng.NextDouble() * 0.02f);
            Assert.Equal(a.Home.Score, b.Home.Score);
            Assert.Equal(a.Away.Score, b.Away.Score);
            Assert.Equal(a.StepCount, b.StepCount);
            Assert.Equal(a.EventLog.Count, b.EventLog.Count);
        }

        [Fact]
        public void SameSeedSameMatch()
        {
            var a = Play(77);
            var b = Play(77);
            Assert.Equal(a.EventLog.Count, b.EventLog.Count);
            for (int i = 0; i < a.EventLog.Count; i++)
            {
                Assert.Equal(a.EventLog[i].Type, b.EventLog[i].Type);
                Assert.Equal(a.EventLog[i].Text, b.EventLog[i].Text);
            }
        }

        [Fact]
        public void ManyMatchesStayHealthy()
        {
            const int n = 24;
            var engines = new MatchEngine[n];
            Parallel.For(0, n, i => engines[i] = Play(500UL + (ulong)i));
            foreach (var e in engines)
            {
                Assert.True(e.IsFinished);
                Assert.Equal(0, e.RecoveredErrors);
                Assert.Equal(0, e.RuleViolations);
                Assert.InRange(e.Home.Score + e.Away.Score, 0, 14);
                foreach (var t in e.Teams)
                {
                    Assert.True(t.Stats.Passes > 100);
                    Assert.True(t.Stats.PassesCompleted <= t.Stats.Passes);
                    Assert.True(t.Stats.ShotsOnTarget <= t.Stats.Shots);
                    Assert.True(t.ActiveCount >= 7, "a team can be reduced but must keep at least 7 players");
                }
            }
            // Calibration guard rails (averages over the batch, wide bands around real-world values).
            double goals = engines.Average(e => e.Home.Score + e.Away.Score);
            double fouls = engines.Average(e => e.Home.Stats.Fouls + e.Away.Stats.Fouls);
            double yellows = engines.Average(e => e.Home.Stats.YellowCards + e.Away.Stats.YellowCards);
            double passPct = engines.Average(e => (double)(e.Home.Stats.PassesCompleted + e.Away.Stats.PassesCompleted) / (e.Home.Stats.Passes + e.Away.Stats.Passes));
            Assert.InRange(goals, 1.5, 5.0);
            Assert.InRange(fouls, 12, 36);
            Assert.InRange(yellows, 1.5, 7);
            Assert.InRange(passPct, 0.7, 0.92);
        }

        [Theory]
        [InlineData(1.0f, 300f, 60f)]
        [InlineData(3.0f, 600f, 120f)]
        [InlineData(2.25f, 120f, 10f)]
        public void OtherTimingConfigurationsAlsoFinish(float motion, float half, float halftime)
        {
            var c = MatchConfig.CreateDefault();
            c.MotionSpeed = motion;
            c.HalfRealSeconds = half;
            c.HalftimeRealSeconds = halftime;
            var e = Play(31, c);
            Assert.True(e.IsFinished);
            Assert.Equal(0, e.RecoveredErrors);
            Assert.Equal(halftime, e.HalftimeRealSeconds, 2);
            Assert.True(e.FirstHalfRealSeconds >= half);
        }

        [Fact]
        public void TacticsCommandIsApplied()
        {
            var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 5);
            var t = new TeamTactics { Mentality = 0.9f, PressingIntensity = 1.4f };
            e.Submit(new ChangeTacticsCommand(0, t));
            e.Advance(0.1f);
            Assert.Equal(0.9f, e.Home.Tactics.Mentality, 3);
            Assert.Equal(1f, e.Home.Tactics.PressingIntensity, 3); // sanitised to 0..1
            e.RunToEnd();
            Assert.True(e.IsFinished);
        }
    }
}

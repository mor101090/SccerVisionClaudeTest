using System;
using System.Linq;
using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>Cup ties: penalties straight after 90 minutes (cup), extra time and then penalties (league cup).</summary>
    public class CupTests
    {
        private static MatchEngine Engine(ulong seed, Competition c)
        {
            var cfg = MatchConfig.CreateDefault();
            cfg.Competition = c;
            return new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), seed, cfg);
        }

        [Fact]
        public void ALevelCupTieIsDecidedOnPenalties()
        {
            int shootouts = 0;
            for (ulong seed = 1; seed <= 30 && shootouts < 2; seed++)
            {
                var e = Engine(seed, Competition.Cup);
                e.RunToEnd();
                Assert.True(e.IsFinished);
                Assert.Equal(0, e.RecoveredErrors);
                Assert.False(e.ExtraTimeReached, "the cup has no extra time");
                if (e.Home.Score != e.Away.Score) { Assert.Null(e.ShootoutWinner); continue; }
                shootouts++;
                Assert.NotNull(e.ShootoutWinner);
                Assert.NotEqual(e.ShootoutScore[0], e.ShootoutScore[1]);
                int kicks0 = e.ShootoutKicks.Count(k => k.team == 0), kicks1 = e.ShootoutKicks.Count(k => k.team == 1);
                Assert.True(Math.Abs(kicks0 - kicks1) <= 1, "the teams kick in turn");
                Assert.True(e.ShootoutKicks.Count >= 6, "at least three kicks each before it can be decided");
                Assert.Contains(e.EventLog, x => x.Type == MatchEventType.ShootoutKick);
            }
            Assert.True(shootouts > 0, "no level tie in 30 matches");
        }

        [Fact]
        public void ALevelLeagueCupTiePlaysExtraTimeWithOneMoreSubstitution()
        {
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var e = Engine(seed, Competition.LeagueCup);
                e.RunToEnd();
                Assert.Equal(0, e.RecoveredErrors);
                if (!e.ExtraTimeReached) continue;
                Assert.Contains(e.EventLog, x => x.Type == MatchEventType.ExtraTime && x.Detail == 1);
                Assert.Contains(e.EventLog, x => x.Type == MatchEventType.ExtraTime && x.Detail == 2);
                Assert.Equal(e.Config.MaxSubstitutions + 1, e.MaxSubstitutionsNow);
                Assert.True(e.Clock.Seconds > 2 * 2700f + 1800f - 60f, "120 minutes were played");
                if (e.Home.Score == e.Away.Score) Assert.NotNull(e.ShootoutWinner);
                return;
            }
            Assert.Fail("no level tie in 30 matches");
        }

        [Fact]
        public void ALeagueMatchCanEndLevel()
        {
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var e = Engine(seed, Competition.League);
                e.RunToEnd();
                if (e.Home.Score != e.Away.Score) continue;
                Assert.False(e.ExtraTimeReached);
                Assert.Null(e.ShootoutWinner);
                return;
            }
        }
    }
}

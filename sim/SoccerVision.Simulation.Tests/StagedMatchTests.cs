using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>The staged match: the same incidents in every run, whatever the weather and the pitch.</summary>
    public class StagedMatchTests
    {
        private static MatchEngine Staged(ulong seed, Weather weather = Weather.Sunny, float maintenance = 1f, float wind = 0f)
        {
            var c = MatchConfig.CreateDefault();
            c.Conditions = new MatchConditions { Weather = weather, Maintenance = maintenance, WindSpeed = wind, WindAngle = 1f };
            c.Script = MatchScript.Staged();
            var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), seed, c);
            e.RunToEnd();
            return e;
        }

        [Theory]
        [InlineData(7274UL, Weather.Sunny, 1f, 0f)]
        [InlineData(5UL, Weather.Rain, 0f, 12f)]
        [InlineData(77UL, Weather.Snow, 0.5f, 0f)]
        [InlineData(4001UL, Weather.Cloudy, 1f, 12f)]
        public void TheStagedMatchHasTwoRedCardsALightAndASeriousInjuryAndASubstitution(ulong seed, Weather weather, float maintenance, float wind)
        {
            var e = Staged(seed, weather, maintenance, wind);
            Assert.True(e.IsFinished);
            Assert.Equal(0, e.RecoveredErrors);
            Assert.Equal(0, e.RuleViolations);
            var log = e.EventLog;
            var reds = log.Where(x => x.Type == MatchEventType.RedCard).ToList();
            Assert.Equal(2, reds.Count);
            Assert.True(reds[0].Value < 0.5f, "the first red is a direct red");
            Assert.True(reds[1].Value > 0.5f, "the second red follows a second yellow");
            Assert.Contains("Second yellow", reds[1].Text);
            // the player of the second red was cautioned before, and the direct red was shown to the other team
            Assert.NotEqual(reds[0].Team, reds[1].Team);
            var yellowsOfSecond = log.Count(x => x.Type == MatchEventType.YellowCard && x.Player == reds[1].Player && x.Team == reds[1].Team);
            Assert.True(yellowsOfSecond >= 2, "the second red's player was booked twice");
            Assert.Equal(1, log.Count(x => x.Type == MatchEventType.Injury && x.Detail == 0));
            Assert.Equal(1, log.Count(x => x.Type == MatchEventType.Injury && x.Detail == 1));
            Assert.Equal(1, log.Count(x => x.Type == MatchEventType.Substitution));
            // in order: light injury, direct red, serious injury, second red
            float Clock(MatchEventType t, int detail = -1) => log.First(x => x.Type == t && (detail < 0 || x.Detail == detail)).ClockSeconds;
            Assert.True(Clock(MatchEventType.Injury, 0) < reds[0].ClockSeconds);
            Assert.True(reds[0].ClockSeconds < Clock(MatchEventType.Injury, 1));
            Assert.True(Clock(MatchEventType.Injury, 1) < reds[1].ClockSeconds);
        }

        [Fact]
        public void TheStagedMatchIsTheSameEveryTime()
        {
            var a = Staged(7274);
            var b = Staged(7274);
            Assert.Equal(a.EventLog.Count, b.EventLog.Count);
            Assert.Equal(a.Home.Score, b.Home.Score);
            Assert.Equal(a.Away.Score, b.Away.Score);
            for (int i = 0; i < a.EventLog.Count; i++) Assert.Equal(a.EventLog[i].Text, b.EventLog[i].Text);
        }
    }
}

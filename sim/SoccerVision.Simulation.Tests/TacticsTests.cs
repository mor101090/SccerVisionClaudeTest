using System.Linq;
using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>Team tactics (line, width, passing, tempo, style), one-twos, the restart pause and the computer manager.</summary>
    public class TacticsTests
    {
        private static MatchEngine Run(ulong seed, System.Action<TeamTactics> home = null)
        {
            var red = DemoTeams.Red();
            home?.Invoke(red.Tactics);
            var e = new MatchEngine(red, DemoTeams.Blue(), seed, MatchConfig.CreateDefault());
            e.RunToEnd();
            Assert.True(e.IsFinished);
            Assert.Equal(0, e.RecoveredErrors);
            return e;
        }

        private static double Avg(ulong[] seeds, System.Func<MatchEngine, double> f, System.Action<TeamTactics> home) =>
            seeds.Select(s => f(Run(s, home))).Average();

        private static readonly ulong[] Seeds = { 11, 12, 13, 14 };

        [Fact]
        public void TheDefensiveLineSliderMovesTheBackLine()
        {
            double deep = Avg(Seeds, e => e.HomeLineSum / e.HomeLineSamples, t => t.DefensiveLine = 0.05f);
            double high = Avg(Seeds, e => e.HomeLineSum / e.HomeLineSamples, t => t.DefensiveLine = 0.95f);
            Assert.True(high > deep + 8, $"high line {high:0.0} m v deep {deep:0.0} m from own goal");
        }

        [Fact]
        public void TheWidthSliderSpreadsThePlayers()
        {
            double narrow = Avg(Seeds, e => e.HomeSpreadSum / e.HomeLineSamples, t => t.Width = 0.05f);
            double wide = Avg(Seeds, e => e.HomeSpreadSum / e.HomeLineSamples, t => t.Width = 0.95f);
            Assert.True(wide > narrow + 6, $"wide {wide:0.0} m v narrow {narrow:0.0} m");
        }

        [Fact]
        public void ShortPassingPassesMoreAndKicksLongLess()
        {
            int Long(MatchEngine e) => e.EventLog.Count(x => x.Type == MatchEventType.LongBall && x.Team == 0);
            double shortPasses = Avg(Seeds, e => e.Home.Stats.Passes, t => t.Passing = PassingStyle.Short);
            double directPasses = Avg(Seeds, e => e.Home.Stats.Passes, t => t.Passing = PassingStyle.Direct);
            double shortLong = Avg(Seeds, e => Long(e), t => t.Passing = PassingStyle.Short);
            double directLong = Avg(Seeds, e => Long(e), t => t.Passing = PassingStyle.Direct);
            Assert.True(shortPasses > directPasses * 1.1, $"passes short {shortPasses:0} v direct {directPasses:0}");
            Assert.True(directLong > shortLong * 1.5, $"long balls direct {directLong:0} v short {shortLong:0}");
        }

        [Fact]
        public void AnAggressiveTeamFoulsMoreThanAGentleOne()
        {
            double gentle = Avg(Seeds, e => e.Home.Stats.Fouls, t => { t.Tackling = TacklingStyle.StayOnFeet; t.PressingIntensity = 0.2f; });
            double hard = Avg(Seeds, e => e.Home.Stats.Fouls, t => { t.Tackling = TacklingStyle.GetStuckIn; t.PressingIntensity = 0.8f; });
            Assert.True(hard > gentle * 1.25, $"fouls aggressive {hard:0.0} v gentle {gentle:0.0}");
        }

        [Fact]
        public void PlayersPlayOneTwos()
        {
            var engines = Seeds.Select(s => Run(s)).ToList();
            int tried = engines.Sum(e => e.Home.Stats.OneTwos + e.Away.Stats.OneTwos);
            int done = engines.Sum(e => e.Home.Stats.OneTwosCompleted + e.Away.Stats.OneTwosCompleted);
            Assert.True(tried >= 6, $"one-twos {tried} in 3 matches");
            Assert.True(done >= 2, $"completed one-twos {done}");
            Assert.Contains(engines.SelectMany(e => e.EventLog), x => x.Type == MatchEventType.Pass && x.Text.Contains("play a one-two"));
        }

        [Fact]
        public void ATacticsChangeIsAnnouncedAndApplied()
        {
            var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 21, MatchConfig.CreateDefault());
            e.Advance(30f);
            e.Submit(new ChangeTacticsCommand(0, new TeamTactics { DefensiveLine = 0.9f, Width = 0.9f, Passing = PassingStyle.Direct, Tempo = 0.85f, Tackling = TacklingStyle.GetStuckIn }));
            e.Advance(2f);
            Assert.Equal(PassingStyle.Direct, e.Home.Tactics.Passing);
            var ev = Assert.Single(e.EventLog, x => x.Type == MatchEventType.TacticsChanged);
            Assert.Equal("Red Rovers change their tactics: high line, wide, balls in behind, fast tempo, aggressive", ev.Text);
        }

        [Fact]
        public void TheComputerAnswersAHighLineWithBallsInBehind()
        {
            var red = DemoTeams.Red();
            red.Tactics.DefensiveLine = 0.9f;
            var e = new MatchEngine(red, DemoTeams.Blue(), 31, MatchConfig.CreateDefault());
            var cpu = new ComputerManager(e, 1);
            for (int i = 0; i < 4000 && e.Clock.Seconds < 20 * 60; i++)
            {
                e.Advance(0.25f);
                cpu.Update();
            }
            e.Advance(1f);
            Assert.Equal(PassingStyle.Direct, e.Away.Tactics.Passing);
            Assert.Contains(e.EventLog, x => x.Type == MatchEventType.TacticsChanged && x.Team == 1 && x.Text.Contains("balls in behind"));
        }

        [Fact]
        public void TheComputerChasesTheGameWhenLosingLate()
        {
            var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 41, MatchConfig.CreateDefault());
            var cpu = new ComputerManager(e, 1);
            while (!e.IsFinished && e.Clock.Seconds < 60 * 60)
            {
                e.Advance(0.25f);
                if (e.Clock.Seconds > 50 * 60) e.Home.Score = e.Away.Score + 1; // the human leads
                cpu.Update();
            }
            e.Advance(1f);
            Assert.True(e.Away.Tactics.Mentality > 0.7f && e.Away.Tactics.DefensiveLine >= 0.7f);
            Assert.Contains(e.EventLog, x => x.Type == MatchEventType.TacticsChanged && x.Team == 1 && x.Text.Contains("all-out attack"));
        }

        [Fact]
        public void PlayStopsForAMomentBeforeARestart()
        {
            var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 51, MatchConfig.CreateDefault());
            // find a throw-in, a corner, a goal kick or a free kick and look at the first second after it is given
            for (int i = 0; i < 200000 && !e.IsFinished; i++)
            {
                e.Advance(0.05f);
                if (e.Phase == MatchPhase.Stoppage && e.CurrentRestart != null && e.CurrentRestart.Type == RestartType.ThrowIn && e.CurrentRestart.PauseLeft > 0.5f)
                {
                    e.Advance(0.3f);
                    Assert.Equal(MatchPhase.Stoppage, e.Phase);
                    Assert.True(e.CurrentRestart.PauseLeft > 0f && !e.CurrentRestart.BallPlaced || e.CurrentRestart.PauseLeft > 0f);
                    return;
                }
            }
            Assert.Fail("no throw-in found");
        }
    }
}

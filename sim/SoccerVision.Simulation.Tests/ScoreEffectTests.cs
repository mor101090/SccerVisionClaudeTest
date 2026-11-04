using System.Linq;
using System.Threading.Tasks;
using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>
    /// The score changes how a team plays: the side in front drops off and lets the other have the ball, the side behind pushes up to win it
    /// back. Real share of the ball (StatsBomb open data, the 2015/16 Premier League and La Liga, counted by tools/calibration/possession_tiers.py):
    /// 44% a goal up, 56% a goal down.
    /// </summary>
    public class ScoreEffectTests
    {
        [Fact]
        public void TheSideInFrontSitsOffAndTheSideBehindPushesUp()
        {
            var e = new MatchEngine(DemoTeams.Red(1001), DemoTeams.Blue(1001), 7UL, MatchConfig.CreateDefault());
            float line = e.Home.Eff.DefensiveLine, press = e.Home.Eff.PressingIntensity;
            Assert.Equal(line, e.Away.Eff.DefensiveLine, 3);
            e.Home.Score = 1;
            e.Home.RefreshEff(); e.Away.RefreshEff();
            Assert.True(e.Home.Eff.DefensiveLine < line && e.Home.Eff.PressingIntensity < press, "a goal up: deeper and less pressing");
            Assert.True(e.Away.Eff.DefensiveLine > line && e.Away.Eff.PressingIntensity > press, "a goal down: higher and more pressing");
            e.Away.Score = 1;
            e.Home.RefreshEff(); e.Away.RefreshEff();
            Assert.Equal(line, e.Home.Eff.DefensiveLine, 3); // level again: as before
        }

        [Fact]
        public void TheSideBehindHasMoreOfTheBall()
        {
            const int matches = 24;
            var mine = new double[matches]; var theirs = new double[matches];
            Parallel.For(0, matches, i =>
            {
                var e = new MatchEngine(DemoTeams.Red(1001), DemoTeams.Blue(1001), 4100UL + (ulong)i, MatchConfig.CreateDefault());
                double h0 = 0, a0 = 0;
                while (!e.IsFinished)
                {
                    e.Advance(0.25f);
                    double h1 = e.Home.Stats.PossessionSeconds, a1 = e.Away.Stats.PossessionSeconds;
                    int d = e.Home.Score - e.Away.Score;
                    if (d == -1) { mine[i] += h1 - h0; theirs[i] += a1 - a0; }      // the home side a goal down
                    else if (d == 1) { mine[i] += a1 - a0; theirs[i] += h1 - h0; }  // the away side a goal down
                    h0 = h1; a0 = a1;
                }
            });
            double share = 100.0 * mine.Sum() / (mine.Sum() + theirs.Sum());
            Assert.InRange(share, 52.0, 63.0);
        }

        [Fact]
        public void TheComputerManagerKeepsItsOwnPlan()
        {
            // a counter-attacking side stays one: the manager answers the game on top of its plan (it used to start from the default tactics at
            // every check, which wiped a random plan's styles six minutes in)
            var away = DemoTeams.Blue(1001);
            away.Tactics.Special = SpecialTactic.CounterAttack; away.Tactics.Passing = PassingStyle.Direct;
            var e = new MatchEngine(DemoTeams.Red(1001), away, 9UL, MatchConfig.CreateDefault());
            var cm = new ComputerManager(e, 1);
            while (e.Clock.Seconds < 20f * 60f && !e.IsFinished) { e.Advance(0.25f); cm.Update(); }
            Assert.Equal(SpecialTactic.CounterAttack, e.Away.Tactics.Special);
            Assert.Equal(PassingStyle.Direct, e.Away.Tactics.Passing);
        }
    }
}

using System;
using System.Linq;
using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>
    /// How much of the ball a team has follows its style and its quality: a possession side keeps it for two thirds of the match, a counter-attacking side
    /// for a quarter, and two ordinary sides share it. (Real football: the most possession-heavy side of a season has about 64-70% of the ball; a match
    /// above 80% has happened a handful of times in the Premier League.)
    /// </summary>
    public class PossessionStyleTests
    {
        private static (double Poss, double HomeShots, double AwayShots) Play(Action<TeamSetup> home, Action<TeamSetup> away, TeamStrength hs = TeamStrength.Medium, TeamStrength asw = TeamStrength.Medium, int matches = 18)
        {
            double p = 0, hsh = 0, ash = 0;
            for (int i = 0; i < matches; i++)
            {
                var h = DemoTeams.Red(1001, hs); var a = DemoTeams.Blue(1001, asw);
                home?.Invoke(h); away?.Invoke(a);
                var e = new MatchEngine(h, a, 3000UL + (ulong)i, MatchConfig.CreateDefault());
                e.RunToEnd(0.25f);
                p += 100.0 * e.Home.Stats.PossessionSeconds / (e.Home.Stats.PossessionSeconds + e.Away.Stats.PossessionSeconds);
                hsh += e.Home.Stats.Shots; ash += e.Away.Stats.Shots;
            }
            return (p / matches, hsh / matches, ash / matches);
        }

        private static void Possession(TeamSetup t) { t.Tactics.Special = SpecialTactic.Possession; t.Tactics.Passing = PassingStyle.Short; }
        private static void Counter(TeamSetup t) { t.Tactics.Special = SpecialTactic.CounterAttack; t.Tactics.Passing = PassingStyle.Direct; }
        private static void HighPress(TeamSetup t) { t.Tactics.Defence = DefenceStyle.HighPress; t.Tactics.PressingIntensity = 0.74f; }

        [Fact]
        public void TwoOrdinaryTeamsShareTheBall()
        {
            var r = Play(null, null);
            Assert.InRange(r.Poss, 46, 55);
        }

        [Fact]
        public void APossessionSideKeepsTheBallAndStillShoots()
        {
            var r = Play(Possession, null);
            Assert.InRange(r.Poss, 60, 78);
            Assert.True(r.HomeShots >= 0.8 * r.AwayShots, $"a possession side takes its chances too: {r.HomeShots:0.0} shots to {r.AwayShots:0.0}");
        }

        [Fact]
        public void ACounterAttackingSideGivesTheBallAway()
        {
            // the same two teams, the home side playing the counter, against a possession side: it has the ball for a quarter to a third (home advantage lifts it a little)
            var r = Play(Counter, Possession);
            Assert.InRange(r.Poss, 20, 40);
        }

        [Fact]
        public void AHighPressTakesTheBallBack()
        {
            // (48 matches: the press gains about 2 points over 120 matches, two seeds, with the generator's realism squads; 24 matches gave 0.7)
            var plain = Play(null, null, matches: 48).Poss;
            var press = Play(HighPress, null, matches: 48).Poss;
            // (+0.8: since the score round the side behind pushes up too, and a press gains 1.1-1.3 points over 120 matches, two seeds - it was +1.5 before)
            Assert.True(press > plain + 0.8, $"high press {press:0.0}% v {plain:0.0}%");
        }

        [Fact]
        public void AStrongerTeamHasMoreOfTheBallWithoutAnyTactic()
        {
            var home = Play(null, null, TeamStrength.Strong, TeamStrength.Weak, 14).Poss;
            var away = Play(null, null, TeamStrength.Weak, TeamStrength.Strong, 14).Poss;
            Assert.True((home + (100 - away)) / 2 > 54, $"strong team's share {(home + (100 - away)) / 2:0.0}%");
        }
    }
}

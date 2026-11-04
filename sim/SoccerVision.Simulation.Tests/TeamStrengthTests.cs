using System.Linq;
using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>
    /// The demo squads come at three strengths (weak / medium / strong). Medium is the squad the game always had, so everything measured
    /// before stays as it was; weak and strong are the same two teams made at the team generator's weaker and excellent level.
    /// </summary>
    public class TeamStrengthTests
    {
        private static double Mean(TeamSetup t) =>
            t.Starters.Average(p => { var a = p.Attributes; return new[] { a.Tackling, a.Marking, a.Passing, a.Finishing, a.Speed, a.BallControl, a.Heading, a.Composure }.Average(); });

        [Fact]
        public void EveryStrengthIsAValidSquad()
        {
            foreach (var s in new[] { TeamStrength.Weak, TeamStrength.Medium, TeamStrength.Strong })
                foreach (var t in new[] { DemoTeams.Red(1001, s), DemoTeams.Blue(1001, s) })
                {
                    t.Validate();
                    Assert.Equal(11, t.Starters.Count);
                    Assert.Equal(9, t.Bench.Count);
                    Assert.Equal(PositionGroup.Goalkeeper, t.Starters[0].NaturalPosition);
                    Assert.Single(t.Bench, b => b.NaturalPosition == PositionGroup.Goalkeeper);
                    Assert.Equal(20, t.Starters.Concat(t.Bench).Select(p => p.ShirtNumber).Distinct().Count());
                    Assert.Equal(20, t.Starters.Concat(t.Bench).Select(p => p.Id).Distinct().Count());
                }
        }

        [Fact]
        public void MediumIsTheSquadTheGameAlwaysHad()
        {
            // the default is medium, and medium is the generated squad untouched (only names, numbers and traits are added)
            var def = DemoTeams.Red(); var med = DemoTeams.Red(1001, TeamStrength.Medium); var raw = GeneratedTeams.Red();
            for (int i = 0; i < 11; i++)
            {
                Assert.Equal(def.Starters[i].Attributes.Speed, med.Starters[i].Attributes.Speed);
                Assert.Equal(def.Starters[i].Attributes.Finishing, med.Starters[i].Attributes.Finishing);
                Assert.Equal(raw.Starters[i].Attributes.Speed, med.Starters[i].Attributes.Speed);
                Assert.Equal(raw.Starters[i].HeightCm, med.Starters[i].HeightCm);
            }
        }

        [Fact]
        public void StrongerMeansBetterPlayers()
        {
            foreach (var make in new System.Func<TeamStrength, TeamSetup>[] { s => DemoTeams.Red(1001, s), s => DemoTeams.Blue(1001, s) })
            {
                double w = Mean(make(TeamStrength.Weak)), m = Mean(make(TeamStrength.Medium)), s2 = Mean(make(TeamStrength.Strong));
                Assert.True(m - w >= 8 && s2 - m >= 8, $"weak {w:0.0}, medium {m:0.0}, strong {s2:0.0}: each step should be a real step");
            }
        }

        private static (double Home, double Away, int HomeWins, int AwayWins) Play(TeamStrength home, TeamStrength away, int matches)
        {
            double hg = 0, ag = 0; int hw = 0, aw = 0;
            for (int i = 0; i < matches; i++)
            {
                var e = new MatchEngine(DemoTeams.Red(1001, home), DemoTeams.Blue(1001, away), 900UL + (ulong)i, MatchConfig.CreateDefault());
                e.RunToEnd();
                hg += e.Home.Score; ag += e.Away.Score;
                if (e.Home.Score > e.Away.Score) hw++; else if (e.Home.Score < e.Away.Score) aw++;
            }
            return (hg / matches, ag / matches, hw, aw);
        }

        [Fact]
        public void AStrongerTeamWinsMoreAndScoresMore_InEitherSeat()
        {
            // 16 matches each way: strong v weak, strong v medium, medium v weak (the stronger team at home, then away)
            foreach (var (hi, lo) in new[] { (TeamStrength.Strong, TeamStrength.Weak), (TeamStrength.Strong, TeamStrength.Medium), (TeamStrength.Medium, TeamStrength.Weak) })
            {
                var home = Play(hi, lo, 16);
                Assert.True(home.Home > home.Away && home.HomeWins > home.AwayWins, $"{hi} at home v {lo}: {home.Home:0.00}-{home.Away:0.00}, {home.HomeWins}-{home.AwayWins} wins");
                var away = Play(lo, hi, 16);
                Assert.True(away.Away > away.Home && away.AwayWins > away.HomeWins, $"{lo} at home v {hi}: {away.Home:0.00}-{away.Away:0.00}, {away.HomeWins}-{away.AwayWins} wins");
            }
        }
    }
}

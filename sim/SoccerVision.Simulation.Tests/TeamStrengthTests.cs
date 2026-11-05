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
                Assert.True(m - w >= 4 && s2 - m >= 5, $"weak {w:0.0}, medium {m:0.0}, strong {s2:0.0}: each step should be a real step (the weak squads are pulled 38% and the strong ones 125% of the way from medium, see TeamStrengthScaling)");
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
        public void AStrongerTeamWinsMoreAndScoresMore()
        {
            // 24 matches in each seat per pairing, pooled (the home edge cancels out): the stronger team scores more and wins more than it loses
            foreach (var (hi, lo) in new[] { (TeamStrength.Strong, TeamStrength.Weak), (TeamStrength.Strong, TeamStrength.Medium), (TeamStrength.Medium, TeamStrength.Weak) })
            {
                var h = Play(hi, lo, 24); var a = Play(lo, hi, 24);
                double goalsFor = h.Home + a.Away, goalsAgainst = h.Away + a.Home;
                int wins = h.HomeWins + a.AwayWins, losses = h.AwayWins + a.HomeWins;
                Assert.True(goalsFor > goalsAgainst && wins > losses, $"{hi} v {lo}: goals {goalsFor / 2:0.00}-{goalsAgainst / 2:0.00}, {wins} wins {losses} defeats in 48");
            }
        }

        [Fact]
        public void TheBiggestMismatchIsNotAMassacre()
        {
            // real Premier League: a top-two side beats a bottom-three side 89% of the time, a top four side at home 80% (10 seasons, 3,800 matches).
            // Strong v weak (both seats, 40 matches) must stay in that range: well above a coin flip, well below "always".
            var h = Play(TeamStrength.Strong, TeamStrength.Weak, 20); var a = Play(TeamStrength.Weak, TeamStrength.Strong, 20);
            double winPct = 100.0 * (h.HomeWins + a.AwayWins) / 40;
            Assert.InRange(winPct, 62, 92);
        }
    }
}

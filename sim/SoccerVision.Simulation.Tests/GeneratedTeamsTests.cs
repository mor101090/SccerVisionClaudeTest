using System.Linq;
using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>The demo squads come from the team generator: real differences between players, which the engine plays.</summary>
    public class GeneratedTeamsTests
    {
        [Fact]
        public void TheSquadsAreValidFourFourTwos()
        {
            foreach (var t in new[] { DemoTeams.Red(), DemoTeams.Blue() })
            {
                t.Validate();
                Assert.Equal(11, t.Starters.Count);
                Assert.Equal(7, t.Bench.Count);
                Assert.Equal(PositionGroup.Goalkeeper, t.Starters[0].NaturalPosition);
                Assert.Single(t.Bench, b => b.NaturalPosition == PositionGroup.Goalkeeper);
                Assert.Equal(18, t.Starters.Concat(t.Bench).Select(p => p.ShirtNumber).Distinct().Count());
            }
        }

        [Fact]
        public void PlayersDifferInPaceHeightAndFoot()
        {
            var all = DemoTeams.Red().Starters.Concat(DemoTeams.Blue().Starters).Where(p => p.NaturalPosition != PositionGroup.Goalkeeper).ToList();
            Assert.True(all.Max(p => p.Attributes.Speed) - all.Min(p => p.Attributes.Speed) >= 20);
            Assert.True(all.Max(p => p.HeightCm) - all.Min(p => p.HeightCm) >= 12);
            Assert.Contains(all, p => p.Attributes.StrongFoot == Foot.Left);
            Assert.Contains(all, p => p.Attributes.StrongFoot == Foot.Right);
            Assert.Contains(all, p => p.Attributes.WeakFootRating == 1);
            Assert.Contains(all, p => p.Attributes.WeakFootRating >= 2);
        }

        [Fact]
        public void LeftFootedFullBacksAndWingersPlayOnTheLeft()
        {
            foreach (var t in new[] { DemoTeams.Red(), DemoTeams.Blue() })
            {
                // slots 1/5 are the right flank, 4/8 the left; a flank pair is split by foot when they differ
                foreach (var (r, l) in new[] { (1, 4), (5, 8), (2, 3), (6, 7), (9, 10) })
                {
                    var right = t.Starters[r].Attributes.StrongFoot; var left = t.Starters[l].Attributes.StrongFoot;
                    Assert.False(left == Foot.Right && right == Foot.Left, $"{t.Name} slots {r}/{l}: a left-footer on the right and a right-footer on the left");
                }
            }
        }

        [Fact]
        public void FasterPlayersCoverMoreGround()
        {
            double Run(int speed)
            {
                var home = DemoTeams.Red();
                foreach (var p in home.Starters.Where(p => p.NaturalPosition != PositionGroup.Goalkeeper)) { p.Attributes.Speed = speed; p.Attributes.Acceleration = speed; }
                var e = new MatchEngine(home, DemoTeams.Blue(), 5, MatchConfig.CreateDefault());
                e.RunToEnd();
                return e.Home.Players.Where(p => !p.IsGoalkeeper).Average(p => p.DistanceCovered);
            }
            Assert.True(Run(90) > Run(35) * 1.05);
        }

        [Fact]
        public void TallPlayersWinMoreHeaders()
        {
            double Reach(int height) { var p = DemoTeams.Red().Starters[2]; p.HeightCm = height; return new MatchEngine(DemoTeams.Red().WithStarter(2, p), DemoTeams.Blue(), 1, MatchConfig.CreateDefault()).Home.Players[2].HeadReach; }
            Assert.True(Reach(195) > Reach(165) + 0.2);
        }
    }

    internal static class SetupExtensions
    {
        public static TeamSetup WithStarter(this TeamSetup t, int i, PlayerProfile p) { t.Starters[i] = p; return t; }
    }
}

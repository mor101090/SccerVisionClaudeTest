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
                Assert.Equal(9, t.Bench.Count);
                Assert.Equal(PositionGroup.Goalkeeper, t.Starters[0].NaturalPosition);
                Assert.Single(t.Bench, b => b.NaturalPosition == PositionGroup.Goalkeeper);
                Assert.Equal(20, t.Starters.Concat(t.Bench).Select(p => p.ShirtNumber).Distinct().Count());
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
        public void TheLeftBackIsLeftFootedAndEveryoneHasARealPosition()
        {
            var positions = new[] { "GK", "CB", "RB", "LB", "DM", "CM", "AM", "RW", "LW", "ST" };
            foreach (var t in new[] { DemoTeams.Red(), DemoTeams.Blue() })
            {
                // a left-back is a left-footer when the squad has one (it does)
                Assert.Equal(3, t.Starters[4].Attributes.LeftFoot);
                foreach (var p in t.Starters.Concat(t.Bench)) Assert.Contains(p.PositionLabel, positions);
                // the bench's positions are the real ones, not a group
                Assert.Contains(t.Bench, p => p.PositionLabel == "CB");
                Assert.Contains(t.Bench, p => p.PositionLabel == "RB" || p.PositionLabel == "LB");
            }
        }

        [Fact]
        public void FasterPlayersCoverMoreGround()
        {
            // (over three matches: one match is too noisy)
            double Run(int speed) => new ulong[] { 5, 6, 7 }.AsParallel().Select(seed =>
            {
                var home = DemoTeams.Red();
                foreach (var p in home.Starters.Where(p => p.NaturalPosition != PositionGroup.Goalkeeper)) { p.Attributes.Speed = speed; p.Attributes.Acceleration = speed; }
                var e = new MatchEngine(home, DemoTeams.Blue(), seed, MatchConfig.CreateDefault());
                e.RunToEnd();
                return e.Home.Players.Where(p => !p.IsGoalkeeper).Average(p => (double)p.DistanceCovered);
            }).Average();
            double fast = Run(90), slow = Run(35);
            // (distance covered is mostly work rate and stamina; pace adds a few per cent)
            Assert.True(fast > slow * 1.04, $"distance per player: fast {fast:0} m v slow {slow:0} m");
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

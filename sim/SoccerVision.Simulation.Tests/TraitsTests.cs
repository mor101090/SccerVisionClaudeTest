using System;
using System.Linq;
using System.Threading.Tasks;
using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>Player traits: about three per player, never contradicting, and they change how he plays.</summary>
    public class TraitsTests
    {
        [Fact]
        public void EveryPlayerHasAboutThreeTraitsThatDoNotContradict()
        {
            var seen = PlayerTrait.None;
            int total = 0, players = 0;
            for (ulong seed = 1; seed <= 30; seed++)
                foreach (var t in new[] { DemoTeams.Red(seed), DemoTeams.Blue(seed) })
                    foreach (var p in t.Starters.Concat(t.Bench))
                    {
                        int n = PlayerTraits.Count(p.Traits);
                        Assert.InRange(n, 1, 4);
                        Assert.True(PlayerTraits.IsValid(p.Traits, p), $"{p.FullName}: {p.Traits}");
                        if (p.Has(PlayerTrait.HardTackler)) Assert.True(p.Attributes.Aggression >= PlayerTraits.HardTacklerAggression);
                        if (p.NaturalPosition == PositionGroup.Goalkeeper)
                            Assert.False(p.Has(PlayerTrait.CutsInsideToShoot) || p.Has(PlayerTrait.BackToGoal) || p.Has(PlayerTrait.ShootsFromDistance));
                        seen |= p.Traits;
                        total += n; players++;
                    }
            foreach (var t in PlayerTraits.All) Assert.True((seen & t) != 0, "never given: " + t);
            double avg = total / (double)players;
            Assert.InRange(avg, 2.5, 3.3);
        }

        [Fact]
        public void TraitsAreTheSameForTheSameSeedAndDifferForAnother()
        {
            var a = DemoTeams.Red(5).Starters.Select(p => p.Traits).ToArray();
            var b = DemoTeams.Red(5).Starters.Select(p => p.Traits).ToArray();
            var c = DemoTeams.Red(6).Starters.Select(p => p.Traits).ToArray();
            Assert.Equal(a, b);
            Assert.NotEqual(a, c);
        }

        private static MatchEngine[] Play(Func<TeamSetup> red, ulong from, int n)
        {
            var es = new MatchEngine[n];
            Parallel.For(0, n, i =>
            {
                es[i] = new MatchEngine(red(), DemoTeams.Blue(), from + (ulong)i, MatchConfig.CreateDefault()) { CollectDiagnostics = true };
                es[i].RunToEnd();
            });
            return es;
        }

        private static TeamSetup Red(PlayerTrait give, Action<PlayerProfile> also = null)
        {
            var t = DemoTeams.Red();
            foreach (var p in t.Starters.Concat(t.Bench))
            {
                if (p.NaturalPosition == PositionGroup.Goalkeeper) { p.Traits = PlayerTrait.None; continue; }
                also?.Invoke(p);
                p.Traits = give;
            }
            return t;
        }

        [Fact]
        public void DribblersDribbleAndLongShootersShootFromDistance()
        {
            var plain = Play(() => Red(PlayerTrait.None), 500, 24);
            var dribblers = Play(() => Red(PlayerTrait.LovesToDribble), 500, 24);
            var shooters = Play(() => Red(PlayerTrait.ShootsFromDistance), 500, 24);
            double dr0 = plain.Average(e => e.Diagnostics.GetValueOrDefault("decide.Dribble")), dr1 = dribblers.Average(e => e.Diagnostics.GetValueOrDefault("decide.Dribble"));
            Assert.True(dr1 > dr0 * 1.03, $"dribble decisions {dr0:0.0} -> {dr1:0.0}");
            static double Far(MatchEngine e) => e.Diagnostics.GetValueOrDefault("shot.long.central"); // (open play, from the middle, 18 m+: counted by the engine)
            double s0 = plain.Average(Far), s1 = shooters.Average(Far);
            Assert.True(s1 > s0 * 1.15, $"shots from 18 m+ {s0:0.0} -> {s1:0.0}");
            Assert.All(plain.Concat(dribblers).Concat(shooters), e => Assert.Equal(0, e.RecoveredErrors));
        }

        [Fact]
        public void HardTacklersGoInMoreAndFoulMore()
        {
            var plain = Play(() => Red(PlayerTrait.None, p => p.Attributes.Aggression = 75), 520, 12);
            var hard = Play(() => Red(PlayerTrait.HardTackler, p => p.Attributes.Aggression = 75), 520, 12);
            // (he goes in sooner, standing or sliding)
            double sl0 = plain.Average(e => e.Home.Stats.Tackles), sl1 = hard.Average(e => e.Home.Stats.Tackles);
            double f0 = plain.Average(e => e.Home.Stats.Fouls), f1 = hard.Average(e => e.Home.Stats.Fouls);
            // (in the box he jockeys like everybody else, so the difference is in the open field: a few per cent more challenges and fouls)
            Assert.True(sl1 + f1 > (sl0 + f0) * 1.04, $"challenges and fouls {sl0 + f0:0.0} -> {sl1 + f1:0.0}");
            Assert.True(f1 >= f0 * 0.9, $"fouls {f0:0.0} -> {f1:0.0}"); // (the hard man jockeys in the box like everybody: the fouls do not rise much)
        }

        [Fact]
        public void UnselfishPlayersShootLess()
        {
            var plain = Play(() => Red(PlayerTrait.None), 540, 24);
            var givers = Play(() => Red(PlayerTrait.Unselfish), 540, 24);
            double s0 = plain.Average(e => e.Home.Stats.Shots), s1 = givers.Average(e => e.Home.Stats.Shots);
            Assert.True(s1 < s0, $"shots {s0:0.0} -> {s1:0.0}");
        }
    }
}

using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>Holding and central midfielders, the corner run-up, the assistant at corners and the new team stats.</summary>
    public class MidfieldCornerStatsTests
    {
        [Fact]
        public void HoldingMidfieldersDoTheDirtyWorkAndCentralMidfieldersMoveTheGame()
        {
            var engines = new MatchEngine[16];
            Parallel.For(0, engines.Length, i =>
            {
                engines[i] = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 7200 + (ulong)i);
                engines[i].RunToEnd();
            });
            var all = engines.SelectMany(e => e.AllPlayers.Where(p => !p.IsGoalkeeper)).ToList();
            double Avg(Func<PlayerAgent, bool> pick, Func<PlayerAgent, int> f) => all.Where(pick).Average(p => (double)f(p));
            bool Anchor(PlayerAgent p) => p == p.Team.Anchor;
            bool OtherOutfield(PlayerAgent p) => !p.Slot.IsCentralMidfielder;
            double anchorFouls = Avg(Anchor, p => p.FoulsCommitted), othersFouls = Avg(OtherOutfield, p => p.FoulsCommitted);
            double anchorTackles = Avg(Anchor, p => p.Tackles), othersTackles = Avg(OtherOutfield, p => p.Tackles);
            double cmPasses = Avg(p => p.Slot.IsCentralMidfielder, p => p.PassesAttempted);
            double cbPasses = Avg(p => p.Slot.IsCentreBack, p => p.PassesAttempted), stPasses = Avg(p => p.Slot.Line == Line.Attack, p => p.PassesAttempted);
            Assert.True(anchorFouls > othersFouls * 1.3, $"holding midfielder fouls {anchorFouls:0.00} vs others {othersFouls:0.00}");
            Assert.True(anchorTackles > othersTackles * 1.2, $"holding midfielder tackles {anchorTackles:0.00} vs others {othersTackles:0.00}");
            Assert.True(cmPasses > cbPasses && cmPasses > stPasses * 2, $"central midfielders pass {cmPasses:0.0}, centre-backs {cbPasses:0.0}, strikers {stPasses:0.0}");
        }

        [Fact]
        public void CornerTakersRunUpFromBehindTheBallTowardsTheBoxAndTheAssistantKeepsAway()
        {
            int corners = 0;
            var sync = new object();
            Parallel.For(0, 6, i =>
            {
                var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 7300 + (ulong)i);
                e.RestartExecuting += r =>
                {
                    if (r.Type != RestartType.CornerKick) return;
                    var team = r.Team;
                    Vec2 toBox = (team.ToWorld(44f, 0f) - r.Spot).Normalized;
                    Vec2 toBall = (r.Spot - r.Taker.Pos).Normalized;
                    // He strikes it moving towards the box: the ball is in front of him, not behind.
                    Assert.True(Vec2.Dot(toBall, toBox) > 0.6f, $"taker approach {Vec2.Dot(toBall, toBox):0.00}");
                    Assert.True(Vec2.Dot(r.Taker.Vel, toBox) > 0f, "the run-up goes towards the box");
                    var nearAssistant = MathF.Sign(r.Spot.X) == e.Assistant1.CoverSign ? e.Assistant1 : e.Assistant2;
                    Assert.True(Vec2.Distance(nearAssistant.Pos, r.Spot) > 3f, "assistant " + Vec2.Distance(nearAssistant.Pos, r.Spot) + " m from the ball");
                    lock (sync) corners++;
                };
                e.RunToEnd();
            });
            Assert.True(corners >= 20, "corners " + corners);
        }

        [Fact]
        public void TeamStatsCountTacklesDribblesAndShotsOnTarget()
        {
            var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 7400);
            e.RunToEnd();
            foreach (var s in new[] { e.Home.Stats, e.Away.Stats })
            {
                Assert.True(s.Dribbles > 5 && s.DribblesWon > 0 && s.DribblesWon < s.Dribbles, $"dribbles {s.Dribbles} won {s.DribblesWon}");
                Assert.True(s.Tackles > 5 && s.TacklesWon > 0 && s.TacklesWon <= s.Tackles, $"tackles {s.Tackles} won {s.TacklesWon}");
                Assert.True(s.ShotsOnTarget <= s.Shots);
                Assert.True(s.PossessionSeconds > 0f);
            }
        }
    }
}

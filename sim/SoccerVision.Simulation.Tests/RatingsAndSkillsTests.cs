using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>Ratings, the walk-out, the kick-off toss, injuries and the ground, random plans, corner set-ups, skills, the computer manager.</summary>
    public class RatingsAndSkillsTests
    {
        private static MatchEngine Engine(ulong seed, Action<MatchConfig> config = null, string homePlan = null)
        {
            var c = MatchConfig.CreateDefault();
            config?.Invoke(c);
            var red = DemoTeams.Red();
            if (homePlan != null) TeamPlan.Parse(homePlan).ApplyBeforeKickOff(red);
            return new MatchEngine(red, DemoTeams.Blue(), seed, c);
        }

        [Fact]
        public void RatingsStayBetweenOneAndTenScorersRateWellAndThereIsOneManOfTheMatch()
        {
            int scorers = 0, scorersAboveSix = 0;
            for (ulong seed = 300; seed < 304; seed++)
            {
                var e = Engine(seed);
                e.RunToEnd();
                var all = e.Home.Records.Concat(e.Away.Records).ToList();
                Assert.All(all, r => Assert.InRange(r.Rating, 1f, 10f));
                Assert.Equal(1, all.Count(r => r.ManOfTheMatch));
                Assert.True(all.Count >= 22, "starters and substitutes all have a record");
                foreach (var r in all.Where(r => r.Goals > 0)) { scorers++; if (r.Rating > 6.5f) scorersAboveSix++; }
                // the goals and assists add up to the team's goals (own goals aside)
                foreach (var t in new[] { e.Home, e.Away })
                    Assert.True(t.Records.Sum(r => r.Goals) <= t.Score);
            }
            Assert.True(scorers > 0, "no goals in four matches");
            Assert.True(scorersAboveSix >= scorers * 0.8, $"scorers rated above 6.5: {scorersAboveSix} of {scorers}");
        }

        [Fact]
        public void TheTeamsWalkOutLineUpAndThenKickOff()
        {
            var e = Engine(21, c => c.WalkOut = true);
            Assert.True(e.WalkingOut);
            Assert.All(e.AllPlayers, p => Assert.True(p.Pos.Y > Pitch.HalfWidth, "everyone starts in the tunnel"));
            float clockAtStart = e.Clock.Seconds;
            for (int i = 0; i < 80 && e.WalkingOut; i++) e.Advance(0.25f);
            Assert.False(e.WalkingOut, "the walk-out ends");
            Assert.Equal(clockAtStart, e.Clock.Seconds); // the clock does not run during it
            Assert.All(e.AllPlayers, p => Assert.True(p.Team.Forward(p.Pos) <= 0.5f, "back in their own half for the kick-off"));
            e.RunToEnd();
            Assert.True(e.IsFinished);
            Assert.Equal(0, e.RecoveredErrors);
        }

        [Fact]
        public void TheKickOffIsDecidedByACoinToss()
        {
            int home = 0;
            for (ulong seed = 1; seed <= 60; seed++) if (Engine(seed).PossessionTeam == null || Engine(seed).PossessionTeam.Index == 0) home++;
            Assert.InRange(home, 15, 45);
        }

        [Fact]
        public void ANeglectedMuddyPitchAndAKnockMakeInjuriesMoreLikely()
        {
            var perfect = Engine(5, c => c.Conditions = new MatchConditions { Maintenance = 1f });
            var neglected = Engine(5, c => c.Conditions = new MatchConditions { Weather = Weather.Rain, Maintenance = 0f });
            Assert.True(neglected.GroundInjuryFactor(Vec2.Zero) > perfect.GroundInjuryFactor(Vec2.Zero) * 1.3f);
        }

        [Fact]
        public void ARandomManagerPicksAValidEleven()
        {
            var setup = DemoTeams.Red();
            var a = TeamPlan.Random(setup, 1);
            var b = TeamPlan.Random(DemoTeams.Red(), 2);
            Assert.Contains(a.Formation, Formation.Names);
            Assert.Equal(11, a.Slots.Distinct().Count());
            var keeper = setup.Starters.Concat(setup.Bench).First(p => p.ShirtNumber == a.Slots[0]);
            Assert.Equal(PositionGroup.Goalkeeper, keeper.NaturalPosition);
            Assert.True(a.Formation != b.Formation || !a.Slots.SequenceEqual(b.Slots) || a.Line != b.Line, "different seeds, different plans");
            a.ApplyBeforeKickOff(setup);
            var e = new MatchEngine(setup, DemoTeams.Blue(), 3, MatchConfig.CreateDefault());
            e.RunToEnd();
            Assert.Equal(0, e.RecoveredErrors);
        }

        [Fact]
        public void TheManagersCornerSetUpIsUsedAndMirrored()
        {
            var red = DemoTeams.Red();
            int cb = red.Starters[3].ShirtNumber;
            var e = Engine(7, homePlan: "cpa=" + cb + ":40.0:6.0");
            int checkedCorners = 0;
            e.RestartExecuting += r =>
            {
                if (r.Type != RestartType.CornerKick || r.Team != e.Home) return;
                var p = e.Home.Players.FirstOrDefault(q => q.Number == cb && !q.SentOff);
                if (p == null || p == r.Taker) return;
                float side = MathF.Sign(e.Home.Lateral(r.Spot));
                Vec2 want = e.Home.ToWorld(40f, 6f * side);
                Assert.True(Vec2.Distance(p.ShapeTarget, want) < 0.6f, $"spot {p.ShapeTarget} v {want}");
                checkedCorners++;
            };
            e.RunToEnd();
            Assert.True(checkedCorners > 0, "no home corners");
        }

        [Fact]
        public void SkillsAreSeenButRare()
        {
            var engines = new MatchEngine[12];
            Parallel.For(0, engines.Length, i =>
            {
                engines[i] = Engine(800 + (ulong)i);
                engines[i].CollectDiagnostics = true;
                engines[i].RunToEnd();
            });
            int D(string k) => engines.Sum(e => e.Diagnostics.GetValueOrDefault(k));
            var log = engines.SelectMany(e => e.EventLog).ToList();
            Assert.True(D("skill.dummy") >= 1, "dummies " + D("skill.dummy"));
            Assert.True(D("skill.backheel") >= 1, "backheels " + D("skill.backheel"));
            Assert.True(D("skill.fakeShot") >= 1, "fake shots " + D("skill.fakeShot"));
            Assert.True(D("skill.backheel") < 12 * 6, "backheels are not everyday passes");
            Assert.True(D("shot.bicycle") <= 12, "bicycle kicks are rare: " + D("shot.bicycle"));
            Assert.Contains(log, x => x.Type == MatchEventType.Skill && x.Text.Length > 0);
        }

        [Fact]
        public void TheComputerManagerChangesWithinTheRules()
        {
            var e = Engine(17);
            var home = new ComputerManager(e, 0);
            var away = new ComputerManager(e, 1);
            while (!e.IsFinished)
            {
                e.Advance(0.5f);
                home.Update();
                away.Update();
            }
            foreach (var t in new[] { e.Home, e.Away })
            {
                Assert.InRange(t.SubstitutionsUsed, 1, e.Config.MaxSubstitutions);
                Assert.InRange(t.WindowsUsed, 0, e.Config.MaxSubstitutionWindows);
            }
            Assert.Equal(0, e.RecoveredErrors);
        }
    }
}

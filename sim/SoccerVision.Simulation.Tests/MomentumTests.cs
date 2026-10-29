using System;
using System.Collections.Generic;
using System.Linq;
using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>Home advantage and momentum.</summary>
    public class MomentumTests
    {
        private static MatchEngine Engine(ulong seed, Action<MatchConfig> config = null)
        {
            var c = MatchConfig.CreateDefault();
            config?.Invoke(c);
            return new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), seed, c);
        }

        [Fact]
        public void TheHomeTeamPlaysWithMoreConfidenceAndTheRefereeIsKinderToIt()
        {
            var e = Engine(1);
            Assert.True(e.Home.ConfidenceFactor > 1f && e.Away.ConfidenceFactor < 1f);
            var neutral = Engine(1, c => c.HomeAdvantage = 0f);
            Assert.Equal(1f, neutral.Home.ConfidenceFactor);
            Assert.Equal(1f, neutral.Away.ConfidenceFactor);
            // the same player is a little better at home, in the skills of the head and feet but not in pace
            var p = e.Home.Players[8];
            var q = neutral.Home.Players[8];
            Assert.True(p.Attr.Passing >= q.Attr.Passing && p.Attr.Composure >= q.Attr.Composure);
            Assert.True(p.Attr.Passing + p.Attr.Composure + p.Attr.Finishing > q.Attr.Passing + q.Attr.Composure + q.Attr.Finishing);
            Assert.Equal(q.Attr.Speed, p.Attr.Speed);
        }

        [Fact]
        public void AGoalSwingsMomentumToTheScorer()
        {
            int checkedGoals = 0;
            for (ulong seed = 70; seed < 90 && checkedGoals < 4; seed++)
            {
                var e = Engine(seed);
                while (!e.IsFinished && checkedGoals < 4)
                {
                    e.Advance(0.05f);
                    foreach (var ev in e.DrainEvents())
                    {
                        if (ev.Type != MatchEventType.Goal) continue;
                        var scorer = ev.Team == 0 ? e.Home : e.Away;
                        Assert.True(e.MomentumOf(scorer) > 0.25f, $"momentum of the scorer after his goal: {e.MomentumOf(scorer):0.00}");
                        Assert.True(e.MomentumOf(scorer.Opponent) < -0.25f);
                        checkedGoals++;
                    }
                }
            }
            Assert.True(checkedGoals >= 3);
        }

        [Fact]
        public void AQuickSecondGoalCountsMoreAnEqualiserAndALateGoalToo()
        {
            float Gain(Action<MatchEngine> setup)
            {
                var e = Engine(2, c => c.MomentumStrength = 1f);
                e.Clock.Seconds = 20 * 60;
                setup(e);
                float before = e.Momentum;
                e.MomentumGoal(e.Home);
                return e.Momentum - before;
            }
            float first = Gain(e => { e.Home.Score = 1; e.Away.Score = 0; });
            float quickSecond = Gain(e => { e.MomentumGoal(e.Home); e.Momentum = 0f; e.Home.Score = 2; e.Away.Score = 0; });
            float equaliser = Gain(e => { e.Home.Score = 1; e.Away.Score = 1; });
            float late = Gain(e => { e.Clock.Seconds = 85 * 60; e.Home.Score = 1; e.Away.Score = 0; });
            Assert.True(quickSecond > first * 1.3f, $"quick second goal {quickSecond:0.00} v first {first:0.00}");
            Assert.True(equaliser > first * 1.15f, $"equaliser {equaliser:0.00} v {first:0.00}");
            Assert.True(late > first * 1.15f, $"late goal {late:0.00} v {first:0.00}");
        }

        [Fact]
        public void MomentumFadesWithTimeAndEveryStoppageTakesSomeAway()
        {
            var e = Engine(3);
            e.AddMomentum(e.Home, 0.8f);
            float m0 = e.Momentum;
            for (int i = 0; i < 480; i++) e.DecayMomentum(1f); // eight match minutes
            Assert.InRange(e.Momentum, m0 * 0.3f, m0 * 0.45f); // about 1/e
            float m1 = e.Momentum;
            e.DampMomentum(0.8f);
            Assert.Equal(m1 * 0.8f, e.Momentum, 3);
        }

        [Fact]
        public void TheTeamWithMomentumPushesOnAndTheOtherCalmsTheGameDown()
        {
            var e = Engine(4);
            var baseHome = e.Home.Tactics.Effective();
            var baseAway = e.Away.Tactics.Effective();
            e.AddMomentum(e.Home, 1f);
            Assert.True(e.Home.Eff.Tempo > baseHome.Tempo && e.Home.Eff.Mentality > baseHome.Mentality && e.Home.Eff.PressingIntensity > baseHome.PressingIntensity);
            Assert.True(e.Away.Eff.Tempo < baseAway.Tempo - 0.1f && e.Away.Eff.Mentality < baseAway.Mentality);
            Assert.Equal(PassingStyle.Short, e.Away.Eff.Passing); // safer, shorter football
            Assert.True(e.Home.ConfidenceFactor > e.Away.ConfidenceFactor);
            // and the rattled team plays a little worse
            var neutral = Engine(4, c => { c.HomeAdvantage = 0f; c.MomentumStrength = 0f; });
            Assert.True(e.Away.Players[8].Attr.Composure < neutral.Away.Players[8].Attr.Composure);
        }

        [Fact]
        public void ScriptedFoulsAreAlwaysContact()
        {
            // the staged match's cards come with a real contact between the two players (never a card for nothing)
            int fouls = 0, cards = 0;
            foreach (ulong seed in new ulong[] { 1, 2, 3 })
            {
                var c = MatchConfig.CreateDefault();
                c.Script = MatchScript.Staged();
                var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), seed, c);
                while (!e.IsFinished)
                {
                    e.Advance(0.05f);
                    foreach (var ev in e.DrainEvents())
                    {
                        if (ev.Type == MatchEventType.Foul)
                        {
                            fouls++;
                            float d = Vec2.Distance(e.AllPlayers[ev.Player].Pos, e.AllPlayers[ev.OtherPlayer].Pos);
                            Assert.True(d < 4f, $"foul at {ev.Minute}: the players are {d:0.0} m apart");
                        }
                        if (ev.Type == MatchEventType.YellowCard || ev.Type == MatchEventType.RedCard) cards++;
                    }
                }
            }
            Assert.True(fouls > 10 && cards >= 6, $"fouls {fouls}, cards {cards}");
        }
    }
}

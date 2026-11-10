using System;
using System.Linq;
using System.Threading.Tasks;
using SoccerVision.Simulation;
using Xunit;
using Xunit.Abstractions;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>In the box the players shoot: they do not run on into the keeper, an open goal is put away, keepers have saves to make.</summary>
    public class FinishingTests
    {
        private readonly ITestOutputHelper _out;
        public FinishingTests(ITestOutputHelper output) { _out = output; }

        [Fact]
        public void PlayersShootInTheBoxPutAwayOpenGoalsAndKeepersMakeSaves()
        {
            var es = new MatchEngine[40];
            Parallel.For(0, es.Length, i =>
            {
                es[i] = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 20000 + (ulong)i, MatchConfig.CreateDefault()) { CollectDiagnostics = true };
                es[i].RunToEnd();
            });
            double D(string k) => es.Average(e => e.Diagnostics.GetValueOrDefault(k));
            double saves = es.Average(e => e.Home.Stats.Saves + e.Away.Stats.Saves), goals = es.Average(e => e.Home.Stats.Goals + e.Away.Stats.Goals);
            _out.WriteLine($"dribbles on to the keeper {D("dribble.atKeeper"):0.0}, box shots {D("box.decide.Shoot"):0.0}, box passes {D("box.decide.Pass"):0.0}, open goals {D("shot.openGoal"):0.00} shot / {D("goal.openGoal"):0.00} scored, saves {saves:0.0}, goals {goals:0.00}");
            Assert.True(D("dribble.atKeeper") < 8, "players run on into the keeper instead of shooting");
            Assert.True(D("box.decide.Pass") > 1, "the cut-back to a better-placed team-mate still happens");
            // (an open goal is now one the keeper cannot get back to: a few in forty matches - 23 of 24 went in over 100 matches)
            // (counted over the 40 matches, not as averages: open goals are rare - a couple in 40 matches - so one miss is allowed for;
            // FinishingIntelligenceTests measures them over 384 matches; SimRunner, 400 matches: 20 of 23 put away)
            double openShots = es.Sum(e => e.Diagnostics.GetValueOrDefault("shot.openGoal")), openGoals = es.Sum(e => e.Diagnostics.GetValueOrDefault("goal.openGoal"));
            Assert.True(openGoals >= openShots * 0.6 - 1, $"open goals are put away: {openGoals} of {openShots}");
            // (real: about 6 a match - the Premier League's 8.8 shots on target less its 2.83 goals, ten seasons counted by tools/calibration/real_leagues.py;
            // the old floor of 8 was the engine's own number, not the real one)
            Assert.True(saves >= 5 && saves <= 8.5, $"the keepers have saves to make, as many as real ones: {saves:0.0}");
            Assert.InRange(goals, 2.0, 4.8);
        }

        [Fact]
        public void TwoChangesAtOneStoppageUseOneOpportunity()
        {
            var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 21, MatchConfig.CreateDefault());
            // play into the first half until there is a stoppage
            while (!(e.Phase == MatchPhase.Stoppage && e.Clock.Seconds > 10 * 60)) e.Advance(0.1f);
            var outs = e.Home.Players.Where(p => !p.IsGoalkeeper && !p.SentOff).Take(2).Select(p => p.Number).ToArray();
            var ins = e.Home.Bench.Where(b => b.Profile.NaturalPosition != PositionGroup.Goalkeeper).Take(2).Select(b => b.Number).ToArray();
            Assert.Null(e.RequestSubstitution(0, outs[0], ins[0]));
            Assert.Null(e.RequestSubstitution(0, outs[1], ins[1]));
            for (int i = 0; i < 600 && e.Home.SubstitutionsUsed < 2; i++) e.Advance(0.1f);
            Assert.Equal(2, e.Home.SubstitutionsUsed);
            Assert.Equal(1, e.Home.WindowsUsed);
        }
    }
}

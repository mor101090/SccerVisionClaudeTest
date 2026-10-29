using System;
using System.Linq;
using System.Threading.Tasks;
using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>The chosen defence, attack and extra tactics: each one does what its name says, and the cost of it shows.</summary>
    public class TacticStyleTests
    {
        private static readonly ulong[] Seeds = { 21, 22, 23, 24, 25, 26 };

        private static double Avg(Action<TeamTactics> home, Func<MatchEngine, double> f)
        {
            var results = new double[Seeds.Length];
            Parallel.For(0, Seeds.Length, i =>
            {
                var red = DemoTeams.Red();
                home?.Invoke(red.Tactics);
                var e = new MatchEngine(red, DemoTeams.Blue(), Seeds[i], MatchConfig.CreateDefault()) { CollectDiagnostics = true };
                e.RunToEnd();
                Assert.True(e.IsFinished);
                Assert.Equal(0, e.RecoveredErrors);
                results[i] = f(e);
            });
            return results.Average();
        }

        private static double Energy(MatchEngine e) => e.Home.Players.Where(p => !p.IsGoalkeeper).Average(p => p.Energy) * 100.0;
        private static double Depth(MatchEngine e) => e.HomeLineSum / e.HomeLineSamples;
        private static double LongShots(MatchEngine e) => e.EventLog.Count(x => x.Type == MatchEventType.Shot && x.Team == 0
            && Math.Min(Math.Sqrt((52.5 - x.X) * (52.5 - x.X) + x.Y * x.Y), Math.Sqrt((52.5 + x.X) * (52.5 + x.X) + x.Y * x.Y)) > 18);

        [Fact]
        public void AHighPressTiresThePlayersFaster()
        {
            double normal = Avg(null, Energy), press = Avg(t => t.Defence = DefenceStyle.HighPress, Energy);
            Assert.True(press < normal - 8, $"stamina left: high press {press:0} v normal {normal:0}");
        }

        [Fact]
        public void ParkingTheBusSitsDeepAndSavesTheLegs()
        {
            double depthNormal = Avg(null, Depth), depthBus = Avg(t => t.Defence = DefenceStyle.ParkTheBus, Depth);
            double energyNormal = Avg(null, Energy), energyBus = Avg(t => t.Defence = DefenceStyle.ParkTheBus, Energy);
            Assert.True(depthBus < depthNormal - 8, $"back line {depthBus:0} m v {depthNormal:0} m from own goal");
            Assert.True(energyBus > energyNormal + 8, $"stamina left: bus {energyBus:0} v {energyNormal:0}");
        }

        [Fact]
        public void TheOffsideTrapCatchesMoreForwardsOffside()
        {
            double normal = Avg(null, e => e.Away.Stats.Offsides), trap = Avg(t => t.Defence = DefenceStyle.OffsideTrap, e => e.Away.Stats.Offsides);
            Assert.True(trap > normal * 2 && trap >= 3, $"opponents caught offside: trap {trap:0.0} v normal {normal:0.0}");
        }

        [Fact]
        public void CrossesIntoTheBoxCrossesMore()
        {
            double normal = Avg(null, e => e.Home.Stats.Crosses), crosses = Avg(t => t.Attack = AttackStyle.CrossesIntoBox, e => e.Home.Stats.Crosses);
            Assert.True(crosses > normal * 1.4, $"crosses: style {crosses:0.0} v normal {normal:0.0}");
        }

        [Fact]
        public void LongShotsStyleShootsFromDistance()
        {
            double normal = Avg(null, LongShots), shots = Avg(t => t.Attack = AttackStyle.LongShots, LongShots);
            Assert.True(shots > normal * 1.5, $"shots from 18 m+: style {shots:0.0} v normal {normal:0.0}");
        }

        [Fact]
        public void RunsInBehindPlaysMoreThroughBalls()
        {
            double normal = Avg(null, e => e.Home.Stats.ThroughBalls), runs = Avg(t => t.Attack = AttackStyle.RunsInBehind, e => e.Home.Stats.ThroughBalls);
            Assert.True(runs > normal * 1.3, $"through balls: style {runs:0.0} v normal {normal:0.0}");
        }

        [Fact]
        public void KeepingThePossessionMeansMorePassesAndFewerLongBalls()
        {
            int Long(MatchEngine e) => e.EventLog.Count(x => x.Type == MatchEventType.LongBall && x.Team == 0);
            double normalPasses = Avg(null, e => e.Home.Stats.Passes), keep = Avg(t => t.Special = SpecialTactic.Possession, e => e.Home.Stats.Passes);
            double normalLong = Avg(null, e => Long(e)), keepLong = Avg(t => t.Special = SpecialTactic.Possession, e => Long(e));
            Assert.True(keep > normalPasses * 1.05, $"passes: possession {keep:0} v normal {normalPasses:0}");
            Assert.True(keepLong < normalLong * 0.8, $"long balls: possession {keepLong:0.0} v normal {normalLong:0.0}");
        }

        [Fact]
        public void TimeWastingTakesRestartsSlowly()
        {
            double Setup(Action<TeamTactics> home)
            {
                var all = new System.Collections.Concurrent.ConcurrentBag<double>();
                Parallel.For(0, Seeds.Length, i =>
                {
                    var red = DemoTeams.Red();
                    home?.Invoke(red.Tactics);
                    var e = new MatchEngine(red, DemoTeams.Blue(), Seeds[i], MatchConfig.CreateDefault());
                    Restart last = null;
                    while (!e.IsFinished)
                    {
                        e.Advance(0.1f);
                        var r = e.CurrentRestart;
                        if (r != null && r != last && r.Team == e.Home && (r.Type == RestartType.ThrowIn || r.Type == RestartType.GoalKick)) all.Add(r.SetupTime);
                        last = r;
                    }
                });
                return all.Average();
            }
            double normal = Setup(null), waste = Setup(t => t.Special = SpecialTactic.TimeWasting);
            Assert.True(waste > normal * 1.6, $"setup time of throw-ins and goal kicks: time wasting {waste:0.0} v normal {normal:0.0} s");
        }

        [Fact]
        public void ACounterAttackStartsWhenTheBallIsWonInOurHalf()
        {
            double starts = Avg(t => t.Special = SpecialTactic.CounterAttack, e => e.Diagnostics.GetValueOrDefault("counter.start"));
            Assert.True(starts >= 10, $"counter-attacks per match {starts:0.0}");
        }

        [Fact]
        public void RelentlessPressureBuildsAHighLineAndTiresThePlayers()
        {
            double depthNormal = Avg(null, Depth), depthSiege = Avg(t => t.Special = SpecialTactic.Siege, Depth);
            double energyNormal = Avg(null, Energy), energySiege = Avg(t => t.Special = SpecialTactic.Siege, Energy);
            Assert.True(depthSiege > depthNormal + 5, $"back line {depthSiege:0} m v {depthNormal:0} m");
            Assert.True(energySiege < energyNormal - 8, $"stamina left {energySiege:0} v {energyNormal:0}");
        }

        [Fact]
        public void TheStylesAreAnnounced()
        {
            var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 5, MatchConfig.CreateDefault());
            e.Advance(20f);
            e.Submit(new ChangeTacticsCommand(0, new TeamTactics { Defence = DefenceStyle.OffsideTrap, Attack = AttackStyle.CrossesIntoBox, Special = SpecialTactic.TimeWasting }));
            e.Advance(2f);
            var ev = Assert.Single(e.EventLog, x => x.Type == MatchEventType.TacticsChanged);
            Assert.Contains("offside trap", ev.Text);
            Assert.Contains("crosses into the box", ev.Text);
            Assert.Contains("wasting time", ev.Text);
        }
    }
}

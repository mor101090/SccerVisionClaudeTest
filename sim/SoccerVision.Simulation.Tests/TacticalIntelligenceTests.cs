using System;
using System.Linq;
using System.Threading.Tasks;
using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>
    /// The high backline and inverted full-backs, and the principles every team now plays by: rest defence, the counter-press,
    /// counter-movements, third-man runs, wide overloads and long throws.
    /// </summary>
    public class TacticalIntelligenceTests
    {
        private static readonly ulong[] Seeds = { 31, 32, 33, 34, 35, 36 };

        /// <summary>Plays a match per seed and averages <paramref name="f"/>; <paramref name="sample"/> runs every 0.25 s of real time.</summary>
        private static double Avg(Action<TeamTactics> home, Func<MatchEngine, double> f, Action<MatchEngine> sample = null)
        {
            var results = new double[Seeds.Length];
            Parallel.For(0, Seeds.Length, i =>
            {
                var red = DemoTeams.Red();
                home?.Invoke(red.Tactics);
                var e = new MatchEngine(red, DemoTeams.Blue(), Seeds[i], MatchConfig.CreateDefault());
                if (sample == null) e.RunToEnd();
                else
                    for (int k = 0; k < 2000000 && !e.IsFinished; k++) { e.Advance(0.25f); if (e.Phase == MatchPhase.Live) sample(e); }
                Assert.True(e.IsFinished);
                Assert.Equal(0, e.RecoveredErrors);
                results[i] = f(e);
            });
            return results.Average();
        }

        private static double Depth(MatchEngine e) => e.HomeLineSum / e.HomeLineSamples;

        [Fact]
        public void AHighBacklinePlaysHighAndCompactWithTheKeeperBehindIt()
        {
            double normal = Avg(null, Depth), high = Avg(t => t.Defence = DefenceStyle.HighBackline, Depth);
            Assert.True(high > normal + 5, $"back line {high:0} m v {normal:0} m from own goal");
            // compact: the distance from the back line to the forwards out of possession is shorter
            double Spread(Action<TeamTactics> tac)
            {
                double sum = 0; int n = 0;
                object gate = new object();
                Avg(tac, _ => 0, e =>
                {
                    if (e.PossessionTeam != e.Away) return;
                    var outfield = e.Home.Players.Where(p => !p.SentOff && !p.IsGoalkeeper).Select(p => e.Home.Forward(p.Pos)).ToList();
                    lock (gate) { sum += outfield.Max() - outfield.Min(); n++; }
                });
                return sum / n;
            }
            double spreadNormal = Spread(null), spreadHigh = Spread(t => t.Defence = DefenceStyle.HighBackline);
            Assert.True(spreadHigh < spreadNormal - 1.5, $"block length out of possession: high backline {spreadHigh:0.0} m v {spreadNormal:0.0} m");
        }

        [Fact]
        public void InvertedFullBacksComeInsideInPossession()
        {
            double Lateral(Action<TeamTactics> tac)
            {
                double sum = 0; int n = 0;
                object gate = new object();
                Avg(tac, _ => 0, e =>
                {
                    if (e.PossessionTeam != e.Home || e.Ball.Owner == null || e.Ball.Owner.Team != e.Home) return;
                    foreach (var p in e.Home.Players)
                        if (!p.SentOff && p.Slot.IsFullBack && p != e.Ball.Owner)
                            lock (gate) { sum += Math.Abs(e.Home.Lateral(p.Pos)); n++; }
                });
                return sum / n;
            }
            double normal = Lateral(null), inverted = Lateral(t => t.Defence = DefenceStyle.InvertedFullBacks);
            Assert.True(inverted < normal - 6, $"full-backs from the middle in possession: inverted {inverted:0.0} m v {normal:0.0} m");
        }

        [Fact]
        public void RestDefenceKeepsPlayersBehindTheBallWhileAttacking()
        {
            double sum = 0; int n = 0;
            object gate = new object();
            Avg(null, _ => 0, e =>
            {
                var t = e.Home;
                if (e.Ball.Owner == null || e.Ball.Owner.Team != t || t.Forward(e.Ball.Pos) < 15f) return;
                float ballF = t.Forward(e.Ball.Pos);
                int behind = t.Players.Count(p => !p.SentOff && !p.IsGoalkeeper && t.Forward(p.Pos) < ballF - 5f);
                lock (gate) { sum += behind; n++; }
            });
            double avg = sum / n;
            Assert.True(avg >= 4.5, $"players behind the ball in the final third: {avg:0.0}");
        }

        [Fact]
        public void TheNewPrinciplesAllShowUpInAMatch()
        {
            int Both(MatchEngine e, Func<TeamStats, int> s) => s(e.Home.Stats) + s(e.Away.Stats);
            double thirdMan = Avg(null, e => Both(e, s => s.ThirdManRuns));
            double layOffs = Avg(null, e => Both(e, s => s.ThirdManPasses));
            double counterMoves = Avg(null, e => Both(e, s => s.CounterMovements));
            double counterPress = Avg(null, e => Both(e, s => s.CounterPresses));
            double wonBack = Avg(null, e => Both(e, s => s.CounterPressWins));
            double overloads = Avg(null, e => Both(e, s => s.Overloads));
            Assert.True(thirdMan > 15 && layOffs >= 2, $"third-man runs {thirdMan:0.0}, lay-offs {layOffs:0.0}");
            Assert.True(counterMoves > 30, $"counter-movements {counterMoves:0.0}");
            Assert.True(counterPress > 30 && wonBack > 2, $"counter-presses {counterPress:0.0}, won back {wonBack:0.0}");
            Assert.True(overloads > 50, $"wide overloads {overloads:0.0}");
        }

        [Fact]
        public void LongThrowsGoIntoTheSixYardBox()
        {
            // a team of strong throwers: long throws near the box land around the six-yard box
            int longThrows = 0;
            Parallel.For(0, Seeds.Length, i =>
            {
                var red = DemoTeams.Red();
                foreach (var p in red.Starters) p.Attributes.PhysicalPower = 85;
                var e = new MatchEngine(red, DemoTeams.Blue(), Seeds[i], MatchConfig.CreateDefault());
                e.RunToEnd();
                System.Threading.Interlocked.Add(ref longThrows, e.Home.Stats.LongThrows);
            });
            Assert.True(longThrows >= 3, $"long throws in {Seeds.Length} matches: {longThrows}");
        }

        [Fact]
        public void TheComputerAnswersTheNewDefences()
        {
            var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 5, MatchConfig.CreateDefault());
            e.Submit(new ChangeTacticsCommand(0, new TeamTactics { Defence = DefenceStyle.HighBackline }));
            var cm = new ComputerManager(e, 1);
            for (int k = 0; k < 4000 && e.Clock.Seconds < 7f * 60f; k++) { e.Advance(0.25f); cm.Update(); }
            Assert.Equal(AttackStyle.RunsInBehind, e.Away.Tactics.Attack);
        }
    }
}

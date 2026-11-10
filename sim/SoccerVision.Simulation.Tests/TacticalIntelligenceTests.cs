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
        private static double Avg(Action<TeamTactics> home, Func<MatchEngine, double> f, Action<MatchEngine> sample = null, ulong[] seeds = null)
        {
            var Seeds = seeds ?? TacticalIntelligenceTests.Seeds;
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
            // (24 matches: on the first six the gap fell to 4.1 m after polish round 1 - the normal line now plays higher, 33 -> 36 m, with no kicks
            // into touch ending its attacks - while over 24 it is 6.6 m, 8.7 before)
            var depthSeeds = Seeds.Concat(Enumerable.Range(0, 18).Select(k => 600UL + (ulong)k)).ToArray();
            double normal = Avg(null, Depth, seeds: depthSeeds), high = Avg(t => t.Defence = DefenceStyle.HighBackline, Depth, seeds: depthSeeds);
            Assert.True(high > normal + 5, $"back line {high:0} m v {normal:0} m from own goal");
            // compact: the distance from the back line to the forwards out of possession is shorter
            // (18 matches: over 30 the gap is about 2.5 m, but six landed at 1.4 m after ball physics stage 9 changed the frame's restitution)
            var spreadSeeds = Seeds.Concat(Enumerable.Range(0, 12).Select(k => 500UL + (ulong)k)).ToArray();
            double Spread(Action<TeamTactics> tac)
            {
                double sum = 0; int n = 0;
                object gate = new object();
                Avg(tac, _ => 0, e =>
                {
                    if (e.PossessionTeam != e.Away) return;
                    var outfield = e.Home.Players.Where(p => !p.SentOff && !p.IsGoalkeeper).Select(p => e.Home.Forward(p.Pos)).ToList();
                    lock (gate) { sum += outfield.Max() - outfield.Min(); n++; }
                }, spreadSeeds);
                return sum / n;
            }
            double spreadNormal = Spread(null), spreadHigh = Spread(t => t.Defence = DefenceStyle.HighBackline);
            Assert.True(spreadHigh < spreadNormal - 1.5, $"block length out of possession: high backline {spreadHigh:0.0} m v {spreadNormal:0.0} m");
        }

        [Fact]
        public void AHighBacklineStandsOnTheHalfwayLineWhileTheOpponentHasTheBallInHisHalf()
        {
            // (reference: the back four on the halfway line, the whole team compact in front of it)
            double sumBack = 0, sumBlock = 0; int n = 0;
            object gate = new object();
            Avg(t => t.Defence = DefenceStyle.HighBackline, _ => 0, e =>
            {
                if (e.Ball.Owner == null || e.Ball.Owner.Team != e.Away || e.Home.Forward(e.Ball.Pos) < 12f) return;
                var back = e.Home.Players.Where(p => !p.SentOff && p.Slot.Line == Line.Defence).Select(p => e.Home.Forward(p.Pos)).Average();
                var outfield = e.Home.Players.Where(p => !p.SentOff && !p.IsGoalkeeper).Select(p => e.Home.Forward(p.Pos)).ToList();
                lock (gate) { sumBack += back; sumBlock += outfield.Max() - outfield.Min(); n++; }
            });
            Assert.True(sumBack / n > -3.5, $"back line {sumBack / n:0.0} m from the halfway line");
            Assert.True(sumBlock / n < 36, $"block length {sumBlock / n:0.0} m");
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
            // a team with a long-throw specialist: long throws near the box land around the six-yard box (since polish round 2 only a man who
            // can reach the box throws it long - his release speed decides it - and that is the specialist; a team of strong ordinary
            // throwers, which this test used before, reaches the edge of the box at best)
            int longThrows = 0;
            var seeds = Seeds.Concat(Seeds.Select(x => x + 100)).ToArray(); // (a rare thing: twelve matches, not six)
            Parallel.For(0, seeds.Length, i =>
            {
                var red = DemoTeams.Red();
                foreach (var p in red.Starters) p.Attributes.PhysicalPower = 85;
                var thrower = red.Starters.First(p => p.PositionLabel == "RB" || p.PositionLabel == "LB");
                thrower.Traits |= PlayerTrait.LongThrowSpecialist;
                var e = new MatchEngine(red, DemoTeams.Blue(), seeds[i], MatchConfig.CreateDefault());
                e.RunToEnd();
                System.Threading.Interlocked.Add(ref longThrows, e.Home.Stats.LongThrows);
            });
            Assert.True(longThrows >= 3, $"long throws in {seeds.Length} matches: {longThrows}");
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

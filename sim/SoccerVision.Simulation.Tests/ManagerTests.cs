using System;
using System.Linq;
using System.Threading.Tasks;
using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>The manager's tools: formations, the starting eleven, roles, individual instructions, set-piece takers and substitutions.</summary>
    public class ManagerTests
    {
        private static MatchEngine Engine(ulong seed, string homePlan = null)
        {
            var red = DemoTeams.Red();
            if (homePlan != null) TeamPlan.Parse(homePlan).ApplyBeforeKickOff(red);
            return new MatchEngine(red, DemoTeams.Blue(), seed, MatchConfig.CreateDefault());
        }

        private static void PlayUntil(MatchEngine e, Func<bool> done, float maxRealSeconds = 3000f)
        {
            for (float t = 0; t < maxRealSeconds && !e.IsFinished && !done(); t += 0.1f) e.Advance(0.1f);
        }

        [Fact]
        public void TheBenchHasNinePlayers()
        {
            Assert.Equal(9, DemoTeams.Red().Bench.Count);
            Assert.Equal(9, DemoTeams.Blue().Bench.Count);
        }

        [Theory]
        [InlineData("4-4-2 narrow")] [InlineData("4-4-2 diamond")] [InlineData("4-4-2 diamond wide")] [InlineData("4-3-3")] [InlineData("4-3-3 defensive")] [InlineData("3-5-2")]
        [InlineData("3-4-3")] [InlineData("3-4-1-2")] [InlineData("4-2-3-1")] [InlineData("4-1-4-1")] [InlineData("4-5-1")]
        public void EveryFormationPlaysAFullMatch(string formation)
        {
            var e = Engine(41, "formation=" + formation);
            Assert.Equal(formation, e.Home.Formation.Name);
            e.RunToEnd();
            Assert.True(e.IsFinished);
            Assert.Equal(0, e.RecoveredErrors);
            // nobody played as a keeper but the keeper, and the shape moved players to fitting places
            Assert.True(e.Home.Players.Single(p => p.SlotIndex == 0).Profile.NaturalPosition == PositionGroup.Goalkeeper);
        }

        [Fact]
        public void APreMatchPlanPicksTheElevenFromTheSquad()
        {
            var red = DemoTeams.Red();
            var benchStriker = red.Bench.First(p => p.NaturalPosition == PositionGroup.Forward);
            var numbers = red.Starters.Select(p => p.ShirtNumber).ToArray();
            int dropped = numbers[10];
            numbers[10] = benchStriker.ShirtNumber;
            TeamPlan.Parse("slots=" + string.Join(",", numbers) + ";takers=" + numbers[6] + ",0," + numbers[9]).ApplyBeforeKickOff(red);
            Assert.Equal(benchStriker, red.Starters[10]);
            Assert.Contains(red.Bench, p => p.ShirtNumber == dropped);
            Assert.Equal(9, red.Bench.Count);
            Assert.Equal(new[] { numbers[6] }, red.Tactics.CornerTakersRight);
        }

        [Fact]
        public void TheChosenCornerTakerTakesTheCorners()
        {
            var red = DemoTeams.Red();
            int taker = red.Starters[3].ShirtNumber; // a centre-back, who would never be picked otherwise
            TeamPlan.Parse("takers=" + taker + ",0,0").ApplyBeforeKickOff(red);
            var e = new MatchEngine(red, DemoTeams.Blue(), 7, MatchConfig.CreateDefault());
            int corners = 0, byHim = 0;
            e.RestartExecuting += r => { if (r.Type == RestartType.CornerKick && r.Team == e.Home) { corners++; if (r.Taker != null && r.Taker.Number == taker) byHim++; } };
            e.RunToEnd();
            Assert.True(corners > 0, "no corners");
            Assert.Equal(corners, byHim);
        }

        [Fact]
        public void ASubstitutionIsMadeAtTheNextStoppageAndThePlayerCannotReturn()
        {
            var e = Engine(11);
            PlayUntil(e, () => e.Clock.Seconds > 20 * 60);
            var off = e.Home.Players[7];
            int outNumber = off.Number, inNumber = e.Home.Bench[3].Number;
            e.Submit(new SubstituteCommand(0, outNumber, inNumber));
            PlayUntil(e, () => e.Home.Players.Any(p => p.Number == inNumber), 600f);
            Assert.Contains(e.Home.Players, p => p.Number == inNumber);
            Assert.DoesNotContain(e.Home.Players, p => p.Number == outNumber);
            Assert.Equal(1, e.Home.WindowsUsed);
            Assert.Contains(e.Home.SubbedOff, p => p.ShirtNumber == outNumber);
            // he cannot come back on
            e.DrainEvents();
            var refused = new System.Collections.Generic.List<MatchEvent>();
            e.Submit(new SubstituteCommand(0, e.Home.Players[8].Number, outNumber));
            PlayUntil(e, () => { refused.AddRange(e.DrainEvents().Where(x => x.Type == MatchEventType.SubstitutionRequest)); return refused.Count > 0; }, 60f);
            Assert.Single(refused);
            Assert.Equal(1f, refused[0].Value);
            e.RunToEnd();
            Assert.Equal(0, e.RecoveredErrors);
        }

        [Fact]
        public void FiveChangesInThreeOpportunitiesHalfTimeIsFree()
        {
            var e = Engine(12);
            int Changes() => e.Home.SubstitutionsUsed;
            // at half-time: two changes, no opportunity used
            PlayUntil(e, () => e.Phase == MatchPhase.HalfTimeBreak);
            e.Submit(new SubstituteCommand(0, e.Home.Players[9].Number, e.Home.Bench.First(b => b.Profile.NaturalPosition == PositionGroup.Forward).Number));
            e.Submit(new SubstituteCommand(0, e.Home.Players[5].Number, e.Home.Bench.First(b => b.Profile.NaturalPosition == PositionGroup.Midfielder).Number));
            PlayUntil(e, () => Changes() == 2, 200f);
            Assert.Equal(2, Changes());
            Assert.Equal(0, e.Home.WindowsUsed);
            PlayUntil(e, () => e.Clock.Period == MatchPeriod.SecondHalf && e.Clock.Seconds > 50 * 60, 1500f);
            // then three opportunities in play, one change each
            for (int k = 0; k < 3; k++)
            {
                int before = Changes();
                var bench = e.Home.Bench.First(b => b.Profile.NaturalPosition != PositionGroup.Goalkeeper);
                var off = e.Home.Players.First(p => !p.IsGoalkeeper && !p.SentOff && p.Slot.Line != Line.Defence || k == 2 && !p.IsGoalkeeper && !p.SentOff);
                e.Submit(new SubstituteCommand(0, off.Number, bench.Number));
                PlayUntil(e, () => Changes() > before, 900f);
                Assert.Equal(before + 1, Changes());
                PlayUntil(e, () => e.Phase == MatchPhase.Live, 120f);
                float resume = e.Clock.Seconds + 45f; // (play on, so the next change waits for a different stoppage)
                PlayUntil(e, () => e.Clock.Seconds > resume, 300f);
            }
            Assert.Equal(3, e.Home.WindowsUsed);
            // the sixth is refused
            var events = new System.Collections.Generic.List<MatchEvent>();
            e.DrainEvents();
            e.Submit(new SubstituteCommand(0, e.Home.Players[1].Number, e.Home.Bench.Last().Number));
            PlayUntil(e, () => { events.AddRange(e.DrainEvents().Where(x => x.Type == MatchEventType.SubstitutionRequest)); return events.Count > 0; }, 60f);
            Assert.Equal(1f, events[0].Value);
            Assert.Equal(5, Changes());
        }

        [Fact]
        public void TheComputerMakesItsChangesInTheSecondHalf()
        {
            var e = Engine(13);
            var cm = new ComputerManager(e, 1);
            while (!e.IsFinished) { e.Advance(0.25f); cm.Update(); }
            Assert.InRange(e.Away.SubstitutionsUsed, 3, 5);
            Assert.InRange(e.Away.WindowsUsed, 1, 3);
            Assert.Equal(0, e.RecoveredErrors);
        }

        private static readonly ulong[] Seeds = { 51, 52, 53, 54 };

        private static double Avg(string plan, Func<MatchEngine, double> f, Action<MatchEngine> sample = null)
        {
            var r = new double[Seeds.Length];
            Parallel.For(0, Seeds.Length, i =>
            {
                var e = Engine(Seeds[i], plan);
                if (sample == null) e.RunToEnd();
                else while (!e.IsFinished) { e.Advance(0.25f); if (e.Phase == MatchPhase.Live) sample(e); }
                Assert.Equal(0, e.RecoveredErrors);
                r[i] = f(e);
            });
            return r.Average();
        }

        private static string Orders(int slot, string order)
        {
            var o = Enumerable.Repeat("0.1.1.1", 11).ToArray();
            o[slot] = order;
            return "orders=" + string.Join(",", o);
        }

        [Fact]
        public void IndividualInstructionsChangeHowAPlayerPlays()
        {
            // slot 5 (right midfielder): shoot from distance, dribble, tackle - more v less
            double Shots(MatchEngine e) => e.EventLog.Count(x => x.Type == MatchEventType.Shot && x.Team == 0 && x.Player == 5
                && Math.Min(Math.Sqrt((52.5 - x.X) * (52.5 - x.X) + x.Y * x.Y), Math.Sqrt((52.5 + x.X) * (52.5 + x.X) + x.Y * x.Y)) > 18);
            double shotsMore = Avg(Orders(5, "0.2.1.1"), Shots), shotsLess = Avg(Orders(5, "0.0.1.1"), Shots);
            Assert.True(shotsMore > shotsLess * 2 + 0.5, $"long shots by him: more {shotsMore:0.0} v less {shotsLess:0.0}");
            // (take-ons are few for one player: the whole team gets the instruction)
            string AllOrders(string order) => "orders=" + string.Join(",", Enumerable.Repeat(order, 11));
            double dribMore = Avg(AllOrders("0.1.2.1"), e => e.Home.Stats.Dribbles), dribLess = Avg(AllOrders("0.1.0.1"), e => e.Home.Stats.Dribbles);
            Assert.True(dribMore > dribLess * 1.25, $"team take-ons: often {dribMore:0.0} v less {dribLess:0.0}");
            // (fouls are rare for one player: the whole team gets the instruction)
            string All(string order) => "orders=" + string.Join(",", Enumerable.Repeat(order, 11));
            double foulsMore = Avg(All("0.1.1.2"), e => e.Home.Stats.Fouls), foulsLess = Avg(All("0.1.1.0"), e => e.Home.Stats.Fouls);
            Assert.True(foulsMore > foulsLess * 1.3, $"team fouls: very aggressive {foulsMore:0.0} v gentle {foulsLess:0.0}");
        }

        [Fact]
        public void RolesChangeWhereAPlayerPlays()
        {
            // slot 1 (right-back) in possession: a wing-back far up the pitch, a defensive full-back at home
            double Depth(string order)
            {
                double sum = 0; int n = 0; object gate = new object();
                Avg(Orders(1, order), _ => 0, e =>
                {
                    if (e.Ball.Owner == null || e.Ball.Owner.Team != e.Home) return;
                    lock (gate) { sum += e.Home.Forward(e.Home.Players[1].Pos); n++; }
                });
                return sum / n;
            }
            double wingBack = Depth((int)PlayerRole.WingBack + ".1.1.1"), defensive = Depth((int)PlayerRole.DefensiveFullBack + ".1.1.1");
            Assert.True(wingBack > defensive + 4, $"right-back in possession: attacking full-back {wingBack:0.0} m v defensive {defensive:0.0} m");
            // a false nine drops off the line, a target forward stays on it
            double Striker(string order)
            {
                double sum = 0; int n = 0; object gate = new object();
                Avg(Orders(9, order), _ => 0, e =>
                {
                    if (e.Ball.Owner == null || e.Ball.Owner.Team != e.Home) return;
                    lock (gate) { sum += e.Home.Forward(e.Home.Players[9].Pos); n++; }
                });
                return sum / n;
            }
            double target = Striker((int)PlayerRole.TargetForward + ".1.1.1"), falseNine = Striker((int)PlayerRole.FalseNine + ".1.1.1");
            Assert.True(target > falseNine + 4, $"striker in possession: target forward {target:0.0} m v false nine {falseNine:0.0} m");
        }

        [Fact]
        public void APlanDuringTheMatchChangesFormationAndPositions()
        {
            var e = Engine(14);
            PlayUntil(e, () => e.Clock.Seconds > 10 * 60);
            TeamPlan.Parse("formation=4-3-3").Submit(e, 0);
            e.Advance(0.1f);
            Assert.Equal("4-3-3", e.Home.Formation.Name);
            Assert.Equal(11, e.Home.Players.Select(p => p.SlotIndex).Distinct().Count());
            // swap two players by shirt number
            var numbers = new int[11];
            foreach (var p in e.Home.Players) numbers[p.SlotIndex] = p.Number;
            (numbers[6], numbers[7]) = (numbers[7], numbers[6]);
            int six = numbers[6];
            e.Submit(new LineupCommand(0, numbers));
            e.Advance(0.1f);
            Assert.Equal(6, e.Home.Players.First(p => p.Number == six).SlotIndex);
            e.RunToEnd();
            Assert.Equal(0, e.RecoveredErrors);
        }

        [Fact]
        public void TheNextOnTheListTakesOverWhenTheFirstIsGone()
        {
            var red = DemoTeams.Red();
            int first = red.Starters[3].ShirtNumber, second = red.Starters[6].ShirtNumber;
            TeamPlan.Parse("ctr=" + first + "." + second + ";ctl=" + first + "." + second).ApplyBeforeKickOff(red);
            var e = new MatchEngine(red, DemoTeams.Blue(), 9, MatchConfig.CreateDefault());
            // the first on the list is sent off at once: the second takes every corner
            e.Home.Players.First(p => p.Number == first).SentOff = true;
            int corners = 0, bySecond = 0;
            int byFirst = 0;
            e.RestartExecuting += r =>
            {
                if (r.Type != RestartType.CornerKick || r.Team != e.Home) return;
                if (r.Taker != null && r.Taker.Number == first) byFirst++;
                // (while the second is on the pitch; an injury or a change could take him off too)
                if (!e.Home.Players.Any(p => p.Number == second && !p.SentOff && !p.InjuredOff)) return;
                corners++;
                if (r.Taker != null && r.Taker.Number == second) bySecond++;
            };
            e.RunToEnd();
            Assert.True(corners > 0, "no corners");
            Assert.Equal(0, byFirst);
            Assert.Equal(corners, bySecond);
        }

        [Fact]
        public void EachSideOfTheCornersHasItsOwnTaker()
        {
            var red = DemoTeams.Red();
            int right = red.Starters[3].ShirtNumber, left = red.Starters[6].ShirtNumber;
            TeamPlan.Parse("ctr=" + right + ";ctl=" + left).ApplyBeforeKickOff(red);
            var e = new MatchEngine(red, DemoTeams.Blue(), 13, MatchConfig.CreateDefault());
            int r1 = 0, l1 = 0, wrong = 0;
            e.RestartExecuting += r =>
            {
                if (r.Type != RestartType.CornerKick || r.Team != e.Home || r.Taker == null) return;
                bool rightSide = e.Home.Lateral(r.Spot) < 0f;
                if (rightSide) { r1++; if (r.Taker.Number != right) wrong++; } else { l1++; if (r.Taker.Number != left) wrong++; }
            };
            e.RunToEnd();
            Assert.True(r1 + l1 > 0, "no corners");
            Assert.Equal(0, wrong);
        }

        [Fact]
        public void TheCornerPlanParsesRoutineAndZones()
        {
            var plan = TeamPlan.Parse("crr=2;crl=5;czr=11.0.0.0.0.0;czl=0.0.0.0.0.9");
            var t = plan.ToTactics(new TeamTactics());
            Assert.Equal(2, t.CornerRoutineRight);
            Assert.Equal(5, t.CornerRoutineLeft);
            Assert.Equal(11, t.CornerZonesRight[0]);
            Assert.Equal(9, t.CornerZonesLeft[5]);
        }

        [Fact]
        public void TheNewShotTechniquesAreHit()
        {
            var details = new HashSet<int>();
            for (ulong seed = 1; seed <= 12; seed++)
            {
                var e = Engine(seed);
                e.RunToEnd();
                foreach (var x in e.EventLog) if (x.Type == MatchEventType.Shot) details.Add(x.Detail);
            }
            Assert.Contains(14, details); // laces, hit with power
            Assert.Contains(12, details); // half-volley
        }
    }
}

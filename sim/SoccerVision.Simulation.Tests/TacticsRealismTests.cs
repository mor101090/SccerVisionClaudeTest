using System;
using System.Linq;
using System.Threading.Tasks;
using SoccerVision.Simulation;
using Xunit;
using Xunit.Abstractions;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>
    /// Defending and attacking principles from tactical analysis: marking tightness by distance from the ball, the line
    /// pushing out after a clearance, rebounds, blocking shots, the forward steering the press, the set-piece share of
    /// goals, and the regista and trequartista roles.
    /// </summary>
    public class TacticsRealismTests
    {
        private readonly ITestOutputHelper _out;
        public TacticsRealismTests(ITestOutputHelper output) { _out = output; }

        private static MatchEngine[] Play(ulong from, int n, Func<TeamSetup> red = null)
        {
            var es = new MatchEngine[n];
            Parallel.For(0, n, i =>
            {
                es[i] = new MatchEngine(red != null ? red() : DemoTeams.Red(), DemoTeams.Blue(), from + (ulong)i, MatchConfig.CreateDefault()) { CollectDiagnostics = true };
                es[i].RunToEnd();
            });
            return es;
        }

        [Fact]
        public void DefendersMarkByDistanceTheLinePushesOutAndShotsAreFollowedIn()
        {
            var es = Play(7000, 8);
            double D(string k) => es.Average(e => e.Diagnostics.GetValueOrDefault(k));
            foreach (var k in new[] { "mark.tuckIn", "mark.ballWatch", "line.pushUp", "box.followIn", "box.reboundDefend", "cover.block", "press.steer" })
                _out.WriteLine($"{k}: {D(k):0.0} per match");
            Assert.True(D("mark.tuckIn") > 0, "far-side markers never tuck in");
            Assert.True(D("line.pushUp") > 0, "the line never pushes out after a clearance");
            Assert.True(D("box.followIn") > 0 && D("box.reboundDefend") > 0, "nobody follows the shot in / turns to the goal");
            Assert.True(D("cover.block") > 0, "nobody gets in the line of the shot");
            Assert.True(D("press.steer") > 0, "the forward never steers the press");
            // ball-watching is a lapse, not a habit: seen, but a handful of times a match
            Assert.InRange(D("mark.ballWatch"), 0.5, 40);
            Assert.All(es, e => Assert.Equal(0, e.RecoveredErrors));
        }

        [Fact]
        public void ARealisticShareOfGoalsComesFromSetPieces()
        {
            var es = Play(7100, 24);
            int goals = es.Sum(e => e.Home.Stats.Goals + e.Away.Stats.Goals);
            int setPiece = es.Sum(e => e.Home.Stats.SetPieceGoals + e.Away.Stats.SetPieceGoals);
            double share = setPiece / (double)Math.Max(1, goals);
            _out.WriteLine($"set-piece goals {setPiece} of {goals} ({share:P0}); goals per match {goals / 24.0:0.00}");
            // top leagues: roughly a quarter to a third of the goals
            Assert.InRange(share, 0.15, 0.45);
        }

        private static TeamSetup WithRole(PositionKind kind, PlayerRole role)
        {
            var t = DemoTeams.Red();
            for (int i = 0; i < 11; i++) if (t.Formation.KindAt(i) == kind) t.Tactics.Orders[i].Role = role;
            return t;
        }

        [Fact]
        public void TheRegistaPlaysDeeperAndTheTrequartistaStaysUp()
        {
            // average forward position of the players in that kind of slot while the team has the ball / does not
            static (double inPos, double outPos) Average(MatchEngine e, PositionKind kind)
            {
                double a = 0, b = 0; int na = 0, nb = 0;
                while (!e.IsFinished && e.Clock.Seconds < 45 * 60)
                {
                    e.Advance(0.5f);
                    if (e.Phase != MatchPhase.Live) continue;
                    foreach (var p in e.Home.Players)
                    {
                        if (p.SentOff || p.Kind != kind) continue;
                        if (e.PossessionTeam == e.Home) { a += e.Home.Forward(p.Pos); na++; } else { b += e.Home.Forward(p.Pos); nb++; }
                    }
                }
                return (a / Math.Max(1, na), b / Math.Max(1, nb));
            }
            var red = DemoTeams.Red();
            var dmKind = Enumerable.Range(0, 11).Select(i => red.Formation.KindAt(i)).Contains(PositionKind.DefensiveMidfielder) ? PositionKind.DefensiveMidfielder : PositionKind.CentralMidfielder;
            var amKind = Enumerable.Range(0, 11).Select(i => red.Formation.KindAt(i)).Contains(PositionKind.AttackingMidfielder) ? PositionKind.AttackingMidfielder : PositionKind.CentralMidfielder;
            if (dmKind == PositionKind.DefensiveMidfielder)
            {
                var plain = Average(new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 31, MatchConfig.CreateDefault()), dmKind);
                var regista = Average(new MatchEngine(WithRole(dmKind, PlayerRole.Regista), DemoTeams.Blue(), 31, MatchConfig.CreateDefault()), dmKind);
                _out.WriteLine($"DM in possession: plain {plain.inPos:0.0} m, regista {regista.inPos:0.0} m");
                Assert.True(regista.inPos < plain.inPos - 2, "the regista drops in front of the centre-backs");
            }
            if (amKind == PositionKind.AttackingMidfielder)
            {
                var plain = Average(new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 32, MatchConfig.CreateDefault()), amKind);
                var trequartista = Average(new MatchEngine(WithRole(amKind, PlayerRole.Trequartista), DemoTeams.Blue(), 32, MatchConfig.CreateDefault()), amKind);
                _out.WriteLine($"AM out of possession: plain {plain.outPos:0.0} m, trequartista {trequartista.outPos:0.0} m");
                Assert.True(trequartista.outPos > plain.outPos + 2, "the trequartista stays up when the ball is lost");
            }
            Assert.True(PlayerOrders.Fits(PlayerRole.Regista, PositionKind.DefensiveMidfielder));
            Assert.True(PlayerOrders.Fits(PlayerRole.Trequartista, PositionKind.AttackingMidfielder));
        }
    }
}

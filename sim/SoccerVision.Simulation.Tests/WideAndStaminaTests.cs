using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>Full-backs, wingers and fatigue by position.</summary>
    public class WideAndStaminaTests
    {
        private static TeamSetup WithMentality(TeamSetup t, float mentality)
        {
            t.Tactics.Mentality = mentality;
            return t;
        }

        [Fact]
        public void AnAttackingTeamPushesItsFullBacksUpFurtherThanACautiousOne()
        {
            double Advance(float mentality)
            {
                double sum = 0; int n = 0;
                for (ulong seed = 5000; seed < 5004; seed++)
                {
                    var e = new MatchEngine(WithMentality(DemoTeams.Red(), mentality), DemoTeams.Blue(), seed);
                    while (!e.IsFinished && e.Clock.Seconds < 45 * 60)
                    {
                        e.Advance(0.5f);
                        if (e.Phase != MatchPhase.Live || e.PossessionTeam != e.Home || e.Home.Forward(e.Ball.Pos) < 10f) continue;
                        foreach (var p in e.Home.Players)
                            if (!p.SentOff && p.Slot.IsFullBack) { sum += e.Home.Forward(p.Pos) - e.Home.Forward(e.Ball.Pos); n++; }
                    }
                }
                return sum / n;
            }
            double high = Advance(0.95f), low = Advance(0.05f);
            Assert.True(high > low + 3.0, $"full-backs relative to the ball: attacking {high:0.0} m, cautious {low:0.0} m");
        }

        [Fact]
        public void FullBacksAndWingersCrossShootAndSwitchThePlay()
        {
            var engines = new MatchEngine[12];
            Parallel.For(0, engines.Length, i =>
            {
                engines[i] = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 5100 + (ulong)i) { CollectDiagnostics = true };
                engines[i].RunToEnd();
            });
            int Sum(Func<TeamStats, int> f) => engines.Sum(e => f(e.Home.Stats) + f(e.Away.Stats));
            Assert.True(Sum(s => s.FullBackCrosses) >= 12 * 2, "full-back crosses " + Sum(s => s.FullBackCrosses));
            Assert.True(Sum(s => s.WingerCrosses) >= 12 * 3.5, "winger crosses " + Sum(s => s.WingerCrosses)); // (about 4 a match; it depends on the wingers' crossing)
            Assert.True(Sum(s => s.FullBackShots) >= 1, "full-back shots " + Sum(s => s.FullBackShots));
            Assert.True(Sum(s => s.WingerShots) >= 12 * 1, "winger shots " + Sum(s => s.WingerShots));
            Assert.True(Sum(s => s.Switches) >= 12 * 8, "switches " + Sum(s => s.Switches));
            Assert.True(Sum(s => s.SwitchesCompleted) > Sum(s => s.Switches) * 0.5, "switches completed " + Sum(s => s.SwitchesCompleted));
            int fbSwitches = engines.Sum(e => e.Diagnostics.TryGetValue("pass.switch.fullback", out int v) ? v : 0);
            Assert.True(fbSwitches >= 12, "switches by full-backs " + fbSwitches);
            Assert.Contains(engines.SelectMany(e => e.EventLog), x => x.Type == MatchEventType.Pass && x.Text.Contains("switches the play to"));
        }

        [Fact]
        public void CutInWingersCurlTheBallAroundTheKeeperToTheFarPost()
        {
            var engines = new MatchEngine[12];
            Parallel.For(0, engines.Length, i =>
            {
                engines[i] = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 5200 + (ulong)i) { CollectDiagnostics = true };
                engines[i].RunToEnd();
            });
            int curled = engines.Sum(e => e.Diagnostics.TryGetValue("shot.cutin.curl", out int v) ? v : 0);
            Assert.True(curled >= 12 * 1, "curled cut-inside shots " + curled);
            // They are taken from the flank and aimed at the far corner: the shot's origin is wide, the finish is on the other side.
            var shots = engines.SelectMany(e => e.EventLog).Where(x => x.Type == MatchEventType.Shot && x.Detail == 2).ToList();
            Assert.True(shots.Count >= 12, "cut-inside shots " + shots.Count);
            Assert.True(shots.Count(x => MathF.Abs(x.Y) > 8f) > shots.Count * 0.7, "shot from the flank");
        }

        [Fact]
        public void DefendersAndWideMenTireMostAndGoalkeepersBarelyTire()
        {
            var engines = new MatchEngine[8];
            Parallel.For(0, engines.Length, i =>
            {
                engines[i] = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 5300 + (ulong)i);
                engines[i].RunToEnd();
            });
            double Mean(Func<PlayerAgent, bool> pick) => engines.SelectMany(e => e.AllPlayers.Where(pick)).Average(p => p.Energy);
            double fullBack = Mean(p => !p.IsGoalkeeper && p.Slot.IsFullBack);
            double wide = Mean(p => p.Slot.IsWideMidfielder);
            double centreBack = Mean(p => !p.IsGoalkeeper && p.Slot.IsCentreBack);
            double striker = Mean(p => p.Slot.Line == Line.Attack);
            double keeper = Mean(p => p.IsGoalkeeper);
            Assert.True(fullBack < striker - 0.08, $"full-backs {fullBack:0.00} vs strikers {striker:0.00}");
            Assert.True(wide < striker - 0.08, $"wide midfielders {wide:0.00} vs strikers {striker:0.00}");
            Assert.True(centreBack < striker + 0.02, $"centre-backs {centreBack:0.00} vs strikers {striker:0.00}");
            Assert.True(keeper > 0.9, $"keepers {keeper:0.00}");
            Assert.True(keeper > striker + 0.2, $"keepers {keeper:0.00} vs strikers {striker:0.00}");
        }
    }
}

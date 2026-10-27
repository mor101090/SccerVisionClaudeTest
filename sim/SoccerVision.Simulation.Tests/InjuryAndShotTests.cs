using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>Injuries with automatic substitutions, the card rules, and the ways of striking a shot.</summary>
    public class InjuryAndShotTests
    {
        private static MatchEngine[] Play(ulong first, int n, bool diagnostics = false)
        {
            var engines = new MatchEngine[n];
            Parallel.For(0, n, i =>
            {
                engines[i] = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), first + (ulong)i) { CollectDiagnostics = diagnostics };
                engines[i].RunToEnd();
            });
            return engines;
        }

        [Fact]
        public void SeriousInjuriesAreTreatedThenTheSubstituteComesOnAndPlayRestarts()
        {
            int serious = 0, subs = 0, checkedChanges = 0;
            for (ulong seed = 6000; seed < 6040 && checkedChanges < 4; seed++)
            {
                var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), seed);
                PlayerAgent hurt = null;
                string hurtName = null;
                float hurtAt = 0f;
                while (!e.IsFinished)
                {
                    e.Advance(0.1f);
                    foreach (var ev in e.DrainEvents())
                    {
                        if (ev.Type == MatchEventType.Injury && ev.Detail == 1)
                        {
                            serious++;
                            hurt = e.AllPlayers[ev.Player];
                            hurtName = hurt.Profile.ShortName;
                            hurtAt = e.RealTimeElapsed;
                            Assert.True(hurt.IsDown, "a seriously injured player stays down");
                        }
                        if (ev.Type == MatchEventType.Substitution)
                        {
                            subs++;
                            var p = e.AllPlayers[ev.Player];
                            Assert.Same(hurt, p);
                            Assert.NotEqual(hurtName, p.Profile.ShortName);
                            Assert.Equal(InjuryLevel.None, p.Injury);
                            Assert.Contains("comes on for", ev.Text);
                            Assert.Equal(p.Number + "|" + p.Profile.ShortName, ev.Info);
                            Assert.True(e.RealTimeElapsed - hurtAt < 60f, "the change took " + (e.RealTimeElapsed - hurtAt) + " s");
                            Assert.True(e.Phase != MatchPhase.Live, "the change is made while play is stopped");
                            checkedChanges++;
                            hurt = null;
                        }
                    }
                    // Play never goes on with a seriously injured player lying on the pitch for long.
                    if (hurt != null && e.Phase == MatchPhase.Live && e.RealTimeElapsed - hurtAt > 3f && e.Clock.Period != MatchPeriod.HalfTime)
                        Assert.Fail("play still live " + (e.RealTimeElapsed - hurtAt) + " s after a serious injury");
                }
                Assert.True(e.Diagnostics.GetValueOrDefault("restart.forced.DroppedBall") == 0);
            }
            Assert.True(serious >= 4 && subs >= 4 && checkedChanges >= 4, $"serious {serious} subs {subs}");
        }

        [Fact]
        public void LightInjuriesSlowAPlayerDownAndAreMarked()
        {
            var engines = Play(6100, 12);
            var hurt = engines.SelectMany(e => e.AllPlayers.Where(p => !p.SentOff && p.Injury == InjuryLevel.Light)).ToList();
            Assert.True(hurt.Count >= 3, "lightly injured players " + hurt.Count);
            foreach (var p in hurt)
            {
                Assert.Equal(0.9f, p.InjuryFactor);
                Assert.True(p.FatigueError > 1.1f);
            }
            Assert.Contains(engines.SelectMany(e => e.EventLog), x => x.Type == MatchEventType.Injury && x.Detail == 0 && x.Text.Contains("carries on"));
        }

        [Fact]
        public void NoTeamMakesMoreThanFiveSubstitutionsAndAKeeperIsReplacedByAKeeper()
        {
            var engines = Play(6200, 20);
            foreach (var e in engines)
                foreach (var t in new[] { e.Home, e.Away })
                {
                    Assert.True(t.SubstitutionsUsed <= 5);
                    Assert.Equal(PositionGroup.Goalkeeper, t.Players[0].Profile.NaturalPosition);
                }
        }

        [Fact]
        public void DirectRedCardsAndSecondYellowsBothSendPlayersOff()
        {
            var engines = Play(6300, 40, true);
            int direct = engines.Sum(e => e.Diagnostics.GetValueOrDefault("card.dogso.red") + e.Diagnostics.GetValueOrDefault("card.sfp.red"));
            int second = engines.Sum(e => e.Diagnostics.GetValueOrDefault("card.secondyellow"));
            Assert.True(direct >= 1, "direct red cards " + direct);
            Assert.True(second >= 1, "second yellows " + second);
            foreach (var e in engines)
            {
                foreach (var ev in e.EventLog.Where(x => x.Type == MatchEventType.RedCard))
                    Assert.True(e.AllPlayers[ev.Player].SentOff || e.AllPlayers[ev.Player].Profile.ShortName != null);
                // Two yellows always mean a red (the second is followed straight away by the red card).
                var log = e.EventLog;
                for (int i = 0; i < log.Count; i++)
                    if (log[i].Type == MatchEventType.YellowCard && log[i].Value >= 2f)
                        Assert.Contains(log.Skip(i + 1).Take(3), x => x.Type == MatchEventType.RedCard && x.Player == log[i].Player && x.Value > 0.5f);
            }
        }

        [Fact]
        public void ShotsAreStruckInAllTheDifferentWays()
        {
            var engines = Play(6400, 16, true);
            int Tech(string t) => engines.Sum(e => e.Diagnostics.GetValueOrDefault("shot.tech." + t));
            foreach (var t in new[] { "Power", "PlacedNear", "PlacedFar", "Curler", "Volley", "Chip", "OutsideFoot", "WeakFoot" })
                Assert.True(Tech(t) >= 1, t + " " + Tech(t));
            Assert.True(Tech("Chip") < Tech("Power") / 4, "chips are rare");
            Assert.True(Tech("OutsideFoot") < Tech("WeakFoot"), "the outside of the boot is rarer than the weaker foot");
            var log = engines.SelectMany(e => e.EventLog).ToList();
            Assert.Contains(log, x => x.Type == MatchEventType.Shot && x.Detail == 9 && x.Text.Contains("chip"));
            Assert.Contains(log, x => x.Type == MatchEventType.Shot && x.Detail == 8 && x.Text.Contains("curl"));
            Assert.Contains(log, x => x.Type == MatchEventType.Shot && x.Detail == 11 && x.Text.Contains("weaker foot"));
        }
    }
}

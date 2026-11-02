using System;
using System.Linq;
using System.Threading.Tasks;
using SoccerVision.Simulation;
using Xunit;
using Xunit.Abstractions;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>The human side: character traits that change how a player behaves, gestures and calls on the pitch, personal confidence, the big game, the crowd.</summary>
    public class CharacterTests
    {
        private readonly ITestOutputHelper _out;
        public CharacterTests(ITestOutputHelper output) { _out = output; }

        private static TeamSetup Give(TeamSetup t, PlayerTrait trait, bool outfieldOnly = true)
        {
            foreach (var p in t.Starters.Concat(t.Bench))
            {
                bool gk = p.NaturalPosition == PositionGroup.Goalkeeper;
                p.Traits = gk == outfieldOnly ? PlayerTrait.None : trait;
            }
            return t;
        }

        private static MatchEngine[] Play(Func<TeamSetup> home, ulong from, int n, bool diag = true)
        {
            var es = new MatchEngine[n];
            Parallel.For(0, n, i =>
            {
                es[i] = new MatchEngine(home(), DemoTeams.Blue(), from + (ulong)i, MatchConfig.CreateDefault()) { CollectDiagnostics = diag };
                es[i].RunToEnd();
            });
            return es;
        }

        [Fact]
        public void KeepersGetKeeperTraitsAndOutfieldPlayersDoNot()
        {
            var keeperOnly = PlayerTrait.BoldKeeper | PlayerTrait.StaysOnLine;
            bool bold = false, stays = false;
            for (ulong seed = 1; seed <= 25; seed++)
                foreach (var t in new[] { DemoTeams.Red(seed), DemoTeams.Blue(seed) })
                    foreach (var p in t.Starters.Concat(t.Bench))
                    {
                        if (p.NaturalPosition == PositionGroup.Goalkeeper) { bold |= p.Has(PlayerTrait.BoldKeeper); stays |= p.Has(PlayerTrait.StaysOnLine); }
                        else Assert.Equal(PlayerTrait.None, p.Traits & keeperOnly);
                    }
            Assert.True(bold && stays, "both kinds of keeper are seen");
        }

        [Fact]
        public void GesturesAndCallsAreSeenOnThePitchAndEncoded()
        {
            var es = Play(() => DemoTeams.Red(), 8100, 6);
            int D(string k) => es.Sum(e => e.Diagnostics.GetValueOrDefault("gesture." + k));
            foreach (var k in Enum.GetNames(typeof(GestureKind)).Where(n => n != "None")) _out.WriteLine($"{k}: {D(k) / 6.0:0.0} a match");
            foreach (var k in new[] { "Point", "CallForBall", "Shout", "Disappointed", "Celebrate", "Hug", "Encourage", "Protest", "Complain" })
                Assert.True(D(k) > 0, "never seen: " + k);
            Assert.All(es, e => Assert.Equal(0, e.RecoveredErrors));
            // the replay code: kind, fade 1-99 and an angle in degrees
            var e1 = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 8100, MatchConfig.CreateDefault());
            int seen = 0;
            while (!e1.IsFinished && e1.Clock.Seconds < 20 * 60)
            {
                e1.Advance(0.25f);
                foreach (var p in e1.AllPlayers)
                {
                    int c = e1.GestureCode(p);
                    if (c == 0) continue;
                    Assert.InRange(c / 100000, 1, 9);
                    Assert.InRange(c / 1000 % 100, 1, 99);
                    Assert.InRange(c % 1000, 0, 359);
                    seen++;
                }
            }
            Assert.True(seen > 20, "gestures in the first twenty minutes: " + seen);
        }

        [Fact]
        public void ALeaderOnThePitchMakesTheBackLineWorkAsOne()
        {
            var plain = DemoTeams.Red(); foreach (var p in plain.Starters.Concat(plain.Bench)) p.Traits = PlayerTrait.None;
            var led = DemoTeams.Red(); foreach (var p in led.Starters.Concat(led.Bench)) p.Traits = PlayerTrait.None;
            led.Starters.First(p => p.NaturalPosition == PositionGroup.Defender).Traits = PlayerTrait.Leader;
            var a = new MatchEngine(plain, DemoTeams.Blue(), 5, MatchConfig.CreateDefault());
            var b = new MatchEngine(led, DemoTeams.Blue(), 5, MatchConfig.CreateDefault());
            Assert.True(b.Home.DefensiveChemistry > a.Home.DefensiveChemistry, $"{b.Home.DefensiveChemistry:0.00} v {a.Home.DefensiveChemistry:0.00}");
            Assert.NotNull(MatchEngine.FindLeader(b.Home));
            Assert.Null(MatchEngine.FindLeader(a.Home));
        }

        [Fact]
        public void ARunOfFormFadesAndTheBigGamePlayerRisesLate()
        {
            var t = DemoTeams.Red();
            foreach (var p in t.Starters.Concat(t.Bench)) p.Traits = PlayerTrait.None;
            var star = t.Starters.First(p => p.NaturalPosition == PositionGroup.Forward);
            star.Traits = PlayerTrait.BigGamePlayer;
            var e = new MatchEngine(t, DemoTeams.Blue(), 11, MatchConfig.CreateDefault());
            var pa = e.Home.Players.First(p => p.Profile == star);
            int before = pa.Attr.Finishing;
            e.AddConfidence(pa, 1f);
            Assert.True(pa.Attr.Finishing > before, "a hot streak lifts him");
            float hot = pa.PersonalFactor;
            e.AddConfidence(pa, -2f);
            Assert.True(pa.Attr.Finishing < before, "a cold one drains him");
            // late in a close match the big-game player is lifted; early on he is not
            Assert.Equal(0f, pa.SituationBonus);
            while (!e.IsFinished && e.Clock.Seconds < 78 * 60 && Math.Abs(e.Home.Score - e.Away.Score) <= 1) e.Advance(0.5f);
            // (the lift is felt in play: if the whistle has gone - a set piece, a substitution - wait for the ball to be live)
            for (int k = 0; k < 400 && !e.IsFinished && e.Phase != MatchPhase.Live; k++) e.Advance(0.5f);
            e.Advance(0.5f);
            // (only if he is still on the pitch: a player who has gone off - substituted, injured, sent off - is not lifted)
            // (a substitute takes over the agent of the man he replaces: follow the player, not the agent)
            var now = e.Home.Players.FirstOrDefault(q => q.Profile == star && !q.SentOff);
            if (now != null && Math.Abs(e.Home.Score - e.Away.Score) <= 1 && e.Clock.Seconds >= 75 * 60)
                Assert.True(now.SituationBonus > 0f, $"the big-game player rises late in a close match (minute {e.Clock.Seconds / 60:0}, {e.Home.Score}-{e.Away.Score})");
            Assert.True(hot > 1f);
        }

        [Fact]
        public void TheHomeCrowdPressesHarderWhenItsTeamNeedsAGoalLate()
        {
            var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 3, MatchConfig.CreateDefault());
            Assert.Equal(1f, e.CrowdIntensity(), 3);
            e.Home.Score = 0; e.Away.Score = 1;
            while (!e.IsFinished && e.Clock.Seconds < 82 * 60) { e.Advance(0.5f); e.Home.Score = 0; e.Away.Score = 1; }
            float behind = e.CrowdIntensity();
            e.Home.Score = 2;
            Assert.True(behind > 1.4f, $"behind late: {behind:0.00}");
            Assert.True(e.CrowdIntensity() < behind, "ahead, the crowd presses less");
        }

        [Fact]
        public void TheCharactersChangeHowTheyPlay()
        {
            // roamers are nearer the ball, the position-holders and the runners differ in how often they run in behind
            double Dist(Func<TeamSetup> setup, ulong seed, Func<PlayerAgent, bool> pick, Func<MatchEngine, PlayerAgent, double> measure)
            {
                double sum = 0; long n = 0;
                Parallel.For(0, 3, i =>
                {
                    var e = new MatchEngine(setup(), DemoTeams.Blue(), seed + (ulong)i, MatchConfig.CreateDefault());
                    double s = 0; long c = 0;
                    while (!e.IsFinished && e.Clock.Seconds < 40 * 60)
                    {
                        e.Advance(0.5f);
                        if (e.Phase != MatchPhase.Live) continue;
                        foreach (var p in e.Home.Players) if (!p.SentOff && !p.IsGoalkeeper && pick(p)) { s += measure(e, p); c++; }
                    }
                    lock (this) { sum += s; n += c; }
                });
                return sum / Math.Max(1, n);
            }
            double DistToBall(MatchEngine e, PlayerAgent p) => Vec2.Distance(p.Pos, e.Ball.Pos);
            double Runs(MatchEngine e, PlayerAgent p) => p.RunTimer > 0f ? 1 : 0;
            double Energy(MatchEngine e, PlayerAgent p) => p.Energy;

            // roamers: closer to the ball than the same players holding their post
            double roam = Dist(() => Give(DemoTeams.Red(), PlayerTrait.Roams), 8300, p => p.Slot.Line != Line.Defence, DistToBall);
            double hold = Dist(() => Give(DemoTeams.Red(), PlayerTrait.HoldsPosition), 8300, p => p.Slot.Line != Line.Defence, DistToBall);
            _out.WriteLine($"distance to the ball: roamers {roam:0.0} m, position-holders {hold:0.0} m");
            Assert.True(roam < hold - 1.0, "roamers drift towards the ball");
            // runs in behind
            double runBehind = Dist(() => Give(DemoTeams.Red(), PlayerTrait.RunsBehind), 8400, p => p.Slot.Line != Line.Defence, Runs);
            double stay = Dist(() => Give(DemoTeams.Red(), PlayerTrait.HoldsPosition), 8400, p => p.Slot.Line != Line.Defence, Runs);
            _out.WriteLine($"share of the time on a run: runners {runBehind:P1}, position-holders {stay:P1}");
            Assert.True(runBehind > stay * 1.5, "the runner runs in behind more");
            // energy: the presser burns it, the saver keeps it
            double pressE = Dist(() => Give(DemoTeams.Red(), PlayerTrait.PressesHard), 8500, p => true, Energy);
            double saveE = Dist(() => Give(DemoTeams.Red(), PlayerTrait.SavesEnergy), 8500, p => true, Energy);
            _out.WriteLine($"energy after forty minutes: pressers {pressE:P1}, savers {saveE:P1}");
            Assert.True(pressE < saveE, "pressers tire faster");
        }

        [Fact]
        public void BoldKeepersStandHigherAndTheDiverGoesDownNearTheBox()
        {
            double Depth(PlayerTrait t)
            {
                double sum = 0; long n = 0;
                Parallel.For(0, 3, i =>
                {
                    var setup = DemoTeams.Red();
                    foreach (var p in setup.Starters.Concat(setup.Bench)) p.Traits = p.NaturalPosition == PositionGroup.Goalkeeper ? t : PlayerTrait.None;
                    var e = new MatchEngine(setup, DemoTeams.Blue(), 8600 + (ulong)i, MatchConfig.CreateDefault());
                    double s = 0; long c = 0;
                    while (!e.IsFinished && e.Clock.Seconds < 40 * 60)
                    {
                        e.Advance(0.5f);
                        if (e.Phase != MatchPhase.Live || e.PossessionTeam != e.Home) continue;
                        var gk = e.Home.Goalkeeper;
                        if (gk != null && e.Home.Forward(e.Ball.Pos) > 0f) { s += Pitch.HalfLength - Math.Abs(gk.Pos.X); c++; }
                    }
                    lock (this) { sum += s; n += c; }
                });
                return sum / Math.Max(1, n);
            }
            double bold = Depth(PlayerTrait.BoldKeeper), line = Depth(PlayerTrait.StaysOnLine);
            _out.WriteLine($"keeper off his line while his team attacks: bold {bold:0.0} m, on the line {line:0.0} m");
            Assert.True(bold > line + 0.8, "the bold keeper stands higher");

            var divers = Play(() => Give(DemoTeams.Red(), PlayerTrait.DrawsFouls), 8700, 32); // (rare: a dive near the box)
            int bought = divers.Sum(e => e.Diagnostics.GetValueOrDefault("dive.bought")), booked = divers.Sum(e => e.Diagnostics.GetValueOrDefault("dive.booked"));
            _out.WriteLine($"dives bought {bought}, booked {booked} in 32 matches");
            Assert.True(bought > 0 && booked > 0, "the diver sometimes wins it and is sometimes booked");
            Assert.All(divers, e => Assert.Equal(0, e.RecoveredErrors));
        }
    }
}

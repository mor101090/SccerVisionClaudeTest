using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>Marking systems (zonal / man-to-man) and the two kinds of tackle (standing / sliding).</summary>
    public class DefendingTests
    {
        [Fact]
        public void DemoTeamsUseDifferentMarkingAndTacklingStyles()
        {
            Assert.Equal(MarkingStyle.Zonal, DemoTeams.Red().Tactics.Marking);
            Assert.Equal(MarkingStyle.ManToMan, DemoTeams.Blue().Tactics.Marking);
            Assert.Equal(TacklingStyle.GetStuckIn, DemoTeams.Blue().Tactics.Tackling);
        }

        [Fact]
        public void ManMarkersKeepTheirManAndStayCloseToHim()
        {
            // Blue marks man-to-man: one opponent per defender/midfielder, kept for the whole defensive phase.
            int samples = 0, close = 0, changes = 0;
            for (ulong seed = 3100; seed < 3104; seed++)
            {
                var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), seed);
                var blue = e.Away;
                var last = new Dictionary<PlayerAgent, PlayerAgent>();
                while (!e.IsFinished && e.Clock.Seconds < 30 * 60)
                {
                    e.Advance(0.1f);
                    if (e.Phase != MatchPhase.Live) { last.Clear(); continue; }
                    foreach (var p in blue.Players)
                    {
                        if (p.SentOff || p.IsGoalkeeper || p.Slot.Line == Line.Attack) continue;
                        if (p.ManAssignment == null) { last.Remove(p); continue; }
                        if (last.TryGetValue(p, out var before) && before != p.ManAssignment && !before.SentOff) changes++;
                        last[p] = p.ManAssignment;
                        // (only while he is marking - not while he covers a team-mate or steps in to intercept)
                        if (p.MarkTarget != null && p.Task == PlayerTask.Mark)
                        {
                            Assert.Same(p.ManAssignment, p.MarkTarget);
                            samples++;
                            if (Vec2.Distance(p.Pos, p.MarkTarget.Pos) < 5f) close++;
                        }
                    }
                }
            }
            Assert.True(samples > 2000, "samples " + samples);
            Assert.True(changes < samples / 500, $"man-to-man assignments changed mid-phase {changes} times in {samples} samples");
            // (defenders let a man go when he stands beyond the line - he is left offside - so not every sample is close)
            Assert.True(close > samples * 0.5, $"close {close}/{samples}");
        }

        [Fact]
        public void ZonalMarkersHandOverInsteadOfLeavingTheirZone()
        {
            // Red marks zonally: a marker stays near his place in the block and passes runners on to the next zone.
            int samples = 0, inZone = 0;
            for (ulong seed = 3200; seed < 3204; seed++)
            {
                var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), seed);
                while (!e.IsFinished && e.Clock.Seconds < 30 * 60)
                {
                    e.Advance(0.1f);
                    if (e.Phase != MatchPhase.Live) continue;
                    foreach (var p in e.Home.Players)
                    {
                        if (p.SentOff || p.MarkTarget == null) continue;
                        Assert.Null(p.ManAssignment);
                        samples++;
                        if (Vec2.Distance(p.Pos, p.ShapeTarget) < 13f) inZone++;
                    }
                }
                Assert.True(e.Home.Stats.MarkingSwitches > 0, "zonal markers never handed a man over");
            }
            Assert.True(samples > 2000, "samples " + samples);
            Assert.True(inZone > samples * 0.9, $"in zone {inZone}/{samples}");
        }

        [Fact]
        public void StandingAndSlideTacklesHappenAndTheTacklingInstructionMatters()
        {
            var engines = new MatchEngine[12];
            Parallel.For(0, engines.Length, i =>
            {
                var red = DemoTeams.Red();
                red.Tactics.Tackling = TacklingStyle.StayOnFeet;
                engines[i] = new MatchEngine(red, DemoTeams.Blue(), 3300 + (ulong)i);
                engines[i].RunToEnd();
            });
            int standing = engines.Sum(e => e.Home.Stats.StandingTackles + e.Away.Stats.StandingTackles);
            int redSlides = engines.Sum(e => e.Home.Stats.SlideTackles);
            int blueSlides = engines.Sum(e => e.Away.Stats.SlideTackles);
            Assert.True(standing > (redSlides + blueSlides) * 3, $"standing {standing} slides {redSlides + blueSlides}");
            Assert.True(blueSlides >= 12 * 3, "blue (get stuck in) slides " + blueSlides);
            Assert.True(blueSlides > redSlides * 3, $"blue slides {blueSlides} vs red (stay on feet) {redSlides}");
            var log = engines.SelectMany(e => e.EventLog).ToList();
            Assert.Contains(log, x => x.Type == MatchEventType.Tackle && x.Detail == 1 && x.Text.Contains("sliding tackle"));
            Assert.Contains(log, x => x.Type == MatchEventType.Tackle && x.Detail == 0);
        }

        [Fact]
        public void ASlidingPlayerEndsUpOnTheGroundAndCannotPlay()
        {
            int slides = 0, grounded = 0;
            for (ulong seed = 3400; seed < 3406 && grounded < 5; seed++)
            {
                var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), seed);
                var wasSliding = new HashSet<PlayerAgent>();
                while (!e.IsFinished)
                {
                    e.Advance(0.05f);
                    foreach (var p in e.AllPlayers)
                    {
                        if (p.SlideTimer > 0f)
                        {
                            if (wasSliding.Add(p)) slides++;
                            continue;
                        }
                        if (!wasSliding.Remove(p)) continue;
                        // The slide just ended: he is down for a moment, not chasing or pressing.
                        Assert.True(p.GroundTimer > 0.3f, "ground timer " + p.GroundTimer);
                        Assert.True(p.IsDown);
                        Assert.NotSame(p, p.Team.Presser);
                        Assert.NotSame(p, e.Ball.Owner);
                        grounded++;
                    }
                }
            }
            Assert.True(slides >= 5 && grounded >= 5, $"slides {slides} grounded {grounded}");
        }

        [Fact]
        public void BothTeamsReadPassesShieldBallsOutAndWinShoulderDuels()
        {
            // Every defending skill is engine behaviour for both sides, not a blue-team special.
            var engines = new MatchEngine[10];
            Parallel.For(0, engines.Length, i =>
            {
                engines[i] = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 3500 + (ulong)i);
                engines[i].RunToEnd();
            });
            foreach (var side in new Func<MatchEngine, TeamAgent>[] { e => e.Home, e => e.Away })
            {
                Assert.True(engines.Sum(e => side(e).Stats.AnticipatedInterceptions) >= 10 * 3, "reads");
                Assert.True(engines.Sum(e => side(e).Stats.ShoulderDuelsWon) >= 10 * 4, "shoulder duels");
                Assert.True(engines.Sum(e => side(e).Stats.ShieldedOut) >= 3, "shielded out");
            }
            var log = engines.SelectMany(e => e.EventLog).ToList();
            Assert.Contains(log, x => x.Type == MatchEventType.Interception && x.Detail == 1 && x.Text.Contains("steps in front of"));
            Assert.Contains(log, x => x.Type == MatchEventType.ShieldOut && x.Text.Contains("shields it out"));
            Assert.Contains(log, x => x.Type == MatchEventType.ShoulderDuel && x.Text.Contains("muscles"));
        }

        [Fact]
        public void ABackLineWithBetterUnderstandingStaysFlatter()
        {
            static TeamSetup Drilled(TeamSetup t, int level)
            {
                // Teamwork is used only for the understanding between the defenders, so the rest of the game is unchanged.
                foreach (var p in t.Starters) p.Attributes.Teamwork = level;
                return t;
            }
            double Spread(int level, out int offsides)
            {
                double sum = 0; int n = 0; int off = 0;
                for (ulong seed = 3600; seed < 3606; seed++)
                {
                    // red defends zonally with a back four that holds a line
                    var e = new MatchEngine(Drilled(DemoTeams.Red(), level), DemoTeams.Blue(), seed);
                    while (!e.IsFinished)
                    {
                        e.Advance(0.25f);
                        if (e.Phase != MatchPhase.Live || e.PossessionTeam != e.Away || e.Ball.Owner == null) continue;
                        // where the defenders holding the line (not the one pressing the ball or covering him) want to be
                        var line = e.Home.Players.Where(p => !p.SentOff && !p.IsGoalkeeper && p.Slot.Line == Line.Defence
                                                             && (p.Task == PlayerTask.Shape || p.Task == PlayerTask.Mark)).Select(p => e.Home.Forward(p.Target)).ToList();
                        if (line.Count < 3 || e.Home.Forward(e.Ball.Pos) < -16f) continue; // ball near the box: the line drops
                        sum += line.Max() - line.Min();
                        n++;
                    }
                    off += e.Home.Stats.Offsides;
                }
                offsides = off;
                return sum / n;
            }
            // (offside counts are too noisy over a few matches to compare; the shape of the line is not)
            double drilled = Spread(99, out _);
            double sloppy = Spread(1, out _);
            Assert.True(drilled < sloppy * 0.9, $"line spread drilled {drilled:0.0} m vs sloppy {sloppy:0.0} m");
        }

        [Fact]
        public void ThroughBallsAndPassesIntoSpaceAreFoundAndSomeArrive()
        {
            var engines = new MatchEngine[8];
            Parallel.For(0, engines.Length, i =>
            {
                engines[i] = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 3700 + (ulong)i);
                engines[i].RunToEnd();
            });
            int through = engines.Sum(e => e.Home.Stats.ThroughBalls + e.Away.Stats.ThroughBalls);
            int throughOk = engines.Sum(e => e.Home.Stats.ThroughBallsCompleted + e.Away.Stats.ThroughBallsCompleted);
            int space = engines.Sum(e => e.Home.Stats.PassesIntoSpace + e.Away.Stats.PassesIntoSpace);
            Assert.InRange(through / 8.0, 15, 60);
            Assert.InRange(throughOk / (double)through, 0.2, 0.6);
            Assert.True(space >= 8 * 6, "passes into space " + space);
            var log = engines.SelectMany(e => e.EventLog).ToList();
            Assert.Contains(log, x => x.Type == MatchEventType.Pass && x.Text.Contains("into space for"));
            Assert.Contains(log, x => x.Type == MatchEventType.Pass && x.Text.Contains("through ball"));
        }
    }
}

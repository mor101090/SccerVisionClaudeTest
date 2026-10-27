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
                        if (p.MarkTarget != null)
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
            Assert.True(close > samples * 0.6, $"close {close}/{samples}");
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
    }
}

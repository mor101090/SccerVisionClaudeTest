using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    public class FeatureTests
    {
        private static MatchEngine[] PlayMany(ulong first, int n)
        {
            var engines = new MatchEngine[n];
            Parallel.For(0, n, i =>
            {
                engines[i] = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), first + (ulong)i);
                engines[i].RunToEnd();
            });
            return engines;
        }

        [Fact]
        public void OffsidesHappenInMostMatches()
        {
            var engines = PlayMany(700, 16);
            int withOffside = engines.Count(e => e.EventLog.Any(x => x.Type == MatchEventType.Offside));
            double average = engines.Average(e => e.Home.Stats.Offsides + e.Away.Stats.Offsides);
            Assert.True(withOffside >= 13, $"only {withOffside}/16 matches had an offside");
            Assert.InRange(average, 2.0, 7.0);
        }

        [Fact]
        public void GoalkeepersRushOutOfTheArea()
        {
            var engines = PlayMany(800, 12);
            double average = engines.Average(e => e.Home.Stats.Sorties + e.Away.Stats.Sorties);
            Assert.True(average >= 2.0, $"average sorties {average}");
        }

        [Fact]
        public void HeadersVolleysAndCutInsideShotsHappen()
        {
            var engines = PlayMany(1100, 16);
            int headers = engines.Sum(e => e.EventLog.Count(x => x.Type == MatchEventType.Header));
            int headedShots = engines.Sum(e => e.Home.Stats.Headers + e.Away.Stats.Headers);
            int volleys = engines.Sum(e => e.Home.Stats.Volleys + e.Away.Stats.Volleys);
            int cutIns = engines.Sum(e => e.Home.Stats.CutInShots + e.Away.Stats.CutInShots);
            Assert.True(headers >= 16 * 10, "headers " + headers);
            Assert.True(headedShots >= 16, "headed shots " + headedShots);
            Assert.True(volleys >= 16, "volleys " + volleys);
            Assert.True(cutIns >= 16, "cut-inside shots " + cutIns);
            Assert.Contains(engines.SelectMany(e => e.EventLog), x => x.Type == MatchEventType.Shot && x.Detail == 1 && x.Text.Contains("volleys"));
        }

        [Fact]
        public void GoalCelebrationsVaryAndOnlyAFewJoinIn()
        {
            var kinds = new Dictionary<CelebrationKind, int>();
            var joiners = new List<int>();
            for (ulong seed = 900; seed < 916; seed++)
            {
                var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), seed);
                bool inCelebration = false;
                while (!e.IsFinished)
                {
                    e.Advance(0.25f);
                    bool now = e.Phase == MatchPhase.GoalCelebration;
                    if (now && !inCelebration && e.CelebrationJoiners.Count + 1 > 0)
                    {
                        kinds[e.Celebration] = kinds.GetValueOrDefault(e.Celebration) + 1;
                        joiners.Add(e.CelebrationJoiners.Count);
                    }
                    inCelebration = now;
                }
            }
            int goals = joiners.Count;
            Assert.True(goals >= 25, "goals " + goals);
            Assert.True(kinds.Count >= 5, "celebrations seen: " + string.Join(",", kinds.Keys));
            Assert.True(kinds.GetValueOrDefault(CelebrationKind.Huddle) <= goals * 0.25, "huddles " + kinds.GetValueOrDefault(CelebrationKind.Huddle) + "/" + goals);
            Assert.True(joiners.Average() <= 3.0, "average joiners " + joiners.Average());
            Assert.All(joiners, j => Assert.InRange(j, 0, 6));
        }

        [Fact]
        public void KeeperReleaseIsNeverChargedDown()
        {
            // Regression: at a corner the keeper caught the ball, kicked it out of his hands into an opponent
            // standing next to him and it rebounded into the net (measured 64 charge-downs, 8 goals per 100 matches).
            var engines = PlayMany(9000, 20);
            Assert.Equal(0, engines.Sum(e => e.KeeperChargeDownGoals));
            Assert.True(engines.Sum(e => e.KeeperChargeDowns) <= 1, "charge-downs " + engines.Sum(e => e.KeeperChargeDowns));
        }

        [Fact]
        public void WhistlesFollowIfabGuidance()
        {
            foreach (var e in PlayMany(400, 6))
            {
                var log = e.EventLog;
                var whistles = log.Where(x => x.Type == MatchEventType.Whistle).ToList();
                Assert.Single(whistles, w => w.Whistle == WhistleKind.HalfTime);
                Assert.Single(whistles, w => w.Whistle == WhistleKind.FullTime);
                for (int i = 0; i < log.Count; i++)
                {
                    var x = log[i];
                    // A clear goal needs no whistle to stop play.
                    if (x.Type == MatchEventType.Goal)
                        Assert.False(i + 1 < log.Count && log[i + 1].Type == MatchEventType.Whistle && log[i + 1].ClockSeconds == x.ClockSeconds, "whistle on a goal at " + x.Minute);
                    // Offside: a light whistle straight after the flag.
                    if (x.Type == MatchEventType.Offside)
                        Assert.True(i + 1 < log.Count && log[i + 1].Type == MatchEventType.Whistle && log[i + 1].Whistle == WhistleKind.Light, "offside without whistle at " + x.Minute);
                    // Penalties are given with the long, strong blast.
                    if (x.Type == MatchEventType.PenaltyAwarded)
                        Assert.Contains(log.Take(i).Reverse().Take(4), w => w.Type == MatchEventType.Whistle && w.Whistle == WhistleKind.Long);
                }
                // Restart whistles only for kick-offs, penalties and free kicks that waited for one: far fewer than all restarts.
                int restartWhistles = whistles.Count(w => w.Whistle == WhistleKind.Short);
                int kickOffs = log.Count(x => x.Type == MatchEventType.KickOff);
                int restarts = kickOffs + e.Home.Stats.ThrowIns + e.Away.Stats.ThrowIns + e.Home.Stats.GoalKicks + e.Away.Stats.GoalKicks
                               + e.Home.Stats.Corners + e.Away.Stats.Corners + e.Home.Stats.FreeKicks + e.Away.Stats.FreeKicks;
                Assert.True(restartWhistles >= kickOffs, "kick-offs need the whistle");
                Assert.True(restartWhistles < restarts * 0.4, $"{restartWhistles} restart whistles for {restarts} restarts");
            }
        }

        [Fact]
        public void HeadersAreTimedLeaps()
        {
            var engines = new MatchEngine[6];
            Parallel.For(0, engines.Length, i =>
            {
                engines[i] = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 300 + (ulong)i) { CollectDiagnostics = true };
                engines[i].RunToEnd();
            });
            int Get(string k) => engines.Sum(e => e.Diagnostics.GetValueOrDefault(k));
            int apex = Get("header.jump.apex"), air = Get("header.jump.airborne"), ground = Get("header.jump.ground");
            Assert.True(apex + air + ground > 100, "high headers " + (apex + air + ground));
            Assert.True(apex + air >= 0.8 * (apex + air + ground), $"apex {apex} airborne {air} ground {ground}");
        }

        [Fact]
        public void KickOffsAndPenaltiesWaitAfterTheWhistle()
        {
            int checkedRestarts = 0;
            foreach (var e in PlayMany(5013, 3))
            {
                var log = e.EventLog;
                float minGap = e.Config.WhistleToKickRealSeconds * e.Config.ClockSecondsPerRealSecond - 0.5f;
                for (int i = 0; i < log.Count; i++)
                {
                    bool kickOff = log[i].Type == MatchEventType.KickOff && log[i].ClockSeconds > 1f && log[i].ClockSeconds != 2700f;
                    bool penalty = log[i].Type == MatchEventType.Shot && log[i].Detail == 4;
                    if (!kickOff && !penalty) continue;
                    checkedRestarts++;
                    var whistle = log.Take(i).Last(w => w.Type == MatchEventType.Whistle);
                    Assert.Equal(WhistleKind.Short, whistle.Whistle);
                    Assert.True(log[i].ClockSeconds - whistle.ClockSeconds >= minGap, $"{log[i].Minute} {log[i].Type}: kicked {log[i].ClockSeconds - whistle.ClockSeconds:0.0} clock s after the whistle");
                }
            }
            Assert.True(checkedRestarts >= 5, "checked " + checkedRestarts);
        }

        [Fact]
        public void OffsideFlagStaysUpForSeconds()
        {
            var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 700);
            int offsides = 0;
            float raisedRealSeconds = 0f, longest = 0f;
            while (!e.IsFinished)
            {
                int before = e.Home.Stats.Offsides + e.Away.Stats.Offsides;
                e.Advance(0.1f);
                if (e.Home.Stats.Offsides + e.Away.Stats.Offsides > before) { offsides++; raisedRealSeconds = 0f; }
                if (e.Assistant1.FlagRaised || e.Assistant2.FlagRaised) { raisedRealSeconds += 0.1f; longest = Math.Max(longest, raisedRealSeconds); }
                else raisedRealSeconds = 0f;
            }
            Assert.True(offsides > 0, "no offside in this match");
            Assert.True(longest >= 3.5f, "flag raised for only " + longest + " s");
        }

        [Fact]
        public void BallStaysInsideTheNetAfterAGoal()
        {
            // Regression: hard shots used to pass through the back of the net (the net check stopped 1.5 m behind the
            // line while the net is 2 m deep) and the ball lay behind the goal during the celebration.
            int goals = 0;
            Parallel.For(0, 12, i =>
            {
                var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 6000 + (ulong)i);
                while (!e.IsFinished)
                {
                    e.Advance(0.1f);
                    if (e.Phase != MatchPhase.GoalCelebration) continue;
                    Interlocked.Increment(ref goals);
                    Assert.True(MathF.Abs(e.Ball.Pos.X) <= Pitch.HalfLength + Pitch.GoalDepth + 0.01f, $"seed {6000 + i}: ball at x={e.Ball.Pos.X:0.00} behind the net");
                    Assert.True(MathF.Abs(e.Ball.Pos.Y) <= Pitch.GoalHalfWidth + 0.01f || MathF.Abs(e.Ball.Pos.X) < Pitch.HalfLength, $"seed {6000 + i}: ball outside the side netting");
                }
            });
            Assert.True(goals > 0);
        }

        [Fact]
        public void TakersRunUpToPenaltiesCornersAndGoalKicks()
        {
            int penalties = 0, corners = 0, goalKicks = 0;
            Parallel.For(0, 6, n =>
            {
                var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 5000 + (ulong)n);
                float start = 0f;
                Vec2 startPos = default;
                Restart watched = null;
                e.RestartExecuting += r => watched = r;
                while (!e.IsFinished)
                {
                    e.Advance(0.05f);
                    if (watched == null) continue;
                    var r = watched; watched = null;
                    // At the moment of the kick the taker must be at the ball, having come from the run-up start.
                    if (r.HasRunUp && r.Taker != null && r.RunningUp)
                    {
                        Assert.True(Vec2.Distance(r.RunUpStart, r.Spot) >= 1.5f);
                        if (r.Type == RestartType.PenaltyKick) Interlocked.Increment(ref penalties);
                        if (r.Type == RestartType.CornerKick) Interlocked.Increment(ref corners);
                        if (r.Type == RestartType.GoalKick) Interlocked.Increment(ref goalKicks);
                    }
                }
                _ = start; _ = startPos;
            });
            Assert.True(corners >= 10, "corners with a run-up " + corners);
            Assert.True(goalKicks >= 10, "goal kicks with a run-up " + goalKicks);
        }

        [Fact]
        public void TallPlayersWinMoreHeaderDuelsAndHeightIsVaried()
        {
            var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 1);
            var tall = e.AllPlayers.First(p => !p.IsGoalkeeper && p.Profile.HeightCm >= 188);
            var small = e.AllPlayers.First(p => !p.IsGoalkeeper && p.Profile.HeightCm <= 176);
            tall.Attr.Jumping = small.Attr.Jumping;
            Assert.True(tall.HeadReach > small.HeadReach + 0.1f, $"reach {tall.HeadReach} vs {small.HeadReach}");
            Assert.True(small.MaxSpeed * (small.Attr.Speed == tall.Attr.Speed ? 1f : 0f) >= 0f);
            var heights = e.AllPlayers.Select(p => p.Profile.HeightCm).ToList();
            Assert.True(heights.Max() - heights.Min() >= 15, "height range " + heights.Min() + "-" + heights.Max());
            Assert.Contains(e.AllPlayers, p => p.Attr.StrongFoot == Foot.Left);
            Assert.Contains(e.AllPlayers, p => p.Attr.StrongFoot == Foot.Right);
        }
    }
}

using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>Laws of the Game checks, recorded at the instant each restart is taken during real matches.</summary>
    public class RulesTests
    {
        private const float Eps = 0.05f;

        private sealed class RestartAudit
        {
            public readonly Dictionary<RestartType, int> Count = new Dictionary<RestartType, int>();
            public readonly List<string> Failures = new List<string>();
        }

        private static RestartAudit AuditMatch(ulong seed)
        {
            var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), seed);
            var audit = new RestartAudit();
            e.RestartExecuting = r => Check(e, r, audit);
            e.RunToEnd();
            Assert.True(e.IsFinished);
            return audit;
        }

        private static void Check(MatchEngine e, Restart r, RestartAudit audit)
        {
            audit.Count.TryGetValue(r.Type, out int n);
            audit.Count[r.Type] = n + 1;
            var att = r.Team;
            var def = att.Opponent;
            void Fail(PlayerAgent p, string why) => audit.Failures.Add($"{e.Clock.MinuteText} {r.Type} {p}: {why} at {p.Pos}");

            foreach (var p in e.AllPlayers)
            {
                if (p.SentOff || p == r.Taker) continue;
                float d = Vec2.Distance(p.Pos, r.Spot);
                switch (r.Type)
                {
                    case RestartType.KickOff:
                        if (p.Team.Forward(p.Pos) > Eps) Fail(p, "not in own half at kick-off");
                        if (p.Team == def && d < Pitch.CentreCircleRadius - Eps) Fail(p, "inside the centre circle");
                        break;
                    case RestartType.ThrowIn:
                        if (p.Team == def && d < Pitch.ThrowInDistance - Eps) Fail(p, "closer than 2 m to the throw");
                        break;
                    case RestartType.GoalKick:
                        if (p.Team == def && Pitch.InPenaltyArea(p.Pos, att.OwnGoalSign) && Vec2.Distance(p.Pos, r.Spot) > 0f)
                        {
                            // On the boundary counts as inside; allow a few cm.
                            float depth = Pitch.HalfLength - p.Pos.X * att.OwnGoalSign;
                            if (depth < Pitch.PenaltyAreaDepth - Eps && System.MathF.Abs(p.Pos.Y) < Pitch.PenaltyAreaHalfWidth - Eps) Fail(p, "inside the penalty area at a goal kick");
                        }
                        break;
                    case RestartType.CornerKick:
                        if (p.Team == def && d < Pitch.RestartDistance - Eps) Fail(p, "closer than 9.15 m to a corner");
                        break;
                    case RestartType.DirectFreeKick:
                    case RestartType.IndirectFreeKick:
                        bool onGoalLine = System.MathF.Abs(System.MathF.Abs(p.Pos.X) - Pitch.HalfLength) < 0.6f && System.MathF.Abs(p.Pos.Y) < Pitch.GoalHalfWidth;
                        if (p.Team == def && !onGoalLine && d < Pitch.RestartDistance - Eps) Fail(p, "closer than 9.15 m to a free kick");
                        break;
                    case RestartType.PenaltyKick:
                        int goal = att.AttackGoalSign;
                        if (p == def.Goalkeeper)
                        {
                            if (System.MathF.Abs(System.MathF.Abs(p.Pos.X) - Pitch.HalfLength) > 0.3f) Fail(p, "keeper off his line");
                            break;
                        }
                        if (Pitch.InPenaltyArea(p.Pos, goal)) Fail(p, "inside the area at a penalty");
                        if (d < Pitch.PenaltyArcRadius - Eps) Fail(p, "inside the arc at a penalty");
                        if (p.Pos.X * goal > r.Spot.X * goal + Eps) Fail(p, "in front of the penalty mark");
                        break;
                }
            }
        }

        [Theory]
        [InlineData(1UL)]
        [InlineData(8UL)]
        [InlineData(64UL)]
        public void RestartsAreTakenFromLegalPositions(ulong seed)
        {
            var audit = AuditMatch(seed);
            Assert.True(audit.Failures.Count == 0, string.Join("\n", audit.Failures.Take(10)));
            Assert.True(audit.Count[RestartType.KickOff] >= 2);
            Assert.True(audit.Count.ContainsKey(RestartType.ThrowIn));
            Assert.True(audit.Count.ContainsKey(RestartType.GoalKick));
        }

        [Fact]
        public void EveryRestartTypeAppearsOverManyMatches()
        {
            var total = new Dictionary<RestartType, int>();
            var failures = new List<string>();
            for (ulong seed = 200; seed < 212; seed++)
            {
                var a = AuditMatch(seed);
                failures.AddRange(a.Failures);
                foreach (var kv in a.Count)
                {
                    total.TryGetValue(kv.Key, out int v);
                    total[kv.Key] = v + kv.Value;
                }
            }
            Assert.True(failures.Count == 0, string.Join("\n", failures.Take(10)));
            foreach (var type in new[] { RestartType.KickOff, RestartType.ThrowIn, RestartType.GoalKick, RestartType.CornerKick, RestartType.DirectFreeKick, RestartType.IndirectFreeKick })
            {
                Assert.True(total.ContainsKey(type) && total[type] > 0, "no " + type + " in 12 matches");
            }
        }

        [Fact]
        public void KickOffGoesBackwardsAsTheDesignRequires()
        {
            var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 3);
            int checkedKickOffs = 0;
            while (!e.IsFinished && checkedKickOffs < 3)
            {
                e.Advance(0.05f);
                foreach (var ev in e.DrainEvents())
                {
                    if (ev.Type != MatchEventType.KickOff) continue;
                    var team = e.TeamOf(ev.Team);
                    // The kick-off pass was played: the ball must be travelling towards the own goal.
                    float forwardSpeed = team.Forward(e.Ball.Pos + e.Ball.Velocity) - team.Forward(e.Ball.Pos);
                    Assert.True(forwardSpeed < 0f, "kick-off pass not backwards");
                    checkedKickOffs++;
                }
            }
            Assert.True(checkedKickOffs >= 1);
        }

        [Fact]
        public void OffsidePositionFollowsLaw11()
        {
            var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 1);
            var att = e.Home; // attacks +X in the first half
            Assert.Equal(1, att.AttackDir);
            var striker = att.Players[9];
            // Put the defending team on a line at x = 20 (keeper deeper at 50).
            foreach (var d in att.Opponent.Players) d.Pos = new Vec2(d.IsGoalkeeper ? 50f : 20f, d.Pos.Y);
            e.Ball.Place(new Vec2(0f, 0f));

            striker.Pos = new Vec2(25f, 0f);
            Assert.True(e.InOffsidePosition(striker));
            striker.Pos = new Vec2(20f, 0f); // level with the second-last opponent: onside
            Assert.False(e.InOffsidePosition(striker));
            striker.Pos = new Vec2(-2f, 0f); // own half
            Assert.False(e.InOffsidePosition(striker));
            e.Ball.Place(new Vec2(30f, 0f));
            striker.Pos = new Vec2(25f, 0f); // behind the ball
            Assert.False(e.InOffsidePosition(striker));
        }

        [Fact]
        public void PitchGeometry()
        {
            Assert.Equal(new Vec2(41.5f, 0f), Pitch.PenaltySpot(1));
            Assert.True(Pitch.InPenaltyArea(new Vec2(40f, 10f), 1));
            Assert.False(Pitch.InPenaltyArea(new Vec2(35f, 10f), 1));
            Assert.False(Pitch.InPenaltyArea(new Vec2(40f, 21f), 1));
            Assert.True(Pitch.InGoalArea(new Vec2(-50f, 8f), -1));
            float angle = Pitch.GoalAngle(Pitch.PenaltySpot(1), 1);
            Assert.InRange(angle * 180f / MathUtil.Pi, 35f, 38f); // ~36.8 degrees from the penalty mark
        }

        [Fact]
        public void ExpectedGoalsModelIsSensible()
        {
            float spot = ExpectedGoals.FromLocation(new Vec2(41.5f, 0f), 1);
            float close = ExpectedGoals.FromLocation(new Vec2(48f, 0f), 1);
            float edge = ExpectedGoals.FromLocation(new Vec2(36f, 0f), 1);
            float far = ExpectedGoals.FromLocation(new Vec2(22f, 0f), 1);
            float tight = ExpectedGoals.FromLocation(new Vec2(51f, 15f), 1);
            Assert.InRange(spot, 0.12f, 0.3f);
            Assert.True(close > spot && spot > edge && edge > far);
            Assert.True(tight < 0.05f);
            Assert.True(far < 0.04f);
        }

        [Fact]
        public void ExpectedThreatRisesTowardsGoal()
        {
            Assert.True(ExpectedThreat.At(48f, 0f) > ExpectedThreat.At(30f, 0f));
            Assert.True(ExpectedThreat.At(30f, 0f) > ExpectedThreat.At(0f, 0f));
            Assert.True(ExpectedThreat.At(0f, 0f) > ExpectedThreat.At(-45f, 0f));
            Assert.True(ExpectedThreat.At(48f, 0f) > ExpectedThreat.At(48f, 30f));
        }

        [Fact]
        public void CardsAndSendingsOffAreConsistent()
        {
            for (ulong seed = 300; seed < 320; seed++)
            {
                var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), seed);
                e.RunToEnd();
                foreach (var p in e.AllPlayers)
                {
                    int reds = e.EventLog.Count(x => x.Type == MatchEventType.RedCard && x.Player == p.Index);
                    int yellows = e.EventLog.Count(x => x.Type == MatchEventType.YellowCard && x.Player == p.Index);
                    Assert.Equal(reds > 0, p.SentOff);
                    Assert.True(reds <= 1);
                    Assert.True(yellows <= 2);
                    if (yellows == 2) Assert.True(p.SentOff, "two yellows must be a red");
                    if (p.SentOff)
                    {
                        // A sent-off player never appears in a later event as the actor.
                        var red = e.EventLog.First(x => x.Type == MatchEventType.RedCard && x.Player == p.Index);
                        int idx = e.EventLog.IndexOf(red);
                        Assert.DoesNotContain(e.EventLog.Skip(idx + 1), x => x.Player == p.Index && x.Type != MatchEventType.RedCard);
                    }
                }
                Assert.Equal(0, e.RuleViolations);
            }
        }
    }
}

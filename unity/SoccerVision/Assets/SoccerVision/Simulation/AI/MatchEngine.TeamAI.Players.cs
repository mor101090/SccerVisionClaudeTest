using System;
using System.Collections.Generic;
using System.Linq;

namespace SoccerVision.Simulation
{
    /// <summary>Per-player thinking, with and without the ball: what each player does this moment.</summary>
    public sealed partial class MatchEngine
    {
        private void ThinkLive(PlayerAgent p, float dt)
        {
            p.FaceBall = true;
            if (p.StunTimer > 0f)
            {
                p.Target = p.Pos + p.Vel * 0.3f;
                p.Urgency = 0.2f;
                return;
            }

            if (Ball.Owner == p)
            {
                ThinkOnBall(p, dt);
                return;
            }

            if (p.IsGoalkeeper)
            {
                ThinkGoalkeeper(p, dt);
                return;
            }

            var team = p.Team;
            bool free = Ball.Owner == null;

            if (free && team.Shielder == p)
            {
                p.Task = PlayerTask.Shield;
                p.Target = ShieldSpot(p);
                p.Urgency = 0.9f;
                return;
            }

            if (free && (team.Chaser == p || Ball.IntendedReceiver == p || team.Anticipator == p))
            {
                UpdateIntercept(p);
                p.Task = Ball.IntendedReceiver == p ? PlayerTask.Receive : PlayerTask.Chase;
                p.Target = p.InterceptPoint;
                p.Urgency = 1f;
                return;
            }

            if (!free && Ball.Owner.Team != team && (team.Presser == p || team.SecondPresser == p))
            {
                var carrier = Ball.Owner;
                Vec2 toGoal = (team.OwnGoal - carrier.Pos).Normalized;
                // Jockey goal-side; step in tighter when the carrier faces away from our goal.
                bool facingAway = Vec2.Dot(carrier.FacingDir, toGoal) < -0.2f;
                bool nearGoal = Vec2.Distance(carrier.Pos, team.OwnGoal) < 30f;
                float standOff = team.Presser == p ? (facingAway || nearGoal ? 0.95f : 1.6f) : 2.6f;
                Vec2 lateral = team.SecondPresser == p ? toGoal.Perp * (MathF.Sign(Vec2.Cross(toGoal, p.Pos - carrier.Pos)) * 2.5f) : Vec2.Zero;
                p.Task = PlayerTask.Press;
                Vec2 approach = toGoal;
                if (team.Presser == p && !nearGoal)
                {
                    // Cover shadow: the presser curves his run so that his body also blocks the carrier's best forward
                    // pass (to the nearest team-mate ahead of him) - pressing and cutting the lane at once.
                    PlayerAgent option = null;
                    float best = 22f;
                    float carrierF = carrier.Team.Forward(carrier.Pos);
                    // A forward pressing a centre-back curves his run to shut the square pass to the other centre-back and
                    // steers the play down one side (he cannot chase both of them).
                    bool steer = p.Slot.Line == Line.Attack && carrier.Slot.Line == Line.Defence && carrierF < -15f;
                    foreach (var q in carrier.Team.Players)
                    {
                        if (q == carrier || q.SentOff || q.IsGoalkeeper) continue;
                        bool otherBack = steer && q.Slot.Line == Line.Defence && MathF.Abs(carrier.Team.Forward(q.Pos) - carrierF) < 10f;
                        if (!otherBack && carrier.Team.Forward(q.Pos) < carrierF + 3f) continue;
                        float d = Vec2.Distance(q.Pos, carrier.Pos) * (otherBack ? 0.6f : 1f);
                        if (d < best) { best = d; option = q; }
                    }
                    if (steer && option != null && CollectDiagnostics) Count("press.steer");
                    if (option != null) approach = (toGoal * 0.65f + (option.Pos - carrier.Pos).Normalized * 0.35f).Normalized;
                }
                p.Target = carrier.Pos + carrier.Vel * 0.35f + approach * standOff + lateral;
                // Near goal the second man blocks the dribbling lane straight in front of the carrier.
                if (team.SecondPresser == p && nearGoal) p.Target = carrier.Pos + toGoal * 2.6f + carrier.Vel * 0.25f;
                p.Urgency = Vec2.Distance(p.Pos, carrier.Pos) > 4f ? 0.95f : 0.75f;
                return;
            }

            if (!free && Ball.Owner.Team != team && team.Cover == p)
            {
                p.Task = PlayerTask.Cover;
                p.Target = team.CoverSpot;
                p.Urgency = Vec2.Distance(p.Pos, team.CoverSpot) > 3f ? 0.85f : 0.55f;
                return;
            }

            // Free ball heading to an opponent: the nearest player closes the receiver down.
            if (free && Ball.IntendedReceiver != null && Ball.IntendedReceiver.Team != team && team.Presser == null && team.Chaser != p)
            {
                // handled by shape/marking below
            }

            ThinkOffBall(p, dt);
        }

        private void ThinkOffBall(PlayerAgent p, float dt)
        {
            var team = p.Team;
            bool inPossession = PossessionTeam == team;
            Vec2 target = p.ShapeTarget;
            bool wasSupporting = p.Task == PlayerTask.Support;
            p.Task = PlayerTask.Shape;
            float urgency = 0.5f;

            // The roamer leaves his post and drifts towards the ball (up to 4 m, 7 in possession), looking for it between the
            // lines - and leaves a hole behind him.
            if (p.Has(PlayerTrait.Roams))
            {
                Vec2 toBall = Ball.Pos - target;
                float db = toBall.Length;
                if (db > 9f) target += toBall.Normalized * MathF.Min(db - 9f, inPossession ? 7f : 4f);
            }

            // Rebounds: for a moment after a shot near goal, the attackers follow it in (a parry, a post, a block falls
            // to the one who gambled) and the defenders turn and get between them and the goal.
            if (Ball.Owner == null && _lastShotTeam != null && SimTime - _lastShotTime < 1.4f)
            {
                var st = _lastShotTeam;
                Vec2 goal = st.AttackGoal;
                if (Vec2.Distance(Ball.Pos, goal) < 24f && Vec2.Distance(p.Pos, goal) < 28f)
                {
                    float myF = st.Forward(p.Pos), myL = st.Lateral(p.Pos);
                    float lat = MathUtil.Clamp(0.5f * st.Lateral(Ball.Pos) + 0.5f * myL, -9f, 9f);
                    // (not everyone gambles on it: the ones who read it - the better off the ball, the more often)
                    bool gambles = ((p.Number * 7 + (int)(_lastShotTime * 10f)) % 10) < 3 + (int)(5f * MathUtil.A(p.Attr.OffTheBall));
                    if (team == st && p.Slot.Line != Line.Defence && gambles && Vec2.Distance(p.Pos, goal) < 22f)
                    {
                        // (not offside-blind: from where he is, on towards the six-yard box)
                        p.Target = st.ToWorld(MathF.Max(myF + 2f, Pitch.HalfLength - 8f), lat);
                        p.Task = PlayerTask.Run;
                        p.Urgency = 0.95f;
                        if (CollectDiagnostics && p.Number % 3 == 0) Count("box.followIn");
                        return;
                    }
                    if (team != st)
                    {
                        // goal-side: between the nearest danger and the goal, a stride nearer the line
                        p.Target = st.ToWorld(MathF.Max(myF + 1.5f, Pitch.HalfLength - 6f), lat * 0.8f);
                        p.Task = PlayerTask.Mark;
                        p.Urgency = 0.9f;
                        if (CollectDiagnostics && p.Number % 3 == 0) Count("box.reboundDefend");
                        return;
                    }
                }
            }

            if (inPossession)
            {
                var carrier = Ball.Owner != null && Ball.Owner.Team == team ? Ball.Owner : null;
                if (carrier != null && team.Supporters.Contains(p))
                {
                    Vec2 avoid = team.Supporters.Count > 1 && team.Supporters[0] != p ? team.Supporters[0].SupportSpot : new Vec2(999f, 999f);
                    if (p.DecisionTimer <= 0f || !wasSupporting)
                    {
                        p.SupportSpot = BestSupportSpot(p, carrier, avoid);
                        p.DecisionTimer = 0.6f;
                    }
                    p.DecisionTimer -= dt;
                    target = p.SupportSpot;
                    p.Task = PlayerTask.Support;
                    urgency = 0.6f;
                }

                // Give-and-go: the passer bursts past his man for the ball straight back.
                if (p.OneTwoUntil > SimTime && p.OneTwoWith != null && (Ball.Owner == p.OneTwoWith || Ball.IntendedReceiver == p.OneTwoWith || Ball.IntendedReceiver == p))
                {
                    p.Target = p.OneTwoSpot;
                    p.Task = PlayerTask.Run;
                    p.Urgency = 1f;
                    return;
                }

                // Third-man run: the ball is going into (or is with) the team-mate who will lay it off - go now, in behind,
                // while the defenders watch the ball.
                if (p.ThirdManUntil > SimTime && p.ThirdManVia != null && (Ball.Owner == p.ThirdManVia || Ball.IntendedReceiver == p.ThirdManVia || Ball.IntendedReceiver == p))
                {
                    float offsideLine = MathF.Max(MathF.Max(SecondLastDefenderForward(team), team.Forward(Ball.Pos)), 0f);
                    // (onside until the lay-off: hold on the shoulder while the ball goes in, then go)
                    float f = Ball.Owner == p.ThirdManVia ? offsideLine + p.RunPlan.X + 4f : offsideLine - 0.8f;
                    p.Target = team.ToWorld(MathUtil.Clamp(f, -10f, 49f), p.RunPlan.Y);
                    p.Task = PlayerTask.Run;
                    p.Urgency = 1f;
                    return;
                }

                // Runs in behind (strikers / wide players), timed on the last defender's shoulder.
                if (carrier != null && IsRunner(p))
                {
                    float carrierF = team.Forward(carrier.Pos);
                    float pressure = Pressure(carrier, out _);
                    if (p.RunTimer <= 0f && p.RunCooldown <= 0f && carrierF > -25f && pressure < 0.6f)
                    {
                        float willing = 0.2f + 0.4f * MathUtil.A(p.Attr.OffTheBall) + 0.25f * team.Eff.Mentality;
                        if (team.Tactics.Attack == AttackStyle.RunsInBehind) willing *= 1.6f;
                        if (team.CounterUntil > SimTime) willing *= 1.8f;
                        // Central midfielders arrive late from deep (third-man runs), only around the box; the role decides.
                        willing *= RoleRunFactor(p, team.Forward(carrier.Pos));
                        if (p.PlayRole != PlayerRole.FalseNine && FalseNineOn(team)) willing *= 1.25f; // the space a false nine leaves is there to be run into
                        // Timing (a dynamic advantage): the run is made when the carrier can play it - facing forward, head up.
                        // Too early and he cannot use it; a run into a carrier with his back to goal is wasted.
                        bool headUp = Vec2.Dot(carrier.FacingDir, team.DirToWorld(Vec2.UnitX)) > 0.25f && pressure < 0.4f;
                        willing *= headUp ? 1.3f : 0.65f;
                        if (p.Has(PlayerTrait.RunsBehind)) willing *= 1.4f;
                        if (_rng.Chance(willing * 0.45f))
                        {
                            p.RunTimer = _rng.Range(2.2f, 3.6f);
                            if (p.Has(PlayerTrait.RunsBehind) || _rng.Chance(0.35f)) Gesture(p, GestureKind.CallForBall, 1.1f, (carrier.Pos - p.Pos).Angle); // "here!"
                            // Counter-movement: a clever forward first comes short (as if for the ball at his feet) to drag his
                            // marker up, then spins in behind into the space the marker left.
                            if ((p.Slot.Line == Line.Attack || p.Slot.IsWideMidfielder) && _rng.Chance(0.15f + 0.5f * MathUtil.A(p.Attr.OffTheBall)))
                            {
                                p.RunCheckUntil = SimTime + _rng.Range(0.6f, 1.0f);
                                p.RunTimer += 0.8f;
                                team.Stats.CounterMovements++;
                            }
                            // Imperfect timing: low off-the-ball players drift offside more often.
                            float timingError = _rng.Gaussian(0.9f, 2.0f * (1.4f - MathUtil.A(p.Attr.OffTheBall)));
                            float lateral = MathUtil.Clamp(team.Lateral(p.Pos) * 0.8f + _rng.Range(-6f, 6f), -26f, 26f);
                            p.RunPlan = new Vec2(timingError, lateral); // (overshoot beyond the line, lateral) in local space
                        }
                        p.RunCooldown = _rng.Range(4f, 9f) * (team.Tactics.Attack == AttackStyle.RunsInBehind || team.CounterUntil > SimTime ? 0.5f : 1f) * (p.Has(PlayerTrait.RunsBehind) ? 0.7f : 1f);
                    }
                    if (p.RunTimer > 0f && p.RunCheckUntil > SimTime)
                    {
                        // (the check: a few steps towards the ball)
                        target = p.Pos + (carrier.Pos - p.Pos).Normalized * 3.5f;
                        p.Task = PlayerTask.Run;
                        urgency = 0.7f;
                    }
                    else if (p.RunTimer > 0f)
                    {
                        float offsideLine = MathF.Max(MathF.Max(SecondLastDefenderForward(team), team.Forward(Ball.Pos)), 0f);
                        float f = MathUtil.Clamp(offsideLine - 0.5f + p.RunPlan.X, -10f, 49f);
                        target = team.ToWorld(f, p.RunPlan.Y);
                        p.Task = PlayerTask.Run;
                        urgency = 0.85f;
                    }
                }
            }
            else
            {
                p.OneTwoUntil = -1f;
                // Out of possession: goal-side of the man this player is marking (see AssignMarking).
                float ballF = team.Forward(Ball.Pos);
                float ballL = team.Lateral(Ball.Pos);
                var mark = p.MarkTarget;
                if (mark != null && !mark.SentOff)
                {
                    bool inBox = Pitch.InPenaltyArea(mark.Pos, team.OwnGoalSign);
                    Vec2 lead = mark.Pos + mark.Vel * 0.3f;
                    float marking = 0.65f + 0.35f * MathUtil.A(p.Attr.Marking);
                    // Distance marking: the closer his man is to the ball, the tighter; far from it he gives him room and,
                    // on the far side, lets him go a little to tuck in towards the middle (the shape comes first there).
                    float dBall = Vec2.Distance(mark.Pos, Ball.Pos);
                    float loose = inBox ? 0f : MathUtil.Clamp01((dBall - 10f) / 30f);
                    bool farSide = loose > 0.55f && MathF.Abs(ballL) > 8f && MathF.Sign(team.Lateral(mark.Pos)) != MathF.Sign(ballL);
                    if (team.Tactics.Marking == MarkingStyle.ManToMan)
                    {
                        // Tight, goal-side, and he goes wherever his man goes (looser when the ball is far away).
                        // (a poor marker gives his man more room)
                        float gap = (inBox ? 0.9f : 1.1f + 3f * loose) * (2f - marking);
                        Vec2 goalSide = lead + (team.OwnGoal - lead).Normalized * gap;
                        if (farSide)
                        {
                            Vec2 tucked = team.ToWorld(team.Forward(goalSide) - 2f, team.Lateral(goalSide) * 0.6f);
                            goalSide = Vec2.Lerp(goalSide, tucked, MathUtil.Clamp01((loose - 0.55f) / 0.45f));
                            if (CollectDiagnostics) Count("mark.tuckIn");
                        }
                        // never followed deep into the opponents' half
                        if (team.Forward(goalSide) > 8f) goalSide = team.ToWorld(8f, team.Lateral(goalSide));
                        target = goalSide;
                        p.Task = PlayerTask.Mark;
                        float behind = Vec2.Distance(p.Pos, goalSide);
                        urgency = behind > 4f || inBox ? 0.85f : behind > 1.5f ? 0.65f : 0.5f;
                    }
                    else
                    {
                        // Zonal: step towards the man in my zone, but never out of it - he is handed over when he leaves.
                        Vec2 goalSide = lead + (team.OwnGoal - lead).Normalized * (inBox ? 1.0f : 1.8f);
                        float r = ZoneRadius(p) * 0.75f;
                        Vec2 fromZone = goalSide - p.ShapeTarget;
                        if (fromZone.Length > r) goalSide = p.ShapeTarget + fromZone.Normalized * r;
                        float weight = ((p.Slot.Line == Line.Defence ? 0.7f : 0.55f) + (inBox ? 0.25f : 0f)) * (1f - 0.45f * loose);
                        target = Vec2.Lerp(p.ShapeTarget, goalSide, MathUtil.Clamp01(weight * marking));
                        p.Task = PlayerTask.Mark;
                        if (inBox || Vec2.Distance(p.Pos, goalSide) > 4f) urgency = 0.85f;
                    }
                }
                // Ball-watching: now and then a defender near the ball loses his man for a moment and only watches the
                // ball (more often a poor marker, a tired one, or in the chaos of the box) - the runner gets away from him.
                if (mark != null && !mark.SentOff && p.BallWatchUntil < SimTime - 10f && Vec2.Distance(p.Pos, Ball.Pos) < 26f
                    && _rng.Chance(dt * 0.008f * (1.3f - MathUtil.A(p.Attr.Marking)) * (1.4f - MathUtil.A(p.Attr.Positioning) * 0.6f) * (2f - p.FatigueFactor)))
                {
                    p.BallWatchUntil = SimTime + _rng.Range(0.7f, 1.3f);
                    if (CollectDiagnostics) Count("mark.ballWatch");
                }
                if (p.BallWatchUntil > SimTime && mark != null)
                {
                    target = p.Pos + (Ball.Pos - p.Pos).Normalized * 0.4f;
                    urgency = 0.3f;
                }
                // Hold the line: a defender does not drop off to follow a forward standing beyond the line while the
                // ball is still in front of it and under pressure - he is left offside. How reliably the line holds depends on the
                // understanding between the defenders (DefensiveChemistry) and the player's own positioning.
                if (p.Slot.Line == Line.Defence && Ball.Owner != null && Ball.Owner.Team != team && !Ball.InHands)
                {
                    float chem = team.DefensiveChemistry;
                    float line = team.LineForward;
                    // Only with pressure on the ball: an unpressured passer can play it over the top, so then the
                    // line drops with the runners ("ball pressured - hold, ball free - drop").
                    bool ballInFront = team.Forward(Ball.Pos) > line + 4f && Pressure(Ball.Owner, out _) > 0.3f;
                    bool trusts = chem * (0.6f + 0.4f * MathUtil.A(p.Attr.Positioning)) + 0.12f * MathF.Sin(SimTime * 0.31f + p.NoisePhaseB) > 0.55f;
                    float slack = 0.4f + 2.2f * (1f - chem);
                    bool trap = team.Tactics.Defence == DefenceStyle.OffsideTrap || team.Tactics.Defence == DefenceStyle.HighBackline;
                    bool broken = trap && p == team.TrapBreaker && team.TrapBrokenUntil > SimTime;
                    if (trap) { trusts = !broken; slack = 0.3f; ballInFront = team.Forward(Ball.Pos) > line + 4f; } // the trap holds the line whatever the pressure
                    if (broken) target = team.ToWorld(MathF.Min(team.Forward(target), line - 6.5f), team.Lateral(target)); // one man is left behind: nobody is offside
                    if (ballInFront && trusts && !Pitch.InPenaltyArea(target, team.OwnGoalSign) && team.Forward(target) < line - slack)
                    {
                        target = team.ToWorld(line - slack, team.Lateral(target));
                        if (CollectDiagnostics) Count("line.hold");
                    }
                    // ...and in a zonal back four nobody steps out of the line to follow a man into midfield: the
                    // midfield picks him up. (Man-markers do follow their man - that is the point of man-marking.)
                    float stepOut = 2.5f + 5f * (1f - chem);
                    if (p.PlayRole == PlayerRole.Stopper) stepOut += 4f;        // he follows his man into midfield to win it early
                    else if (p.PlayRole == PlayerRole.CoverDefender) stepOut = 0.5f; // he never leaves the line
                    if (team.Tactics.Marking == MarkingStyle.Zonal && trusts && team.Forward(target) > line + stepOut)
                        target = team.ToWorld(line + stepOut, team.Lateral(target));
                }

                // Recovery runs: players caught ahead of the ball sprint back.
                // How hard he works back for his team-mates is his teamwork.
                if (team.Forward(p.Pos) > ballF + 5f && p.Slot.Line != Line.Attack) urgency = 0.78f + 0.2f * MathUtil.A(p.Attr.Teamwork);
            }

            // Space: attackers in the last third do not stand on top of each other - two men in one spot mark themselves
            // (one defender covers both) and leave a hole elsewhere. He moves off the team-mate who is already there.
            if (inPossession && p.Task == PlayerTask.Shape && team.Forward(target) > 24f)
            {
                foreach (var q in team.Players)
                {
                    if (q == p || q.SentOff || q.IsGoalkeeper || q == Ball.Owner) continue;
                    Vec2 d = target - q.Target;
                    float dd = d.Length;
                    if (dd < 4.5f && (q.Task != PlayerTask.Shape || q.Index < p.Index))
                    {
                        Vec2 away = dd > 0.1f ? d / dd : team.DirToWorld(new Vec2(0f, p.Index % 2 == 0 ? 1f : -1f));
                        target += away * (4.5f - dd);
                        if (CollectDiagnostics) Count("box.spaceOut");
                    }
                }
            }

            float dist = Vec2.Distance(p.Pos, target);
            // The back line out of possession moves as a unit with the line (steps up and drops together) instead of
            // strolling the last couple of metres - the better the understanding, the sharper.
            bool lineDefender = !inPossession && p.Slot.Line == Line.Defence && (p.Task == PlayerTask.Shape || p.Task == PlayerTask.Mark);
            if (lineDefender && MathF.Abs(team.Forward(target) - team.Forward(p.Pos)) > 0.8f)
                urgency = MathF.Max(urgency, 0.55f + 0.25f * team.DefensiveChemistry);
            if (dist > 12f) urgency = MathF.Max(urgency, 0.8f);
            // (a marker keeps pace with his man even when close to him)
            else if (dist < 2.5f && !lineDefender && !(p.Task == PlayerTask.Mark && p.MarkTarget != null && p.MarkTarget.Vel.Length > 2f)) urgency = MathF.Min(urgency, 0.35f);
            p.Target = target;
            p.Urgency = urgency;
        }

        /// <summary>How readily the player's role makes runs in behind (1 = as a forward does).</summary>
        private static float RoleRunFactor(PlayerAgent p, float carrierF)
        {
            switch (p.PlayRole)
            {
                case PlayerRole.ShadowStriker: return 1.3f;
                case PlayerRole.BoxToBoxDefensive: return carrierF > 0f ? 0.9f : 0.3f;
                case PlayerRole.Carrier: return carrierF > 5f ? 0.6f : 0f;
                case PlayerRole.AnchorMan: case PlayerRole.DeepLyingPlaymaker: case PlayerRole.Destroyer: case PlayerRole.TempoSetter: case PlayerRole.Regista: return 0f;
                case PlayerRole.Trequartista: return carrierF > 10f ? 1.15f : 0.4f; // the late run into the box
                case PlayerRole.TargetForward: return 0.6f;
                case PlayerRole.FalseNine: return 0.4f;
                case PlayerRole.SecondStriker: return 0.8f;
                case PlayerRole.ClassicWinger: return 1.2f;
            }
            if (p.Slot.IsCentralMidfielder) return carrierF > 4f ? 1.4f : 0f; // (late arrivals from midfield into the box: shots and goals from midfield)
            return 1f;
        }

        private static bool FalseNineOn(TeamAgent team)
        {
            foreach (var q in team.Players) if (!q.SentOff && q.PlayRole == PlayerRole.FalseNine) return true;
            return false;
        }

        private static bool IsRunner(PlayerAgent p)
        {
            if (p.Has(PlayerTrait.HoldsPosition)) return false;
            var slot = p.Slot;
            return slot.Line == Line.Attack || slot.IsWideMidfielder || slot.IsCentralMidfielder || (p.Has(PlayerTrait.RunsBehind) && !p.IsGoalkeeper && slot.Line != Line.Defence);
        }

        /// <summary>Pressure on a player: 0 (free) .. 1 (closed down by several opponents).</summary>
        private float Pressure(PlayerAgent p, out float nearest)
        {
            nearest = float.PositiveInfinity;
            float pressure = 0f;
            foreach (var o in p.Team.Opponent.Players)
            {
                if (o.SentOff) continue;
                float d = Vec2.Distance(o.Pos, p.Pos);
                if (d < nearest) nearest = d;
                if (d < 6f)
                {
                    float w = 1f - d / 6f;
                    pressure += w * w * 1.3f;
                }
            }
            return MathUtil.Clamp01(pressure);
        }
    }
}

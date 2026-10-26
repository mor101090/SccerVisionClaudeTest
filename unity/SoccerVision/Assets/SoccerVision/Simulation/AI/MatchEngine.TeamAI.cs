using System;
using System.Collections.Generic;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// Team behaviour: formation block that slides with the ball, defensive line, pressing (never more than two
    /// players on the ball, per the design document), supporting players, runs in behind and marking.
    /// </summary>
    public sealed partial class MatchEngine
    {
        // ---- Team shape ---------------------------------------------------------------------------------

        private void UpdateTeamShape(TeamAgent team)
        {
            var tac = team.Tactics;
            bool inPossession = PossessionTeam == team;
            float ballF = team.Forward(Ball.Pos);
            float ballL = team.Lateral(Ball.Pos);

            float lineTarget;
            if (inPossession)
            {
                lineTarget = ballF * 0.62f - 17f + 10f * (tac.Mentality - 0.5f) + 4f * (tac.DefensiveLine - 0.5f);
                lineTarget = MathUtil.Clamp(lineTarget, -41f, 3f);
            }
            else
            {
                lineTarget = ballF * 0.45f - 22f + 12f * (tac.DefensiveLine - 0.5f);
                lineTarget = MathUtil.Clamp(lineTarget, -42f, -7f + 12f * (tac.DefensiveLine - 0.5f));
                // Stay goal-side of the ball.
                lineTarget = MathF.Min(lineTarget, ballF - 2f);
                // Drop off when the ball carrier has time to play a ball over the top.
                if (Ball.Owner != null && Ball.Owner.Team != team && !Ball.InHands)
                {
                    float pressure = Pressure(Ball.Owner, out _);
                    if (pressure < 0.3f && ballF < lineTarget + 40f) lineTarget -= 7f * (1f - 0.6f * tac.DefensiveLine);
                }
                // A ball in the air over the line: everybody drops.
                if (Ball.Owner == null && Ball.LastTouchTeam != team && Ball.Z > 2.5f) lineTarget -= 6f;
            }
            lineTarget = MathF.Max(lineTarget, -47.5f);
            // The line moves like a unit: rate limited (steps up / drops at ~4.5 m/s).
            team.LineForward = MathUtil.MoveTowards(team.LineForward, lineTarget, 4.5f * 0.2f);

            float offsideLine = MathF.Max(MathF.Max(SecondLastDefenderForward(team), ballF), 0f);

            foreach (var p in team.Players)
            {
                if (p.SentOff || p.IsGoalkeeper) continue;
                p.ShapeTarget = ComputeShapeTarget(p, team, inPossession, ballF, ballL, offsideLine);
            }

            if (inPossession) AssignSupport(team);
            else team.Supporters.Clear();
            AssignMarking(team, inPossession, ballF);
        }

        private readonly List<PlayerAgent> _markPool = new List<PlayerAgent>(11);
        private readonly List<PlayerAgent> _threats = new List<PlayerAgent>(11);

        /// <summary>
        /// One marker per attacker (no two defenders on the same man): the most dangerous attackers in our half
        /// are picked up first by the nearest free defender/midfielder whose zone they are in.
        /// </summary>
        private void AssignMarking(TeamAgent team, bool inPossession, float ballF)
        {
            foreach (var p in team.Players) p.MarkTarget = null;
            if (inPossession || ballF > 12f) return;
            _threats.Clear();
            foreach (var o in team.Opponent.Players)
            {
                if (o.SentOff || o.IsGoalkeeper || o == Ball.Owner) continue;
                if (team.Forward(o.Pos) < 5f) _threats.Add(o);
            }
            Vec2 goal = team.OwnGoal;
            _threats.Sort((a, b) => Vec2.DistanceSq(a.Pos, goal).CompareTo(Vec2.DistanceSq(b.Pos, goal)));
            _markPool.Clear();
            foreach (var p in team.Players)
            {
                if (p.SentOff || p.IsGoalkeeper || p == team.Presser || p == team.SecondPresser) continue;
                if (p.Slot.Line == Line.Attack) continue;
                _markPool.Add(p);
            }
            foreach (var a in _threats)
            {
                PlayerAgent best = null;
                float bestD = float.PositiveInfinity;
                float reach = Vec2.Distance(a.Pos, goal) < 25f ? 22f : 15f;
                foreach (var d in _markPool)
                {
                    float dist = Vec2.Distance(d.ShapeTarget, a.Pos) + (d.Slot.Line == Line.Defence ? 0f : 3f);
                    if (dist < bestD && dist < reach) { bestD = dist; best = d; }
                }
                if (best == null) continue;
                best.MarkTarget = a;
                _markPool.Remove(best);
                if (_markPool.Count == 0) break;
            }
        }

        private Vec2 ComputeShapeTarget(PlayerAgent p, TeamAgent team, bool inPossession, float ballF, float ballL, float offsideLine)
        {
            var tac = team.Tactics;
            var slot = p.Slot;
            float line = team.LineForward;
            float blockLength = inPossession ? 34f + 10f * tac.Mentality + 5f * tac.Tempo : 26f + 5f * tac.DefensiveLine;
            float widthScale = inPossession ? 1.12f + 0.3f * (tac.Width - 0.5f) : 0.74f + 0.12f * (tac.Width - 0.5f);
            float shift = inPossession ? 0.2f : 0.36f;

            float f = line + slot.Depth * blockLength;
            float l = slot.BaseLateral * widthScale + ballL * shift;

            if (slot.Line == Line.Defence)
            {
                if (inPossession && slot.IsFullBack)
                {
                    // Fullbacks overlap the winger on the ball side (design doc), and push up anyway.
                    bool ballSide = MathF.Sign(ballL) == MathF.Sign(slot.BaseLateral) && MathF.Abs(ballL) > 6f;
                    f += 4f + (ballSide && ballF > -5f ? 12f * (0.5f + tac.Mentality) : 0f);
                    l = MathF.Sign(slot.BaseLateral) * (26f + 4f * tac.Width);
                }
                if (inPossession && slot.IsCentreBack) f = MathF.Min(f, 0f); // CBs: no further than halfway
                if (!inPossession && slot.IsFullBack) f += 1f;
            }
            else if (slot.Line == Line.Midfield)
            {
                if (inPossession && slot.IsWideMidfielder)
                {
                    f += 6f;
                    l = MathF.Sign(slot.BaseLateral) * (29f + 3.5f * tac.Width);
                    // Inverted winger (strong foot on the inside) tucks in when the ball is in the final third.
                    bool inverted = (slot.BaseLateral > 0f) == (p.Attr.StrongFoot == Foot.Right);
                    if (inverted && ballF > 15f) l *= 0.72f;
                }
                if (!inPossession && slot.IsWideMidfielder) f -= 1.5f;
                // Out of possession the midfield stays goal-side of the ball (screens the back four).
                if (!inPossession) f = MathF.Min(f, MathF.Max(ballF + 2.5f, line + 4f));
            }
            else if (slot.Line == Line.Attack)
            {
                if (inPossession)
                {
                    f = MathF.Max(f, ballF - 4f);
                    float hug = 1.1f * MathF.Sin(SimTime * 0.9f + p.NoisePhaseA * 3f) * (1.3f - MathUtil.A(p.Attr.OffTheBall));
                    f = MathF.Min(f, offsideLine - 0.25f + hug);
                    l = slot.BaseLateral * 1.15f + ballL * 0.25f;
                }
                else
                {
                    // Strikers stay higher to start counters and screen the opponent's build-up.
                    f = MathF.Max(f, -14f);
                    f = MathF.Max(f, ballF - 14f);
                    l = slot.BaseLateral * 0.9f + ballL * 0.3f;
                }
            }

            // Box presence when a cross is on (ball wide in the final third).
            if (inPossession && ballF > 24f && MathF.Abs(ballL) > 12f && Ball.Owner != null && Ball.Owner.Team == team && Ball.Owner != p)
            {
                float side = MathF.Sign(ballL);
                if (slot.Line == Line.Attack)
                {
                    bool near = MathF.Sign(slot.BaseLateral) == side;
                    f = near ? 46.5f : 43f;
                    l = near ? side * 3f : -side * 2f;
                    f = MathF.Min(f, offsideLine - 0.6f);
                }
                else if (slot.IsWideMidfielder && MathF.Sign(slot.BaseLateral) != side)
                {
                    f = MathF.Min(46f, offsideLine - 0.6f);
                    l = -side * 5f;
                }
                else if (slot.IsCentralMidfielder && MathF.Sign(slot.BaseLateral) == side)
                {
                    f = 34f;
                    l = side * 4f;
                }
            }

            // Slow organic wobble so lines don't look robotic (+/- ~1.5 m).
            f += 1.4f * MathF.Sin(SimTime * 0.23f + p.NoisePhaseA);
            l += 1.6f * MathF.Sin(SimTime * 0.19f + p.NoisePhaseB);

            f = MathUtil.Clamp(f, -50.5f, 50f);
            l = MathUtil.Clamp(l, -32.5f, 32.5f);
            return team.ToWorld(f, l);
        }

        // ---- Supporting players -----------------------------------------------------------------------

        private void AssignSupport(TeamAgent team)
        {
            team.Supporters.Clear();
            var carrier = Ball.Owner != null && Ball.Owner.Team == team ? Ball.Owner : null;
            Vec2 focus = carrier != null ? carrier.Pos : Ball.Pos;
            PlayerAgent first = null, second = null;
            float d1 = float.PositiveInfinity, d2 = float.PositiveInfinity;
            foreach (var p in team.Players)
            {
                if (p.SentOff || p.IsGoalkeeper || p == carrier || p == Ball.IntendedReceiver) continue;
                float d = Vec2.DistanceSq(p.Pos, focus);
                if (d < d1) { second = first; d2 = d1; first = p; d1 = d; }
                else if (d < d2) { second = p; d2 = d; }
            }
            if (first != null) team.Supporters.Add(first);
            if (second != null) team.Supporters.Add(second);
        }

        /// <summary>
        /// Best support spot near the carrier (after Buckland's "Simple Soccer" support-spot calculator):
        /// safe passing lane, good distance, space, threat (xT) and onside.
        /// </summary>
        private Vec2 BestSupportSpot(PlayerAgent supporter, PlayerAgent carrier, Vec2 avoid)
        {
            var team = supporter.Team;
            Vec2 cLocal = team.ToLocal(carrier.Pos);
            float offsideLine = MathF.Max(MathF.Max(SecondLastDefenderForward(team), team.Forward(Ball.Pos)), 0f);
            Vec2 best = supporter.ShapeTarget;
            float bestScore = float.NegativeInfinity;
            Vec2 shapeLocal = team.ToLocal(supporter.ShapeTarget);
            for (int ring = 0; ring < 2; ring++)
            {
                float radius = ring == 0 ? 11f : 18f;
                for (int k = 0; k < 8; k++)
                {
                    float ang = -1.35f + k * (2.7f / 7f); // forward-facing fan, local frame
                    Vec2 local = cLocal + new Vec2(MathF.Cos(ang), MathF.Sin(ang)) * radius;
                    // Bias the fan towards the supporter's own zone.
                    local = Vec2.Lerp(local, shapeLocal, 0.25f);
                    if (local.X > offsideLine - 0.8f) local = new Vec2(offsideLine - 0.8f, local.Y);
                    if (MathF.Abs(local.Y) > 32f || local.X < -50f || local.X > 50f) continue;
                    Vec2 world = team.ToWorld(local.X, local.Y);

                    float nearestOpp = NearestOpponentDistance(team, world);
                    float laneClear = LaneClearance(team, carrier.Pos, world);
                    float value = ExpectedThreat.At(local.X, local.Y);
                    float distFromShape = Vec2.Distance(local, shapeLocal);
                    float score = value * 60f
                                  + MathUtil.Clamp(laneClear, 0f, 6f) * 0.35f
                                  + MathUtil.Clamp(nearestOpp, 0f, 10f) * 0.2f
                                  - MathF.Max(0f, distFromShape - 10f) * 0.12f
                                  - (Vec2.Distance(world, avoid) < 6f ? 2f : 0f);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = world;
                    }
                }
            }
            return best;
        }

        private float NearestOpponentDistance(TeamAgent team, Vec2 point)
        {
            float best = float.PositiveInfinity;
            foreach (var o in team.Opponent.Players)
            {
                if (o.SentOff) continue;
                float d = Vec2.Distance(o.Pos, point);
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>Smallest distance from an opponent to the segment from-&gt;to (excluding its very ends).</summary>
        private float LaneClearance(TeamAgent team, Vec2 from, Vec2 to)
        {
            float best = float.PositiveInfinity;
            foreach (var o in team.Opponent.Players)
            {
                if (o.SentOff) continue;
                float d = Vec2.DistanceToSegment(o.Pos, from, to, out float t);
                if (t < 0.05f || t > 0.97f) continue;
                if (d < best) best = d;
            }
            return best;
        }

        // ---- Ball duties: chaser, presser, receivers ---------------------------------------------------

        private void AssignBallDuties()
        {
            foreach (var t in Teams)
            {
                t.Chaser = null;
                if (t.Presser != null && (t.Presser.SentOff || Ball.Owner == null)) t.Presser = null;
                t.SecondPresser = null;
            }

            if (Ball.Owner != null)
            {
                var carrier = Ball.Owner;
                var def = carrier.Team.Opponent;
                if (Ball.InHands)
                {
                    def.Presser = null;
                    return;
                }
                PlayerAgent best = null, second = null;
                float t1 = float.PositiveInfinity, t2 = float.PositiveInfinity;
                Vec2 goalSide = carrier.Pos + (def.OwnGoal - carrier.Pos).Normalized * 1.2f;
                foreach (var d in def.Players)
                {
                    if (d.SentOff || d.IsGoalkeeper) continue;
                    float t = d.TimeToReach(goalSide);
                    if (d == def.Presser) t *= 0.8f; // hysteresis: keep the same presser
                    if (t < t1) { second = best; t2 = t1; best = d; t1 = t; }
                    else if (t < t2) { second = d; t2 = t; }
                }
                var tac = def.Tactics;
                // Pressing trigger: how far from his zone a player will engage the carrier.
                float engage = 8f + 14f * tac.PressingIntensity;
                if (best != null)
                {
                    float fromZone = Vec2.Distance(best.ShapeTarget, carrier.Pos);
                    bool inOwnThird = def.Forward(carrier.Pos) < -18f;
                    if (fromZone < engage || inOwnThird || t1 < 1.2f) def.Presser = best;
                    else def.Presser = null;
                }
                if (def.Presser != null && second != null && tac.PressingIntensity > 0.3f && t2 < 1.4f + 1.6f * tac.PressingIntensity)
                {
                    def.SecondPresser = second; // never more than two on the ball
                }
                return;
            }

            // Free ball: each team sends its fastest interceptor; the intended receiver always goes.
            foreach (var t in Teams)
            {
                PlayerAgent best = null;
                float bestT = float.PositiveInfinity;
                foreach (var p in t.Players)
                {
                    if (p.SentOff) continue;
                    if (p.IsGoalkeeper && !GoalkeeperMayChase(p)) continue;
                    UpdateIntercept(p);
                    float tt = p.InterceptTime;
                    if (p.IsGoalkeeper) tt -= 0.3f; // keepers take responsibility in their area
                    if (tt < bestT) { bestT = tt; best = p; }
                }
                var receiver = Ball.IntendedReceiver;
                if (receiver != null && receiver.Team == t && !receiver.SentOff)
                {
                    UpdateIntercept(receiver);
                    t.Chaser = receiver;
                }
                else
                {
                    t.Chaser = best;
                }
            }
        }

        private bool GoalkeeperMayChase(PlayerAgent gk)
        {
            UpdateIntercept(gk);
            return Pitch.InPenaltyArea(gk.InterceptPoint, gk.Team.OwnGoalSign);
        }

        /// <summary>Earliest point on the predicted ball path this player can reach (pitch-control style).</summary>
        private void UpdateIntercept(PlayerAgent p)
        {
            if (p.InterceptVersion == _trajectory.Version && p.InterceptRecalc > 0f) return;
            // Nobody reads a new ball path instantly: players who did not expect it (opponents of the
            // kicker, everyone after a deflection) keep their previous plan for their reaction time.
            bool expected = _ballChangeExpectedBy == p.Team || Ball.IntendedReceiver == p;
            if (!expected && SimTime - _ballChangedAt < p.ReactionTime && p.InterceptVersion >= 0) return;
            p.InterceptVersion = _trajectory.Version;
            p.InterceptRecalc = 0.12f;
            float elapsed = _trajectoryAge;
            float reach = p.IsGoalkeeper ? p.GoalkeeperReach : p.HeadReach;
            int count = _trajectory.Count;
            float lastT = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = _trajectory.TimeAt(i) - elapsed;
                if (t < 0f) continue;
                Vec2 pos = _trajectory.PositionAt(i);
                if (!Pitch.InsideField(pos, 0.4f))
                {
                    // Ball is going out: run to where it leaves the pitch.
                    p.InterceptPoint = Pitch.ClampToField(pos, -0.3f);
                    p.InterceptTime = MathF.Max(t, p.TimeToReach(p.InterceptPoint)) + 0.5f;
                    return;
                }
                lastT = t;
                float z = _trajectory.HeightAt(i);
                if (z > reach) continue;
                float tp = p.TimeToReach(pos);
                if (tp <= t + 0.05f)
                {
                    p.InterceptPoint = pos;
                    p.InterceptTime = t;
                    return;
                }
            }
            Vec2 final = Pitch.ClampToField(_trajectory.FinalPosition, -0.3f);
            p.InterceptPoint = final;
            p.InterceptTime = MathF.Max(lastT, p.TimeToReach(final));
        }

        // ---- Per-player thinking ----------------------------------------------------------------------

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

            if (free && (team.Chaser == p || Ball.IntendedReceiver == p))
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
                p.Target = carrier.Pos + carrier.Vel * 0.35f + toGoal * standOff + lateral;
                // Near goal the second man blocks the dribbling lane straight in front of the carrier.
                if (team.SecondPresser == p && nearGoal) p.Target = carrier.Pos + toGoal * 2.6f + carrier.Vel * 0.25f;
                p.Urgency = Vec2.Distance(p.Pos, carrier.Pos) > 4f ? 0.95f : 0.75f;
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

                // Runs in behind (strikers / wide players), timed on the last defender's shoulder.
                if (carrier != null && IsRunner(p))
                {
                    float carrierF = team.Forward(carrier.Pos);
                    float pressure = Pressure(carrier, out _);
                    if (p.RunTimer <= 0f && p.RunCooldown <= 0f && carrierF > -25f && pressure < 0.6f)
                    {
                        float willing = 0.2f + 0.4f * MathUtil.A(p.Attr.OffTheBall) + 0.25f * team.Tactics.Mentality;
                        if (_rng.Chance(willing * 0.35f))
                        {
                            p.RunTimer = _rng.Range(2.2f, 3.6f);
                            // Imperfect timing: low off-the-ball players drift offside more often.
                            float timingError = _rng.Gaussian(0.2f, 1.7f * (1.4f - MathUtil.A(p.Attr.OffTheBall)));
                            float lateral = MathUtil.Clamp(team.Lateral(p.Pos) * 0.8f + _rng.Range(-6f, 6f), -26f, 26f);
                            p.RunPlan = new Vec2(timingError, lateral); // (overshoot beyond the line, lateral) in local space
                        }
                        p.RunCooldown = _rng.Range(4f, 9f);
                    }
                    if (p.RunTimer > 0f)
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
                // Out of possession: man-oriented zonal marking, goal-side of the assigned attacker.
                float ballF = team.Forward(Ball.Pos);
                var mark = p.MarkTarget;
                if (mark != null && !mark.SentOff && ballF < 12f)
                {
                    bool inBox = Pitch.InPenaltyArea(mark.Pos, team.OwnGoalSign);
                    Vec2 lead = mark.Pos + mark.Vel * 0.3f;
                    Vec2 goalSide = lead + (team.OwnGoal - lead).Normalized * (inBox ? 1.0f : 1.7f);
                    float weight = p.Slot.Line == Line.Defence ? 0.6f : 0.45f;
                    if (ballF < -20f) weight += 0.25f;
                    if (inBox) weight += 0.25f;
                    weight *= 0.65f + 0.35f * MathUtil.A(p.Attr.Marking);
                    target = Vec2.Lerp(p.ShapeTarget, goalSide, MathUtil.Clamp01(weight));
                    p.Task = PlayerTask.Mark;
                    if (inBox || Vec2.Distance(p.Pos, goalSide) > 4f) urgency = 0.85f;
                }
                // Recovery runs: players caught ahead of the ball sprint back.
                if (team.Forward(p.Pos) > ballF + 5f && p.Slot.Line != Line.Attack) urgency = 0.9f;
            }

            float dist = Vec2.Distance(p.Pos, target);
            if (dist > 12f) urgency = MathF.Max(urgency, 0.8f);
            else if (dist < 2.5f) urgency = MathF.Min(urgency, 0.35f);
            p.Target = target;
            p.Urgency = urgency;
        }

        private static bool IsRunner(PlayerAgent p)
        {
            var slot = p.Slot;
            return slot.Line == Line.Attack || slot.IsWideMidfielder;
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

        // ---- Locomotion -------------------------------------------------------------------------------

        private void MovePlayer(PlayerAgent p, float dt)
        {
            if (p.SentOff)
            {
                // Walk off the pitch over the nearest touchline.
                p.Target = new Vec2(p.Pos.X, (Pitch.HalfWidth + 6f) * (p.Pos.Y >= 0f ? 1f : -1f));
                p.Urgency = 0.25f;
            }

            Vec2 toTarget = p.Target - p.Pos;
            float dist = toTarget.Length;
            float u = MathUtil.Clamp01(p.Urgency);
            float vmax = p.MaxSpeed;
            float accelBoost = 1f;
            if (p.DiveTimer > 0f)
            {
                // Explosive dive: the keeper launches sideways faster than he can run.
                vmax *= 1.3f + 0.15f * MathUtil.A(p.Attr.Reflexes);
                accelBoost = 2.6f;
            }
            const float walk = 1.5f, jog = 3.9f;
            float cap = u < 0.5f ? MathUtil.Lerp(walk, jog, u / 0.5f) : MathUtil.Lerp(jog, vmax, (u - 0.5f) / 0.5f);
            if (Ball.Owner == p && !Ball.InHands)
            {
                cap = MathF.Min(cap, p.DribbleSpeed);
                // Physical pressure: defenders tight on the carrier slow him down (shoulder-to-shoulder).
                int tight = 0;
                foreach (var o in p.Team.Opponent.Players)
                {
                    if (!o.SentOff && o.StunTimer <= 0f && Vec2.DistanceSq(o.Pos, p.Pos) < 1.3f * 1.3f) tight++;
                }
                if (tight > 0) cap *= MathF.Max(0.55f, 1f - 0.22f * tight * (1.15f - 0.3f * MathUtil.A(p.Attr.PhysicalPower)));
            }
            if (p.HasPendingPlan) cap = MathF.Min(cap, 3.5f);
            if (p.StunTimer > 0f) cap = MathF.Min(cap, 1.5f);

            float decel = p.MaxDecel;
            float desiredSpeed = dist < 0.25f ? 0f : MathF.Min(cap, MathF.Sqrt(2f * decel * 0.55f * dist));
            Vec2 desired = dist > 1e-4f ? toTarget / dist * desiredSpeed : Vec2.Zero;

            // Separation keeps players from overlapping (teammates more, opponents less so duels still happen).
            if (!p.SentOff && Phase != MatchPhase.HalfTimeBreak)
            {
                Vec2 push = Vec2.Zero;
                foreach (var q in AllPlayers)
                {
                    if (q == p || q.SentOff) continue;
                    float radius = q.Team == p.Team ? 1.4f : 0.85f;
                    Vec2 d = p.Pos - q.Pos;
                    float dl = d.LengthSq;
                    if (dl < radius * radius && dl > 1e-6f)
                    {
                        float len = MathF.Sqrt(dl);
                        push += d / len * ((radius - len) / radius) * 3f;
                    }
                }
                desired += push;
            }

            Vec2 dv = desired - p.Vel;
            bool accelerating = Vec2.Dot(desired, p.Vel) >= p.Vel.LengthSq * 0.95f;
            float maxDv = (accelerating ? p.MaxAccel : decel) * accelBoost * dt;
            p.Vel += dv.ClampLength(maxDv);
            float speed = p.Vel.Length;
            if (speed > vmax) { p.Vel = p.Vel / speed * vmax; speed = vmax; }
            p.Pos += p.Vel * dt;
            p.Pos = Pitch.ClampToField(p.Pos, p.SentOff ? 8f : 3.5f);
            p.DistanceCovered += speed * dt;

            // Facing: run direction when moving, otherwise the ball. A pending kick turns the body first.
            float targetFacing;
            float turnRate;
            if (p.HasPendingPlan)
            {
                targetFacing = p.PendingPlan.Heading;
                turnRate = 14f;
            }
            else if (speed > 1.2f && !(p.FaceBall && speed < 2.5f))
            {
                targetFacing = p.Vel.Angle;
                turnRate = 9f;
            }
            else if (Ball.Owner != p)
            {
                targetFacing = (Ball.Pos - p.Pos).Angle;
                turnRate = 6f;
            }
            else
            {
                targetFacing = p.Facing;
                turnRate = 6f;
            }
            p.Facing = MathUtil.MoveTowardsAngle(p.Facing, targetFacing, turnRate * dt);

            // Jump (visual elevation for headers / goalkeeper takes).
            if (p.JumpZ > 0f || p.JumpVz > 0f)
            {
                p.JumpZ += p.JumpVz * dt;
                p.JumpVz -= Config.Gravity * dt;
                if (p.JumpZ <= 0f) { p.JumpZ = 0f; p.JumpVz = 0f; }
            }

            UpdateEnergy(p, speed, dt);
        }

        private void UpdateEnergy(PlayerAgent p, float speed, float dt)
        {
            float clockDt = dt * Config.ClockSecondsPerSimSecond;
            float intensity = speed / MathF.Max(1f, p.MaxSpeed);
            float staminaFactor = 1.35f - 0.7f * MathUtil.A(p.Attr.Stamina);
            float drain = (0.00006f + 0.00085f * intensity * intensity * intensity) * staminaFactor;
            if (p.IsGoalkeeper) drain *= 0.5f;
            float recover = speed < 2.2f ? 0.00004f : 0f;
            p.Energy = MathUtil.Clamp(p.Energy - (drain - recover) * clockDt, 0.25f, 1f);
        }
    }
}

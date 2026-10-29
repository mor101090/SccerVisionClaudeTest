using System;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// Goalkeeper logic from the design document: stays around the goal area, advances slightly when the team
    /// attacks, charges out only 1v1 or to claim balls, dives/parries shots, and distributes long or short.
    /// </summary>
    public sealed partial class MatchEngine
    {
        private void ThinkGoalkeeper(PlayerAgent gk, float dt)
        {
            var team = gk.Team;
            Vec2 goal = team.OwnGoal;
            int goalSign = team.OwnGoalSign;
            gk.Task = PlayerTask.Goalkeeping;

            // 0) Penalty: the keeper has committed to a side.
            if (SimTime < _penaltyDiveUntil && Ball.Shooter != null && Ball.Shooter.Team != team)
            {
                gk.Target = _penaltyDiveTarget;
                gk.Urgency = 1f;
                return;
            }

            // 1) A ball (shot) heading for goal: get across, dive if needed.
            if (Ball.Owner == null && BallThreatensGoal(team, out Vec2 savePoint, out float timeToLine))
            {
                Vec2 target = savePoint;
                float gap = Vec2.Distance(gk.Pos, target);
                float lateralSpeedNeeded = gap / MathF.Max(0.05f, timeToLine);
                if (gk.DiveTimer <= 0f && gap > 0.9f && (timeToLine < 0.8f || lateralSpeedNeeded > 4f))
                {
                    gk.DiveTimer = 0.7f;
                    if (Ball.Z > 1.2f || gap > 1.5f) StartJump(gk, 0.25f);
                }
                gk.Target = target;
                gk.Urgency = 1f;
                return;
            }

            // 2) Loose ball / cross the keeper is best placed to take.
            if (Ball.Owner == null && team.Chaser == gk)
            {
                UpdateIntercept(gk);
                gk.Target = gk.InterceptPoint;
                gk.Urgency = 1f;
                return;
            }

            var carrier = Ball.Owner;
            // 3) One-on-one: no defender between the carrier and goal -> narrow the angle / charge.
            if (carrier != null && carrier.Team != team && !Ball.InHands)
            {
                float d = Vec2.Distance(carrier.Pos, goal);
                if (d < 24f && NoDefenderBetween(carrier, team))
                {
                    float rush = MathUtil.A(gk.Attr.OneOnOne);
                    float outDist = MathUtil.Clamp(d * (0.35f + 0.25f * rush), 1.5f, MathF.Min(d - 1.2f, 13f));
                    gk.Target = goal + (carrier.Pos - goal).Normalized * outDist;
                    gk.Urgency = 0.95f;
                    return;
                }
            }

            // 4) Positioning on the line between ball and goal centre.
            Vec2 ballPos = Ball.Pos;
            Vec2 toBall = ballPos - goal;
            float dist = toBall.Length;
            float depth = MathUtil.Clamp(1.0f + 0.07f * dist, 1.0f, 6f);
            if (PossessionTeam == team && team.Forward(ballPos) > -5f)
            {
                // Sweeper position behind the defensive line, ready for counters.
                depth = MathUtil.Clamp(8f + 0.12f * (team.Forward(ballPos) + 5f) + 4f * team.Eff.DefensiveLine, 8f, 19f);
                depth = MathF.Min(depth, team.LineForward + Pitch.HalfLength - 6f);
                depth = MathF.Max(depth, 2f);
            }
            Vec2 dir = dist > 0.1f ? toBall / dist : team.DirToWorld(Vec2.UnitX);
            Vec2 pos = goal + dir * depth;
            float f = MathF.Max(team.Forward(pos), -Pitch.HalfLength + 0.6f);
            float l = MathUtil.Clamp(team.Lateral(pos), -Pitch.GoalHalfWidth - 1.5f, Pitch.GoalHalfWidth + 1.5f);
            if (depth > 7f) l = MathUtil.Clamp(team.Lateral(pos), -14f, 14f);
            gk.Target = team.ToWorld(f, l);
            gk.Urgency = Vec2.Distance(gk.Pos, gk.Target) > 6f ? 0.85f : 0.55f;
        }

        /// <summary>True if the free ball's predicted path crosses our goal line between (or near) the posts.</summary>
        private bool BallThreatensGoal(TeamAgent team, out Vec2 savePoint, out float timeToLine)
        {
            savePoint = team.OwnGoal;
            timeToLine = float.PositiveInfinity;
            int goalSign = team.OwnGoalSign;
            if (Ball.Velocity.X * goalSign <= 2f || Ball.M.Speed < 6f) return false;
            var gk = team.Goalkeeper;
            float gkDepth = gk != null ? MathF.Max(0.3f, Pitch.HalfLength - gk.Pos.X * goalSign) : 1f;
            float elapsed = _trajectoryAge;
            bool found = false;
            for (int i = 0; i < _trajectory.Count; i++)
            {
                float t = _trajectory.TimeAt(i) - elapsed;
                if (t < 0f) continue;
                Vec2 p = _trajectory.PositionAt(i);
                float depth = Pitch.HalfLength - p.X * goalSign;
                if (!found && depth <= gkDepth)
                {
                    savePoint = p;
                    found = true;
                }
                if (depth <= 0f)
                {
                    float z = _trajectory.HeightAt(i);
                    timeToLine = t;
                    bool onFrame = MathF.Abs(p.Y) < Pitch.GoalHalfWidth + 0.8f && z < Pitch.GoalHeight + 0.4f;
                    if (!found) savePoint = p;
                    // Stay on the pitch side of the line.
                    savePoint = new Vec2((Pitch.HalfLength - MathF.Min(gkDepth, MathF.Max(0.4f, gkDepth))) * goalSign, MathUtil.Clamp(savePoint.Y, -Pitch.GoalHalfWidth - 0.5f, Pitch.GoalHalfWidth + 0.5f));
                    return onFrame && t < 2.5f;
                }
            }
            return false;
        }

        private bool NoDefenderBetween(PlayerAgent carrier, TeamAgent defending)
        {
            Vec2 goal = defending.OwnGoal;
            foreach (var d in defending.Players)
            {
                if (d.SentOff || d.IsGoalkeeper) continue;
                float dist = Vec2.DistanceToSegment(d.Pos, carrier.Pos, goal, out float t);
                if (t > 0.05f && dist < 2.5f) return false;
            }
            return true;
        }

        /// <summary>Ball in the keeper's hands: walk to the edge of the area, let the team spread, then distribute.</summary>
        private void ThinkGoalkeeperHold(PlayerAgent gk, float dt)
        {
            var team = gk.Team;
            gk.Task = PlayerTask.GoalkeeperHold;
            if (gk.RunUpPhase == 0)
            {
                float f = MathUtil.Clamp(team.Forward(gk.Pos) + 1.5f, -51f, -Pitch.HalfLength + Pitch.PenaltyAreaDepth - 2f);
                float l = MathUtil.Clamp(team.Lateral(gk.Pos), -12f, 12f);
                gk.Target = team.ToWorld(f, l);
                gk.Urgency = 0.25f;
                gk.DecisionTimer -= dt;
                if (gk.DecisionTimer > 0f && _goalkeeperHoldTimer < 6f) return;
                // Wait for opponents to clear out of the way before releasing (they must drift away; see MovePlayer).
                if (_goalkeeperHoldTimer < 6.5f && NearestOpponentDistance(team, gk.Pos) < 6f)
                {
                    gk.DecisionTimer = 0.3f;
                    return;
                }
            }

            float tempo = team.Eff.Tempo;
            float kicking = MathUtil.A(gk.Attr.Kicking);

            // Short option: a team-mate in space nearby (roll/throw).
            PlayerAgent shortTarget = null;
            float bestShort = float.NegativeInfinity;
            foreach (var q in team.Players)
            {
                if (q.SentOff || q == gk) continue;
                float d = Vec2.Distance(q.Pos, gk.Pos);
                if (d < 8f || d > 34f) continue;
                float space = NearestOpponentDistance(team, q.Pos);
                float lane = LaneClearance(team, gk.Pos, q.Pos);
                if (space < 10f || lane < 4f) continue;
                float score = space * 0.3f + MathF.Min(lane, 8f) * 0.2f + team.Forward(q.Pos) * 0.03f;
                if (score > bestShort) { bestShort = score; shortTarget = q; }
            }

            float pShort = MathUtil.Clamp(0.3f + 0.5f * (1f - tempo) + (shortTarget != null ? 0.15f : -1f) - 0.2f * kicking, 0f, 0.85f);
            bool goShort = gk.RunUpPhase == 0 && shortTarget != null && _rng.Chance(pShort);

            // A long kick from the hands starts with a run-up: a few steps back, then forward and strike.
            if (!goShort && _goalkeeperHoldTimer < 5.5f)
            {
                Vec2 fwd = team.DirToWorld(Vec2.UnitX);
                if (gk.RunUpPhase == 0)
                {
                    gk.RunUpPhase = 1;
                    gk.RunUpFrom = gk.Pos;
                    gk.RunUpTimer = 0f;
                }
                gk.RunUpTimer += dt;
                if (gk.RunUpPhase == 1)
                {
                    Vec2 backSpot = gk.RunUpFrom - fwd * 3.2f;
                    gk.Target = Pitch.ClampToField(backSpot, 1f);
                    gk.Urgency = 0.35f;
                    if (Vec2.Distance(gk.Pos, gk.Target) < 0.6f || gk.RunUpTimer > 2.2f) { gk.RunUpPhase = 2; gk.RunUpTimer = 0f; }
                    gk.DecisionTimer = 0.1f;
                    return;
                }
                gk.Target = gk.RunUpFrom + fwd * 0.6f;
                gk.Urgency = 0.7f;
                gk.DecisionTimer = 0.1f;
                if (Vec2.Distance(gk.Pos, gk.RunUpFrom) > 1.2f && gk.RunUpTimer < 2f) return; // still running up
            }
            gk.RunUpPhase = 0;
            Ball.InHands = false;
            NoteKeeperRelease(gk);
            if (goShort)
            {
                Vec2 target = shortTarget.Pos + shortTarget.Vel * 0.4f;
                float d = Vec2.Distance(Ball.Pos, target);
                float v0 = Roll.KickSpeedFor(d, 6f);
                Vec2 dir = (target - Ball.Pos).Normalized.Rotate(_rng.Gaussian(0f, 0.03f));
                Vec2 from = Ball.Pos;
                KickBall(gk, new Vec2(from.X, from.Y), dir * v0, 0f, 0f, 0f, KickKind.GoalkeeperDistribution, shortTarget, target, false, false);
                Emit(MatchEventType.Pass, team, gk, shortTarget, from);
                return;
            }

            // Long kick towards the most advanced team-mates.
            PlayerAgent longTarget = null;
            float bestF = float.NegativeInfinity;
            foreach (var q in team.Players)
            {
                if (q.SentOff || q == gk) continue;
                float qf = team.Forward(q.Pos) + _rng.Range(0f, 8f);
                if (qf > bestF) { bestF = qf; longTarget = q; }
            }
            Vec2 aim = longTarget != null ? longTarget.Pos : team.ToWorld(5f, 0f);
            float distance = MathUtil.Clamp(Vec2.Distance(gk.Pos, aim), 30f, 30f + 35f * kicking);
            Vec2 kickTarget = gk.Pos + (aim - gk.Pos).Normalized * distance;
            kickTarget = Pitch.ClampToField(kickTarget, -3f);
            KickLofted(gk, kickTarget, 0.62f, KickKind.GoalkeeperDistribution, longTarget, 0f, kicking, false, 1.0f);
        }

        /// <summary>Keeper with the ball at his feet (back-pass or outside the area): safety first.</summary>
        private KickPlan ChooseGoalkeeperFeetAction(PlayerAgent gk)
        {
            var team = gk.Team;
            Vec2 pos = Ball.Pos;
            float pressure = Pressure(gk, out _);
            _options.Clear();
            _options.Add(new KickPlan
            {
                Action = BallAction.Hold,
                DribbleTarget = gk.Pos,
                Utility = ExpectedThreat.At(team, pos) * 0.9f - pressure * LossCost(team, pos),
            });
            EvaluatePasses(gk, pos, pressure);
            EvaluateClearance(gk, pos, MathF.Max(pressure, 0.5f));
            int best = 0;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < _options.Count; i++)
            {
                var o = _options[i];
                if (o.Action == BallAction.Pass && o.Receiver != null && o.Receiver.IsGoalkeeper) continue;
                float perceived = o.Utility + _rng.Gaussian(0f, 0.003f);
                if (perceived > bestScore) { bestScore = perceived; best = i; }
            }
            var plan = _options[best];
            if (plan.Action == BallAction.Hold && gk.HoldTime > 1.5f)
            {
                plan = _options[_options.Count - 1]; // clearance
            }
            return plan;
        }
    }
}

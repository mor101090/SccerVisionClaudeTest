using System;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// Referee and assistants (design doc: referee central, two linesmen on opposite touchlines moving with
    /// play). The referee runs the classic left diagonal; each assistant covers one half and stays in line
    /// with the second-last defender (or the ball when it is nearer the goal line).
    /// </summary>
    public sealed partial class MatchEngine
    {
        private void UpdateOfficials(float dt)
        {
            if (Phase == MatchPhase.HalfTimeBreak || Phase == MatchPhase.FullTime) return;
            Vec2 ball = Ball.Pos;

            // Referee: diagonal from (-35,-20) to (35,20), keeping 12-25 m from the ball.
            float rx = MathUtil.Clamp(ball.X - MathF.Sign(ball.X) * 7f, -38f, 38f);
            float ry = rx * (20f / 35f);
            Vec2 target = new Vec2(rx, ry);
            Vec2 toBall = ball - target;
            float d = toBall.Length;
            if (d > 25f) target += toBall.Normalized * (d - 25f);
            if (d < 12f && d > 0.01f) target -= toBall.Normalized * (12f - d);
            target = Pitch.ClampToField(target, -2f);
            // Kick-off (and walking back after a goal): the referee waits outside the centre circle, in the half of the team
            // that is not kicking off, on his diagonal - not on the centre spot next to the ball.
            TeamAgent kicking = Phase == MatchPhase.GoalCelebration ? _kickOffAfterGoal
                              : (Phase == MatchPhase.Stoppage && _restart != null && _restart.Type == RestartType.KickOff ? _restart.Team : null);
            if (kicking != null)
            {
                float side = kicking.Opponent.OwnGoalSign;
                target = new Vec2(11f * side, 13f * side);
                // Walk round the centre circle, not across it (the players are lining up there).
                Vec2 r = Referee.Pos;
                float clear = Pitch.CentreCircleRadius + 2f;
                if (Vec2.DistanceToSegment(Vec2.Zero, r, target, out _) < clear)
                {
                    if (r.Length < clear) target = (r.LengthSq > 0.01f ? r.Normalized : new Vec2(0f, side)) * (clear + 1f);
                    else
                    {
                        float a = r.Angle, b = target.Angle;
                        float step = MathF.Sign(MathUtil.AngleDiff(a, b)) * 1.2f;
                        target = Vec2.FromAngle(a + (step == 0f ? 1.2f : step)) * (clear + 1f);
                    }
                }
            }
            MoveOfficial(Referee, target, 7.2f, dt, ball);

            UpdateAssistant(Assistant1, dt, ball);
            UpdateAssistant(Assistant2, dt, ball);
        }

        private void UpdateAssistant(Official a, float dt, Vec2 ball)
        {
            // The team defending the goal in the assistant's half.
            int goalSign = a.CoverSign;
            var defending = TeamDefendingGoal(goalSign);
            var attacking = defending.Opponent;
            float lineF = SecondLastDefenderForward(attacking); // attacking frame
            float lineX = lineF * attacking.AttackDir;
            float x = lineX;
            if (ball.X * goalSign > lineX * goalSign) x = ball.X;
            // Stay in own half of the pitch (between the halfway line and the goal line).
            x = MathUtil.Clamp(x * goalSign, 0f, Pitch.HalfLength) * goalSign;
            if (a.HoldPosition && a.FlagTimer > 0f) x = a.HoldX; // flag up, standing in line with the offence
            else a.HoldPosition = false;
            var target = new Vec2(x, (Pitch.HalfWidth + 1.2f) * a.TouchlineSign);
            // A corner in his half: he stands back behind the goal line, a few metres from the corner flag, out of the
            // taker's way (not next to the ball).
            if (Phase == MatchPhase.Stoppage && _restart != null && _restart.Type == RestartType.CornerKick && MathF.Sign(_restart.Spot.X) == goalSign)
            {
                float ySide = MathF.Sign(_restart.Spot.Y);
                target = ySide == a.TouchlineSign
                    ? new Vec2((Pitch.HalfLength + 1.6f) * goalSign, (Pitch.HalfWidth - 4f) * ySide)       // near corner: behind the goal line
                    : new Vec2((Pitch.HalfLength - 2.5f) * goalSign, (Pitch.HalfWidth + 1.2f) * a.TouchlineSign); // far corner: own touchline
            }
            MoveOfficial(a, target, 7f, dt, ball);
        }

        private static void MoveOfficial(Official o, Vec2 target, float maxSpeed, float dt, Vec2 lookAt)
        {
            Vec2 to = target - o.Pos;
            float dist = to.Length;
            float speed = MathF.Min(maxSpeed, dist * 1.6f);
            Vec2 desired = dist > 0.05f ? to / dist * speed : Vec2.Zero;
            Vec2 dv = (desired - o.Vel).ClampLength(5f * dt);
            o.Vel += dv;
            o.Pos += o.Vel * dt;
            o.Facing = MathUtil.MoveTowardsAngle(o.Facing, (lookAt - o.Pos).Angle, 5f * dt);
        }
    }
}

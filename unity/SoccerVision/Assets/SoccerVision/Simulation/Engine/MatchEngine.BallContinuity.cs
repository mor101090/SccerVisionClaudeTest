using System;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// One ball at a time (polish round 7). When the ball goes out it is either the same ball that is played again - a throw-in where it
    /// stops within <see cref="ThrowInRetrieveDistance"/> beyond the touchline, near the spot: the thrower walks to it, picks it up and
    /// carries it to the spot - or a
    /// new ball from an attendant out of view: behind the goal line for a goal kick or a corner, beyond the touchline for a throw-in. The ball
    /// that went out is then only a picture (it touches nothing) until a ball boy collects it; it is gone as soon as the new ball comes near
    /// it. The match's ball never jumps on the field: it rolls in, or it is carried.
    /// </summary>
    public sealed partial class MatchEngine
    {
        /// <summary>Same ball, new attendants (false: every restart from out of play with a ball rolled in from beyond the touchline, as before).</summary>
        public static bool BallContinuity = true;
        /// <summary>A ball that stops no further than this beyond the touchline (m) is picked up by the thrower and thrown in: no new ball.</summary>
        public static float ThrowInRetrieveDistance = 1.5f;
        /// <summary>...and no further than this along the line from the spot (m): he walks it back there.</summary>
        public static float ThrowInRetrieveAlong = 8f;
        /// <summary>The fastest an attendant's ball rolls in (m/s): the further he is, the longer it takes.</summary>
        public static float NewBallRollSpeed = 8f;
        /// <summary>How far beyond the line the attendant stands (m) - out of the picture.</summary>
        public static float AttendantDistance = 6f;

        /// <summary>The throw-in is taken with the ball that went out: it rolls to a stop, the thrower picks it up.</summary>
        private bool _retrieveSameBall;
        /// <summary>The thrower has the ball in his hands and walks to the spot.</summary>
        private bool _retrieveCarried;

        /// <summary>Where a ball rolling out comes to rest (the same physics as the match ball; at most 6 s ahead).</summary>
        private Vec2 PredictRest(BallMotion m)
        {
            float dt = Config.FixedStep;
            for (int i = 0; i < (int)(6f / dt); i++)
            {
                BallPhysics.Step(ref m, Config, dt);
                if (m.Z <= Pitch.BallRadius + 0.01f && m.Vx * m.Vx + m.Vy * m.Vy < 0.01f) break;
            }
            return new Vec2(m.X, m.Y);
        }

        /// <summary>A throw-in with the same ball: it stops within reach of the spot (and on the grass by the line, not up in the stand).</summary>
        private bool CanRetrieveSameBall(RestartType type, Vec2 spot)
        {
            if (!BallContinuity || type != RestartType.ThrowIn) return false;
            Vec2 rest = PredictRest(Ball.M);
            return MathF.Abs(rest.Y) <= Pitch.HalfWidth + ThrowInRetrieveDistance && MathF.Abs(rest.X - spot.X) <= ThrowInRetrieveAlong
                   && MathF.Abs(rest.X) < Pitch.HalfLength; // (beside the pitch, not behind the goal line)
        }

        /// <summary>The attendant with the new ball: behind the goal line for a goal kick or a corner, beyond the touchline for a throw-in.</summary>
        private Vec2 AttendantPosition(RestartType type, Vec2 spot)
        {
            float sx = spot.X >= 0f ? 1f : -1f, sy = spot.Y >= 0f ? 1f : -1f;
            if (!BallContinuity) return new Vec2(spot.X, (Pitch.HalfWidth + 9f) * sy);
            switch (type)
            {
                case RestartType.GoalKick:
                    // behind the goal, to the side of it (not through the net)
                    return new Vec2(sx * (Pitch.HalfLength + AttendantDistance), sy * (Pitch.GoalHalfWidth + 5f));
                case RestartType.CornerKick:
                    return new Vec2(sx * (Pitch.HalfLength + AttendantDistance), sy * (Pitch.HalfWidth - 3f));
                default:
                    return new Vec2(spot.X, sy * (Pitch.HalfWidth + AttendantDistance));
            }
        }

        /// <summary>
        /// The thrower and the ball that went out: he walks to it; once he has it, it is in his hands and goes with him to the spot (the
        /// throw-in itself then takes over). Returns the point he walks to.
        /// </summary>
        private Vec2 StepRetrievedBall(Restart r, float dt, Vec2 approach)
        {
            var t = r.Taker;
            if (!_retrieveCarried)
            {
                // it rolls to a stop
                if (Ball.M.Speed > 0.05f || Ball.Z > Pitch.BallRadius + 0.01f) BallPhysics.Step(ref Ball.M, Config, dt);
                if (t != null && Vec2.Distance(t.Pos, Ball.Pos) < 0.8f && Ball.M.Speed < 1.5f)
                {
                    _retrieveCarried = true;
                    if (CollectDiagnostics) Count("out.retrieved.pickedUp");
                }
                else return Ball.Pos;
            }
            if (t != null)
            {
                Ball.M = new BallMotion { X = t.Pos.X + t.FacingDir.X * 0.25f, Y = t.Pos.Y + t.FacingDir.Y * 0.25f, Z = 1.0f };
                Ball.PrevPos = Ball.Pos;
                if (Vec2.Distance(t.Pos, approach) < 1.2f) { r.BallPlaced = true; _retrieveSameBall = false; _retrieveCarried = false; }
            }
            return approach;
        }
    }
}

using System;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// The ball that went out of play rolls on (or bounces on) beyond the lines until it stops against the advertising
    /// boards, off the outside of the net, or on the grass - and then it is gone. The game does not wait for it: the restart
    /// is taken with a new ball (the ball boys' ball). Only the viewers see it; it touches nothing in the match.
    /// </summary>
    public sealed partial class MatchEngine
    {
        /// <summary>The ball that went out (Active while it is still moving or lying there).</summary>
        public BallMotion DeadBall;
        public bool DeadBallActive;
        private float _deadBallRest, _deadBallAge;

        private const float BoardsX = Pitch.HalfLength + 5f, BoardsY = Pitch.HalfWidth + 4f;

        /// <summary>The ball has just gone out: from here a copy of it carries on beyond the line.</summary>
        private void ReleaseDeadBall()
        {
            DeadBall = Ball.M;
            DeadBallActive = true;
            _deadBallRest = 0f;
            _deadBallAge = 0f;
        }

        private void UpdateDeadBall(float dt)
        {
            if (!DeadBallActive) return;
            ref var b = ref DeadBall;
            float px = b.X, py = b.Y, pz = b.Z;
            // gravity, air drag and the grass
            b.Vz -= Config.Gravity * dt;
            b.X += b.Vx * dt; b.Y += b.Vy * dt; b.Z += b.Vz * dt;
            if (b.Z <= Pitch.BallRadius)
            {
                b.Z = Pitch.BallRadius;
                if (b.Vz < -1.2f) { b.Vz = -b.Vz * 0.45f; b.Vx *= 0.8f; b.Vy *= 0.8f; }
                else b.Vz = 0f;
                float decay = MathF.Max(0f, 1f - 1.1f * dt); // rolling on the grass beyond the lines (a bit longer than on the pitch)
                b.Vx *= decay; b.Vy *= decay;
            }
            // the outside of the net: the side netting and the roof stop it (it never goes through into the goal)
            for (int side = -1; side <= 1; side += 2)
            {
                float line = Pitch.HalfLength * side;
                bool inBox = (b.X - line) * side > 0f && (b.X - line) * side < Pitch.GoalDepth && MathF.Abs(b.Y) < Pitch.GoalHalfWidth + 0.1f && b.Z < Pitch.GoalHeight + 0.1f;
                if (!inBox) continue;
                bool wasInBox = (px - line) * side > 0f && (px - line) * side < Pitch.GoalDepth && MathF.Abs(py) < Pitch.GoalHalfWidth + 0.1f && pz < Pitch.GoalHeight + 0.1f;
                if (wasInBox) continue;
                if (MathF.Abs(py) >= Pitch.GoalHalfWidth + 0.1f) { b.Y = MathF.Sign(py) * (Pitch.GoalHalfWidth + 0.12f); b.Vy = -b.Vy * 0.15f; b.Vx *= 0.35f; }
                // (on the roof: the netting sags towards the back - it rolls off behind the goal and drops to the grass)
                else if (pz >= Pitch.GoalHeight + 0.1f) { b.Z = Pitch.GoalHeight + 0.12f; b.Vz = 0f; b.Vx = side * MathF.Max(MathF.Abs(b.Vx) * 0.5f, 1.2f); b.Vy *= 0.5f; }
                else { b.X = line + side * (Pitch.GoalDepth + 0.05f); b.Vx = MathF.Abs(b.Vx) * 0.2f * side; } // (the back of the net, from behind)
            }
            // the advertising boards
            if (MathF.Abs(b.X) > BoardsX) { b.X = MathF.Sign(b.X) * BoardsX; b.Vx = -b.Vx * 0.3f; }
            if (MathF.Abs(b.Y) > BoardsY) { b.Y = MathF.Sign(b.Y) * BoardsY; b.Vy = -b.Vy * 0.3f; }
            // lying still for a moment, then it is collected
            float speed = MathF.Sqrt(b.Vx * b.Vx + b.Vy * b.Vy + b.Vz * b.Vz);
            _deadBallRest = speed < 0.15f && b.Z <= Pitch.BallRadius + 0.01f ? _deadBallRest + dt : 0f;
            _deadBallAge += dt;
            // (and whatever happens, a ball boy has it after a while - it never lies anywhere for good)
            if (_deadBallRest > 1.5f || _deadBallAge > 9f) DeadBallActive = false;
        }
    }
}

using System;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// The goal net as netting, not a wall (polish round 8): a ball that reaches the back, a side or the roof of the net pushes it out and is
    /// slowed over the give of the net - an overdamped spring, integrated in small steps from the moment the ball reaches it - then
    /// drops into the bottom of the net with hardly a bounce; along the net it is slowed by the mesh. The ball never stops dead on the net and
    /// never passes through it (at most <see cref="NetMaxGive"/> beyond it). The renderers draw the bulge from the ball's depth and NetHit.
    /// </summary>
    public sealed partial class MatchEngine
    {
        /// <summary>The soft net (false: the hard net of before - the ball reflected at <see cref="MatchConfig.NetRestitution"/>).</summary>
        public static bool NetAbsorption = true;
        /// <summary>The net's spring (1/s^2, per unit of the ball's mass): with the damping, a 30 m/s shot is stopped 0.32 m into the net
        /// (the renderers draw a bulge of up to 0.35 m), a 10 m/s one 0.1 m. An approximation (D): no measured figure for a goal net found.</summary>
        public static float NetStiffness = 800f;
        /// <summary>Damping ratio of the net (1 = critical): over 1 the net does not throw the ball back - it eases back to the slack net
        /// and the ball drops (D).</summary>
        public static float NetDampingRatio = 1.1f;
        /// <summary>The mesh slows the ball along the net (1/s) while it presses into it.</summary>
        public static float NetFriction = 6f;
        /// <summary>The furthest the ball pushes the net out (m): beyond it the net is taut.</summary>
        public static float NetMaxGive = 0.5f;

        private int _netContact; // (bit per surface: back 1, sides 2, roof 4 - a NetHit is emitted when the ball first reaches one)

        /// <summary>
        /// One surface of the net: <paramref name="coord"/> and <paramref name="vel"/> are the ball's position and speed across it,
        /// <paramref name="plane"/> where the slack net hangs, <paramref name="sign"/> +1 when "into the net" is the positive direction. True
        /// while the ball presses into it.
        /// </summary>
        private bool PressNet(ref float coord, ref float vel, float prevCoord, float plane, float sign, float dt)
        {
            float p = (coord - plane) * sign;
            if (p <= 0f) return false;
            float p0 = (prevCoord - plane) * sign;
            // from the moment it reached the net (part of this step) or the whole step (already in it)
            float t = p0 < 0f ? dt * p / (p - p0) : dt;
            float x = p0 < 0f ? 0f : p0, v = vel * sign;
            float k = NetStiffness, c = 2f * NetDampingRatio * MathF.Sqrt(NetStiffness);
            int n = Math.Max(1, (int)MathF.Ceiling(t / 0.002f));
            float h = t / n;
            for (int i = 0; i < n; i++)
            {
                if (x < 0f) { x += v * h; continue; } // (it has come back off the net: free again)
                v -= (k * x + c * v) * h;
                x += v * h;
            }
            if (x > NetMaxGive) { x = NetMaxGive; if (v > 0f) v = 0f; }
            coord = plane + x * sign;
            vel = v * sign;
            return x > 0f;
        }

        /// <summary>The ball inside the goal against the netting: back, sides, roof.</summary>
        private void SoftNet(ref BallMotion b, Vec3 from, int side, float dt)
        {
            const float r = Pitch.BallRadius;
            float impact = b.Speed;
            int contact = 0;
            // the back of the net
            float x = b.X, vx = b.Vx;
            if (PressNet(ref x, ref vx, from.X, (Pitch.HalfLength + Pitch.GoalDepth - r) * side, side, dt)) contact |= 1;
            b.X = x; b.Vx = vx;
            // the side netting (only behind the goal line)
            if ((b.X - Pitch.HalfLength * side) * side > 0f)
            {
                float sy = b.Y >= 0f ? 1f : -1f, y = b.Y, vy = b.Vy;
                if (PressNet(ref y, ref vy, from.Y, (Pitch.GoalHalfWidth - r) * sy, sy, dt)) contact |= 2;
                b.Y = y; b.Vy = vy;
                float z = b.Z, vz = b.Vz;
                if (PressNet(ref z, ref vz, from.Z, Pitch.GoalHeight - r, 1f, dt)) contact |= 4;
                b.Z = z; b.Vz = vz;
            }
            if (contact != 0)
            {
                // the mesh drags on it along the net
                float keep = MathF.Max(0f, 1f - NetFriction * dt);
                if ((contact & 1) != 0) { b.Vy *= keep; b.Vz *= keep; }
                if ((contact & 2) != 0) { b.Vx *= keep; b.Vz *= keep; }
                if ((contact & 4) != 0) { b.Vx *= keep; b.Vy *= keep; }
                b.Wx *= keep; b.Wy *= keep; b.Wz *= keep;
                if ((contact & ~_netContact) != 0 && Ball.InGoal && impact > 1.5f)
                {
                    Emit(MatchEventType.NetHit, null, null, null, b.Position2D, impact);
                    if (CollectDiagnostics) Count("net.hit." + ((contact & 1) != 0 ? "back" : (contact & 2) != 0 ? "side" : "roof"));
                }
            }
            _netContact = contact;
        }
    }
}

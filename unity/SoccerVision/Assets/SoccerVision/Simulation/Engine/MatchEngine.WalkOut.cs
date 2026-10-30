using System;
using System.Collections.Generic;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// Before the kick-off the two teams and the officials walk out of the tunnel (on the bench side, at the halfway line)
    /// side by side, line up facing the main stand - the home team on one side of the referee, the visitors on the other -
    /// and then jog to their kick-off places. The clock does not run; play starts with the usual kick-off.
    /// </summary>
    public sealed partial class MatchEngine
    {
        private float _walkOutT = -1f;
        private Dictionary<PlayerAgent, Vec2> _walkOutKickOff;
        private Vec2[] _walkOutOfficials;

        /// <summary>True while the teams are walking out (before the first kick-off).</summary>
        public bool WalkingOut => _walkOutT >= 0f;

        private const float WalkInSeconds = 16f, LineUpSeconds = 7f, WalkOutTimeout = 45f;
        private const float LineY = 7f, TunnelY = Pitch.HalfWidth + 4f;

        private void BeginWalkOut()
        {
            _walkOutT = 0f;
            _walkOutKickOff = new Dictionary<PlayerAgent, Vec2>();
            foreach (var p in AllPlayers) _walkOutKickOff[p] = p.Pos;
            _walkOutOfficials = new Vec2[Officials.Length];
            for (int i = 0; i < Officials.Length; i++) _walkOutOfficials[i] = Officials[i].Pos;
            // in the tunnel: two files, the home team on the left, the visitors on the right, the officials in front
            foreach (var t in Teams)
            {
                int k = 0;
                foreach (var p in t.Players)
                {
                    float x = t == Home ? -0.9f : 0.9f;
                    p.Pos = p.PrevPos = new Vec2(x, TunnelY + 2f + k * 1.1f);
                    p.Vel = Vec2.Zero;
                    p.Facing = -MathUtil.Pi / 2f;
                    p.Target = p.Pos;
                    k++;
                }
            }
            for (int i = 0; i < Officials.Length; i++)
            {
                Officials[i].Pos = Officials[i].PrevPos = new Vec2((i - 1) * 0.9f, TunnelY);
                Officials[i].Vel = Vec2.Zero;
            }
        }

        /// <summary>Where each one stands in the line-up: the officials in the middle, a team to each side.</summary>
        private Vec2 LineUpSpot(PlayerAgent p)
        {
            int k = p.Team.Players.IndexOf(p);
            float x = 2.4f + k * 1.15f;
            return new Vec2(p.Team == Home ? -x : x, LineY);
        }

        private void StepWalkOut(float dt)
        {
            _walkOutT += dt;
            bool toKickOff = _walkOutT >= WalkInSeconds + LineUpSeconds;
            bool allThere = true;
            foreach (var p in AllPlayers)
            {
                Vec2 to = toKickOff ? _walkOutKickOff[p] : LineUpSpot(p);
                // (a file: each one waits for the man in front to leave the tunnel)
                float delay = toKickOff ? 0f : p.Team.Players.IndexOf(p) * 0.55f;
                if (_walkOutT < delay) continue;
                allThere &= MoveTowards(ref p.Pos, ref p.Vel, to, toKickOff ? 3.6f : 1.5f, dt);
                p.Facing = p.Vel.LengthSq > 0.04f ? p.Vel.Angle : toKickOff ? (p.Team.AttackDir > 0 ? 0f : MathUtil.Pi) : -MathUtil.Pi / 2f;
                p.Target = to;
            }
            for (int i = 0; i < Officials.Length; i++)
            {
                var o = Officials[i];
                Vec2 to = toKickOff ? _walkOutOfficials[i] : new Vec2((i - 1) * 1.1f, LineY);
                allThere &= MoveTowards(ref o.Pos, ref o.Vel, to, toKickOff ? 3.6f : 1.5f, dt);
                o.Facing = o.Vel.LengthSq > 0.04f ? o.Vel.Angle : -MathUtil.Pi / 2f;
            }
            if ((toKickOff && allThere) || _walkOutT > WalkOutTimeout) EndWalkOut();
        }

        private static bool MoveTowards(ref Vec2 pos, ref Vec2 vel, Vec2 to, float speed, float dt)
        {
            Vec2 d = to - pos;
            float len = d.Length;
            if (len < 0.05f) { pos = to; vel = Vec2.Zero; return true; }
            float step = MathF.Min(len, speed * dt);
            pos += d / len * step;
            vel = d / len * speed;
            return len < 0.3f;
        }

        private void EndWalkOut()
        {
            foreach (var p in AllPlayers)
            {
                p.Pos = p.PrevPos = _walkOutKickOff[p];
                p.Vel = Vec2.Zero;
                p.Facing = p.Team.AttackDir > 0 ? 0f : MathUtil.Pi;
                p.Target = p.ShapeTarget = p.Pos;
            }
            for (int i = 0; i < Officials.Length; i++) { Officials[i].Pos = _walkOutOfficials[i]; Officials[i].Vel = Vec2.Zero; }
            _walkOutT = -1f;
            _walkOutKickOff = null;
        }
    }
}

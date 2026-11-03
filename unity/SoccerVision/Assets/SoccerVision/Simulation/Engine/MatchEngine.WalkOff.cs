using System;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// The end of a half is not the end of the picture: at half time the players and the officials walk off to the tunnel (and
    /// come back out of it for the second half, jogging to their places); at full time the winners celebrate together, go to
    /// their fans to applaud them and then walk off, while the losers trudge off with their heads down; after a draw both walk
    /// off. The clock has stopped; nothing in the match changes - it is only what the viewer sees.
    /// </summary>
    public sealed partial class MatchEngine
    {
        private enum WalkOffKind { HalfTime, FullTime }

        private float _walkOffT = -1f;
        private WalkOffKind _walkOffKind;
        private Action _walkOffThen;
        private TeamAgent _walkOffWinner;

        /// <summary>True while the players are walking off (at half time or at full time).</summary>
        public bool WalkingOff => _walkOffT >= 0f;

        private const float HalfTimeWalkOffTimeout = 26f, FullTimeWalkOffTimeout = 42f;
        private const float CelebrateSeconds = 7f, FansSeconds = 11f;

        private void BeginWalkOff(WalkOffKind kind, Action then)
        {
            _walkOffT = 0f;
            _walkOffKind = kind;
            _walkOffThen = then;
            _walkOffWinner = Home.Score > Away.Score ? Home : Away.Score > Home.Score ? Away : null;
            Ball.Owner = null;
            Ball.InHands = false;
            Ball.Stop();
            foreach (var p in AllPlayers)
            {
                p.StunTimer = 0f; p.SlideTimer = 0f; p.GroundTimer = 0f; p.DiveTimer = 0f; p.KickTimer = 0f;
                if (kind == WalkOffKind.FullTime && _walkOffWinner != null && p.Team != _walkOffWinner && !p.SentOff)
                    Gesture(p, GestureKind.Disappointed, 4f, 0f);
            }
        }

        /// <summary>His place in the file into the tunnel (the home team on the left of it, the visitors on the right).</summary>
        private Vec2 TunnelSpot(PlayerAgent p)
        {
            int k = p.Team.Players.IndexOf(p);
            return new Vec2(p.Team == Home ? -0.9f : 0.9f, TunnelY + 1.5f + k * 0.7f);
        }

        private void StepWalkOff(float dt)
        {
            _walkOffT += dt;
            bool allIn = true;
            float t = _walkOffT;
            var winner = _walkOffWinner;
            foreach (var p in AllPlayers)
            {
                if (p.SentOff) continue;
                // a little difference between one man and the next, so they do not move as one block
                float pace = 1f + 0.12f * MathF.Sin(p.Index * 1.7f);
                Vec2 to = TunnelSpot(p);
                float speed = 1.7f * pace;
                if (_walkOffKind == WalkOffKind.FullTime && winner != null)
                {
                    if (p.Team == winner)
                    {
                        if (t < CelebrateSeconds)
                        {
                            // together, round the middle of the team, arms up, a jump or two
                            Vec2 c = TeamCentre(winner);
                            int k = winner.Players.IndexOf(p);
                            to = c + Vec2.FromAngle(k * 0.62f) * (1.1f + 0.15f * (k % 3));
                            speed = 3.2f * pace;
                            if (((int)(t * 3f) + k) % 9 == 0 && p.JumpZ <= 0f && Vec2.Distance(p.Pos, to) < 1.5f) StartJump(p, 0.3f);
                            if (((int)(t * 2f) + k) % 6 == 0) Gesture(p, k % 2 == 0 ? GestureKind.Celebrate : GestureKind.Hug, 1.2f, (c - p.Pos).Angle);
                        }
                        else if (t < CelebrateSeconds + FansSeconds)
                        {
                            // to their fans, in front of the stand on the far side, and applaud them
                            int k = winner.Players.IndexOf(p);
                            float side = winner == Home ? -1f : 1f;
                            to = new Vec2(side * (6f + k * 2.4f), -Pitch.HalfWidth + 1.5f);
                            speed = 2.4f * pace;
                            if (Vec2.Distance(p.Pos, to) < 2f && ((int)(t * 2f) + k) % 5 == 0) Gesture(p, GestureKind.Encourage, 1.0f, -MathUtil.Pi / 2f);
                        }
                        else speed = 1.9f * pace;
                    }
                    else
                    {
                        // the losers: slowly, heads down, straight to the tunnel
                        speed = (t < CelebrateSeconds ? 0.7f : 1.3f) * pace;
                        if (((int)(t * 1.5f) + p.Index) % 8 == 0) Gesture(p, GestureKind.Disappointed, 1.5f, 0f);
                    }
                }
                bool there = MoveTowards(ref p.Pos, ref p.Vel, to, speed, dt);
                p.Facing = p.Vel.LengthSq > 0.04f ? p.Vel.Angle : p.Facing;
                p.Target = to;
                if (p.JumpZ > 0f || p.JumpVz > 0f)
                {
                    p.JumpZ += p.JumpVz * dt;
                    p.JumpVz -= JumpGravity * dt;
                    if (p.JumpZ <= 0f) { p.JumpZ = 0f; p.JumpVz = 0f; }
                }
                allIn &= there && Vec2.Distance(p.Pos, TunnelSpot(p)) < 0.5f;
            }
            for (int i = 0; i < Officials.Length; i++)
            {
                var o = Officials[i];
                Vec2 to = new Vec2((i - 1) * 0.9f, TunnelY + 0.8f);
                MoveTowards(ref o.Pos, ref o.Vel, to, 1.8f, dt);
                o.Facing = o.Vel.LengthSq > 0.04f ? o.Vel.Angle : o.Facing;
            }
            float timeout = _walkOffKind == WalkOffKind.HalfTime ? HalfTimeWalkOffTimeout : FullTimeWalkOffTimeout;
            if (allIn || _walkOffT > timeout)
            {
                _walkOffT = -1f;
                var then = _walkOffThen;
                _walkOffThen = null;
                then?.Invoke();
            }
        }

        private static Vec2 TeamCentre(TeamAgent t)
        {
            Vec2 s = Vec2.Zero; int n = 0;
            foreach (var p in t.Players) { if (p.SentOff) continue; s += p.Pos; n++; }
            return n > 0 ? s / n : Vec2.Zero;
        }

        /// <summary>
        /// The second half: the players come back out of the tunnel and jog to their kick-off places (the walk-out without the
        /// line-up).
        /// </summary>
        private void BeginComeOut()
        {
            _walkOutT = WalkInSeconds + LineUpSeconds;
            _walkOutKickOff = new System.Collections.Generic.Dictionary<PlayerAgent, Vec2>();
            foreach (var p in AllPlayers)
            {
                _walkOutKickOff[p] = p.Pos;
                if (p.SentOff) continue;
                Vec2 s = TunnelSpot(p);
                p.Pos = p.PrevPos = s;
                p.Vel = Vec2.Zero;
            }
            _walkOutOfficials = new Vec2[Officials.Length];
            for (int i = 0; i < Officials.Length; i++)
            {
                _walkOutOfficials[i] = Officials[i].Pos;
                Officials[i].Pos = Officials[i].PrevPos = new Vec2((i - 1) * 0.9f, TunnelY);
                Officials[i].Vel = Vec2.Zero;
            }
        }
    }
}

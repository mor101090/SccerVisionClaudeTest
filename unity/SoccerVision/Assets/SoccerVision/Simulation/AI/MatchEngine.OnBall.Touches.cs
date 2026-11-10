using System;

namespace SoccerVision.Simulation
{
    /// <summary>How a player runs with the ball (polish round 5): the length of his touches.</summary>
    public enum DribbleStyle
    {
        /// <summary>Close control: a man near him - the ball never more than a stride away.</summary>
        Close,
        /// <summary>Progressive carrying: space in front - the ball pushed two or three metres ahead and run onto.</summary>
        Progressive,
    }

    /// <summary>
    /// Running with the ball as touches (polish round 5): the ball leaves the foot and rolls by its own physics, the carrier runs onto it and
    /// touches it again - short touches with a man near him, longer ones into space - and a kick waits until his foot is at the ball. The
    /// knock-and-run (the ball played past a defender into space and chased) is a kick, see TryKnockAndRun.
    /// </summary>
    public sealed partial class MatchEngine
    {
        /// <summary>The ball rolls between touches (false: the old model, the ball held a set distance in front of the feet).</summary>
        public static bool DribbleTouches = true;
        /// <summary>Below this speed (m/s) he keeps the ball under his foot (class D: a walk).</summary>
        public static float DribbleTouchMinSpeed = 1.6f;
        /// <summary>How much faster than him (m/s) a touch sends the ball: close control / progressive (class D: the ball ends about one and two to
        /// three strides ahead).</summary>
        public static float CloseTouchPush = 0.6f, ProgressiveTouchPush = 1.8f;
        /// <summary>Within this distance (m) a man in front of him makes it close control (class D).</summary>
        public static float CloseControlDistance = 5f;
        /// <summary>Beyond this far upfield (m from halfway) he keeps the ball close whatever the space (class D: the last 27 m).</summary>
        public static float CloseControlFinalThird = 25f;
        /// <summary>The ball is at his foot within this distance (m): he can touch it, pass it or shoot (class D: the engine's contact limit for a kick).</summary>
        public static float FootReachForKick = 1.25f;
        /// <summary>Beyond this distance (m) from its carrier a rolling ball is loose - his no longer (class D: a heavy touch).</summary>
        public static float DribbleLooseDistance = 2.5f;

        /// <summary>Is the ball at his foot (he can play it)?</summary>
        private bool BallAtFoot(PlayerAgent p) => !DribbleTouches || Ball.InHands || Vec2.Distance(Ball.Pos, p.Pos) <= FootReachForKick;

        /// <summary>The style for a run with the ball: close control with a man near in front or beside him, or near a line; otherwise progressive.</summary>
        private DribbleStyle ChooseDribbleStyle(PlayerAgent p, Vec2 target)
        {
            Vec2 dir = target - p.Pos;
            dir = dir.LengthSq > 0.01f ? dir.Normalized : p.FacingDir;
            if (!Pitch.InsideField(p.Pos + dir * 3f, -1.5f)) return DribbleStyle.Close;
            // (near the box the ball is kept close: the defenders are near, and the shot or the pass can come at any stride)
            if (p.Team.Forward(p.Pos) > CloseControlFinalThird) return DribbleStyle.Close;
            foreach (var o in p.Team.Opponent.Players)
            {
                if (o.SentOff || o.IsDown) continue;
                Vec2 rel = o.Pos - p.Pos;
                float d = rel.Length;
                if (d < CloseControlDistance && (d < 2.5f || Vec2.Dot(rel / d, dir) > -0.2f)) return DribbleStyle.Close;
            }
            return DribbleStyle.Progressive;
        }

        /// <summary>
        /// The ball with its carrier, each step. Walking (or a fake shot) he keeps it under his foot; running, the ball rolls by its own physics
        /// and he touches it again when he has caught it up (or it has drifted off his path within reach) - each touch a little faster than him,
        /// by his style. A ball out of his reach is not pulled back: he goes to it (see the movement) and only then can play it.
        /// </summary>
        private void CarryBall(PlayerAgent owner, float dt)
        {
            float speed = owner.Vel.Length;
            if (!DribbleTouches || speed < DribbleTouchMinSpeed || owner.FakeUntil > SimTime)
            {
                // at a walk the ball is shepherded back under the foot (it is at most a stride away: it rolls there, it does not jump)
                Vec2 foot = owner.Pos + owner.FacingDir * 0.45f;
                Vec2 toFoot = foot - Ball.Pos;
                if (DribbleTouches && toFoot.Length > 0.25f && owner.FakeUntil <= SimTime)
                {
                    Vec2 step = toFoot.ClampLength(MathF.Max(2.5f, speed + 1.5f) * dt);
                    Ball.M = new BallMotion { X = Ball.Pos.X + step.X, Y = Ball.Pos.Y + step.Y, Z = Pitch.BallRadius, Vx = step.X / dt, Vy = step.Y / dt };
                    return;
                }
                AttachBallToOwner(owner);
                return;
            }
            var m = Ball.M;
            if (m.Z > Pitch.BallRadius + 0.05f) { m.Z = Pitch.BallRadius; m.Vz = 0f; }
            BallPhysics.Step(ref m, Config, dt);
            m.Z = Pitch.BallRadius; m.Vz = 0f;
            Ball.M = m;
            Vec2 dir = owner.Vel / speed;
            Vec2 rel = Ball.Pos - owner.Pos;
            float lead = Vec2.Dot(rel, dir), side = MathF.Abs(rel.X * dir.Y - rel.Y * dir.X);
            bool reach = rel.Length <= FootReachForKick;
            // caught up with it (or it is drifting off his path while still within reach): the next touch
            if (reach && (lead < 0.45f || side > 0.55f))
            {
                var style = owner.DribbleStyleNow;
                float push = style == DribbleStyle.Close ? CloseTouchPush : ProgressiveTouchPush;
                // (never a long touch towards a line he is close to)
                if (!Pitch.InsideField(owner.Pos + dir * 3f, -1f)) push = MathF.Min(push, 0.4f);
                Vec2 foot = owner.Pos + dir * 0.45f;
                Vec2 v = dir * (speed + push);
                Ball.M = new BallMotion { X = foot.X, Y = foot.Y, Z = Pitch.BallRadius, Vx = v.X, Vy = v.Y };
                if (CollectDiagnostics) Count("dribble.touch." + style);
            }
            if (rel.Length > DribbleLooseDistance)
            {
                // a touch too heavy (or he turned away from it): the ball is no longer his - a loose ball he has to win back
                Ball.Owner = null;
                Ball.IntendedReceiver = null;
                MarkBallChanged();
                RebuildTrajectory();
                _ballChangedAt = SimTime;
                _ballChangeExpectedBy = owner.Team;
                if (CollectDiagnostics) Count("dribble.loose");
                AssignBallDuties();
                return;
            }
            if (!Pitch.InsideField(Ball.Pos, -0.2f))
            {
                // (a carried ball is never let roll out: he stops it on the line)
                Vec2 c = Pitch.ClampToField(Ball.Pos, -0.2f);
                Ball.M = new BallMotion { X = c.X, Y = c.Y, Z = Pitch.BallRadius };
                if (CollectDiagnostics) Count("dribble.stoppedAtLine");
            }
        }

        /// <summary>How readily a quick player knocks it past his man and races him (class D/E: no count of knock-ons was found; set so that it
        /// is a few a match, not the usual way past a man).</summary>
        public static float KnockAndRunChance = 0.7f;

        /// <summary>
        /// Knock-and-run: one defender in front (3-8 m), open grass behind him on one side, and the carrier quicker than him - the ball is played
        /// past him, 7-11 m on, and the race is on (the defender is not fooled, as by a nutmeg: he turns and chases; the quicker one gets there).
        /// </summary>
        private bool TryKnockAndRun(PlayerAgent p, ref KickPlan chosen, Vec2 pos)
        {
            var team = p.Team;
            if (Ball.Owner != p || team.Forward(pos) < -10f) return false;
            Vec2 dir = (chosen.DribbleTarget - pos).Normalized;
            if (dir.LengthSq < 0.01f) dir = p.FacingDir;
            PlayerAgent v = null; float vd = 99f;
            foreach (var o in team.Opponent.Players)
            {
                if (o.SentOff || o.IsDown) continue;
                Vec2 rel = o.Pos - pos; float d = rel.Length;
                if (d < 2f) return false; // (too tight: that is close control, or a nutmeg)
                if (d > 8f || Vec2.Dot(rel / d, dir) < 0.8f) continue;
                if (d < vd) { vd = d; v = o; }
            }
            if (v == null || v.IsGoalkeeper || vd < 3f) return false;
            // quicker than him by a clear margin
            float pace = p.MaxSpeed - v.MaxSpeed;
            if (pace < 0.4f) return false;
            // past him on the side with more room, and nobody else there
            Vec2 across = new Vec2(-dir.Y, dir.X);
            float side = Vec2.Cross(dir, v.Pos - pos) > 0f ? -1f : 1f;
            float beyond = _rng.Range(7f, 11f);
            Vec2 landing = v.Pos + dir * (beyond - vd * 0.5f) + across * side * 2.2f;
            if (!Pitch.InsideField(landing, -2f)) return false;
            foreach (var o in team.Opponent.Players)
            {
                if (o.SentOff || o == v) continue;
                if (Vec2.Distance(o.Pos, landing) < 7f) return false;
            }
            float flair = MathUtil.A((p.Attr.Dribbling + p.Attr.Speed) / 2);
            float chance = KnockAndRunChance * MathUtil.Clamp01(pace / 1.2f) * (0.5f + flair) * (p.Has(PlayerTrait.LovesToDribble) ? 1.4f : 1f) * (p.Has(PlayerTrait.RunsBehind) ? 1.3f : 1f);
            if (!_rng.Chance(MathUtil.Clamp(chance, 0f, 0.6f))) return false;
            float dist = Vec2.Distance(Ball.Pos, landing);
            float speed = Roll.KickSpeedFor(dist, 2.5f);
            KickBall(p, Ball.Pos, (landing - Ball.Pos).Normalized * speed, 0f, 0f, 0f, KickKind.KnockOn, null, landing, false, false);
            p.NutmegChaseUntil = SimTime + 3f; // (he goes for his own ball - the same chase as after a nutmeg)
            team.Stats.KnockOns++;
            if (CollectDiagnostics) Count("dribble.knockOn");
            chosen = new KickPlan { Action = BallAction.Dribble, DribbleTarget = landing, Utility = chosen.Utility };
            return true;
        }

        /// <summary>A challenge that gets to the ball before its owner does (a touch too far ahead of him) plays the ball: fewer fouls (class D).</summary>
        public static float BallFirstFoulReduction = 0.6f;

        private float BallFirstFoulFactor(PlayerAgent d, PlayerAgent carrier) =>
            DribbleTouches && Vec2.Distance(d.Pos, Ball.Pos) < Vec2.Distance(carrier.Pos, Ball.Pos) ? BallFirstFoulReduction : 1f;
    }
}

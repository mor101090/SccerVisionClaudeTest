using System;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// Decisions by the touchline (polish round 1). A player under pressure near a line weighs the cross, the pass back, cutting inside,
    /// shielding and taking his man on (the existing options; a cross now sees the man who would block it). In his own half he may also put it
    /// out on purpose, in the middle third only when he is all but caught, in the attacking third never: there he may instead play it off the
    /// defender for a corner or a throw-in. Accidental outs - heavy touches, tackles, deflections - come from the contacts and are untouched.
    /// </summary>
    public sealed partial class MatchEngine
    {
        /// <summary>The pressure above which a player in his own half may put the ball out on purpose (class D; unchanged from before).</summary>
        public static float KickOutPressureOwnHalf = 0.55f;
        /// <summary>The pressure above which a player in the middle third may (class D: only when he is all but caught).</summary>
        public static float KickOutPressureMiddle = 0.85f;
        /// <summary>How far the defender may stand from the line (m) for the attacker to try to play the ball off him for a restart (class D).</summary>
        public static float PlayOffLineDistance = 4f;
        /// <summary>The pressure from which a player pinned by the line looks to play it off his man (class D: he is closed down, not just watched).</summary>
        public static float PlayOffPressure = 0.7f;
        /// <summary>
        /// What a corner is worth to the team that takes it, in expected goals (class B: 0.0389 StatsBomb xG a corner kick, 0.0348 goals,
        /// Premier League 2015/16, all 380 matches - `tools/calibration/corners.py`).
        /// </summary>
        public static float CornerValue = 0.039f;
        /// <summary>How far outside the defender's centre (m) a clean play-off meets his leg (class D: the outer leg, inside his reach).</summary>
        public static float PlayOffLegOffset = 0.15f;
        /// <summary>The spread of where on the leg a clean play-off lands, as a change of the contact offset (class D).</summary>
        public static float PlayOffFaceSpread = 0.15f;
        /// <summary>The speed (m/s) of the strike at the defender's leg for the least technical player; the best add 6 (class D: a driven ball
        /// from two metres, between a firm pass and a shot).</summary>
        public static float PlayOffStrikeSpeed = 12f;
        /// <summary>How far past the line (m) the predicted ball must go for the attackers to let a play-off run out (class D).</summary>
        public static float PlayOffRunOutMargin = 0.3f;

        // the play-off in flight: the defender it is aimed at, until when, and the outer side of him (towards the line)
        private PlayerAgent _playOffDefender;
        private float _playOffUntil = -1f;
        private Vec2 _playOffOutward, _playOffDefenderVel;
        private float _playOffStruckAt;
        private bool _playOffClean;
        private float _playOffContactAt = -10f;
        private TeamAgent _playOffDefenders;

        private float _playOffLetRunAt = -10f;

        /// <summary>
        /// After a clean play-off the attacking side does not chase a ball that is going out for its own restart: the trajectory prediction
        /// says it crosses the line, so it is left to run (the shielding logic covers an opponent who could keep it in). A ball that will stop
        /// short is chased as usual.
        /// </summary>
        private void LetPlayOffRunOut()
        {
            if (SimTime - _playOffContactAt > 3f || _playOffDefenders == null || Ball.Owner != null || Ball.LastTouchTeam != _playOffDefenders) return;
            if (_trajectoryDirty) RebuildTrajectory();
            if (!CrossesLine(_trajectory)) return;
            var attackers = _playOffDefenders.Opponent;
            if (attackers.Chaser != null && attackers.Chaser != Ball.IntendedReceiver) attackers.Chaser = null;
            _playOffLetRunAt = SimTime;
        }

        /// <summary>A player of the attacking side leaving a play-off to run out (see <see cref="LetPlayOffRunOut"/>).</summary>
        private bool LeavesPlayOffToRun(PlayerAgent p) => SimTime - _playOffLetRunAt < 0.15f && _playOffDefenders != null && p.Team == _playOffDefenders.Opponent;

        /// <summary>(Diagnostics) what a clean play-off contact led to: the restart that followed within 3 s, and for whom.</summary>
        private void CountPlayOffResult(RestartType type, TeamAgent restartTeam)
        {
            if (!CollectDiagnostics || SimTime - _playOffContactAt > 3f || _playOffDefenders == null) return;
            _playOffContactAt = -10f;
            Count("playoff.result." + type + (restartTeam == _playOffDefenders ? ".lost" : ".won"));
        }

        /// <summary>
        /// Playing it off the defender: with a defender tight on him (within 2.5 m) and a line close behind that defender (the touchline, or the
        /// by-line, within PlayOffLineDistance of him), the attacker can drive the ball at the defender's outside leg so that it comes off him and goes out - a
        /// corner or a throw-in to the attacking side. How cleanly he hits that leg depends on his technique; a miss is an ordinary contact.
        /// </summary>
        private void EvaluatePlayOffDefender(PlayerAgent p, Vec2 pos, float pressure)
        {
            if (pressure < PlayOffPressure) return;
            var team = p.Team;
            float f = team.Forward(pos), l = team.Lateral(pos);
            if (MathF.Min(Pitch.HalfWidth - MathF.Abs(l), Pitch.HalfLength - f) > PlayOffLineDistance + 2.5f) return;
            PlayerAgent d = null;
            float best = 2.5f;
            foreach (var o in team.Opponent.Players)
            {
                if (o.SentOff || o.IsGoalkeeper || o.IsDown) continue;
                float dist = Vec2.Distance(o.Pos, pos);
                if (dist < best) { best = dist; d = o; }
            }
            if (d == null) return;
            // the line must be close behind the defender: the ball comes off his shin with a few metres of pace, not twenty
            float dl = team.Lateral(d.Pos), df = team.Forward(d.Pos);
            float toTouch = Pitch.HalfWidth - MathF.Abs(dl), toByLine = Pitch.HalfLength - df;
            bool byTouch = toTouch < PlayOffLineDistance && MathF.Sign(dl) == MathF.Sign(l), byGoalLine = toByLine < PlayOffLineDistance;
            if (!byTouch && !byGoalLine) return;
            // the restart it would win: a corner off a defender by the by-line, a throw-in by the touchline
            bool corner = byGoalLine && (!byTouch || toByLine < toTouch);
            float technique = MathUtil.A((p.Attr.Dribbling + p.Attr.BallControl) / 2);
            Vec2 outward = (corner ? team.ToWorld(1f, 0f) - team.ToWorld(0f, 0f) : team.ToWorld(0f, MathF.Sign(l)) - team.ToWorld(0f, 0f)).Normalized;
            // He hits the leg cleanly with this chance; and a clean hit only wins the restart if the ball then carries over the line - which the
            // contact model and the roll decide (predicted here as the execution will play it, so the choice knows what the physics gives).
            float pClean = 0.35f + 0.35f * technique;
            float pOut = PredictPlayOffOut(pos, d, outward, PlayOffStrikeSpeed + 6f * technique);
            if (pOut <= 0f) return;
            float pWon = pClean * pOut;
            Vec2 spot = corner ? team.ToWorld(Pitch.HalfLength, MathF.Sign(l) * Pitch.HalfWidth) : team.ToWorld(f, MathF.Sign(l) * Pitch.HalfWidth);
            float value = corner ? CornerValue : 0.6f * ExpectedThreat.At(team, spot);
            // (not won: a strike that misses the leg runs on past him at pace towards the line, one that hits it badly is his ball - either way
            // it is lost)
            float utility = pWon * value - (1f - pWon) * LossCost(team, pos);
            _options.Add(new KickPlan
            {
                Action = BallAction.PlayOff,
                Kind = KickKind.Pass,
                Target = d.Pos + outward * 0.35f,
                Heading = (d.Pos - pos).Angle,
                Receiver = d,
                Utility = utility,
                Success = pClean,
                Corner = corner,
                Outward = outward,
            });
        }

        /// <summary>Drives the ball at the defender's outside leg.</summary>
        private void ExecutePlayOff(PlayerAgent p, KickPlan plan)
        {
            var d = plan.Receiver;
            float technique = MathUtil.A((p.Attr.Dribbling + p.Attr.BallControl) / 2);
            _playOffDefender = d;
            _playOffUntil = SimTime + 0.6f;
            _playOffClean = _rng.Chance(plan.Success);
            Vec2 outward = plan.Outward;
            float speed = PlayOffStrikeSpeed + 6f * technique;
            // where he will be when the ball gets there (he is moving onto the ball), and his outer leg there; a clean strike finds that leg,
            // a poor one goes wider of it - past him, or into him as an ordinary touch
            float t = Vec2.Distance(d.Pos, Ball.Pos) / speed;
            Vec2 at = d.Pos + d.Vel * t;
            Vec2 aim = at + outward * (_playOffClean ? PlayOffLegOffset : _rng.Range(0.3f, 0.9f));
            _playOffOutward = outward;
            _playOffDefenderVel = d.Vel;
            _playOffStruckAt = SimTime;
            Vec2 dir = (aim - Ball.Pos).Normalized;
            KickBall(p, Ball.Pos, dir * speed, 0f, 0f, 0f, KickKind.Pass, null, aim, false, false);
            if (CollectDiagnostics) Count("playoff." + (plan.Corner ? "corner" : "throw") + (_playOffClean ? ".clean" : ".miss"));
        }

        /// <summary>
        /// The defender the play-off was aimed at touches the ball: on a clean strike it comes off his outside leg (an impact off the body, the
        /// stage 8 model: where it leaves him is the physics of that contact); otherwise it is his ordinary touch.
        /// </summary>
        private bool TryPlayOffContact(PlayerAgent p)
        {
            if (p != _playOffDefender || SimTime > _playOffUntil) return false;
            _playOffDefender = null;
            if (!_playOffClean || Config.Collisions == null) return false;
            _playOffDefenders = p.Team;
            // He met the leg's outer face: the face that looks between where the ball came from and the line behind him (the bisector of
            // the two), with the spread of a real strike. Where it leaves him is the physics of that contact (stage 8): the normal part
            // rebounds, the rest runs on - a ball driven across him glances off towards the line, one driven along the line grazes on.
            Vec2 rel = Ball.Velocity - (SimTime - _playOffStruckAt < p.ReactionTime ? _playOffDefenderVel : p.Vel);
            Vec2 u = rel.LengthSq > 0.01f ? rel.Normalized : Ball.Velocity.Normalized;
            float offset = PlayOffFaceOffset(u, _playOffOutward);
            offset = MathUtil.Clamp(offset + _rng.Range(-PlayOffFaceSpread, PlayOffFaceSpread), -0.92f, 0.92f);
            // (inside his reaction time he is still moving as he was when it was struck: he cannot have stepped away or into it yet)
            Vec2 bodyVel = SimTime - _playOffStruckAt < p.ReactionTime ? _playOffDefenderVel : p.Vel;
            BodyImpact(p, _rng.Range(0.3f, 0.5f), offset, _rng.Range(0f, 0.25f), bodyVel);
            _playOffContactAt = SimTime;
            if (CollectDiagnostics) Count("playoff.contact");
            return true;
        }
        /// <summary>
        /// Where on the leg a ball coming along <paramref name="u"/> meets the defender, as the contact offset of
        /// <see cref="BodyContactNormal"/>: the face that looks between where the ball came from and the line behind him (the bisector);
        /// when the line is ahead of him along the ball's path, a graze.
        /// </summary>
        internal static float PlayOffFaceOffset(Vec2 u, Vec2 outward)
        {
            Vec2 side = new Vec2(-u.Y, u.X);
            Vec2 face = outward - u;
            if (face.LengthSq <= 0.01f) return MathF.Sign(Vec2.Dot(outward, side));
            float offset = Vec2.Dot(face.Normalized, side);
            if (Vec2.Dot(face, u) > 0f) offset = MathF.Sign(offset);
            return offset;
        }

        private BallTrajectory _playOffProbe;

        /// <summary>
        /// Whether a predicted path crosses a touchline or a goal line (by <see cref="PlayOffRunOutMargin"/>), on the ground or in the air. The
        /// first crossing is what counts: the ball is out of play there, whatever its spin would do to it afterwards.
        /// </summary>
        private static bool CrossesLine(BallTrajectory t)
        {
            for (int i = 0; i < t.Count; i++)
            {
                Vec2 q = t.PositionAt(i);
                if (MathF.Abs(q.X) > Pitch.HalfLength + PlayOffRunOutMargin || MathF.Abs(q.Y) > Pitch.HalfWidth + PlayOffRunOutMargin) return true;
            }
            return false;
        }

        /// <summary>
        /// The share of clean strikes that would carry over a line: the contact (the stage 8 impact at the middle of its restitution and lift,
        /// at the middle of the strike's spread and at either edge of it) and then the ball's flight and roll (the trajectory predictor).
        /// </summary>
        private float PredictPlayOffOut(Vec2 from, PlayerAgent d, Vec2 outward, float speed)
        {
            var cm = Config.Collisions;
            if (cm == null) return 0f;
            if (_playOffProbe == null) _playOffProbe = new BallTrajectory();
            Vec2 at = d.Pos + d.Vel * (Vec2.Distance(d.Pos, from) / speed);
            Vec2 v = (at + outward * PlayOffLegOffset - from).Normalized * speed;
            Vec2 rel = v - d.Vel;
            float offset0 = PlayOffFaceOffset(rel.LengthSq > 0.01f ? rel.Normalized : v.Normalized, outward);
            int outCount = 0;
            for (int k = -1; k <= 1; k++)
            {
                float offset = MathUtil.Clamp(offset0 + k * PlayOffFaceSpread, -0.92f, 0.92f);
                // (a driven ball along the ground arrives rolling: its spin is v / R about the horizontal axis across its path)
                float r = Pitch.BallRadius;
                var m = new BallMotion { X = at.X, Y = at.Y, Z = r, Vx = v.X, Vy = v.Y, Wx = -v.Y / r, Wy = v.X / r, HasWorldSpin = true };
                BallPhysics.Impact(ref m, Config, BodyContactNormal(v, d.Vel, offset, 0.125f), 0.4f, cm.BodyFriction, new Vec3(d.Vel.X, d.Vel.Y, 0f));
                if (m.Vz < 0f) m.Vz = 0f;
                _playOffProbe.Build(m, Config);
                if (CrossesLine(_playOffProbe)) outCount++;
            }
            return outCount / 3f;
        }
    }
}

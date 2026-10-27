using System;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// How the weather and the pitch reach the players (see <see cref="MatchConditions"/>; the ball side lives in
    /// <see cref="BallPhysics"/> and <see cref="PitchSurface"/>). On a dry, perfect pitch in still air every factor here
    /// is exactly 1 and no random number is drawn, so such a match plays exactly as before.
    /// </summary>
    public sealed partial class MatchEngine
    {
        /// <summary>Weather, wind and pitch of this match.</summary>
        public MatchConditions Conditions => Config.Conditions;
        /// <summary>Mud and puddles (null on a uniform pitch).</summary>
        public PitchSurface Surface { get; private set; }

        /// <summary>True when the footing anywhere can be less than perfect (rain, snow, mud).</summary>
        private bool _slippery;

        private void SetUpConditions()
        {
            Config.ApplyConditions();
            var c = Config.Conditions;
            if (c.IsWet || c.Maintenance < 0.999f)
            {
                var surface = new PitchSurface(c, Seed);
                if (surface.HasMud || surface.HasWater)
                {
                    Surface = surface;
                    Config.Surface = surface;
                }
            }
            _slippery = c.IsWet || c.IsSnow || Surface != null;
        }

        /// <summary>
        /// Grip of the boots at <paramref name="pos"/> (1 = perfect). Wet grass costs a little, snow more, mud and standing
        /// water most. It scales acceleration, braking and turning, and the chance to slip.
        /// </summary>
        internal float Footing(Vec2 pos)
        {
            if (!_slippery) return 1f;
            float f = 1f;
            if (Conditions.IsWet) f -= 0.08f;
            else if (Conditions.IsSnow) f -= 0.12f;
            if (Surface != null) f -= 0.22f * Surface.MudAt(pos.X, pos.Y) + 0.18f * Surface.WaterAt(pos.X, pos.Y);
            return f;
        }

        /// <summary>
        /// Close control at <paramref name="pos"/> (1 = perfect): a wet ball skids off the foot, snow and mud stop it, so
        /// first touches, dribbles and passes are harder.
        /// </summary>
        internal float BallControlFactor(Vec2 pos)
        {
            if (!_slippery) return 1f;
            float f = 1f;
            if (Conditions.IsWet) f -= 0.07f;
            else if (Conditions.IsSnow) f -= 0.1f;
            if (Surface != null) f -= 0.15f * Surface.MudAt(pos.X, pos.Y) + 0.2f * Surface.WaterAt(pos.X, pos.Y);
            return f;
        }

        /// <summary>
        /// A player who sprints and changes direction hard on a slippery surface can lose his footing: he goes down for a
        /// moment, and a ball at his feet runs loose.
        /// </summary>
        private void MaybeSlip(PlayerAgent p, float footing, float speed, float vmax, float demand, float dt)
        {
            if (footing >= 0.999f || Phase != MatchPhase.Live || p.JumpZ > 0f || p.DiveTimer > 0f || p.IsDown || speed < 3.5f) return;
            float loss = 1f - footing;
            float run = speed / MathF.Max(1f, vmax);
            float rate = 0.062f * loss * run * run * MathUtil.Clamp01(demand / 5f) * (1.15f - 0.3f * MathUtil.A(p.Attr.Acceleration));
            if (!_rng.Chance(rate * dt)) return;
            p.GroundTimer = _rng.Range(0.7f, 1.3f);
            p.StunTimer = MathF.Max(p.StunTimer, p.GroundTimer + 0.2f);
            p.Vel *= 0.6f;
            if (CollectDiagnostics) Count("slip" + (Ball.Owner == p ? ".onball" : ""));
            if (Ball.Owner == p && !Ball.InHands)
            {
                // the ball runs on without him
                SetDeflection(p, p.Vel * 1.3f + p.FacingDir * 1.2f, 0f);
                Emit(MatchEventType.Slip, p.Team, p, null, p.Pos, 0f, WhistleKind.Short, 1);
            }
            else Emit(MatchEventType.Slip, p.Team, p, null, p.Pos, 0f, WhistleKind.Short, 0);
        }

        /// <summary>
        /// A lofted kick in a wind: the kicker aims off to allow for the drift (and kicks softer with the wind, harder
        /// into it). Nobody judges it perfectly - the better the technique, the more of the drift he takes out.
        /// </summary>
        private Vec2 AllowForWind(PlayerAgent p, Vec2 from, float startZ, Vec2 hv, float vz, float sideSpin, float topSpin, KickKind kind)
        {
            if (Config.WindX == 0f && Config.WindY == 0f) return hv;
            if (vz < 0.4f && startZ < 0.5f) return hv; // driven along the ground: the wind hardly touches it
            var still = Config.StillAir;
            var a = new BallMotion { X = from.X, Y = from.Y, Z = MathF.Max(Pitch.BallRadius, startZ) + 0.001f, Vx = hv.X, Vy = hv.Y, Vz = vz, SideSpin = sideSpin, TopSpin = topSpin };
            float dt = Config.FixedStep, t = 0f;
            for (int i = 0; i < 125; i++)
            {
                BallPhysics.Step(ref a, still, dt);
                t += dt;
                if (a.Z <= Pitch.BallRadius + 0.0005f && a.Vz <= 0.0001f) break;
            }
            if (t < 0.15f) return hv;
            Vec2 aim = new Vec2(a.X, a.Y);
            var b = new BallMotion { X = from.X, Y = from.Y, Z = MathF.Max(Pitch.BallRadius, startZ) + 0.001f, Vx = hv.X, Vy = hv.Y, Vz = vz, SideSpin = sideSpin, TopSpin = topSpin };
            for (float s = 0f; s < t - 1e-4f; s += dt) BallPhysics.Step(ref b, Config, dt);
            Vec2 miss = new Vec2(b.X, b.Y) - aim;
            int skill = p.IsGoalkeeper ? p.Attr.Kicking : kind == KickKind.Shot ? p.Attr.LongShots : kind == KickKind.Cross ? p.Attr.Crossing : p.Attr.Passing;
            float judged = 0.5f + 0.4f * MathUtil.A(skill);
            if (CollectDiagnostics) Count("wind.allowance");
            return hv - miss / t * judged;
        }
    }
}

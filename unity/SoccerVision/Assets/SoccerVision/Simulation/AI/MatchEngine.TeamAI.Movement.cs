using System;
using System.Collections.Generic;
using System.Linq;

namespace SoccerVision.Simulation
{
    /// <summary>Locomotion: how a player moves towards his target (speed, jumps, energy), headers anticipated.</summary>
    public sealed partial class MatchEngine
    {
        private void MovePlayer(PlayerAgent p, float dt)
        {
            if (!p.SentOff && p.IsDown)
            {
                // Sliding: carried along the ground by the lunge; then lying there for a moment and getting up.
                if (p.SlideTimer > 0f) p.Vel = p.SlideDir * p.SlideSpeed;
                else p.Vel = p.Vel.ClampLength(MathF.Max(0f, p.Vel.Length - 14f * dt));
                p.Pos = Pitch.ClampToField(p.Pos + p.Vel * dt, 1.5f);
                p.DistanceCovered += p.Vel.Length * dt;
                UpdateEnergy(p, p.Vel.Length, dt);
                return;
            }
            if (p.SentOff)
            {
                // Walk off the pitch over the nearest touchline.
                p.Target = new Vec2(p.Pos.X, (Pitch.HalfWidth + 6f) * (p.Pos.Y >= 0f ? 1f : -1f));
                p.Urgency = 0.25f;
            }
            else if (Phase == MatchPhase.Live && Ball.InHands && Ball.Owner != null && Ball.Owner.Team != p.Team)
            {
                // Opponents drift away from a goalkeeper holding the ball: they may not challenge him or block the release.
                var gk = Ball.Owner;
                const float keep = 10f;
                Vec2 away = p.Target - gk.Pos;
                if (away.Length < keep)
                {
                    Vec2 dir = away.Length > 0.5f ? away.Normalized : (p.Pos - gk.Pos).Length > 0.5f ? (p.Pos - gk.Pos).Normalized : gk.Team.DirToWorld(Vec2.UnitX);
                    p.Target = gk.Pos + dir * keep;
                    p.Urgency = MathF.Max(p.Urgency, 0.4f);
                }
            }

            Vec2 toTarget = p.Target - p.Pos;
            float dist = toTarget.Length;
            float u = MathUtil.Clamp01(p.Urgency);
            float vmax = p.MaxSpeed;
            // Wet grass, snow and mud: less grip to accelerate, brake and turn on, and a little less pace.
            float footing = Footing(p.Pos);
            if (footing < 1f) vmax *= 1f - 0.45f * (1f - footing);
            float accelBoost = 1f;
            if (p.DiveTimer > 0f)
            {
                // Explosive dive: the keeper launches sideways faster than he can run.
                vmax *= 1.3f + 0.15f * MathUtil.A(p.Attr.Reflexes);
                accelBoost = 2.6f;
            }
            const float walk = 1.5f, jog = 3.9f;
            float cap = u < 0.5f ? MathUtil.Lerp(walk, jog, u / 0.5f) : MathUtil.Lerp(jog, vmax, (u - 0.5f) / 0.5f);
            if (Ball.Owner == p && !Ball.InHands)
            {
                cap = MathF.Min(cap, p.DribbleSpeed);
                // Physical pressure: defenders tight on the carrier slow him down (shoulder-to-shoulder).
                int tight = 0;
                foreach (var o in p.Team.Opponent.Players)
                {
                    if (!o.SentOff && o.StunTimer <= 0f && Vec2.DistanceSq(o.Pos, p.Pos) < 1.3f * 1.3f) tight++;
                }
                if (tight > 0) cap *= MathF.Max(0.55f, 1f - 0.22f * tight * (1.15f - 0.3f * MathUtil.A(p.Attr.PhysicalPower)));
            }
            if (p.HasPendingPlan) cap = MathF.Min(cap, 3.5f);
            if (p.StunTimer > 0f) cap = MathF.Min(cap, 1.5f);

            float decel = p.MaxDecel;
            float desiredSpeed = dist < 0.25f ? 0f : MathF.Min(cap, MathF.Sqrt(2f * decel * 0.55f * dist));
            Vec2 desired = dist > 1e-4f ? toTarget / dist * desiredSpeed : Vec2.Zero;

            // Separation keeps players from overlapping (teammates more, opponents less so duels still happen).
            if (!p.SentOff && Phase != MatchPhase.HalfTimeBreak)
            {
                Vec2 push = Vec2.Zero;
                foreach (var q in AllPlayers)
                {
                    if (q == p || q.SentOff) continue;
                    float radius = q.Team == p.Team ? 1.4f : 0.85f;
                    float strength = 3f;
                    if (q.Team != p.Team && q.Task == PlayerTask.Shield)
                    {
                        // A player shielding the ball holds the opponent off with his body (arm out, side on).
                        radius = 1.3f;
                        strength = 3f * MathUtil.Clamp(1.2f + 2f * (BodyStrength(q) - BodyStrength(p)), 0.5f, 2f);
                    }
                    Vec2 d = p.Pos - q.Pos;
                    float dl = d.LengthSq;
                    if (dl < radius * radius && dl > 1e-6f)
                    {
                        float len = MathF.Sqrt(dl);
                        push += d / len * ((radius - len) / radius) * strength;
                    }
                }
                desired += push;
            }

            // Airborne players cannot change direction: no ground contact until they land.
            if (p.JumpZ > 0f && p.DiveTimer <= 0f) desired = p.Vel;
            Vec2 dv = desired - p.Vel;
            bool accelerating = Vec2.Dot(desired, p.Vel) >= p.Vel.LengthSq * 0.95f;
            float maxDv = (accelerating ? p.MaxAccel : decel) * accelBoost * dt;
            if (footing < 1f)
            {
                maxDv *= 0.5f + 0.5f * footing;
                MaybeSlip(p, footing, p.Vel.Length, vmax, dv.Length, dt);
            }
            p.Vel += dv.ClampLength(maxDv);
            float speed = p.Vel.Length;
            if (speed > vmax) { p.Vel = p.Vel / speed * vmax; speed = vmax; }
            p.Pos += p.Vel * dt;
            // (a corner taker steps back off the pitch for his run-up)
            p.Pos = Pitch.ClampToField(p.Pos, p.SentOff ? 8f : (Phase == MatchPhase.Stoppage && _restart != null && _restart.Taker == p ? 3.5f : 1.5f));
            p.DistanceCovered += speed * dt;

            if (p.KickTimer > 0f) p.KickTimer -= dt;
            // Facing: run direction when moving, otherwise the ball. A pending kick turns the body first.
            float targetFacing;
            float turnRate;
            if (Phase == MatchPhase.Stoppage && _restart != null && _restart.Taker == p && RestartFacing(_restart, p, out float restartFacing))
            {
                // the player taking a throw-in, corner, goal kick or free kick faces the way he will play it, into the field
                targetFacing = restartFacing;
                turnRate = 7f;
            }
            else if (p.HasPendingPlan)
            {
                targetFacing = p.PendingPlan.Heading;
                turnRate = 14f;
            }
            else if (speed > 1.2f && !(p.FaceBall && speed < 2.5f))
            {
                targetFacing = p.Vel.Angle;
                turnRate = 9f;
            }
            else if (Ball.Owner != p)
            {
                targetFacing = (Ball.Pos - p.Pos).Angle;
                turnRate = 6f;
            }
            else
            {
                targetFacing = p.Facing;
                turnRate = 6f;
            }
            if (footing < 1f) turnRate *= 0.55f + 0.45f * footing;
            if (p.HeelUntil > SimTime) { targetFacing = speed > 1.2f ? p.Vel.Angle : p.Facing; turnRate = 5f; } // (after a heel flick he keeps looking forward, he does not turn to watch it go)
            p.Facing = MathUtil.MoveTowardsAngle(p.Facing, targetFacing, turnRate * dt);

            // Jump: ballistic flight of the body (headers, goalkeeper takes, walls, celebrations).
            if (p.JumpZ > 0f || p.JumpVz > 0f)
            {
                p.JumpZ += p.JumpVz * dt;
                p.JumpVz -= JumpGravity * dt;
                if (p.JumpZ <= 0f) { p.JumpZ = 0f; p.JumpVz = 0f; }
            }

            UpdateEnergy(p, speed, dt);
        }

        /// <summary>
        /// Gravity for the body's jump, in sim time. Players move at MotionSpeed x real time, so true gravity would make
        /// a leap last a blink on screen; dividing by MotionSpeed gives a visible hang time of about 0.45-0.6 real
        /// seconds for a 0.4-0.7 m leap (a real one lasts 0.55-0.75 s).
        /// </summary>
        private float JumpGravity => Config.Gravity / Config.MotionSpeed;

        /// <summary>Highest standing leap of the centre of mass (m) from the Jumping attribute (0.35-0.75 m).</summary>
        private static float MaxJumpHeight(PlayerAgent p) => 0.35f + 0.4f * MathUtil.A(p.Attr.Jumping) + (p.IsGoalkeeper ? 0.3f : 0f); // (a keeper goes up for a ball in the top corner)

        /// <summary>Take off for a leap of <paramref name="height"/> metres: v0 = sqrt(2 g h).</summary>
        private void StartJump(PlayerAgent p, float height)
        {
            if (p.JumpZ > 0f || p.SentOff) return;
            float h = MathUtil.Clamp(height, 0.1f, MaxJumpHeight(p));
            p.JumpVz = MathF.Sqrt(2f * JumpGravity * h);
            p.JumpZ = 0.001f;
            if (p.DiveTimer <= 0f) p.Vel *= 0.55f; // plant and take off
        }

        /// <summary>Time from take-off to the top of a leap of the given height.</summary>
        private float TimeToApex(float height) => MathF.Sqrt(2f * height / JumpGravity);

        /// <summary>
        /// Anticipated leaps: a player under a high ball takes off early so that the top of his jump meets the ball,
        /// like a real header, instead of hopping after the contact.
        /// </summary>
        private void AnticipateHeaders()
        {
            if (Ball.Owner != null || Ball.InGoal || Ball.Z < 1.2f && Ball.M.Vz <= 0f) return;
            Vec2 bv = new Vec2(Ball.M.Vx, Ball.M.Vy);
            float bv2 = bv.LengthSq;
            if (bv2 < 4f) return;
            foreach (var p in AllPlayers)
            {
                if (p.SentOff || p.IsGoalkeeper || p.JumpZ > 0f || p.StunTimer > 0f) continue;
                Vec2 rel = p.Pos - Ball.Pos;
                float tc = Vec2.Dot(rel, bv) / bv2;
                bool charger = p.Has(PlayerTrait.AerialCharger); // he goes for every high ball: further, and stretches for it
                if (tc <= 0f || tc > (charger ? 1.5f : 1.2f)) continue;
                Vec2 closest = Ball.Pos + bv * tc;
                if (Vec2.Distance(closest, p.Pos + p.Vel * tc) > (charger ? 1.7f : 1.1f)) continue;
                // Ball height at that moment (drag ignored over such a short time).
                float z = Ball.Z + Ball.M.Vz * tc - 0.5f * Config.Gravity * tc * tc;
                if (z < 1.75f || z > p.HeadReach + (charger ? 0.35f : 0.15f)) continue;
                float need = MathUtil.Clamp(z - 1.75f + 0.15f, 0.2f, MaxJumpHeight(p));
                if (tc <= TimeToApex(need) + 0.02f) StartJump(p, need);
            }
        }

        /// <summary>How attacking a full-back is by nature (0..1): crossing, pace, stamina and running off the ball.</summary>
        public static float FullBackDrive(PlayerAgent p) =>
            MathUtil.Clamp01((p.Attr.Crossing + p.Attr.Speed + p.Attr.Stamina + p.Attr.OffTheBall) / 400f);

        /// <summary>
        /// How hard a position works, as a multiplier on the energy drain. Defenders run all the time (full-backs up and
        /// down the flank, centre-backs stepping out and recovering), wide midfielders and central midfielders cover the
        /// most ground, strikers press in bursts, and a goalkeeper hardly tires.
        /// </summary>
        public static float WorkRate(PlayerAgent p)
        {
            var slot = p.Slot;
            if (p.IsGoalkeeper) return 0.35f;
            if (slot.IsFullBack) return 1.25f;
            if (slot.IsWideMidfielder) return 1.15f;
            if (slot.IsCentralMidfielder) return 1.1f;
            if (slot.IsCentreBack) return 1.05f;
            return 0.95f;
        }

        private int _personalStep = -1;

        private void UpdateEnergy(PlayerAgent p, float speed, float dt)
        {
            float clockDt = dt * Config.ClockSecondsPerSimSecond;
            float intensity = speed / MathF.Max(1f, p.MaxSpeed);
            float staminaFactor = 1.35f - 0.7f * MathUtil.A(p.Attr.Stamina);
            float drain = (0.00005f + 0.00066f * intensity * intensity * intensity) * staminaFactor;
            drain *= WorkRate(p) * p.Team.Tactics.EnergyDrain * (p.Team == Away ? 1f + 0.03f * Config.HomeAdvantage : 1f); // (the travel)
            if ((p.Profile.Traits & (PlayerTrait.PressesHard | PlayerTrait.SavesEnergy | PlayerTrait.Fragile)) != 0)
                drain *= (p.Has(PlayerTrait.PressesHard) ? 1.1f : 1f) * (p.Has(PlayerTrait.SavesEnergy) ? 0.9f : 1f) * (p.Has(PlayerTrait.Fragile) ? 1.12f : 1f);
            if (StepCount != _personalStep) { _personalStep = StepCount; UpdateBigGameBonus(); }
            UpdatePersonalState(p, clockDt);
            float recover = speed < 2.2f ? 0.00004f : 0f;
            p.Energy = MathUtil.Clamp(p.Energy - (drain - recover) * clockDt, 0.25f, 1f);
            MaybeStrain(p, intensity, clockDt);
        }
    }
}

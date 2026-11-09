using System;

namespace SoccerVision.Simulation
{
    /// <summary>Raw 3D ball motion state (Z is the height of the ball centre, never below the radius).</summary>
    public struct BallMotion
    {
        public float X, Y, Z;
        public float Vx, Vy, Vz;
        /// <summary>Spin around the vertical axis (rad/s, positive curls to the left of travel). Drives the Magnus curl.</summary>
        public float SideSpin;
        /// <summary>Topspin (rad/s, positive dips the ball).</summary>
        public float TopSpin;
        /// <summary>
        /// The ball's angular velocity in world axes (rad/s, X along the pitch, Y across it, Z up), used by the physical flight model
        /// (<see cref="MatchConfig.Aero"/>). A kick sets <see cref="SideSpin"/> and <see cref="TopSpin"/> (relative to the direction it flies
        /// in); the first physical step turns them into this vector once (<see cref="HasWorldSpin"/>) and from then on the vector alone is the spin.
        /// </summary>
        public float Wx, Wy, Wz;
        public bool HasWorldSpin;

        public Vec2 Position2D => new Vec2(X, Y);
        public Vec2 Velocity2D => new Vec2(Vx, Vy);
        public float HorizontalSpeed => MathF.Sqrt(Vx * Vx + Vy * Vy);
        public float Speed => MathF.Sqrt(Vx * Vx + Vy * Vy + Vz * Vz);
        public bool IsAirborne => Z > Pitch.BallRadius + 0.005f || Vz > 0.05f;

        /// <summary>Angular velocity (rad/s) as a vector, whichever representation holds the spin.</summary>
        public Vec3 AngularVelocity => HasWorldSpin ? new Vec3(Wx, Wy, Wz) : KickFrameSpin();

        /// <summary>
        /// The spin a kick set (side spin about the vertical axis, top spin about the axis across the direction of travel) as a world
        /// vector, using the horizontal direction the ball is moving in now. A ball with no horizontal motion cannot have top spin.
        /// </summary>
        private Vec3 KickFrameSpin()
        {
            float hs = HorizontalSpeed;
            if (hs < 1e-4f || TopSpin == 0f) return new Vec3(0f, 0f, SideSpin);
            // Top spin: the top of the ball moves forward, w = (z x heading) * TopSpin; heading (hx, hy) -> z x h = (-hy, hx, 0)
            return new Vec3(-Vy / hs * TopSpin, Vx / hs * TopSpin, SideSpin);
        }

        /// <summary>Turns the kick's spin into the world vector (once) and clears the two scalars, so nothing reads the spin twice.</summary>
        public void AdoptWorldSpin()
        {
            if (HasWorldSpin) return;
            var w = KickFrameSpin();
            Wx = w.X; Wy = w.Y; Wz = w.Z;
            SideSpin = 0f; TopSpin = 0f;
            HasWorldSpin = true;
        }

        /// <summary>Clears all spin, in both representations.</summary>
        public void ClearSpin()
        {
            SideSpin = 0f; TopSpin = 0f;
            Wx = 0f; Wy = 0f; Wz = 0f;
            HasWorldSpin = false;
        }
    }

    /// <summary>
    /// Ball integrator shared by the live simulation and every prediction (passing, interception, shots),
    /// so AI plans always agree with what the ball really does.
    /// Model: gravity + quadratic air drag + Magnus (curl/dip) in flight; bounce with restitution;
    /// rolling resistance + grass drag on the ground.
    /// </summary>
    public static class BallPhysics
    {
        private const float BounceThreshold = 1.1f; // below this vertical impact speed the ball starts to roll

        public static void Step(ref BallMotion b, MatchConfig c, float dt)
        {
            const float r = Pitch.BallRadius;
            bool rolling = b.Z <= r + 0.0005f && b.Vz <= 0.0001f;

            if (!rolling)
            {
                // Air drag works on the speed through the air: with wind the ball drifts and carries with it.
                float rvx = b.Vx - c.WindX, rvy = b.Vy - c.WindY;
                float speed = MathF.Sqrt(rvx * rvx + rvy * rvy + b.Vz * b.Vz);
                float ax, ay, az;

                if (c.Aero != null)
                {
                    FlightAcceleration(ref b, c, rvx, rvy, speed, out ax, out ay, out az);
                }
                else
                {
                    float drag = c.AirDragK * speed;
                    ax = -drag * rvx;
                    ay = -drag * rvy;
                    az = -c.Gravity - drag * b.Vz;

                    float hs = b.HorizontalSpeed;
                    if (hs > 0.5f)
                    {
                        float lateral = c.MagnusK * b.SideSpin * speed;
                        ax += lateral * (-b.Vy / hs);
                        ay += lateral * (b.Vx / hs);
                        az -= c.MagnusK * b.TopSpin * speed;
                    }
                }

                b.Vx += ax * dt;
                b.Vy += ay * dt;
                b.Vz += az * dt;
                b.X += b.Vx * dt;
                b.Y += b.Vy * dt;
                b.Z += b.Vz * dt;

                if (b.Z <= r)
                {
                    b.Z = r;
                    if (b.Vz < -BounceThreshold && c.Ground != null)
                    {
                        Bounce(ref b, c);
                    }
                    else if (b.Vz < -BounceThreshold)
                    {
                        float restitution = c.GroundRestitution, friction = c.BounceFriction;
                        if (c.Surface != null)
                        {
                            // A ball landing in mud plugs; in a puddle it splashes down and hardly bounces.
                            float mud = c.Surface.MudAt(b.X, b.Y), water = c.Surface.WaterAt(b.X, b.Y);
                            restitution *= 1f - 0.45f * mud - 0.75f * water;
                            friction *= 1f - 0.15f * mud - 0.35f * water;
                        }
                        b.Vz = -b.Vz * restitution;
                        b.Vx *= friction;
                        b.Vy *= friction;
                        b.SideSpin *= 0.5f;
                        b.TopSpin *= 0.5f;
                        b.Wx *= 0.5f; b.Wy *= 0.5f; b.Wz *= 0.5f; // (the bounce is stage 4: until then the ground still takes half the spin, as before)
                    }
                    else
                    {
                        b.Vz = 0f;
                    }
                }
            }
            else
            {
                b.Z = r;
                b.Vz = 0f;
                float hs = b.HorizontalSpeed;
                if (hs > 0f)
                {
                    float decel = c.RollingDecel + c.GrassLinearDrag * hs + c.AirDragK * hs * hs;
                    if (c.Surface != null) decel += c.Surface.ExtraRollingDecel(b.X, b.Y, hs);
                    float newHs = MathF.Max(0f, hs - decel * dt);
                    float scale = newHs / hs;
                    b.Vx *= scale;
                    b.Vy *= scale;
                }
                b.X += b.Vx * dt;
                b.Y += b.Vy * dt;
                b.SideSpin *= MathF.Max(0f, 1f - 3f * dt);
                b.TopSpin = 0f;
                if (b.HasWorldSpin)
                {
                    float roll = MathF.Max(0f, 1f - 3f * dt);
                    b.Wz *= roll; b.Wx = 0f; b.Wy = 0f; // (rolling: only the spin about the vertical axis is left, as before; the rolling model is stage 5)
                }
            }

            if (c.Aero != null)
            {
                if (b.HasWorldSpin)
                {
                    float wd = MathF.Exp(-c.Aero.SpinDecayPerSecond * dt);
                    b.Wx *= wd; b.Wy *= wd; b.Wz *= wd;
                }
                float sd = MathF.Max(0f, 1f - c.Aero.SpinDecayPerSecond * dt);
                b.SideSpin *= sd;
                b.TopSpin *= sd;
            }
            else
            {
                float decay = MathF.Max(0f, 1f - c.SpinDecayPerSecond * dt);
                b.SideSpin *= decay;
                b.TopSpin *= decay;
            }
        }

        /// <summary>
        /// A bounce as an impulse at the contact point (stage 4). Normal: the vertical speed comes back times e(impact speed, surface).
        /// Tangential: friction at the contact point acts against its slip (the ball's horizontal velocity plus the velocity its spin gives the
        /// bottom of the ball), up to the Coulomb limit mu x the normal impulse; if that is enough the bottom of the ball stops slipping
        /// (it grips and leaves rolling), otherwise it slides through. The friction impulse also changes the spin: L = I w with I = k m R^2.
        /// A ball with no spin landing at an angle leaves with topspin; backspin bites (the ball checks); topspin kicks on. The spin about the
        /// vertical axis is untouched (a bounce cannot reach it through a contact at the bottom).
        /// </summary>
        public static void Bounce(ref BallMotion b, MatchConfig c)
        {
            b.AdoptWorldSpin();
            var g = c.Ground;
            float vn = -b.Vz;                                          // normal impact speed, > 0
            float e = g.RestitutionAt(vn), mu = g.Friction;
            if (c.Surface != null)
            {
                // A ball landing in mud plugs; in a puddle it splashes down and hardly bounces (as before, class D).
                float mud = c.Surface.MudAt(b.X, b.Y), water = c.Surface.WaterAt(b.X, b.Y);
                e *= 1f - 0.45f * mud - 0.75f * water;
                mu *= 1f - 0.15f * mud - 0.35f * water;
            }
            float R = c.Ball.RadiusM, k = c.Ball.InertiaFactor;
            // slip of the contact point (the ground does not move): v_t + w x r, r = (0, 0, -R)
            float ux = b.Vx - R * b.Wy, uy = b.Vy + R * b.Wx;
            float us = MathF.Sqrt(ux * ux + uy * uy);
            if (us > 1e-6f)
            {
                float toGrip = us * k / (1f + k);                         // the speed change that stops the slip
                float limit = mu * (1f + e) * vn;                          // what friction can give (per unit mass)
                float dv = MathF.Min(toGrip, limit);
                float jx = -ux / us * dv, jy = -uy / us * dv;              // tangential impulse / m
                b.Vx += jx;
                b.Vy += jy;
                // dw = (r x J) / I = (R J_y, -R J_x, 0) / (k m R^2)
                b.Wx += jy / (k * R);
                b.Wy -= jx / (k * R);
            }
            b.Vz = e * vn;
        }

        /// <summary>
        /// The acceleration of a ball in the air (physical flight model): gravity, drag 1/2 rho Cd(Re, Sp) A v^2 against the airflow, and the
        /// Magnus force 1/2 rho Cl(Sp) A v^2 along (w x v) / |w x v| - perpendicular to both the spin and the airflow, so backspin lifts,
        /// topspin dips and sidespin curves, and spin about the line of flight does nothing. All in SI; v is the velocity relative to the air.
        /// </summary>
        public static void FlightAcceleration(ref BallMotion b, MatchConfig c, float rvx, float rvy, float speed, out float ax, out float ay, out float az)
        {
            var ball = c.Ball;
            b.AdoptWorldSpin();
            float rvz = b.Vz;
            ax = 0f; ay = 0f; az = -c.Gravity;
            if (speed < 1e-3f) return;

            float inv = 1f / speed;
            float ux = rvx * inv, uy = rvy * inv, uz = rvz * inv;           // the airflow direction (the ball's, relative to the air)
            float wx = b.Wx, wy = b.Wy, wz = b.Wz;
            // spin perpendicular to the flight: w x u has the magnitude |w_perp|
            float cx = wy * uz - wz * uy, cy = wz * ux - wx * uz, cz = wx * uy - wy * ux;
            float wPerp = MathF.Sqrt(cx * cx + cy * cy + cz * cz);
            float sp = ball.RadiusM * wPerp * inv;                            // the spin parameter R w_perp / v

            float re = AeroModel.ReynoldsNumber(c.AirDensity, speed, 2f * ball.RadiusM, c.AirDynamicViscosity);
            float beta = 0.5f * c.AirDensity * ball.CrossSectionAreaM2 / ball.MassKg; // rho A / 2m, 1/m
            float cd = c.Aero.Cd(re, sp);
            float drag = beta * cd * speed * speed;
            ax -= drag * ux; ay -= drag * uy; az -= drag * uz;

            if (wPerp > 1e-3f)
            {
                float magnus = beta * c.Aero.Cl(sp) * speed * speed / wPerp;
                ax += magnus * cx; ay += magnus * cy; az += magnus * cz;
            }
        }
    }

    /// <summary>
    /// Pre-integrated rolling behaviour of a ground ball. Because the rolling equation is autonomous, the
    /// distance travelled between two speeds is StopDistance(v0) - StopDistance(v1); this gives exact
    /// (same integrator) answers to "how hard must I pass so the ball arrives at 6 m/s after 25 m?".
    /// </summary>
    public sealed class GroundRollTable
    {
        private const float SpeedStep = 0.25f;
        public const float MaxSpeed = 45f;
        private readonly float[] _stopDistance; // index k -> speed k*SpeedStep
        private readonly float[] _stopTime;

        public GroundRollTable(MatchConfig c)
        {
            int n = (int)(MaxSpeed / SpeedStep) + 1;
            _stopDistance = new float[n];
            _stopTime = new float[n];

            // Integrate once from MaxSpeed to rest with the live integrator's discretisation.
            float dt = c.FixedStep;
            int cap = 200000;
            var speeds = new float[cap];
            var dists = new float[cap];
            var times = new float[cap];
            float v = MaxSpeed, d = 0f, t = 0f;
            int count = 0;
            speeds[count] = v; dists[count] = d; times[count] = t; count++;
            while (v > 0f && count < cap)
            {
                float decel = c.RollingDecel + c.GrassLinearDrag * v + c.AirDragK * v * v;
                v = MathF.Max(0f, v - decel * dt);
                d += v * dt;
                t += dt;
                speeds[count] = v; dists[count] = d; times[count] = t; count++;
            }
            float totalD = d, totalT = t;

            // Sample on a uniform speed grid (speeds[] is strictly decreasing until 0).
            int j = 0;
            for (int k = n - 1; k >= 0; k--)
            {
                float s = k * SpeedStep;
                while (j < count - 1 && speeds[j + 1] > s) j++;
                float s0 = speeds[j];
                float s1 = j < count - 1 ? speeds[j + 1] : 0f;
                float f = s0 - s1 > 1e-6f ? (s0 - s) / (s0 - s1) : 0f;
                float dAt = dists[j] + (j < count - 1 ? (dists[j + 1] - dists[j]) * f : 0f);
                float tAt = times[j] + (j < count - 1 ? (times[j + 1] - times[j]) * f : 0f);
                _stopDistance[k] = MathF.Max(0f, totalD - dAt);
                _stopTime[k] = MathF.Max(0f, totalT - tAt);
            }
            _stopDistance[0] = 0f;
            _stopTime[0] = 0f;
        }

        private static float Lookup(float[] table, float speed)
        {
            if (speed <= 0f) return 0f;
            float idx = speed / SpeedStep;
            int i = (int)idx;
            if (i >= table.Length - 1) return table[table.Length - 1];
            float f = idx - i;
            return table[i] + (table[i + 1] - table[i]) * f;
        }

        private static float Inverse(float[] table, float value)
        {
            if (value <= 0f) return 0f;
            int lo = 0, hi = table.Length - 1;
            if (value >= table[hi]) return MaxSpeed;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (table[mid] < value) lo = mid; else hi = mid;
            }
            float span = table[hi] - table[lo];
            float f = span > 1e-6f ? (value - table[lo]) / span : 0f;
            return (lo + f) * SpeedStep;
        }

        public float StopDistance(float speed) => Lookup(_stopDistance, speed);
        public float StopTime(float speed) => Lookup(_stopTime, speed);

        /// <summary>Speed the ball has left after rolling <paramref name="distance"/> metres (0 if it stops first).</summary>
        public float SpeedAfterDistance(float v0, float distance)
        {
            float remaining = StopDistance(v0) - distance;
            return remaining <= 0f ? 0f : Inverse(_stopDistance, remaining);
        }

        /// <summary>Seconds to roll <paramref name="distance"/> metres, or +inf if the ball stops before.</summary>
        public float TimeToDistance(float v0, float distance)
        {
            if (distance <= 0f) return 0f;
            float total = StopDistance(v0);
            if (total < distance) return float.PositiveInfinity;
            float vAt = Inverse(_stopDistance, total - distance);
            return StopTime(v0) - StopTime(vAt);
        }

        public float DistanceAfterTime(float v0, float time)
        {
            float remaining = StopTime(v0) - time;
            if (remaining <= 0f) return StopDistance(v0);
            float vAt = Inverse(_stopTime, remaining);
            return StopDistance(v0) - StopDistance(vAt);
        }

        /// <summary>Kick speed so that after <paramref name="distance"/> metres the ball still moves at <paramref name="arrivalSpeed"/>.</summary>
        public float KickSpeedFor(float distance, float arrivalSpeed)
        {
            return Inverse(_stopDistance, distance + StopDistance(arrivalSpeed));
        }

        /// <summary>Kick speed so the ball covers <paramref name="distance"/> in about <paramref name="time"/> seconds.</summary>
        public float KickSpeedForTime(float distance, float time, float minArrivalSpeed)
        {
            float lo = KickSpeedFor(distance, minArrivalSpeed);
            float hi = MaxSpeed;
            if (TimeToDistance(lo, distance) <= time) return lo;
            for (int i = 0; i < 24; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (TimeToDistance(mid, distance) > time) lo = mid; else hi = mid;
            }
            return hi;
        }
    }

    /// <summary>Forward prediction of the free ball (no players, posts or lines) sampled at a fixed interval.</summary>
    public sealed class BallTrajectory
    {
        public const float SampleInterval = 0.06f;
        public const float Horizon = 6f;
        private readonly int _capacity;
        private readonly float[] _x, _y, _z;
        public int Count { get; private set; }
        public int Version { get; private set; }

        public BallTrajectory()
        {
            _capacity = (int)(Horizon / SampleInterval) + 2;
            _x = new float[_capacity];
            _y = new float[_capacity];
            _z = new float[_capacity];
        }

        public void Build(BallMotion start, MatchConfig c)
        {
            Version++;
            Count = 0;
            float dt = c.FixedStep;
            int stepsPerSample = Math.Max(1, (int)MathF.Round(SampleInterval / dt));
            var b = start;
            Record(b);
            int idleSamples = 0;
            while (Count < _capacity)
            {
                for (int i = 0; i < stepsPerSample; i++) BallPhysics.Step(ref b, c, dt);
                Record(b);
                if (!b.IsAirborne && b.HorizontalSpeed < 0.01f)
                {
                    if (++idleSamples > 1) break;
                }
            }
        }

        private void Record(BallMotion b)
        {
            if (Count >= _capacity) return;
            _x[Count] = b.X;
            _y[Count] = b.Y;
            _z[Count] = b.Z;
            Count++;
        }

        public float TimeAt(int i) => i * SampleInterval;
        public Vec2 PositionAt(int i) => new Vec2(_x[i], _y[i]);
        public float HeightAt(int i) => _z[i];
        public Vec2 FinalPosition => Count > 0 ? new Vec2(_x[Count - 1], _y[Count - 1]) : Vec2.Zero;

    }

    /// <summary>Numerical solvers that turn an intention (land here / hit that corner) into a kick velocity.</summary>
    public static class KickSolver
    {
        /// <summary>Simulates flight until the first ground contact and returns the horizontal carry distance.</summary>
        public static float CarryDistance(float speed, float launchAngle, MatchConfig c, out float flightTime, float topSpin = 0f)
        {
            c = c.StillAir;
            var b = new BallMotion
            {
                Z = Pitch.BallRadius + 0.001f,
                Vx = speed * MathF.Cos(launchAngle),
                Vz = speed * MathF.Sin(launchAngle),
                TopSpin = topSpin,
            };
            float dt = c.FixedStep;
            float t = 0f;
            for (int i = 0; i < 1000; i++)
            {
                BallPhysics.Step(ref b, c, dt);
                t += dt;
                if (b.Z <= Pitch.BallRadius + 0.0001f && i > 0) break;
            }
            flightTime = t;
            return b.X;
        }

        /// <summary>
        /// Where a ball kicked along +X at this speed and elevation, with this spin (the kick frame: side spin about the vertical axis, top spin
        /// across the direction of travel), first touches the ground, in still air. With spin the path is not a straight line in plan: the
        /// landing point is off to the side and a little short or long.
        /// </summary>
        public static Vec2 LandingPoint(float speed, float launchAngle, MatchConfig c, float sideSpin, float topSpin, out float flightTime, float startZ = Pitch.BallRadius + 0.001f)
        {
            c = c.StillAir;
            var b = new BallMotion { Z = startZ, Vx = speed * MathF.Cos(launchAngle), Vz = speed * MathF.Sin(launchAngle), SideSpin = sideSpin, TopSpin = topSpin };
            float dt = c.FixedStep, t = 0f;
            for (int i = 0; i < 1000; i++)
            {
                BallPhysics.Step(ref b, c, dt);
                t += dt;
                if (b.Z <= Pitch.BallRadius + 0.0001f && i > 0) break;
            }
            flightTime = t;
            return new Vec2(b.X, b.Y);
        }

        /// <summary>
        /// Speed for a lofted kick with this spin whose first bounce lands <paramref name="distance"/> metres from the kicker (the straight line
        /// to the landing point; <see cref="CurlYaw"/> gives the direction to aim in). Same as <see cref="LoftSpeedFor"/> when there is no spin.
        /// </summary>
        public static float LoftSpeedWithSpin(float distance, float launchAngle, MatchConfig c, float sideSpin, float topSpin, out float flightTime)
        {
            float lo = 3f, hi = 42f;
            for (int i = 0; i < 22; i++)
            {
                float mid = 0.5f * (lo + hi);
                float d = LandingPoint(mid, launchAngle, c, sideSpin, topSpin, out _).Length;
                if (d < distance) lo = mid; else hi = mid;
            }
            LandingPoint(hi, launchAngle, c, sideSpin, topSpin, out flightTime);
            return hi;
        }

        /// <summary>
        /// The angle (radians, counter-clockwise positive) to add to the direction of the target so that a lofted kick with this spin comes
        /// down on it: the curl bends the flight, so the kicker aims off to the side the ball is coming back from. -atan2 of the landing point.
        /// </summary>
        public static float CurlYaw(float speed, float launchAngle, MatchConfig c, float sideSpin, float topSpin)
        {
            var l = LandingPoint(speed, launchAngle, c, sideSpin, topSpin, out _);
            return -MathF.Atan2(l.Y, l.X);
        }

        /// <summary>
        /// For a drive (not a loft): the yaw to aim off by so that a ball struck at this horizontal speed and vertical speed, with this spin, has
        /// reached the target line <paramref name="distance"/> metres ahead (measured along the ground) on the line to the target.
        /// </summary>
        public static float CurlYawToReach(float horizontalSpeed, float startZ, float vz, float distance, MatchConfig c, float sideSpin, float topSpin)
        {
            c = c.StillAir;
            var b = new BallMotion { Z = MathF.Max(Pitch.BallRadius, startZ), Vx = horizontalSpeed, Vz = vz, SideSpin = sideSpin, TopSpin = topSpin };
            if (b.Z <= Pitch.BallRadius && b.Vz <= 0f) b.Vz = 0.0001f;
            float dt = c.FixedStep;
            for (int i = 0; i < 600; i++)
            {
                BallPhysics.Step(ref b, c, dt);
                if (b.X * b.X + b.Y * b.Y >= distance * distance) break;
                if (b.HorizontalSpeed < 0.5f) break;
            }
            return -MathF.Atan2(b.Y, b.X);
        }

        /// <summary>Speed for a lofted kick at <paramref name="launchAngle"/> whose first bounce lands at <paramref name="distance"/>.</summary>
        public static float LoftSpeedFor(float distance, float launchAngle, MatchConfig c, out float flightTime)
        {
            float lo = 3f, hi = 42f;
            flightTime = 0f;
            for (int i = 0; i < 22; i++)
            {
                float mid = 0.5f * (lo + hi);
                float d = CarryDistance(mid, launchAngle, c, out _);
                if (d < distance) lo = mid; else hi = mid;
            }
            CarryDistance(hi, launchAngle, c, out flightTime);
            return hi;
        }

        /// <summary>
        /// Vertical launch speed so a ball kicked with horizontal velocity <paramref name="horizontal"/> from
        /// <paramref name="start"/> is at height <paramref name="targetZ"/> when it has travelled <paramref name="distance"/>.
        /// </summary>
        public static float VerticalSpeedFor(Vec2 horizontal, float startZ, float distance, float targetZ, MatchConfig c, float sideSpin = 0f, float topSpin = 0f)
        {
            float lo = -3f, hi = 16f;
            for (int i = 0; i < 20; i++)
            {
                float mid = 0.5f * (lo + hi);
                float z = HeightAtDistance(horizontal, startZ, mid, distance, c, sideSpin, topSpin);
                if (z < targetZ) lo = mid; else hi = mid;
            }
            return 0.5f * (lo + hi);
        }

        public static float HeightAtDistance(Vec2 horizontal, float startZ, float vz, float distance, MatchConfig c, float sideSpin = 0f, float topSpin = 0f)
        {
            c = c.StillAir;
            var b = new BallMotion
            {
                Z = MathF.Max(Pitch.BallRadius, startZ),
                Vx = horizontal.X,
                Vy = horizontal.Y,
                Vz = vz,
                SideSpin = sideSpin,
                TopSpin = topSpin,
            };
            if (b.Z <= Pitch.BallRadius && b.Vz <= 0f) b.Vz = 0.0001f; // treat as kicked, not rolling
            float dt = c.FixedStep;
            float travelled = 0f;
            for (int i = 0; i < 600; i++)
            {
                float px = b.X, py = b.Y;
                BallPhysics.Step(ref b, c, dt);
                travelled += MathF.Sqrt((b.X - px) * (b.X - px) + (b.Y - py) * (b.Y - py));
                if (travelled >= distance) return b.Z;
                if (b.HorizontalSpeed < 0.5f) break;
            }
            return -10f; // never got there
        }
    }
}

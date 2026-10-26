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

        public Vec2 Position2D => new Vec2(X, Y);
        public Vec2 Velocity2D => new Vec2(Vx, Vy);
        public float HorizontalSpeed => MathF.Sqrt(Vx * Vx + Vy * Vy);
        public float Speed => MathF.Sqrt(Vx * Vx + Vy * Vy + Vz * Vz);
        public bool IsAirborne => Z > Pitch.BallRadius + 0.005f || Vz > 0.05f;
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
                float speed = b.Speed;
                float drag = c.AirDragK * speed;
                float ax = -drag * b.Vx;
                float ay = -drag * b.Vy;
                float az = -c.Gravity - drag * b.Vz;

                float hs = b.HorizontalSpeed;
                if (hs > 0.5f)
                {
                    float lateral = c.MagnusK * b.SideSpin * speed;
                    ax += lateral * (-b.Vy / hs);
                    ay += lateral * (b.Vx / hs);
                    az -= c.MagnusK * b.TopSpin * speed;
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
                    if (b.Vz < -BounceThreshold)
                    {
                        b.Vz = -b.Vz * c.GroundRestitution;
                        b.Vx *= c.BounceFriction;
                        b.Vy *= c.BounceFriction;
                        b.SideSpin *= 0.5f;
                        b.TopSpin *= 0.5f;
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
                    float newHs = MathF.Max(0f, hs - decel * dt);
                    float scale = newHs / hs;
                    b.Vx *= scale;
                    b.Vy *= scale;
                }
                b.X += b.Vx * dt;
                b.Y += b.Vy * dt;
                b.SideSpin *= MathF.Max(0f, 1f - 3f * dt);
                b.TopSpin = 0f;
            }

            float decay = MathF.Max(0f, 1f - c.SpinDecayPerSecond * dt);
            b.SideSpin *= decay;
            b.TopSpin *= decay;
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

        public Vec2 PositionAtTime(float t, out float z)
        {
            if (Count == 0) { z = Pitch.BallRadius; return Vec2.Zero; }
            float idx = t / SampleInterval;
            int i = (int)idx;
            if (i >= Count - 1) { z = _z[Count - 1]; return FinalPosition; }
            if (i < 0) { z = _z[0]; return PositionAt(0); }
            float f = idx - i;
            z = _z[i] + (_z[i + 1] - _z[i]) * f;
            return new Vec2(_x[i] + (_x[i + 1] - _x[i]) * f, _y[i] + (_y[i + 1] - _y[i]) * f);
        }
    }

    /// <summary>Numerical solvers that turn an intention (land here / hit that corner) into a kick velocity.</summary>
    public static class KickSolver
    {
        /// <summary>Simulates flight until the first ground contact and returns the horizontal carry distance.</summary>
        public static float CarryDistance(float speed, float launchAngle, MatchConfig c, out float flightTime, float topSpin = 0f)
        {
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

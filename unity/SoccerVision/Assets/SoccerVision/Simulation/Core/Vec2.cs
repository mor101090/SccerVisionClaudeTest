using System;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// Small immutable 2D vector in pitch metres. The simulation does not use UnityEngine types so the
    /// exact same code runs inside Unity, on a .NET server and in headless tests.
    /// </summary>
    [Serializable]
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        public readonly float X;
        public readonly float Y;

        public static readonly Vec2 Zero = new Vec2(0f, 0f);
        public static readonly Vec2 UnitX = new Vec2(1f, 0f);
        public static readonly Vec2 UnitY = new Vec2(0f, 1f);

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float Length => MathF.Sqrt(X * X + Y * Y);
        public float LengthSq => X * X + Y * Y;

        public Vec2 Normalized
        {
            get
            {
                float len = Length;
                return len > 1e-6f ? new Vec2(X / len, Y / len) : Zero;
            }
        }

        /// <summary>Vector rotated +90 degrees (counter-clockwise).</summary>
        public Vec2 Perp => new Vec2(-Y, X);

        /// <summary>Angle in radians measured from +X.</summary>
        public float Angle => MathF.Atan2(Y, X);

        public bool IsFinite => !(float.IsNaN(X) || float.IsNaN(Y) || float.IsInfinity(X) || float.IsInfinity(Y));

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator -(Vec2 a) => new Vec2(-a.X, -a.Y);
        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator *(float s, Vec2 a) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator /(Vec2 a, float s) => new Vec2(a.X / s, a.Y / s);
        public static bool operator ==(Vec2 a, Vec2 b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(Vec2 a, Vec2 b) => !(a == b);

        public static float Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;
        public static float Cross(Vec2 a, Vec2 b) => a.X * b.Y - a.Y * b.X;
        public static float Distance(Vec2 a, Vec2 b) => (a - b).Length;
        public static float DistanceSq(Vec2 a, Vec2 b) => (a - b).LengthSq;
        public static Vec2 Lerp(Vec2 a, Vec2 b, float t) => new Vec2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        public static Vec2 FromAngle(float radians) => new Vec2(MathF.Cos(radians), MathF.Sin(radians));

        public Vec2 Rotate(float radians)
        {
            float c = MathF.Cos(radians);
            float s = MathF.Sin(radians);
            return new Vec2(X * c - Y * s, X * s + Y * c);
        }

        public Vec2 ClampLength(float max)
        {
            float lenSq = LengthSq;
            if (lenSq <= max * max || lenSq < 1e-12f) return this;
            float len = MathF.Sqrt(lenSq);
            return new Vec2(X / len * max, Y / len * max);
        }

        /// <summary>Distance from point p to the segment a-b.</summary>
        public static float DistanceToSegment(Vec2 p, Vec2 a, Vec2 b, out float t)
        {
            Vec2 ab = b - a;
            float lenSq = ab.LengthSq;
            t = lenSq < 1e-9f ? 0f : MathUtil.Clamp01(Dot(p - a, ab) / lenSq);
            return Distance(p, a + ab * t);
        }

        public bool Equals(Vec2 other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Vec2 other && Equals(other);
        public override int GetHashCode() => (X.GetHashCode() * 397) ^ Y.GetHashCode();
        public override string ToString() => "(" + X.ToString("0.00") + ", " + Y.ToString("0.00") + ")";
    }
}

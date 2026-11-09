using System;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// Small immutable 3D vector (SI units: metres, m/s, rad/s, newtons...). Axes as the rest of the engine: X along the pitch,
    /// Y across it, Z up. Used by the ball's rotational physics (angular velocity, angular momentum, Magnus force direction).
    /// </summary>
    [Serializable]
    public readonly struct Vec3 : IEquatable<Vec3>
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public static readonly Vec3 Zero = new Vec3(0f, 0f, 0f);
        public static readonly Vec3 UnitX = new Vec3(1f, 0f, 0f);
        public static readonly Vec3 UnitY = new Vec3(0f, 1f, 0f);
        public static readonly Vec3 UnitZ = new Vec3(0f, 0f, 1f);

        public Vec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float Length => MathF.Sqrt(X * X + Y * Y + Z * Z);
        public float LengthSq => X * X + Y * Y + Z * Z;

        public Vec3 Normalized
        {
            get
            {
                float len = Length;
                return len > 1e-6f ? new Vec3(X / len, Y / len, Z / len) : Zero;
            }
        }

        public static float Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Vec3 Cross(Vec3 a, Vec3 b) => new Vec3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator -(Vec3 a) => new Vec3(-a.X, -a.Y, -a.Z);
        public static Vec3 operator *(Vec3 a, float s) => new Vec3(a.X * s, a.Y * s, a.Z * s);
        public static Vec3 operator *(float s, Vec3 a) => new Vec3(a.X * s, a.Y * s, a.Z * s);
        public static Vec3 operator /(Vec3 a, float s) => new Vec3(a.X / s, a.Y / s, a.Z / s);

        public bool Equals(Vec3 other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is Vec3 v && Equals(v);
        public override int GetHashCode() => ((X.GetHashCode() * 397) ^ Y.GetHashCode()) * 397 ^ Z.GetHashCode();
        public override string ToString() => "(" + X.ToString("0.000") + ", " + Y.ToString("0.000") + ", " + Z.ToString("0.000") + ")";
    }
}

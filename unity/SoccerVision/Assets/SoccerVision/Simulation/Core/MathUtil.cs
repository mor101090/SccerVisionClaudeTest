using System;

namespace SoccerVision.Simulation
{
    public static class MathUtil
    {
        public const float Pi = 3.14159265f;
        public const float TwoPi = 6.28318531f;
        public const float Deg2Rad = Pi / 180f;

        public static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
        public static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;

        public static float InverseLerp(float a, float b, float v)
        {
            if (MathF.Abs(b - a) < 1e-9f) return 0f;
            return Clamp01((v - a) / (b - a));
        }

        /// <summary>Maps v from [a,b] to [c,d], clamped.</summary>
        public static float Remap(float v, float a, float b, float c, float d) => Lerp(c, d, InverseLerp(a, b, v));

        public static float Sigmoid(float x) => 1f / (1f + MathF.Exp(-x));

        public static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = InverseLerp(edge0, edge1, x);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Wraps an angle into (-PI, PI].</summary>
        public static float WrapAngle(float a)
        {
            while (a > Pi) a -= TwoPi;
            while (a <= -Pi) a += TwoPi;
            return a;
        }

        public static float AngleDiff(float from, float to) => WrapAngle(to - from);

        public static float MoveTowardsAngle(float current, float target, float maxDelta)
        {
            float diff = AngleDiff(current, target);
            if (MathF.Abs(diff) <= maxDelta) return WrapAngle(target);
            return WrapAngle(current + MathF.Sign(diff) * maxDelta);
        }

        public static float MoveTowards(float current, float target, float maxDelta)
        {
            if (MathF.Abs(target - current) <= maxDelta) return target;
            return current + MathF.Sign(target - current) * maxDelta;
        }

        /// <summary>Attribute (0..100) to 0..1.</summary>
        public static float A(int attribute) => Clamp01(attribute / 100f);
    }
}

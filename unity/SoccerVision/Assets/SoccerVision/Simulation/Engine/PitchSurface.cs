using System;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// The state of the grass, cell by cell (1 m grid over the pitch and its run-offs): bare mud where a neglected pitch
    /// is worn, standing water where a wet pitch does not drain. Built once per match from Perlin noise (fractal sums of
    /// gradient noise, so the patches have soft, natural edges) shaped by where a pitch really wears: the goalmouths,
    /// the penalty spots, the centre circle, the middle of the pitch and the assistants' channels along the touchlines.
    /// The renderers draw exactly this map, so a puddle you see is a puddle the ball stops in.
    /// </summary>
    public sealed class PitchSurface
    {
        public const float Cell = 1f;
        public const float MinX = -54f, MinY = -36f;
        public const int Columns = 109, Rows = 73;

        /// <summary>0..1 bare mud per grid node (row-major, Y rows from MinY).</summary>
        public readonly float[] Mud = new float[Columns * Rows];
        /// <summary>0..1 standing water per grid node.</summary>
        public readonly float[] Water = new float[Columns * Rows];
        public readonly bool HasMud, HasWater;

        public PitchSurface(MatchConditions conditions, ulong seed)
        {
            var noise = new PerlinNoise(seed ^ 0x5EEDF00DUL);
            float neglect = 1f - MathUtil.Clamp01(conditions.Maintenance);
            bool rain = conditions.IsWet;
            for (int j = 0; j < Rows; j++)
            {
                for (int i = 0; i < Columns; i++)
                {
                    float x = MinX + i * Cell, y = MinY + j * Cell;
                    float mud = 0f;
                    if (neglect > 0.001f)
                    {
                        // worn turf + patchy noise against a threshold that falls as the pitch is neglected
                        float n = noise.Fbm(x / 9f, y / 9f, 5) * 0.5f + 0.5f;
                        float raw = Wear(x, y) * 1.1f + n * 0.65f - (1.45f - 0.85f * neglect);
                        mud = MathUtil.Clamp01(raw * 2.6f) * MathUtil.Clamp01(neglect * 2.2f);
                    }
                    float water = 0f;
                    if (rain)
                    {
                        // puddles lie in the low spots; compacted mud and poor drainage hold more of them
                        float n2 = noise.Fbm(x / 13f + 31.7f, y / 13f - 12.3f, 4) * 0.5f + 0.5f;
                        float raw = n2 + mud * 0.2f - (0.78f - 0.14f * neglect);
                        water = MathUtil.Clamp01(raw * 4.5f);
                    }
                    Mud[j * Columns + i] = mud;
                    Water[j * Columns + i] = water;
                    HasMud |= mud > 0f;
                    HasWater |= water > 0f;
                }
            }
        }

        /// <summary>How hard this spot is worn by a season of football (0..1).</summary>
        public static float Wear(float x, float y)
        {
            float ax = MathF.Abs(x), ay = MathF.Abs(y);
            float goalmouth = MathF.Exp(-Sq((ax - 49.5f) / 4.5f) - Sq(y / 7.5f));
            float spot = 0.55f * MathF.Exp(-Sq((ax - 41.5f) / 4f) - Sq(y / 5.5f));
            float centre = 0.55f * MathF.Exp(-(x * x + y * y) / 60f);
            float middle = 0.3f * MathF.Exp(-Sq(y / 13f)) * (1f - 0.4f * MathF.Exp(-Sq((ax - 52f) / 3f)));
            // the assistants run up and down one half each: assistant 1 on the -Y touchline (x > 0), assistant 2 on +Y (x < 0)
            float channel = 0.5f * MathF.Exp(-Sq((ay - 33f) / 1.6f)) * (y < 0f ? Smooth(x) : Smooth(-x));
            return MathUtil.Clamp01(goalmouth + spot + centre + middle + channel);
        }

        private static float Smooth(float x) => MathUtil.SmoothStep(-2f, 4f, x) * (1f - MathUtil.SmoothStep(46f, 53f, x));
        private static float Sq(float v) => v * v;

        public float MudAt(float x, float y) => HasMud ? Sample(Mud, x, y) : 0f;
        public float WaterAt(float x, float y) => HasWater ? Sample(Water, x, y) : 0f;

        private static float Sample(float[] a, float x, float y)
        {
            float fx = (x - MinX) / Cell, fy = (y - MinY) / Cell;
            if (fx < 0f) fx = 0f; else if (fx > Columns - 1.001f) fx = Columns - 1.001f;
            if (fy < 0f) fy = 0f; else if (fy > Rows - 1.001f) fy = Rows - 1.001f;
            int i = (int)fx, j = (int)fy;
            float u = fx - i, v = fy - j;
            int k = j * Columns + i;
            float top = a[k] + (a[k + 1] - a[k]) * u;
            float bottom = a[k + Columns] + (a[k + Columns + 1] - a[k + Columns]) * u;
            return top + (bottom - top) * v;
        }

        /// <summary>Extra rolling resistance (m/s^2) of mud and standing water under a ball rolling at <paramref name="speed"/>.</summary>
        public float ExtraRollingDecel(float x, float y, float speed)
        {
            float mud = MudAt(x, y), water = WaterAt(x, y);
            return mud * (1.6f + 0.08f * speed) + water * (5f + 0.6f * speed);
        }

        /// <summary>Byte copies (0..255) for the replay file.</summary>
        public int[] Bytes(float[] a)
        {
            var r = new int[a.Length];
            for (int k = 0; k < a.Length; k++) r[k] = (int)MathF.Round(MathUtil.Clamp01(a[k]) * 255f);
            return r;
        }
    }

    /// <summary>Classic 2D Perlin gradient noise with a seeded permutation, and its fractal (fBm) sum.</summary>
    public sealed class PerlinNoise
    {
        private readonly int[] _perm = new int[512];

        public PerlinNoise(ulong seed)
        {
            var rng = new Rng(seed);
            var p = new int[256];
            for (int i = 0; i < 256; i++) p[i] = i;
            for (int i = 255; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                (p[i], p[j]) = (p[j], p[i]);
            }
            for (int i = 0; i < 512; i++) _perm[i] = p[i & 255];
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        private static float Grad(int h, float x, float y)
        {
            switch (h & 7)
            {
                case 0: return x + y;
                case 1: return -x + y;
                case 2: return x - y;
                case 3: return -x - y;
                case 4: return x;
                case 5: return -x;
                case 6: return y;
                default: return -y;
            }
        }

        /// <summary>Noise in about [-1, 1].</summary>
        public float Noise(float x, float y)
        {
            int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
            float xf = x - xi, yf = y - yi;
            xi &= 255; yi &= 255;
            float u = Fade(xf), v = Fade(yf);
            int aa = _perm[_perm[xi] + yi], ab = _perm[_perm[xi] + yi + 1];
            int ba = _perm[_perm[xi + 1] + yi], bb = _perm[_perm[xi + 1] + yi + 1];
            float x1 = MathUtil.Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1f, yf), u);
            float x2 = MathUtil.Lerp(Grad(ab, xf, yf - 1f), Grad(bb, xf - 1f, yf - 1f), u);
            return MathUtil.Lerp(x1, x2, v);
        }

        /// <summary>Fractal sum of <paramref name="octaves"/> octaves (each twice the frequency, half the amplitude), about [-1, 1].</summary>
        public float Fbm(float x, float y, int octaves)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += Noise(x, y) * amp;
                norm += amp;
                amp *= 0.5f;
                x = x * 2f + 17.13f;
                y = y * 2f - 9.71f;
            }
            return MathUtil.Clamp(sum / norm * 1.6f, -1f, 1f);
        }
    }
}

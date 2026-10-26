using System;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// PCG32 random generator. We use our own generator (not System.Random / UnityEngine.Random) so a
    /// seed produces the identical match on every runtime: Unity Mono, IL2CPP/WebGL and a .NET server.
    /// </summary>
    public sealed class Rng
    {
        private ulong _state;
        private readonly ulong _inc;
        private bool _hasSpare;
        private float _spare;

        public Rng(ulong seed, ulong sequence = 0xDA3E39CB94B95BDBUL)
        {
            _inc = (sequence << 1) | 1UL;
            _state = 0UL;
            NextUInt();
            _state += seed;
            NextUInt();
        }

        public uint NextUInt()
        {
            ulong old = _state;
            _state = old * 6364136223846793005UL + _inc;
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        public float Range(float minInclusive, float maxExclusive) => minInclusive + (maxExclusive - minInclusive) * NextFloat();

        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));
        }

        public bool Chance(float probability) => NextFloat() < probability;

        /// <summary>Standard normal sample (Box-Muller).</summary>
        public float Gaussian()
        {
            if (_hasSpare)
            {
                _hasSpare = false;
                return _spare;
            }
            float u1 = MathF.Max(NextFloat(), 1e-7f);
            float u2 = NextFloat();
            float mag = MathF.Sqrt(-2f * MathF.Log(u1));
            _spare = mag * MathF.Sin(MathUtil.TwoPi * u2);
            _hasSpare = true;
            return mag * MathF.Cos(MathUtil.TwoPi * u2);
        }

        public float Gaussian(float mean, float stdDev) => mean + Gaussian() * stdDev;

        /// <summary>Picks an index with probability proportional to weights (non-positive weights are skipped).</summary>
        public int PickWeighted(float[] weights, int count)
        {
            float total = 0f;
            for (int i = 0; i < count; i++) if (weights[i] > 0f) total += weights[i];
            if (total <= 0f) return -1;
            float r = NextFloat() * total;
            for (int i = 0; i < count; i++)
            {
                if (weights[i] <= 0f) continue;
                r -= weights[i];
                if (r <= 0f) return i;
            }
            for (int i = count - 1; i >= 0; i--) if (weights[i] > 0f) return i;
            return -1;
        }
    }
}

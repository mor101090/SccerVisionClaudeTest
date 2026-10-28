using UnityEngine;

namespace SoccerVision.Runtime
{
    /// <summary>
    /// A 3D-looking football drawn into a small texture every frame: a truncated icosahedron (12 black pentagons,
    /// 20 white hexagons) lit from the top left and rotated as the ball rolls. Same model as web/src/replay/ball3d.ts.
    /// The sprite is one world unit across; scale its transform to the ball's on-screen size.
    /// </summary>
    public sealed class Ball3DTexture
    {
        private static readonly Vector3[] Pent = new Vector3[12];
        private static readonly Vector3[] Hex = new Vector3[20];
        // Face plane distances of a truncated icosahedron with edge 1: a direction lies on the face with max(n.N / d).
        private const float RP = 2.32744f, RH = 2.26728f;
        private static readonly Vector3 Light = new Vector3(-0.45f, 0.55f, 0.7f).normalized;
        private static readonly Vector3 Half = (Light + Vector3.forward).normalized;

        static Ball3DTexture()
        {
            float phi = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new[]
            {
                new Vector3(0, 1, phi), new Vector3(0, -1, phi), new Vector3(0, 1, -phi), new Vector3(0, -1, -phi),
                new Vector3(1, phi, 0), new Vector3(-1, phi, 0), new Vector3(1, -phi, 0), new Vector3(-1, -phi, 0),
                new Vector3(phi, 0, 1), new Vector3(-phi, 0, 1), new Vector3(phi, 0, -1), new Vector3(-phi, 0, -1),
            };
            for (int i = 0; i < 12; i++) Pent[i] = v[i].normalized;
            int n = 0;
            for (int i = 0; i < 12; i++)
                for (int j = i + 1; j < 12; j++)
                    for (int k = j + 1; k < 12; k++)
                        if (Mathf.Abs((v[i] - v[j]).magnitude - 2f) < 1e-4f && Mathf.Abs((v[j] - v[k]).magnitude - 2f) < 1e-4f && Mathf.Abs((v[i] - v[k]).magnitude - 2f) < 1e-4f)
                            Hex[n++] = (v[i] + v[j] + v[k]).normalized;
        }

        public Sprite Sprite { get; }
        private readonly Texture2D _tex;
        private readonly Color32[] _px;
        private readonly int _size;
        private Quaternion _rot = Quaternion.identity;
        private Vector3 _axis = Vector3.up;
        private float _w;
        private Vector2? _prev;

        public Ball3DTexture(int size)
        {
            _size = size;
            _tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "sv_ball" };
            _px = new Color32[size * size];
            Sprite = Sprite.Create(_tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            Redraw();
        }

        /// <summary>Advances the spin from the ball's movement on the ground (metres) over dt seconds; a held ball does not spin.</summary>
        public void Spin(Vector2 pos, float height, float dt, bool held)
        {
            if (held)
            {
                _w = 0f;
                _prev = pos;
                return;
            }
            if (_prev.HasValue && dt > 0f)
            {
                Vector2 d = pos - _prev.Value;
                float dist = d.magnitude;
                if (dist > 0.0005f && dist < 3f)
                {
                    // Rolling without slipping on the ground (axis = up x direction of travel); in the air a gentler spin.
                    // The visual rate is capped so the spin stays readable.
                    _axis = new Vector3(-d.y / dist, d.x / dist, 0f);
                    _w = Mathf.Min(dist / 0.3f, 0.5f) / dt * (height < 0.35f ? 1f : 0.35f);
                }
                else _w = 0f;
                if (_w > 0f) _rot = Quaternion.AngleAxis(Mathf.Min(0.5f, _w * dt) * Mathf.Rad2Deg, _axis) * _rot;
            }
            _prev = pos;
        }

        /// <summary>High-visibility ball (orange with navy pentagons), used in the snow.</summary>
        public bool Winter;

        public void Redraw()
        {
            float c = _size * 0.5f, rad = c - 1f;
            var inv = Quaternion.Inverse(_rot);
            for (int py = 0; py < _size; py++)
                for (int px = 0; px < _size; px++)
                {
                    float nx = (px + 0.5f - c) / rad, ny = (py + 0.5f - c) / rad, r2 = nx * nx + ny * ny;
                    int o = py * _size + px;
                    if (r2 > 1f)
                    {
                        _px[o] = new Color32(0, 0, 0, 0);
                        continue;
                    }
                    var n = new Vector3(nx, ny, Mathf.Sqrt(1f - r2));
                    Vector3 b = inv * n;
                    float best = -9f, second = -9f;
                    bool pent = false;
                    for (int q = 0; q < 12; q++)
                    {
                        float sc = Vector3.Dot(Pent[q], b) / RP;
                        if (sc > best) { second = best; best = sc; pent = true; } else if (sc > second) second = sc;
                    }
                    for (int q = 0; q < 20; q++)
                    {
                        float sc = Vector3.Dot(Hex[q], b) / RH;
                        if (sc > best) { second = best; best = sc; pent = false; } else if (sc > second) second = sc;
                    }
                    Vector3 col = Winter ? (pent ? new Vector3(22f, 30f, 80f) : new Vector3(255f, 104f, 20f)) : (pent ? new Vector3(28f, 28f, 28f) : new Vector3(246f, 246f, 246f));
                    if (best - second < 0.006f) col = Winter ? (pent ? new Vector3(50f, 56f, 100f) : new Vector3(176f, 64f, 10f)) : (pent ? new Vector3(60f, 60f, 60f) : new Vector3(150f, 150f, 150f)); // stitched seams
                    float diff = 0.38f + 0.62f * Mathf.Max(0f, Vector3.Dot(n, Light));
                    float spec = Mathf.Pow(Mathf.Max(0f, Vector3.Dot(n, Half)), 36f) * (pent ? 120f : 90f);
                    float edge = 1f - Mathf.Sqrt(r2);
                    // a thin dark rim keeps the ball readable on the light grass
                    float rim = edge * rad < 1.2f ? 0.55f : 1f;
                    byte Ch(float v) => (byte)Mathf.Min(255f, (v * diff + spec) * rim);
                    _px[o] = new Color32(Ch(col.x), Ch(col.y), Ch(col.z), (byte)(255f * Mathf.Clamp01(edge * rad + 0.5f)));
                }
            _tex.SetPixels32(_px);
            _tex.Apply(false);
        }
    }
}

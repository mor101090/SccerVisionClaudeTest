using SoccerVision.Simulation;
using UnityEngine;

namespace SoccerVision.Runtime
{
    /// <summary>
    /// Draws the 105 x 68 m pitch (IFAB Law 1 markings, mown bands, technical areas, goal frames) into one texture, with
    /// the state of the grass: the engine's mud and puddle map (edges broken up with Perlin noise), or a layer of snow
    /// with the lines painted blue. Mowing pattern: bands along the pitch, bands across it, or rings around the centre.
    /// Assign <see cref="fieldOverride"/> to use the designer's PNG instead (centred, sized by
    /// <see cref="overrideWorldSize"/> in metres).
    /// </summary>
    public sealed class PitchRenderer : MonoBehaviour
    {
        public const float Margin = 5f;
        /// <summary>World extent of the pitch texture: a wider band on top (scoreboard, technical areas) and below (commentary bar).</summary>
        public const float Top = 46f, Bottom = -41f;
        public Texture2D fieldOverride;
        public Vector2 overrideWorldSize = new Vector2(Pitch.Length + 2f * Margin, Top - Bottom);
        public int pixelsPerMetre = 14;

        private Color32[] _px;
        private int _w, _h;
        private float _ppm;
        private MatchLook _look = new MatchLook();
        private PitchSurface _surface;
        private Color32 _line = LineColor;

        // Natural, slightly muted turf (mown bands along the length of the pitch) with a fine grain.
        private static readonly Color32 GrassA = new Color32(91, 135, 51, 255);
        private static readonly Color32 GrassB = new Color32(83, 121, 45, 255);
        private static readonly Color32 LineColor = new Color32(246, 246, 246, 255);

        public void Build(MatchLook look, PitchSurface surface)
        {
            _look = look ?? new MatchLook();
            _surface = surface;
            Build();
        }

        public void Build()
        {
            var sr = gameObject.GetComponent<SpriteRenderer>();
            if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sortingOrder = -100;
            if (fieldOverride != null)
            {
                float ppu = fieldOverride.width / Mathf.Max(1f, overrideWorldSize.x);
                sr.sprite = Sprite.Create(fieldOverride, new Rect(0, 0, fieldOverride.width, fieldOverride.height), new Vector2(0.5f, -Bottom / (Top - Bottom)), ppu);
                return;
            }
            sr.sprite = Sprite.Create(Generate(), new Rect(0, 0, _w, _h), new Vector2(0.5f, -Bottom / (Top - Bottom)), _ppm, 0, SpriteMeshType.FullRect);
        }

        private Texture2D Generate()
        {
            _ppm = Mathf.Clamp(pixelsPerMetre, 6, 24);
            _w = Mathf.RoundToInt((Pitch.Length + 2f * Margin) * _ppm);
            _h = Mathf.RoundToInt((Top - Bottom) * _ppm);
            _px = new Color32[_w * _h];

            const int bands = 12;
            var rng = new System.Random(11);
            bool wet = _look.sky == Weather.Rain, snow = _look.sky == Weather.Snow;
            Color32 light = wet ? new Color32(79, 133, 48, 255) : GrassA, dark = wet ? new Color32(70, 118, 43, 255) : GrassB;
            for (int y = 0; y < _h; y++)
            {
                float wy = y / _ppm + Bottom;
                int band = Mathf.FloorToInt((Top - wy) / ((Top - Bottom) / bands));
                for (int x = 0; x < _w; x++)
                {
                    float wx = x / _ppm - Pitch.HalfLength - Margin;
                    bool lightBand;
                    if (_look.grass == GrassPattern.Across) lightBand = (Mathf.FloorToInt((wx + 52.5f) / (105f / 18f)) & 1) == 0;
                    else if (_look.grass == GrassPattern.Circles) lightBand = (Mathf.FloorToInt(Mathf.Sqrt(wx * wx + wy * wy) / 5f) & 1) == 1;
                    else lightBand = (band & 1) == 1;
                    Color32 c = lightBand ? light : dark;
                    int grain = rng.Next(-7, 8);
                    _px[y * _w + x] = new Color32((byte)Mathf.Clamp(c.r + grain, 0, 255), (byte)Mathf.Clamp(c.g + grain, 0, 255), (byte)Mathf.Clamp(c.b + grain, 0, 255), 255);
                }
            }
            _noise = new PerlinNoise((ulong)UnityEngine.Random.Range(1, int.MaxValue));
            if (_surface != null) PaintSurface(wet);
            if (snow) PaintSnow();
            _line = snow ? new Color32(255, 255, 255, 255) : LineColor;

            if (snow)
            {
                // the cleared, freshly painted lines read on the frost: a soft dark edge under crisp white
                _line = new Color32(20, 52, 20, 120);
                DrawMarkings(0.34f);
                _line = new Color32(255, 255, 255, 255);
            }
            DrawMarkings(0.12f);

            var tex = new Texture2D(_w, _h, TextureFormat.RGBA32, true)
            {
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 4,
                name = "sv_pitch",
            };
            tex.SetPixels32(_px);
            tex.Apply(true, true);
            _px = null;
            return tex;
        }

        /// <summary>IFAB markings, technical areas and the goal frames, with line width <paramref name="lw"/> (max 12 cm).</summary>
        private void DrawMarkings(float lw)
        {
            // Boundary and halfway line
            Line(-Pitch.HalfLength, -Pitch.HalfWidth, Pitch.HalfLength, -Pitch.HalfWidth, lw);
            Line(-Pitch.HalfLength, Pitch.HalfWidth, Pitch.HalfLength, Pitch.HalfWidth, lw);
            Line(-Pitch.HalfLength, -Pitch.HalfWidth, -Pitch.HalfLength, Pitch.HalfWidth, lw);
            Line(Pitch.HalfLength, -Pitch.HalfWidth, Pitch.HalfLength, Pitch.HalfWidth, lw);
            Line(0f, -Pitch.HalfWidth, 0f, Pitch.HalfWidth, lw);
            Circle(0f, 0f, Pitch.CentreCircleRadius, lw, 0f, 360f);
            Disc(0f, 0f, 0.2f, _line);
            // Technical areas (coaches) above the top touchline, either side of the halfway line.
            for (int side = -1; side <= 1; side += 2)
            {
                float x0 = 1.5f * side, x1 = 19f * side;
                Line(x0, 36f, x1, 36f, lw); Line(x0, 39.6f, x1, 39.6f, lw);
                Line(x0, 36f, x0, 39.6f, lw); Line(x1, 36f, x1, 39.6f, lw);
            }

            for (int s = -1; s <= 1; s += 2)
            {
                float gl = Pitch.HalfLength * s;
                float pa = (Pitch.HalfLength - Pitch.PenaltyAreaDepth) * s;
                float ga = (Pitch.HalfLength - Pitch.GoalAreaDepth) * s;
                // Penalty area
                Line(gl, -Pitch.PenaltyAreaHalfWidth, pa, -Pitch.PenaltyAreaHalfWidth, lw);
                Line(gl, Pitch.PenaltyAreaHalfWidth, pa, Pitch.PenaltyAreaHalfWidth, lw);
                Line(pa, -Pitch.PenaltyAreaHalfWidth, pa, Pitch.PenaltyAreaHalfWidth, lw);
                // Goal area
                Line(gl, -Pitch.GoalAreaHalfWidth, ga, -Pitch.GoalAreaHalfWidth, lw);
                Line(gl, Pitch.GoalAreaHalfWidth, ga, Pitch.GoalAreaHalfWidth, lw);
                Line(ga, -Pitch.GoalAreaHalfWidth, ga, Pitch.GoalAreaHalfWidth, lw);
                // Penalty mark and arc (only the part outside the area)
                float spot = (Pitch.HalfLength - Pitch.PenaltySpotDistance) * s;
                Disc(spot, 0f, 0.15f, _line);
                float half = Mathf.Acos((Pitch.PenaltyAreaDepth - Pitch.PenaltySpotDistance) / Pitch.PenaltyArcRadius) * Mathf.Rad2Deg;
                float centre = s > 0 ? 180f : 0f;
                Circle(spot, 0f, Pitch.PenaltyArcRadius, lw, centre - half, centre + half);
                // Corner arcs
                for (int c = -1; c <= 1; c += 2)
                {
                    float start = s > 0 ? (c > 0 ? 180f : 90f) : (c > 0 ? 270f : 0f);
                    Circle(gl, Pitch.HalfWidth * c, Pitch.CornerArcRadius, lw, start, start + 90f);
                }
                // Goal frame (posts and crossbar); the net itself is a deforming GoalNetView.
                Disc(gl, -Pitch.GoalHalfWidth - Pitch.PostRadius, 0.12f, LineColor);
                Disc(gl, Pitch.GoalHalfWidth + Pitch.PostRadius, 0.12f, LineColor); // the posts stay white
                Line(gl, -Pitch.GoalHalfWidth, gl, Pitch.GoalHalfWidth, 0.1f);
            }

        }

        // ---- Ground textures (same layers as the web views' textures.ts) --------------------------------------------
        // Seeded Perlin noise, its fractal sum ("cloud") and a domain-warped cloud for organic, seamless edges; fine Perlin
        // and per-pixel grain for grass tufts in the dirt and the water. A new texture seed every time the pitch is built.

        private PerlinNoise _noise;
        private float Perlin(float x, float y) => _noise.Noise(x, y);

        private float Fbm(float x, float y, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += Perlin(x, y) * amp;
                norm += amp;
                amp *= 0.5f;
                x = x * 2.03f + 17.1f;
                y = y * 2.03f - 9.7f;
            }
            return Mathf.Clamp01(0.5f + sum / norm * 0.9f);
        }

        private float Warped(float x, float y, int octaves = 5)
        {
            float qx = Fbm(x + 3.1f, y - 1.7f, 3) - 0.5f, qy = Fbm(x - 5.2f, y + 8.3f, 3) - 0.5f;
            return Fbm(x + 2.2f * qx, y + 2.2f * qy, octaves);
        }

        private static float Grain(int i, int j)
        {
            uint h = (uint)(i * 374761393) ^ (uint)(j * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) / 4294967295f;
        }

        private static float Smooth(float a, float b, float x)
        {
            float k = Mathf.Clamp01((x - a) / (b - a));
            return k * k * (3f - 2f * k);
        }

        /// <summary>
        /// Worn grass, bare mud and standing water from the engine's map. Mud: sandy, yellowish dirt in several tones (dry,
        /// damp, clods), broken up by grass tufts and fading into thin yellowed grass. Water: a shallow mirror of the sky,
        /// silvery with cloud reflections, sheen and sparkles, the grass showing through where it is thin, in a ring of dark
        /// soaked grass.
        /// </summary>
        private void PaintSurface(bool wet)
        {
            bool night = _look.night;
            for (int y = 0; y < _h; y++)
            {
                float wy = y / _ppm + Bottom;
                for (int x = 0; x < _w; x++)
                {
                    float wx = x / _ppm - Pitch.HalfLength - Margin;
                    float m = _surface.MudAt(wx, wy), wa = _surface.WaterAt(wx, wy);
                    if (m < 0.01f && wa < 0.01f) continue;
                    int i = y * _w + x;
                    Color b = _px[i];
                    float grain = Grain(x, y), cloud = Warped(wx * 0.22f, wy * 0.22f), tuftNoise = Perlin(wx * 2.6f, wy * 2.6f) * 0.5f + 0.5f;
                    if (m > 0.01f)
                    {
                        float breakup = Perlin(wx * 0.75f + 9f, wy * 0.75f - 4f) * 0.5f + 0.5f;
                        float bare = m * 1.3f + (cloud - 0.5f) * 0.8f + (breakup - 0.5f) * 0.35f + (tuftNoise - 0.5f) * 0.18f;
                        float dirt = Smooth(0.32f, 0.9f, bare), halo = Smooth(0.05f, 0.4f, bare) * (1f - dirt);
                        float tuft = Smooth(0.58f, 0.8f, tuftNoise * 0.55f + grain * 0.45f) * (1f - Smooth(0.75f, 1f, bare));
                        float tone = Fbm(wx * 0.9f + 40f, wy * 0.9f - 12f, 4), damp = Fbm(wx * 0.35f - 70f, wy * 0.35f + 30f, 3);
                        var dc = new Color(Mathf.Lerp(196f, 166f, tone), Mathf.Lerp(172f, 140f, tone), Mathf.Lerp(118f, 90f, tone)) / 255f;
                        float dk = Smooth(0.45f, 0.85f, damp) * (wet ? 0.6f : 0.3f);
                        dc = Color.Lerp(dc, new Color(128f, 104f, 66f) / 255f, dk);
                        if (wet) dc = new Color(dc.r * 0.9f, dc.g * 0.88f, dc.b * 0.84f);
                        if (grain > 0.975f) dc *= 0.87f;
                        else if (grain < 0.04f) dc = new Color(Mathf.Min(1f, dc.r * 1.15f), Mathf.Min(1f, dc.g * 1.15f), Mathf.Min(1f, dc.b * 1.12f));
                        b = Color.Lerp(b, new Color(150f / 255f, 152f / 255f, 78f / 255f), halo * 0.42f * (0.7f + 0.3f * grain));
                        b = Color.Lerp(b, dc, dirt * (1f - 0.7f * tuft) * (0.9f + 0.1f * grain));
                    }
                    if (wa > 0.01f)
                    {
                        float wv = wa + (cloud - 0.5f) * 0.34f + (tuftNoise - 0.5f) * 0.1f;
                        float pool = Smooth(0.28f, 0.5f, wv), soaked = Smooth(0.1f, 0.3f, wv) * (1f - pool);
                        b = Color.Lerp(b, new Color(34f, 62f, 30f) / 255f, soaked * 0.5f);
                        if (pool > 0f)
                        {
                            float sky = Warped(wx * 0.1f + 90f, wy * 0.1f - 40f, 4), sheen = Smooth(0.55f, 0.95f, Fbm(wx * 0.06f - 20f, wy * 0.25f + 5f, 3));
                            float ripple = Perlin(wx * 1.7f + 11f, wy * 5.5f) * 0.5f + 0.5f, depth = Smooth(0.42f, 0.85f, wv);
                            var wc = night ? new Color(Mathf.Lerp(36f, 96f, sky), Mathf.Lerp(46f, 106f, sky), Mathf.Lerp(64f, 130f, sky))
                                           : new Color(Mathf.Lerp(118f, 196f, sky), Mathf.Lerp(134f, 206f, sky), Mathf.Lerp(150f, 218f, sky));
                            float lift = sheen * (night ? 60f : 48f) + (ripple - 0.5f) * 16f;
                            wc = new Color(wc.r + lift, wc.g + lift, wc.b + lift) / 255f;
                            if (grain > 0.992f) wc = Color.white; // sparkle
                            float blade = grain > 0.86f && depth < 0.6f ? 0.6f * (1f - depth) : 0f;
                            float rim = (1f - Smooth(0.3f, 0.42f, wv)) * pool;
                            wc = new Color(Mathf.Min(1f, wc.r + rim * 0.16f), Mathf.Min(1f, wc.g + rim * 0.16f), Mathf.Min(1f, wc.b + rim * 0.14f));
                            b = Color.Lerp(b, wc, pool * (0.42f + 0.5f * depth) * (1f - blade));
                        }
                    }
                    _px[i] = b;
                }
            }
        }

        /// <summary>Powdery frost over the whole ground: thick and thin, the green showing through in specks and streaks.</summary>
        private void PaintSnow()
        {
            for (int y = 0; y < _h; y++)
            {
                float wy = y / _ppm + Bottom;
                for (int x = 0; x < _w; x++)
                {
                    float wx = x / _ppm - Pitch.HalfLength - Margin;
                    float cover = 0.52f + 0.32f * Warped(wx * 0.07f + 50f, wy * 0.07f, 4);
                    float streak = Perlin(wx * 0.35f, wy * 2.2f) * 0.5f + 0.5f, fine = Perlin(wx * 2.4f, wy * 2.4f) * 0.5f + 0.5f, grain = Grain(x, y);
                    float v = fine * 0.6f + streak * 0.25f + grain * 0.15f;
                    float a = Smooth(1.05f - cover - 0.16f, 1.05f - cover + 0.16f, v) * 0.88f;
                    float shade = (226f + 18f * fine + 8f * grain) / 255f;
                    int i = y * _w + x;
                    _px[i] = Color.Lerp(_px[i], new Color(shade - 6f / 255f, shade - 2f / 255f, Mathf.Min(1f, shade + 8f / 255f)), a);
                }
            }
        }

        private void Line(float x1, float y1, float x2, float y2, float width) => Line(x1, y1, x2, y2, width, _line);

        private void Line(float x1, float y1, float x2, float y2, float width, Color32 color)
        {
            float len = Mathf.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
            int steps = Mathf.Max(1, Mathf.CeilToInt(len * _ppm * 1.5f));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Disc(x1 + (x2 - x1) * t, y1 + (y2 - y1) * t, width * 0.5f, color);
            }
        }

        private void Circle(float cx, float cy, float r, float width, float fromDeg, float toDeg)
        {
            float span = (toDeg - fromDeg) * Mathf.Deg2Rad;
            int steps = Mathf.Max(8, Mathf.CeilToInt(Mathf.Abs(span) * r * _ppm * 1.5f));
            for (int i = 0; i <= steps; i++)
            {
                float a = fromDeg * Mathf.Deg2Rad + span * i / steps;
                Disc(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r, width * 0.5f, _line);
            }
        }

        /// <summary>Antialiased filled disc in pitch coordinates (alpha-blended over the grass).</summary>
        private void Disc(float wx, float wy, float radius, Color32 color)
        {
            float px = (wx + Pitch.HalfLength + Margin) * _ppm;
            float py = (wy - Bottom) * _ppm;
            float rp = Mathf.Max(0.6f, radius * _ppm);
            int x0 = Mathf.FloorToInt(px - rp - 1), x1 = Mathf.CeilToInt(px + rp + 1);
            int y0 = Mathf.FloorToInt(py - rp - 1), y1 = Mathf.CeilToInt(py + rp + 1);
            for (int y = Mathf.Max(0, y0); y <= Mathf.Min(_h - 1, y1); y++)
            {
                for (int x = Mathf.Max(0, x0); x <= Mathf.Min(_w - 1, x1); x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - px) * (x + 0.5f - px) + (y + 0.5f - py) * (y + 0.5f - py));
                    float a = Mathf.Clamp01(rp - d + 0.5f) * (color.a / 255f);
                    if (a <= 0f) continue;
                    int i = y * _w + x;
                    Color32 b = _px[i];
                    _px[i] = new Color32(
                        (byte)(b.r + (color.r - b.r) * a),
                        (byte)(b.g + (color.g - b.g) * a),
                        (byte)(b.b + (color.b - b.b) * a),
                        255);
                }
            }
        }
    }
}

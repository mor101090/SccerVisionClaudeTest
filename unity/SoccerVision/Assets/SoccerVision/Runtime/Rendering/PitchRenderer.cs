using SoccerVision.Simulation;
using UnityEngine;

namespace SoccerVision.Runtime
{
    /// <summary>
    /// Draws the 105 x 68 m pitch (IFAB Law 1 markings, mown bands, technical areas, goal frames) into one texture.
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

        // Natural, slightly muted turf (mown bands along the length of the pitch) with a fine grain.
        private static readonly Color32 GrassA = new Color32(91, 135, 51, 255);
        private static readonly Color32 GrassB = new Color32(83, 121, 45, 255);
        private static readonly Color32 LineColor = new Color32(246, 246, 246, 255);

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
            for (int y = 0; y < _h; y++)
            {
                float wy = y / _ppm + Bottom;
                int band = Mathf.FloorToInt((Top - wy) / ((Top - Bottom) / bands));
                Color32 c = (band & 1) == 0 ? GrassB : GrassA;
                for (int x = 0; x < _w; x++)
                {
                    int grain = rng.Next(-7, 8);
                    _px[y * _w + x] = new Color32((byte)Mathf.Clamp(c.r + grain, 0, 255), (byte)Mathf.Clamp(c.g + grain, 0, 255), (byte)Mathf.Clamp(c.b + grain, 0, 255), 255);
                }
            }

            const float lw = 0.12f; // line width (max 12 cm)
            // Boundary and halfway line
            Line(-Pitch.HalfLength, -Pitch.HalfWidth, Pitch.HalfLength, -Pitch.HalfWidth, lw);
            Line(-Pitch.HalfLength, Pitch.HalfWidth, Pitch.HalfLength, Pitch.HalfWidth, lw);
            Line(-Pitch.HalfLength, -Pitch.HalfWidth, -Pitch.HalfLength, Pitch.HalfWidth, lw);
            Line(Pitch.HalfLength, -Pitch.HalfWidth, Pitch.HalfLength, Pitch.HalfWidth, lw);
            Line(0f, -Pitch.HalfWidth, 0f, Pitch.HalfWidth, lw);
            Circle(0f, 0f, Pitch.CentreCircleRadius, lw, 0f, 360f);
            Disc(0f, 0f, 0.2f, LineColor);
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
                Disc(spot, 0f, 0.15f, LineColor);
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
                Disc(gl, Pitch.GoalHalfWidth + Pitch.PostRadius, 0.12f, LineColor);
                Line(gl, -Pitch.GoalHalfWidth, gl, Pitch.GoalHalfWidth, 0.1f);
            }

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

        private void Line(float x1, float y1, float x2, float y2, float width) => Line(x1, y1, x2, y2, width, LineColor);

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
                Disc(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r, width * 0.5f, LineColor);
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
                        (byte)Mathf.Max(b.r, b.r + (color.r - b.r) * a),
                        (byte)Mathf.Max(b.g, b.g + (color.g - b.g) * a),
                        (byte)Mathf.Max(b.b, b.b + (color.b - b.b) * a),
                        255);
                }
            }
        }
    }
}

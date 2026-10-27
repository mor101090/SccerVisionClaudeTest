using SoccerVision.Simulation;
using UnityEngine;

namespace SoccerVision.Runtime
{
    /// <summary>
    /// The sky over the pitch, drawn above everything else: rain streaks and splashes (and rings in the puddles), falling
    /// snow, cloud shadows drifting with the wind, a grey sky's dimmer light, and at night the dark beyond the reach of
    /// the four floodlight towers. Same look as the web views.
    /// </summary>
    public sealed class WeatherView : MonoBehaviour
    {
        private const float MinX = -60f, MaxX = 60f, MinY = -42f, MaxY = 48f;
        private SpriteRenderer[] _drops, _flakes, _rings, _clouds;
        private Vector3[] _dropState, _flakeState, _ringState;
        private SpriteRenderer _tint, _dark;
        private Vector2[] _puddles = new Vector2[0];
        private int _nextRing;

        public void Init(PitchSurface surface)
        {
            _drops = Make("Drop", SpriteFactory.Square, 360, 80, out _dropState);
            _flakes = Make("Flake", SpriteFactory.Circle, 300, 80, out _flakeState);
            _rings = Make("Ring", SpriteFactory.Ring, 90, 79, out _ringState);
            _clouds = Make("Cloud", SpriteFactory.Shadow, 6, 78, out _);
            for (int i = 0; i < _drops.Length; i++) _dropState[i] = new Vector3(Random.Range(MinX, MaxX), Random.Range(MinY, MaxY), Random.Range(0.8f, 1.3f));
            for (int i = 0; i < _flakes.Length; i++)
            {
                _flakeState[i] = new Vector3(Random.Range(MinX, MaxX), Random.Range(MinY, MaxY), Random.Range(0f, 6.3f));
                float r = Random.Range(0.12f, 0.36f);
                _flakes[i].transform.localScale = new Vector3(r, r, 1f);
                _flakes[i].color = new Color(1f, 1f, 1f, 0.88f);
            }
            for (int i = 0; i < _rings.Length; i++) { _ringState[i] = new Vector3(0f, 0f, 0.35f); _ringAge[i] = 99f; }
            for (int i = 0; i < _clouds.Length; i++)
            {
                float d = 44f + 52f * Hash(i, 3);
                _clouds[i].transform.localScale = new Vector3(d, d, 1f);
            }
            _tint = SpriteFactory.AddSprite(transform, "SkyTint", SpriteFactory.Square, Color.clear, 85, 1f);
            _tint.transform.localScale = new Vector3(MaxX - MinX + 20f, MaxY - MinY + 20f, 1f);
            _tint.transform.localPosition = new Vector3(0f, (MinY + MaxY) * 0.5f, 0f);
            _dark = BuildDarkness();

            // where the rain makes rings: the deep puddles of the engine's map
            var list = new System.Collections.Generic.List<Vector2>();
            if (surface != null && surface.HasWater)
                for (float x = -54f; x <= 54f; x += 1f)
                    for (float y = -36f; y <= 36f; y += 1f)
                        if (surface.WaterAt(x, y) > 0.55f) list.Add(new Vector2(x, y));
            _puddles = list.ToArray();
        }

        private SpriteRenderer[] Make(string name, Sprite sprite, int n, int order, out Vector3[] state)
        {
            var arr = new SpriteRenderer[n];
            state = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                arr[i] = SpriteFactory.AddSprite(transform, name, sprite, Color.clear, order, 1f);
                arr[i].enabled = false;
            }
            return arr;
        }

        private static float Hash(int i, int j)
        {
            uint h = (uint)(i * 374761393 + j * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) / 4294967295f;
        }

        /// <summary>Night: dark everywhere the floodlights do not reach, with the towers' glare at the corners.</summary>
        private SpriteRenderer BuildDarkness()
        {
            const int ppm = 2;
            int w = Mathf.RoundToInt((MaxX - MinX) * ppm), h = Mathf.RoundToInt((MaxY - MinY) * ppm);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "sv_night" };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var p = new Vector2(MinX + (x + 0.5f) / ppm, MinY + (y + 0.5f) / ppm);
                    float keep = 1f, glare = 0f;
                    foreach (var t in Atmosphere.Towers)
                    {
                        float d = Vector2.Distance(p, t * 0.42f) / 62f;
                        float cut = d < 0.6f ? Mathf.Lerp(0.22f, 0.13f, d / 0.6f) : Mathf.Lerp(0.13f, 0f, (d - 0.6f) / 0.4f);
                        keep *= 1f - Mathf.Max(0f, cut);
                        glare = Mathf.Max(glare, Mathf.Clamp01(1f - Vector2.Distance(p, t) / 7f));
                    }
                    float a = 0.8f * keep;
                    var dark = new Color(2f / 255f, 7f / 255f, 22f / 255f, a);
                    var lamp = new Color(1f, 0.98f, 0.88f, 0.9f * glare * glare);
                    var c = Color.Lerp(dark, lamp, lamp.a);
                    c.a = Mathf.Max(a, lamp.a);
                    px[y * w + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var go = new GameObject("Night");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(MinX, MinY, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, w, h), Vector2.zero, ppm, 0, SpriteMeshType.FullRect);
            sr.sortingOrder = 90;
            sr.enabled = false;
            return sr;
        }

        private void Update()
        {
            var look = Atmosphere.Look;
            float dt = Mathf.Min(0.1f, Time.deltaTime), now = Time.time;
            Vector2 wind = new Vector2(Mathf.Cos(Atmosphere.WindAngle), Mathf.Sin(Atmosphere.WindAngle)) * Atmosphere.WindSpeed;
            bool rain = look.sky == Weather.Rain, snow = look.sky == Weather.Snow;

            // Rain: seen from above, a fast hail of short streaks blown along by the wind.
            Vector2 fall = new Vector2(wind.x * 0.06f + 0.12f, -1f + wind.y * 0.03f).normalized;
            for (int i = 0; i < _drops.Length; i++)
            {
                var sr = _drops[i];
                if (sr.enabled != rain) sr.enabled = rain;
                if (!rain) continue;
                var s = _dropState[i];
                s.x += fall.x * 60f * s.z * dt;
                s.y += fall.y * 60f * s.z * dt;
                if (s.y < MinY) { s.y += MaxY - MinY; s.x = Random.Range(MinX, MaxX); }
                if (s.x > MaxX) s.x -= MaxX - MinX;
                if (s.x < MinX) s.x += MaxX - MinX;
                _dropState[i] = s;
                sr.transform.localPosition = new Vector3(s.x, s.y, 0f);
                sr.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(fall.y, fall.x) * Mathf.Rad2Deg);
                sr.transform.localScale = new Vector3(1.1f * s.z, 0.07f, 1f);
                sr.color = look.night ? new Color(0.88f, 0.92f, 1f, 0.42f) : new Color(0.8f, 0.85f, 0.91f, 0.34f);
            }
            // splashes on the grass and rings in the puddles
            if (rain)
            {
                for (int k = 0; k < 4; k++) Ring(new Vector2(Random.Range(-54f, 54f), Random.Range(-36f, 36f)), 0.35f);
                if (_puddles.Length > 0 && Random.value < 0.6f) Ring(_puddles[Random.Range(0, _puddles.Length)] + Random.insideUnitCircle * 0.5f, 0.7f);
            }
            for (int i = 0; i < _rings.Length; i++)
            {
                var s = _ringState[i];
                var sr = _rings[i];
                float life = s.z < 0.5f ? 0.3f : 0.9f;
                bool alive = _ringAge[i] < life;
                if (sr.enabled != alive) sr.enabled = alive;
                if (!alive) continue;
                _ringAge[i] += dt;
                float q = _ringAge[i] / life;
                float d = 2f * s.z * q;
                sr.transform.localPosition = new Vector3(s.x, s.y, 0f);
                sr.transform.localScale = new Vector3(d, d * 0.8f, 1f);
                sr.color = new Color(0.86f, 0.91f, 0.94f, 0.45f * (1f - q));
            }

            // Snow: flakes drifting down the picture with the wind, swaying.
            for (int i = 0; i < _flakes.Length; i++)
            {
                var sr = _flakes[i];
                if (sr.enabled != snow) sr.enabled = snow;
                if (!snow) continue;
                var s = _flakeState[i];
                float v = 0.5f + 0.8f * Hash(i, 1);
                s.x += (wind.x * 0.7f * v + Mathf.Sin(now * 1.3f + s.z) * 1.2f) * dt;
                s.y += (wind.y * 0.7f * v - 2.2f * v) * dt;
                if (s.y < MinY) { s.y += MaxY - MinY; s.x = Random.Range(MinX, MaxX); }
                if (s.y > MaxY) s.y -= MaxY - MinY;
                if (s.x > MaxX) s.x -= MaxX - MinX;
                if (s.x < MinX) s.x += MaxX - MinX;
                _flakeState[i] = s;
                sr.transform.localPosition = new Vector3(s.x, s.y, 0f);
            }

            // Cloud shadows drifting with the wind; a grey sky dims the light.
            bool clouds = !look.night;
            float strength = look.sky == Weather.Sunny ? 0.07f : look.sky == Weather.Cloudy ? 0.1f : 0.08f;
            float drift = Mathf.Max(1.5f, Atmosphere.WindSpeed) * now * 0.6f;
            float ang = Atmosphere.Windy ? Atmosphere.WindAngle : 0.4f;
            for (int i = 0; i < _clouds.Length; i++)
            {
                var sr = _clouds[i];
                if (sr.enabled != clouds) sr.enabled = clouds;
                if (!clouds) continue;
                float ox = Mathf.Repeat(Hash(i, 7) * 260f + drift * Mathf.Cos(ang), 260f) - 130f;
                float oy = Mathf.Repeat(Hash(i, 9) * 160f + drift * Mathf.Sin(ang), 160f) - 80f;
                sr.transform.localPosition = new Vector3(ox, oy, 0f);
                sr.color = new Color(0.04f, 0.07f, 0.11f, strength * 1.6f);
            }
            Color tint = look.night ? Color.clear
                : look.sky == Weather.Sunny ? new Color(1f, 0.84f, 0.55f, 0.05f)
                : look.sky == Weather.Rain ? new Color(0.07f, 0.11f, 0.17f, 0.28f)
                : look.sky == Weather.Snow ? new Color(0.16f, 0.2f, 0.29f, 0.12f) : new Color(0.09f, 0.12f, 0.16f, 0.16f);
            _tint.color = tint;
            if (_dark.enabled != look.night) _dark.enabled = look.night;
        }

        private float[] _ringAge = new float[90];

        private void Ring(Vector2 at, float size)
        {
            int i = _nextRing;
            _nextRing = (_nextRing + 1) % _rings.Length;
            _ringState[i] = new Vector3(at.x, at.y, size);
            _ringAge[i] = 0f;
        }
    }
}

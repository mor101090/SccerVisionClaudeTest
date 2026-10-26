using UnityEngine;

namespace SoccerVision.Runtime
{
    /// <summary>
    /// Procedural placeholder sprites (the designer's PNG assets will replace them). Every sprite is one world
    /// unit (= one metre) wide, so a GameObject's scale is its size in metres.
    /// </summary>
    public static class SpriteFactory
    {
        private static Sprite _circle;
        private static Sprite _ring;
        private static Sprite _shadow;
        private static Sprite _square;
        private static Sprite _flag;
        private static Font _font;

        public static Sprite Circle => _circle != null ? _circle : (_circle = MakeDisc(128, 0f, false));
        public static Sprite Ring => _ring != null ? _ring : (_ring = MakeDisc(128, 0.82f, false));
        public static Sprite Shadow => _shadow != null ? _shadow : (_shadow = MakeDisc(64, 0f, true));
        public static Sprite Square => _square != null ? _square : (_square = MakeSquare());
        public static Sprite Flag => _flag != null ? _flag : (_flag = MakeFlag());

        /// <summary>Built-in font (LegacyRuntime.ttf since Unity 2022.2, Arial.ttf before).</summary>
        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                try { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { _font = null; }
                if (_font == null)
                {
                    try { _font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { _font = null; }
                }
                return _font;
            }
        }

        private static Sprite MakeDisc(int size, float innerFraction, bool soft)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = soft ? "sv_shadow" : (innerFraction > 0f ? "sv_ring" : "sv_circle"),
            };
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            float outer = size * 0.5f - 0.5f;
            float inner = outer * innerFraction;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a;
                    if (soft)
                    {
                        float t = Mathf.Clamp01(d / outer);
                        a = Mathf.Clamp01(1f - t * t) * Mathf.Clamp01(1f - t);
                    }
                    else
                    {
                        a = Mathf.Clamp01(outer - d + 0.5f);
                        if (innerFraction > 0f) a *= Mathf.Clamp01(d - inner + 0.5f);
                    }
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
        }

        private static Sprite MakeSquare()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "sv_square" };
            var px = new Color32[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4, 0, SpriteMeshType.FullRect);
        }

        /// <summary>Triangular flag cloth, pivot at the pole (left edge, centre).</summary>
        private static Sprite MakeFlag()
        {
            const int w = 64, h = 48;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "sv_flag" };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float fx = x / (float)(w - 1);
                    float extent = (1f - fx) * (h * 0.5f);
                    float dy = Mathf.Abs(y - (h - 1) * 0.5f);
                    float a = Mathf.Clamp01(extent - dy + 0.5f);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0f, 0.5f), w, 0, SpriteMeshType.FullRect);
        }

        public static Color Hex(string hex, Color fallback)
        {
            return !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;
        }

        public static SpriteRenderer AddSprite(Transform parent, string name, Sprite sprite, Color color, int order, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(size, size, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        public static TextMesh AddText(Transform parent, string name, string text, Color color, int order, float characterSize, int fontSize = 64)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tm = go.AddComponent<TextMesh>();
            var font = Font;
            if (font != null)
            {
                tm.font = font;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            tm.text = text;
            tm.fontSize = fontSize;
            tm.characterSize = characterSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            go.GetComponent<MeshRenderer>().sortingOrder = order;
            return tm;
        }
    }
}

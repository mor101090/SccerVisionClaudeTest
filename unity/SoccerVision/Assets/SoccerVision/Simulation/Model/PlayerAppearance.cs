using System;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// How a player looks - the data a portrait is made from (see tools/faces). It is decided once, when the player is created,
    /// and stays with him for good; only his age moves on (the portrait of his age is shown). It plays no part in the match.
    /// </summary>
    [Serializable]
    public sealed class PlayerAppearance
    {
        /// <summary>The look of his face for the portrait: european, eastern-european, mediterranean, middle-eastern, east-african, west-african, latin-american, east-asian.</summary>
        public string Look = "european";
        /// <summary>Skin tone 1 (lightest) to 6 (darkest).</summary>
        public int Skin = 2;
        /// <summary>short, buzz, wavy, curly, afro, receding, bald, long.</summary>
        public string Hair = "short";
        /// <summary>black, dark-brown, brown, light-brown, blond, red (it greys with age in the portrait).</summary>
        public string HairColour = "dark-brown";
        /// <summary>none, stubble, short-beard, full-beard, moustache.</summary>
        public string Facial = "none";
        /// <summary>slim, athletic, stocky.</summary>
        public string Build = "athletic";
        /// <summary>Seeds the portrait's generation, so the same man comes out the same.</summary>
        public int FaceSeed;
    }

    /// <summary>Gives players their looks: by where they come from, with a spread of hair, beards and build (deterministic for the seed).</summary>
    public static class PlayerAppearances
    {
        // origin -> (look, share of each skin tone 1..6)
        private static readonly (string origin, (string look, float weight)[] looks)[] Looks =
        {
            ("jewish", new[] { ("mediterranean", 0.45f), ("middle-eastern", 0.30f), ("european", 0.15f), ("eastern-european", 0.10f) }),
            ("arab", new[] { ("middle-eastern", 0.8f), ("mediterranean", 0.2f) }),
            ("russian", new[] { ("eastern-european", 0.85f), ("european", 0.15f) }),
            ("ethiopian", new[] { ("east-african", 1f) }),
            ("spanish", new[] { ("mediterranean", 0.7f), ("european", 0.2f), ("latin-american", 0.1f) }),
            ("american", new[] { ("european", 0.4f), ("west-african", 0.25f), ("latin-american", 0.2f), ("east-asian", 0.05f), ("mediterranean", 0.1f) }),
        };

        private static readonly (string look, int lo, int hi)[] SkinRange =
        {
            ("european", 1, 2), ("eastern-european", 1, 2), ("mediterranean", 2, 4), ("middle-eastern", 3, 4),
            ("east-african", 5, 6), ("west-african", 5, 6), ("latin-american", 3, 4), ("east-asian", 2, 3),
        };

        private static string Pick((string, float)[] list, Random r)
        {
            float t = 0f; foreach (var (_, w) in list) t += w;
            float x = (float)r.NextDouble() * t;
            foreach (var (n, w) in list) { x -= w; if (x <= 0f) return n; }
            return list[list.Length - 1].Item1;
        }

        public static PlayerAppearance Generate(PlayerProfile p, Random rng)
        {
            string origin = string.IsNullOrEmpty(p.Origin) ? "american" : p.Origin;
            var list = Array.Find(Looks, l => l.origin == origin).looks ?? Looks[0].looks;
            var a = new PlayerAppearance { Look = Pick(list, rng), FaceSeed = rng.Next(1, int.MaxValue) };
            var (_, lo, hi) = Array.Find(SkinRange, s => s.look == a.Look);
            a.Skin = rng.Next(lo, hi + 1);
            bool african = a.Look.EndsWith("african");
            bool darkHair = african || a.Look == "east-asian" || a.Look == "middle-eastern" || a.Look == "mediterranean" || a.Look == "latin-american";
            a.HairColour = darkHair
                ? (rng.NextDouble() < 0.7 ? "black" : "dark-brown")
                : Pick(new[] { ("dark-brown", 0.3f), ("brown", 0.3f), ("light-brown", 0.2f), ("blond", 0.15f), ("red", 0.05f) }, rng);
            if (african) a.Hair = Pick(new[] { ("short", 0.25f), ("buzz", 0.25f), ("afro", 0.2f), ("curly", 0.2f), ("bald", 0.1f) }, rng);
            else if (a.Look == "east-asian") a.Hair = Pick(new[] { ("short", 0.6f), ("buzz", 0.2f), ("long", 0.1f), ("wavy", 0.1f) }, rng);
            else a.Hair = Pick(new[] { ("short", 0.4f), ("buzz", 0.15f), ("wavy", 0.2f), ("curly", 0.1f), ("long", 0.05f), ("receding", 0.07f), ("bald", 0.03f) }, rng);
            // the older, the more often a beard, and a receding hairline
            double beardChance = 0.25 + 0.012 * Math.Max(0, p.Age - 20);
            a.Facial = rng.NextDouble() < beardChance
                ? Pick(new[] { ("stubble", 0.4f), ("short-beard", 0.3f), ("full-beard", 0.2f), ("moustache", 0.1f) }, rng)
                : "none";
            if (p.Age >= 31 && (a.Hair == "short" || a.Hair == "wavy") && rng.NextDouble() < 0.25) a.Hair = "receding";
            a.Build = Pick(new[] { ("slim", 0.3f), ("athletic", 0.5f), ("stocky", 0.2f) }, rng);
            if (p.NaturalPosition == PositionGroup.Goalkeeper && rng.NextDouble() < 0.4) a.Build = "stocky";
            return a;
        }

        /// <summary>Gives every player of the squad his look (its own random stream: the match is not touched).</summary>
        public static void Assign(TeamSetup t, int seed)
        {
            var rng = new Random(unchecked(seed * (int)2246822519u + 1013904223));
            foreach (var p in t.Starters) p.Appearance = Generate(p, rng);
            foreach (var p in t.Bench) p.Appearance = Generate(p, rng);
        }
    }
}

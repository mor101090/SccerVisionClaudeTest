using System;
using SoccerVision.Simulation;
using UnityEngine;

namespace SoccerVision.Runtime
{
    public enum GrassPattern { Along, Across, Circles }

    /// <summary>
    /// Developer settings of the match view: pace, mowing pattern, sky, pitch upkeep, wind, day or night, net and team
    /// colours. Sky, upkeep and wind are also the conditions the engine plays in (rain, snow, wind and a worn pitch change
    /// the football); the rest only changes the picture. Set in the inspector, from the HUD or from React (setLook).
    /// </summary>
    [Serializable]
    public sealed class MatchLook
    {
        [Range(50, 100)] public int pace = 100;
        public GrassPattern grass = GrassPattern.Along;
        public Weather sky = Weather.Sunny;
        [Tooltip("2 perfect, 1 average, 0 neglected")]
        [Range(0, 2)] public int upkeep = 2;
        [Tooltip("Strong wind (12 m/s) from a direction fixed per match")]
        public bool windy;
        public bool night;
        public Color net = new Color(0.92f, 0.94f, 0.92f, 1f);
        [Tooltip("Hex colours; empty keeps the team's own kit")]
        public string homePrimary = "", homeSecondary = "", awayPrimary = "", awaySecondary = "";
        [Tooltip("Hex colours of the shirt numbers and the goalkeeper's shirt; empty keeps the kit (or follows the shirt)")]
        public string homeNumber = "", homeKeeper = "", awayNumber = "", awayKeeper = "";
        [Tooltip("Referee and assistants' shirt (hex); empty keeps black with yellow trim")]
        public string officials = "";

        public MatchLook Clone() => (MatchLook)MemberwiseClone();

        /// <summary>What the engine plays in. The wind blows from a direction fixed per match.</summary>
        public MatchConditions ToConditions(long seed) => new MatchConditions
        {
            Weather = sky,
            Maintenance = upkeep >= 2 ? 1f : upkeep == 1 ? 0.5f : 0f,
            WindSpeed = windy ? 12f : 0f,
            WindAngle = (seed * 97 % 360) * Mathf.Deg2Rad,
            Night = night,
        };

        /// <summary>Same football: sunny and cloudy play alike, and night only changes the light.</summary>
        public bool SamePlay(MatchLook o)
        {
            bool Wet(Weather w) => w == Weather.Rain;
            bool Snow(Weather w) => w == Weather.Snow;
            return Wet(sky) == Wet(o.sky) && Snow(sky) == Snow(o.sky) && upkeep == o.upkeep && windy == o.windy;
        }

        public static MatchLook From(LookDto d, MatchLook current)
        {
            var l = current != null ? current.Clone() : new MatchLook();
            if (d == null) return l;
            l.pace = Mathf.Clamp(d.pace, 50, 100);
            l.grass = d.grass == "across" ? GrassPattern.Across : d.grass == "circles" ? GrassPattern.Circles : GrassPattern.Along;
            l.sky = d.sky == "Rain" ? Weather.Rain : d.sky == "Snow" ? Weather.Snow : d.sky == "Cloudy" ? Weather.Cloudy : Weather.Sunny;
            l.upkeep = Mathf.Clamp(d.upkeep, 0, 2);
            l.windy = d.windy;
            l.night = d.night;
            l.net = SpriteFactory.Hex(d.net, l.net);
            l.homePrimary = d.homePrimary ?? "";
            l.homeSecondary = d.homeSecondary ?? "";
            l.awayPrimary = d.awayPrimary ?? "";
            l.awaySecondary = d.awaySecondary ?? "";
            l.homeNumber = d.homeNumber ?? "";
            l.homeKeeper = d.homeKeeper ?? "";
            l.awayNumber = d.awayNumber ?? "";
            l.awayKeeper = d.awayKeeper ?? "";
            l.officials = d.officials ?? "";
            return l;
        }
    }

    /// <summary>The light and air the views draw with this frame (set by the MatchController).</summary>
    public static class Atmosphere
    {
        public static MatchLook Look = new MatchLook();
        /// <summary>Wind in m/s and the direction it blows towards (world radians).</summary>
        public static float WindSpeed, WindAngle;
        public static bool Windy => WindSpeed > 0.5f;

        /// <summary>Floodlight towers outside the corners (world metres).</summary>
        public static readonly Vector2[] Towers = { new Vector2(-60f, -42f), new Vector2(-60f, 42f), new Vector2(60f, -42f), new Vector2(60f, 42f) };

        /// <summary>
        /// Where the shadows fall from a point: one long one from the low sun, a soft faint one under a grey sky, four
        /// faint ones away from the floodlight towers at night. Returns the number of shadows written (offset in metres,
        /// stretch along it, opacity).
        /// </summary>
        public static int Shadows(Vector2 at, Vector3[] into)
        {
            if (Look.night)
            {
                for (int i = 0; i < 4; i++)
                {
                    Vector2 d = (at - Towers[i]).normalized * 0.55f;
                    into[i] = new Vector3(d.x, d.y, 0.32f);
                }
                return 4;
            }
            into[0] = Look.sky == Weather.Sunny ? new Vector3(0.6f, -0.45f, 1.05f) : new Vector3(0.1f, -0.14f, 0.62f);
            return 1;
        }
    }
}

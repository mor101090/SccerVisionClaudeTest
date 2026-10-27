using System;

namespace SoccerVision.Simulation
{
    public enum Weather { Sunny, Cloudy, Rain, Snow }

    /// <summary>
    /// Weather and pitch for a match. Sunny/cloudy and day/night only change the picture; rain, snow, wind and a badly
    /// kept pitch change the football:
    ///
    /// - Rain: a wet surface lets the ball skid on and keep its pace (lower rolling resistance, a lower, skidding bounce),
    ///   the ball is harder to control and to dribble with, players lose their footing, slides go further, keepers spill
    ///   more. Where the pitch drains badly water stands in puddles that stop a rolling ball dead.
    /// - Snow: the ball dies in the snow (high rolling resistance, hardly any bounce), players are slower and slip often.
    /// - Wind: the air drag acts on the ball's speed relative to the moving air, so a lofted ball drifts with the wind and
    ///   carries further with it (shorter against it). Players allow for it, the better ones more.
    /// - Maintenance: a neglected pitch has bare, muddy patches where it is worn most (goalmouths, the centre, the
    ///   assistants' channels). Mud slows the ball and the players and makes them slip; in rain it holds the water.
    /// </summary>
    [Serializable]
    public sealed class MatchConditions
    {
        public Weather Weather = Weather.Sunny;
        /// <summary>How well the pitch is kept: 1 perfect, 0.5 average, 0 neglected.</summary>
        public float Maintenance = 1f;
        /// <summary>Wind speed in m/s (a strong wind is about 10-14 m/s).</summary>
        public float WindSpeed;
        /// <summary>Direction the wind blows towards, in world radians (0 = towards +X).</summary>
        public float WindAngle;
        /// <summary>Evening kick-off under floodlights (picture only).</summary>
        public bool Night;

        public bool IsWet => Weather == Weather.Rain;
        public bool IsSnow => Weather == Weather.Snow;
        public Vec2 Wind => Vec2.FromAngle(WindAngle) * WindSpeed;

        /// <summary>True when nothing about the football changes (a dry, perfect pitch and no wind).</summary>
        public bool AffectsPlay => IsWet || IsSnow || Maintenance < 0.999f || WindSpeed > 0.01f;

        public MatchConditions Clone() => (MatchConditions)MemberwiseClone();

        public void Sanitize()
        {
            Maintenance = MathUtil.Clamp01(Maintenance);
            WindSpeed = MathUtil.Clamp(WindSpeed, 0f, 25f);
        }
    }
}

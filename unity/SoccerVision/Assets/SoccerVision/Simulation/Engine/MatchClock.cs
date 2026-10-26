using System;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// The match clock (what the scoreboard shows). It runs continuously from each kick-off, like a real
    /// referee's watch, and stoppage-time is announced as a whole number of minutes at 45:00 / 90:00.
    /// </summary>
    public sealed class MatchClock
    {
        public MatchPeriod Period = MatchPeriod.NotStarted;
        /// <summary>Absolute clock seconds (second half starts at 45:00).</summary>
        public float Seconds;
        public bool Running;
        /// <summary>Announced added minutes for the current half, -1 until announced.</summary>
        public int AddedMinutes = -1;
        /// <summary>Clock seconds lost this half (goals, cards, penalties...) used for the added time.</summary>
        public float LostSeconds;
        public float RegulationHalfSeconds = 2700f;

        public float PeriodStartSeconds => Period == MatchPeriod.SecondHalf ? RegulationHalfSeconds : 0f;
        public float PeriodRegulationEnd => PeriodStartSeconds + RegulationHalfSeconds;
        public float PeriodElapsed => Seconds - PeriodStartSeconds;
        public bool InAddedTime => Seconds >= PeriodRegulationEnd;
        public float AddedTimeEnd => AddedMinutes < 0 ? float.PositiveInfinity : PeriodRegulationEnd + AddedMinutes * 60f;

        /// <summary>Football style minute: "1'" during the first minute, "45+2'" in added time.</summary>
        public string MinuteText
        {
            get
            {
                if (Period == MatchPeriod.NotStarted) return "0'";
                if (Period == MatchPeriod.HalfTime) return "HT";
                if (Period == MatchPeriod.FullTime) return "FT";
                float end = PeriodRegulationEnd;
                if (Seconds < end)
                {
                    int minute = (int)(Seconds / 60f) + 1;
                    return minute + "'";
                }
                int over = (int)((Seconds - end) / 60f) + 1;
                return ((int)(end / 60f)) + "+" + over + "'";
            }
        }

        /// <summary>Broadcast style mm:ss (keeps counting in added time, e.g. 47:12).</summary>
        public string ClockText
        {
            get
            {
                int total = (int)Seconds;
                return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
            }
        }
    }
}

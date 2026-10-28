using System;
using System.Collections.Generic;

namespace SoccerVision.Simulation
{
    public enum ScriptKind
    {
        /// <summary>A foul that earns a yellow card (the offender is remembered for a later second yellow).</summary>
        Yellow,
        /// <summary>A foul by the player cautioned earlier: second yellow, then the red card.</summary>
        SecondYellow,
        /// <summary>A reckless foul that earns a direct red card.</summary>
        DirectRed,
        /// <summary>A player goes down hurt and carries on.</summary>
        LightInjury,
        /// <summary>A player cannot continue: treated, helped off and replaced.</summary>
        SeriousInjury,
    }

    /// <summary>One thing the script makes happen, at the first fitting moment after <see cref="AtClockSeconds"/>.</summary>
    [Serializable]
    public sealed class ScriptItem
    {
        public ScriptKind Kind;
        /// <summary>Match clock seconds (45:00 = 2700) after which it may happen.</summary>
        public float AtClockSeconds;
        /// <summary>The team it happens to: 0 home, 1 away, -1 either.</summary>
        public int Team = -1;
    }

    /// <summary>
    /// A staged match: the same incidents in every run, whatever the weather and pitch, so a match can be repeated (for
    /// a trailer, for testing). The incidents are forced through the normal rules - the fouls, the cards, the injuries and
    /// the substitution all go the usual way - at the first fitting moment: a foul needs the ball carrier of the other team
    /// to be close to a defender, for example. With <see cref="Exclusive"/> nothing else of the kind happens: no random
    /// red cards, no second yellows, no random injuries.
    /// </summary>
    [Serializable]
    public sealed class MatchScript
    {
        public List<ScriptItem> Items = new List<ScriptItem>();
        public bool Exclusive = true;

        /// <summary>
        /// The staged match: a light injury (18th minute, Blue), a yellow card (24th, Red), a direct red card (33rd, Blue),
        /// a serious injury with a substitution (52nd, Red) and the second yellow and red card for the cautioned Red
        /// player (68th).
        /// </summary>
        public static MatchScript Staged() => new MatchScript
        {
            Items =
            {
                new ScriptItem { Kind = ScriptKind.LightInjury, AtClockSeconds = 18 * 60, Team = 1 },
                new ScriptItem { Kind = ScriptKind.Yellow, AtClockSeconds = 24 * 60, Team = 0 },
                new ScriptItem { Kind = ScriptKind.DirectRed, AtClockSeconds = 33 * 60, Team = 1 },
                new ScriptItem { Kind = ScriptKind.SeriousInjury, AtClockSeconds = 52 * 60, Team = 0 },
                new ScriptItem { Kind = ScriptKind.SecondYellow, AtClockSeconds = 68 * 60, Team = 0 },
            },
        };
    }
}

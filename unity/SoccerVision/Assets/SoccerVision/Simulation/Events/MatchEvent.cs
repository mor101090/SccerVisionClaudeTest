using System;

namespace SoccerVision.Simulation
{
    public enum MatchPeriod { NotStarted, FirstHalf, HalfTime, SecondHalf, FullTime }

    public enum MatchEventType
    {
        KickOff,
        Pass,
        Cross,
        LongBall,
        Clearance,
        Header,
        Shot,
        Goal,
        Save,
        ShotBlocked,
        ShotOffTarget,
        HitWoodwork,
        GoalkeeperClaim,
        Tackle,
        Interception,
        Foul,
        Advantage,
        YellowCard,
        RedCard,
        FreeKick,
        PenaltyAwarded,
        PenaltyMissed,
        Corner,
        GoalKick,
        ThrowIn,
        Offside,
        GoalkeeperHoldViolation,
        AddedTime,
        HalfTime,
        SecondHalfStart,
        FullTime,
        Whistle,
        NetHit,
    }

    /// <summary>Referee whistle patterns from the audio design section of the design document.</summary>
    public enum WhistleKind
    {
        /// <summary>Single short whistle: start/restart of play.</summary>
        Short,
        /// <summary>Quick, louder blasts: foul or violation.</summary>
        Foul,
        /// <summary>One long whistle: serious stoppage, penalty, goal.</summary>
        Long,
        /// <summary>Two short/medium whistles: end of the first half.</summary>
        HalfTime,
        /// <summary>Three whistles (short-short-long): end of the match.</summary>
        FullTime,
    }

    /// <summary>
    /// A discrete match event. Indices refer to MatchEngine.AllPlayers (-1 when unused) and
    /// Team is 0 (home) / 1 (away) / -1.
    /// </summary>
    [Serializable]
    public sealed class MatchEvent
    {
        public MatchEventType Type;
        public MatchPeriod Period;
        public float ClockSeconds;
        public string Minute = "";
        public int Team = -1;
        public int Player = -1;
        public int OtherPlayer = -1;
        public float X, Y;
        public float Value;
        public WhistleKind Whistle;
        /// <summary>Flavour of the event: for shots 1 = volley, 2 = cut inside from the wing.</summary>
        public int Detail;
        public string Text = "";

        public override string ToString() => Minute + " " + Type + " " + Text;
    }
}

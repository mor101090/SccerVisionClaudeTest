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
        SlideTackleMissed,
        /// <summary>A defender shields a ball running out of play with his body (Detail 0 goal kick, 1 throw-in).</summary>
        ShieldOut,
        /// <summary>Shoulder-to-shoulder race for a loose ball won by Player against OtherPlayer.</summary>
        ShoulderDuel,
        /// <summary>A player is hurt: Detail 0 light (plays on), 1 serious (has to go off).</summary>
        Injury,
        /// <summary>Player (the shirt index) is replaced: Info = "number|short name" of the player coming on.</summary>
        Substitution,
        /// <summary>Play restarted with a dropped ball after the referee stopped it for an injury (Law 8).</summary>
        DroppedBall,
    }

    /// <summary>
    /// Referee whistle signals. When the whistle is (and is not) blown follows the IFAB "Practical guidelines for
    /// match officials" (body language, communication and whistle); the blast lengths follow common refereeing
    /// practice: the stronger the offence, the longer and louder the blast.
    /// </summary>
    public enum WhistleKind
    {
        /// <summary>One clear, medium blast: start play (kick-off) or restart a free kick that waited for the whistle.</summary>
        Short,
        /// <summary>A sharp, firm blast: stop play for a foul.</summary>
        Foul,
        /// <summary>One long, strong blast: penalty kick or a sending-off offence.</summary>
        Long,
        /// <summary>Two blasts (short, then long): end of the first half.</summary>
        HalfTime,
        /// <summary>Three blasts (short, short, long): end of the match.</summary>
        FullTime,
        /// <summary>A quick, light tweet: stop play for offside or a technical offence.</summary>
        Light,
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
        /// <summary>
        /// Flavour of the event. Shots: 1 = volley, 2 = cut inside from the wing (a curler), 3 = direct free kick, 4 = penalty
        /// kick, 6 = side-footed into the near corner, 7 = side-footed into the far corner, 8 = curler to the far corner,
        /// 9 = chip over the keeper, 10 = outside of the boot, 11 = weaker foot, 0 = driven shot.
        /// Free kicks: 1 = dangerous position with a wall. Goals: shot kind (as for shots, 5 = header). Saves: 0 held, 1 tipped over, 2 round the post, 3 parried. Woodwork: 1 post, 2 crossbar.
        /// Tackles: 0 standing, 1 slide. Interceptions: 0 loose pass picked up, 1 read the pass and stepped in front of
        /// the receiver (OtherPlayer is then the receiver). Passes: Value 1 = through ball, 2 = pass into space.
        /// </summary>
        public int Detail;
        public string Text = "";
        /// <summary>Extra data: for a substitution, "number|short name" of the player coming on.</summary>
        public string Info = "";

        public override string ToString() => Minute + " " + Type + " " + Text;
    }
}

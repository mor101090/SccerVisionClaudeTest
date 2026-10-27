namespace SoccerVision.Simulation
{
    public enum CommentaryTone { Normal, Goal, Foul, Card, Chance, Info }

    /// <summary>
    /// Text commentary for the live commentary box (design doc examples: "#10 passes to #7", "Foul by #3").
    /// Lives in the simulation so Unity and the React UI show identical text.
    /// </summary>
    public static class Commentary
    {
        public static string Describe(MatchEvent e, MatchEngine m)
        {
            string P(int index)
            {
                var p = m.PlayerByIndex(index);
                return p == null ? "" : p.Profile.Label;
            }
            string T(int index) => index < 0 ? "" : m.TeamOf(index).Name;

            switch (e.Type)
            {
                case MatchEventType.KickOff:
                    return T(e.Team) + " kick off";
                case MatchEventType.SecondHalfStart:
                    return "Second half is about to begin";
                case MatchEventType.Pass:
                    if (e.OtherPlayer < 0) return P(e.Player) + " plays it forward";
                    return P(e.Player) + (e.Value > 0.5f ? " slides a through ball to " : " passes to ") + P(e.OtherPlayer);
                case MatchEventType.LongBall:
                    return P(e.Player) + " goes long" + (e.OtherPlayer >= 0 ? " for " + P(e.OtherPlayer) : "");
                case MatchEventType.Cross:
                    return P(e.Player) + " whips in a cross";
                case MatchEventType.Clearance:
                    return P(e.Player) + " clears";
                case MatchEventType.Header:
                    return P(e.Player) + " heads it" + (e.OtherPlayer >= 0 ? " to " + P(e.OtherPlayer) : "");
                case MatchEventType.Shot:
                    if (e.Detail == 1) return P(e.Player) + " volleys it first time!";
                    if (e.Detail == 2) return P(e.Player) + " cuts inside and shoots!";
                    if (e.Detail == 4) return P(e.Player) + " steps up... the penalty!";
                    if (e.Detail == 3) return P(e.Player) + " goes for goal from the free kick - over the wall!";
                    return P(e.Player) + " shoots!";
                case MatchEventType.Goal:
                    if (e.Value > 0.5f) return "GOAL! Own goal by " + P(e.Player) + " - " + T(e.Team) + " lead the way";
                    return "GOAL! " + P(e.Player) + (e.Detail == 5 ? " heads it in" : e.Detail == 1 ? " with a volley" : e.Detail == 3 ? " straight from the free kick" : e.Detail == 4 ? " from the spot" : " scores")
                           + " for " + T(e.Team) + (e.OtherPlayer >= 0 ? " (assist " + P(e.OtherPlayer) + ")" : "");
                case MatchEventType.Save:
                    if (e.Detail == 1) return P(e.Player) + " tips it over the bar!";
                    if (e.Detail == 2) return P(e.Player) + " pushes it round the post!";
                    if (e.Detail == 3) return P(e.Player) + " parries it away!";
                    return P(e.Player) + " saves and holds on to it!";
                case MatchEventType.ShotBlocked:
                    return "Blocked by " + P(e.Player);
                case MatchEventType.ShotOffTarget:
                    return P(e.Player) + " misses the target";
                case MatchEventType.HitWoodwork:
                    return e.Detail == 2 ? "Off the crossbar!" : "Off the post!";
                case MatchEventType.GoalkeeperClaim:
                    return e.Value > 0.5f ? P(e.Player) + " punches clear" : P(e.Player) + " claims it";
                case MatchEventType.Tackle:
                    return e.Detail == 1
                        ? "Great sliding tackle by " + P(e.Player) + " on " + P(e.OtherPlayer)
                        : P(e.Player) + " wins it from " + P(e.OtherPlayer);
                case MatchEventType.SlideTackleMissed:
                    return P(e.Player) + " goes to ground but " + P(e.OtherPlayer) + " rides the challenge";
                case MatchEventType.Interception:
                    return P(e.Player) + " intercepts";
                case MatchEventType.Foul:
                    return "Foul by " + P(e.Player) + " on " + P(e.OtherPlayer);
                case MatchEventType.Advantage:
                    return "Advantage played";
                case MatchEventType.YellowCard:
                    return "Yellow card: " + P(e.Player);
                case MatchEventType.RedCard:
                    return (e.Value > 0.5f ? "Second yellow - red card: " : "Red card! ") + P(e.Player) + " is sent off";
                case MatchEventType.FreeKick:
                    if (e.Detail == 1) return "Free kick to " + T(e.Team) + " in a dangerous position - " + P(e.Player) + " stands over it, the wall lines up";
                    return "Free kick to " + T(e.Team);
                case MatchEventType.PenaltyAwarded:
                    return "PENALTY to " + T(e.Team) + "!";
                case MatchEventType.PenaltyMissed:
                    return P(e.Player) + " misses the penalty";
                case MatchEventType.Corner:
                    return "Corner to " + T(e.Team);
                case MatchEventType.GoalKick:
                    return "Goal kick for " + T(e.Team);
                case MatchEventType.ThrowIn:
                    return "Throw-in for " + T(e.Team);
                case MatchEventType.Offside:
                    return "Offside! " + P(e.Player) + " was caught behind the line";
                case MatchEventType.GoalkeeperHoldViolation:
                    return P(e.Player) + " held the ball too long - corner kick";
                case MatchEventType.AddedTime:
                    return "+" + (int)e.Value + " minutes of added time";
                case MatchEventType.HalfTime:
                    return "Half time: " + m.Home.Name + " " + m.Home.Score + " - " + m.Away.Score + " " + m.Away.Name;
                case MatchEventType.FullTime:
                    return "Full time: " + m.Home.Name + " " + m.Home.Score + " - " + m.Away.Score + " " + m.Away.Name;
                default:
                    return "";
            }
        }

        public static CommentaryTone ToneOf(MatchEventType type)
        {
            switch (type)
            {
                case MatchEventType.Goal: return CommentaryTone.Goal;
                case MatchEventType.Foul:
                case MatchEventType.Offside:
                case MatchEventType.GoalkeeperHoldViolation: return CommentaryTone.Foul;
                case MatchEventType.YellowCard:
                case MatchEventType.RedCard:
                case MatchEventType.PenaltyAwarded: return CommentaryTone.Card;
                case MatchEventType.Shot:
                case MatchEventType.Save:
                case MatchEventType.HitWoodwork: return CommentaryTone.Chance;
                case MatchEventType.KickOff:
                case MatchEventType.HalfTime:
                case MatchEventType.FullTime:
                case MatchEventType.AddedTime:
                case MatchEventType.SecondHalfStart: return CommentaryTone.Info;
                default: return CommentaryTone.Normal;
            }
        }

        /// <summary>Events worth a line in the commentary box (whistles and net hits are audio/visual only).</summary>
        public static bool IsCommentary(MatchEventType type)
        {
            return type != MatchEventType.Whistle && type != MatchEventType.NetHit;
        }
    }
}

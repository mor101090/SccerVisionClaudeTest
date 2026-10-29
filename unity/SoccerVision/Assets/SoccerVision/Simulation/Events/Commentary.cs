namespace SoccerVision.Simulation
{
    public enum CommentaryTone { Normal, Goal, Foul, Card, Chance, Info, Red }

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
                    if (e.Value > 3.5f) return P(e.Player) + " and " + P(e.OtherPlayer) + " play a one-two";
                    if (e.Value > 2.5f) return P(e.Player) + " switches the play to " + P(e.OtherPlayer);
                    if (e.Value > 1.5f) return P(e.Player) + " plays it into space for " + P(e.OtherPlayer);
                    return P(e.Player) + (e.Value > 0.5f ? " slides a through ball to " : " passes to ") + P(e.OtherPlayer);
                case MatchEventType.LongBall:
                    return P(e.Player) + " goes long" + (e.OtherPlayer >= 0 ? " for " + P(e.OtherPlayer) : "");
                case MatchEventType.Cross:
                    if (e.Detail > 0)
                    {
                        string swing = e.Value > 1.5f ? "an out-swinger" : "an in-swinger";
                        switch (e.Detail)
                        {
                            case 1: return P(e.Player) + " swings " + swing + " to the near post";
                            case 2: return P(e.Player) + " curls " + swing + " to the far post";
                            case 4: return P(e.Player) + " pulls the corner back to the edge of the box";
                            default: return P(e.Player) + " whips " + swing + " into the middle";
                        }
                    }
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
                    if (e.Detail == 6) return P(e.Player) + " side-foots it towards the near corner";
                    if (e.Detail == 7) return P(e.Player) + " passes it towards the far corner";
                    if (e.Detail == 8) return P(e.Player) + " tries to curl it into the far corner!";
                    if (e.Detail == 9) return P(e.Player) + " tries to chip the keeper!";
                    if (e.Detail == 10) return P(e.Player) + " tries the outside of the boot!";
                    if (e.Detail == 11) return P(e.Player) + " has a go with his weaker foot";
                    return P(e.Player) + " shoots!";
                case MatchEventType.Goal:
                    if (e.Value > 0.5f) return "GOAL! Own goal by " + P(e.Player) + " - " + T(e.Team) + " lead the way";
                    return "GOAL! " + P(e.Player) + GoalHow(e.Detail)
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
                    return e.Detail == 1
                        ? P(e.Player) + " reads it and steps in front of " + P(e.OtherPlayer)
                        : P(e.Player) + " intercepts";
                case MatchEventType.Injury:
                    return e.Detail == 1
                        ? P(e.Player) + " is down injured - the physio is on, he can't continue"
                        : P(e.Player) + " takes a knock but carries on";
                case MatchEventType.Substitution:
                    return "Substitution for " + T(e.Team);
                case MatchEventType.SubstitutionRequest:
                    return T(e.Team) + " get ready to make a change";
                case MatchEventType.MomentumShift:
                    return T(e.Team) + " have the momentum";
                case MatchEventType.TacticsChanged:
                    return T(e.Team) + " change their tactics";
                case MatchEventType.DroppedBall:
                    return "The referee restarts with a dropped ball for " + T(e.Team);
                case MatchEventType.ShieldOut:
                    return P(e.Player) + " shields it out" + (e.OtherPlayer >= 0 ? " from " + P(e.OtherPlayer) : "") + (e.Detail == 0 ? " for a goal kick" : " for a throw-in");
                case MatchEventType.ShoulderDuel:
                    return P(e.Player) + " muscles " + P(e.OtherPlayer) + " off the ball";
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
                case MatchEventType.Slip:
                    if (e.Detail == 1) return P(e.Player) + " loses his footing and the ball runs away from him";
                    return P(e.Player) + " slips on the " + (m.Conditions.IsSnow ? "snow" : m.Surface != null && m.Surface.MudAt(e.X, e.Y) > 0.3f ? "mud" : "wet grass");
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

        /// <summary>"Substitution for Red Rovers: #14 Name comes on for #7 Name".</summary>
        public static string Substitution(TeamAgent team, PlayerAgent incoming, string outgoingLabel)
        {
            return "Substitution for " + team.Name + ": " + incoming.Label + " comes on for " + outgoingLabel;
        }

        /// <summary>How the goal went in, from the shot kind (see MatchEvent.Detail).</summary>
        private static string GoalHow(int detail)
        {
            switch (detail)
            {
                case 1: return " with a volley";
                case 2: return " cuts inside and curls it in";
                case 3: return " straight from the free kick";
                case 4: return " from the spot";
                case 5: return " heads it in";
                case 6: return " side-foots it into the near corner";
                case 7: return " passes it into the far corner";
                case 8: return " curls it into the far corner";
                case 9: return " chips the keeper";
                case 10: return " with the outside of the boot";
                case 11: return " on his weaker foot";
                default: return " scores";
            }
        }

        public static CommentaryTone ToneOf(MatchEventType type)
        {
            switch (type)
            {
                case MatchEventType.Goal: return CommentaryTone.Goal;
                case MatchEventType.Foul:
                case MatchEventType.Offside:
                case MatchEventType.GoalkeeperHoldViolation:
                case MatchEventType.Injury: return CommentaryTone.Foul;
                case MatchEventType.Substitution:
                case MatchEventType.SubstitutionRequest:
                case MatchEventType.MomentumShift:
                case MatchEventType.TacticsChanged: return CommentaryTone.Info;
                case MatchEventType.RedCard: return CommentaryTone.Red;
                case MatchEventType.YellowCard:
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

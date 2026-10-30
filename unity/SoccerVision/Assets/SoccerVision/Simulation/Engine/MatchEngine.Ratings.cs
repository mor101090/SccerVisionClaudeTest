using System;
using System.Collections.Generic;

namespace SoccerVision.Simulation
{
    /// <summary>One player's match: when he came on and off, what he did, and his rating.</summary>
    public sealed class PlayerRecord
    {
        public PlayerProfile Profile;
        public TeamAgent Team;
        /// <summary>The agent he is on the pitch as (null once he has come off).</summary>
        public PlayerAgent Agent;
        public int SlotIndex;
        public float OnClock, OffClock = -1f;
        /// <summary>The sum of what he did (goals, assists, saves, tackles, mistakes, cards...) in rating points.</summary>
        public float Points;
        public int Goals, Assists, OwnGoals, YellowCards;
        public bool SentOff;
        // (taken from the agent when he goes off)
        public int PassesAttempted, PassesCompleted, Dribbles;
        public bool ManOfTheMatch;
        public float Rating = 6f;
    }

    /// <summary>
    /// Player ratings, 1 to 10, as the match goes (like the ratings on a match centre): everyone starts at 6.0 and moves
    /// with what he does - a goal +1.1 (an own goal -1.0), an assist +0.7, a save +0.3, a tackle won +0.12, an interception
    /// +0.1, a take-on +0.05, a shot +0.05 (on the woodwork +0.1), a foul -0.1, a penalty given away -0.5, a yellow card
    /// -0.4, a red card -1.6; his passing (completion against 75%, weighed by how much he passed); a goal conceded costs
    /// the keeper 0.35 and each defender 0.15; the score moves everyone a little (up to +-0.45); a clean sheet is worth
    /// up to +0.3 to the keeper and the defenders. At full time the best rated is the man of the match.
    /// </summary>
    public sealed partial class MatchEngine
    {
        private void OpenRecord(PlayerAgent p)
        {
            var r = new PlayerRecord { Profile = p.Profile, Team = p.Team, Agent = p, SlotIndex = p.SlotIndex, OnClock = Clock.Seconds };
            p.Record = r;
            p.Team.Records.Add(r);
        }

        private void CloseRecord(PlayerAgent p)
        {
            var r = p.Record;
            if (r == null) return;
            r.PassesAttempted = p.PassesAttempted;
            r.PassesCompleted = p.PassesCompleted;
            r.Dribbles = p.Dribbles;
            r.Rating = RatingOf(r);
            r.OffClock = Clock.Seconds;
            r.Agent = null;
            p.Record = null;
        }

        private static void Add(PlayerAgent p, float points)
        {
            if (p?.Record != null) p.Record.Points += points;
        }

        private void RateEvent(MatchEvent e, PlayerAgent player, PlayerAgent other)
        {
            switch (e.Type)
            {
                case MatchEventType.Goal:
                    if (e.Value > 0.5f) { Add(player, -1.0f); if (player?.Record != null) player.Record.OwnGoals++; }
                    else
                    {
                        Add(player, 1.1f);
                        if (player?.Record != null) player.Record.Goals++;
                        if (other != null && other.Team == player?.Team) { Add(other, 0.7f); if (other.Record != null) other.Record.Assists++; }
                    }
                    var conceding = TeamOf(e.Team).Opponent;
                    foreach (var q in conceding.Players)
                    {
                        if (q.SentOff) continue;
                        if (q.IsGoalkeeper) Add(q, -0.35f);
                        else if (q.Slot.Line == Line.Defence) Add(q, -0.15f);
                    }
                    break;
                case MatchEventType.Shot: Add(player, 0.05f); break;
                case MatchEventType.HitWoodwork: Add(player, 0.1f); break;
                case MatchEventType.Save: Add(player, 0.3f); break;
                case MatchEventType.GoalkeeperClaim: Add(player, 0.05f); break;
                case MatchEventType.ShotBlocked: Add(player, 0.1f); break;
                case MatchEventType.Tackle: Add(player, 0.12f); break;
                case MatchEventType.Interception: Add(player, 0.1f); break;
                case MatchEventType.Foul: Add(player, -0.1f); break;
                case MatchEventType.PenaltyAwarded: Add(other, -0.5f); Add(player, 0.2f); break;
                case MatchEventType.YellowCard: Add(player, -0.4f); if (player?.Record != null) player.Record.YellowCards++; break;
                case MatchEventType.RedCard: Add(player, -1.6f); if (player?.Record != null) player.Record.SentOff = true; break;
                case MatchEventType.FullTime: FinishRatings(); break;
            }
        }

        /// <summary>The rating of a player now (live) or when he came off.</summary>
        public float RatingOf(PlayerRecord r)
        {
            var p = r.Agent;
            int att = p != null ? p.PassesAttempted : r.PassesAttempted, comp = p != null ? p.PassesCompleted : r.PassesCompleted;
            int drib = p != null ? p.Dribbles : r.Dribbles;
            float v = 6f + r.Points;
            if (att > 0) v += ((float)comp / att - 0.75f) * 1.6f * MathF.Min(1f, att / 25f);
            v += 0.05f * MathF.Min(drib, 8);
            // how long he played: the score and a clean sheet count in proportion
            float end = r.OffClock >= 0f ? r.OffClock : Clock.Seconds;
            float share = MathUtil.Clamp01((end - r.OnClock) / (Clock.RegulationHalfSeconds * 2f));
            int diff = r.Team.Score - r.Team.Opponent.Score;
            v += MathUtil.Clamp(diff * 0.15f, -0.45f, 0.45f) * share;
            bool back = r.Profile.NaturalPosition == PositionGroup.Goalkeeper || r.Profile.NaturalPosition == PositionGroup.Defender;
            if (back && r.Team.Opponent.Score == 0) v += 0.3f * share * MathUtil.Clamp01(Clock.Seconds / (Clock.RegulationHalfSeconds * 2f));
            return MathUtil.Clamp(v, 1f, 10f);
        }

        /// <summary>Brings the live ratings up to date (call before reading them; cheap).</summary>
        public void UpdateRatings()
        {
            foreach (var t in Teams)
                foreach (var r in t.Records)
                    if (r.Agent != null) r.Rating = RatingOf(r);
        }

        private void FinishRatings()
        {
            UpdateRatings();
            PlayerRecord best = null;
            foreach (var t in Teams)
                foreach (var r in t.Records)
                {
                    // (the winners' best usually gets it: a small edge for the winning side)
                    float score = r.Rating + (r.Team.Score > r.Team.Opponent.Score ? 0.2f : 0f);
                    float bestScore = best == null ? float.NegativeInfinity : best.Rating + (best.Team.Score > best.Team.Opponent.Score ? 0.2f : 0f);
                    if (score > bestScore) best = r;
                }
            if (best != null) best.ManOfTheMatch = true;
        }
    }
}

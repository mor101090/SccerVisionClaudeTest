using System;

namespace SoccerVision.Simulation
{
    /// <summary>What a player is doing with his arms and his face (drawn as a small sign on the disc): purely visual.</summary>
    public enum GestureKind
    {
        None = 0,
        /// <summary>An arm out: "step up!" / "get across!" - the defence leader organising the line or a set piece.</summary>
        Point = 1,
        /// <summary>A hand up towards the ball: "I am free, give it to me" (a run in behind).</summary>
        CallForBall = 2,
        /// <summary>The keeper shouting (a corner, a free kick, a cross coming in).</summary>
        Shout = 3,
        /// <summary>An arm out towards the referee: "that was never a foul".</summary>
        Protest = 4,
        /// <summary>Hands on the head: a miss, a goal conceded.</summary>
        Disappointed = 5,
        /// <summary>Both hands up at a team-mate: a bad pass, a ball not played to him.</summary>
        Complain = 6,
        /// <summary>A pat on the back: the leader lifts a team-mate.</summary>
        Encourage = 7,
        /// <summary>Arms up: the scorer.</summary>
        Celebrate = 8,
        /// <summary>The team-mates joining the scorer.</summary>
        Hug = 9,
    }

    /// <summary>
    /// The human side of the match: gestures and calls (shown as signs on the circles), the personal confidence of a
    /// player, the big-game player's lift, the crowd's pressure on the referee.
    /// </summary>
    public sealed partial class MatchEngine
    {
        private static int GesturePriority(GestureKind k) =>
            k == GestureKind.Celebrate || k == GestureKind.Hug ? 5 : k == GestureKind.Disappointed || k == GestureKind.Protest ? 3 : 2;

        /// <summary>Shows a gesture on a player for a few seconds (a lesser one never replaces a stronger one that is still on).</summary>
        internal void Gesture(PlayerAgent p, GestureKind kind, float seconds, float angle)
        {
            if (p == null || p.SentOff || kind == GestureKind.None) return;
            if (p.Gesture != GestureKind.None && p.GestureUntil > SimTime && GesturePriority(p.Gesture) > GesturePriority(kind)) return;
            p.Gesture = kind;
            p.GestureLen = seconds;
            p.GestureUntil = SimTime + seconds;
            p.GestureAngle = angle;
            if (CollectDiagnostics) Count("gesture." + kind);
            // a team-mate who has just let the side down is picked up by the leader
            if (kind == GestureKind.Disappointed || kind == GestureKind.Complain)
            {
                var leader = FindLeader(p.Team, p);
                if (leader != null && Vec2.Distance(leader.Pos, p.Pos) < 30f && leader.Gesture != GestureKind.Encourage)
                    Gesture(leader, GestureKind.Encourage, 1.6f, (p.Pos - leader.Pos).Angle);
            }
        }

        /// <summary>The replay code of what the player is doing: kind * 100000 + fade (0-99) * 1000 + angle in degrees (0 means nothing).</summary>
        public int GestureCode(PlayerAgent p)
        {
            if (p.Gesture == GestureKind.None || p.SentOff) return 0;
            float left = p.GestureUntil - SimTime;
            if (left <= 0f) return 0;
            int fade = (int)MathUtil.Clamp(MathF.Round(99f * left / MathF.Max(0.1f, p.GestureLen)), 1f, 99f);
            float deg = p.GestureAngle * 180f / MathF.PI;
            int ang = ((int)MathF.Round(deg) % 360 + 360) % 360;
            return (int)p.Gesture * 100000 + fade * 1000 + ang;
        }

        /// <summary>The team's leader on the pitch (never <paramref name="except"/>): the man with the trait, or null.</summary>
        internal static PlayerAgent FindLeader(TeamAgent team, PlayerAgent except = null)
        {
            PlayerAgent best = null;
            foreach (var q in team.Players)
            {
                if (q.SentOff || q == except || (q.Profile.Traits & PlayerTrait.Leader) == 0) continue;
                if (best == null || q.Attr.Teamwork + q.Attr.Decisions > best.Attr.Teamwork + best.Attr.Decisions) best = q;
            }
            return best;
        }

        /// <summary>Who calls the line: the leader, otherwise the centre-back who reads the game best.</summary>
        private PlayerAgent Organiser(TeamAgent team)
        {
            var l = FindLeader(team);
            if (l != null) return l;
            PlayerAgent best = null;
            foreach (var q in team.Players)
            {
                if (q.SentOff || q.IsGoalkeeper || !q.Slot.IsCentreBack) continue;
                if (best == null || q.Attr.Positioning + q.Attr.Decisions > best.Attr.Positioning + best.Attr.Decisions) best = q;
            }
            return best;
        }

        /// <summary>"Step up!": the line moves out together after a clearance or when the trap is sprung (at most every few seconds).</summary>
        private void CallStepUp(TeamAgent team)
        {
            if (SimTime - team.LastCallAt < 7f) return;
            team.LastCallAt = SimTime;
            Gesture(Organiser(team), GestureKind.Point, 1.4f, team.DirToWorld(Vec2.UnitX).Angle);
        }

        /// <summary>Before a dangerous set piece the keeper shouts and the leader points the markers to their men.</summary>
        private void OrganiseDefence(RestartType type, TeamAgent taking, Vec2 spot)
        {
            var defending = taking.Opponent;
            bool danger = type == RestartType.CornerKick || type == RestartType.PenaltyKick
                          || (type == RestartType.DirectFreeKick && taking.Forward(spot) > 16f);
            if (!danger) return;
            var gk = defending.Goalkeeper;
            if (gk != null) Gesture(gk, GestureKind.Shout, 2.6f, (spot - gk.Pos).Angle);
            var org = Organiser(defending);
            if (org != null && org != gk) Gesture(org, GestureKind.Point, 2.2f, (spot - org.Pos).Angle);
        }

        // ---- a player's own confidence --------------------------------------------------------------------

        /// <summary>A goal, a save or a tackle won lifts a player; a miss or a goal conceded drains him (it fades over minutes).</summary>
        internal void AddConfidence(PlayerAgent p, float delta)
        {
            if (p == null || p.SentOff || delta == 0f) return;
            p.Confidence = MathUtil.Clamp(p.Confidence + delta, -1f, 1f);
            p.RefreshPersonal();
        }

        private float _bigGameBonus;

        /// <summary>The lift of the big-game players: the last quarter of an hour of a close match, extra time; flat when it is settled.</summary>
        private void UpdateBigGameBonus()
        {
            float minute = Clock.Seconds / 60f;
            int diff = Math.Abs(Home.Score - Away.Score);
            bool extra = Clock.Period == MatchPeriod.ExtraTimeFirstHalf || Clock.Period == MatchPeriod.ExtraTimeSecondHalf;
            _bigGameBonus = (extra || (minute >= 75f && diff <= 1)) ? 0.07f : diff >= 3 ? -0.04f : 0f;
        }

        /// <summary>Per player, now and then: his confidence fades towards nothing, the big-game player feels the match.</summary>
        private void UpdatePersonalState(PlayerAgent p, float clockDt)
        {
            if (p.Confidence != 0f)
            {
                p.Confidence = MathUtil.MoveTowards(p.Confidence, 0f, clockDt / 420f);
                p.RefreshPersonal();
            }
            float bonus = (p.Profile.Traits & PlayerTrait.BigGamePlayer) != 0 ? _bigGameBonus : 0f;
            if (bonus != p.SituationBonus) { p.SituationBonus = bonus; p.RefreshPersonal(); }
        }

        // ---- the crowd and the referee -----------------------------------------------------------------

        /// <summary>
        /// How hard the home crowd presses on the referee (1 = as usual): more when its team is behind late, or level
        /// with the minutes running out. It tilts the 50-50 calls, never the clear ones.
        /// </summary>
        internal float CrowdIntensity()
        {
            float minute = Clock.Seconds / 60f;
            int diff = Home.Score - Away.Score;
            float late = MathUtil.Clamp01((minute - 55f) / 35f);
            float need = diff < 0 ? 1f : diff == 0 ? 0.5f : 0f;
            return 1f + 0.6f * late * need;
        }

        // ---- reactions ----------------------------------------------------------------------------------

        /// <summary>After a goal: the scorer and those who join him celebrate, the others hang their heads, the leader lifts them.</summary>
        private void OnGoalReactions(TeamAgent scoring, TeamAgent defending, PlayerAgent scorer, PlayerAgent assist, bool ownGoal)
        {
            float len = Config.GoalCelebrationSeconds * 0.5f;
            if (!ownGoal && scorer != null)
            {
                Gesture(scorer, GestureKind.Celebrate, len, 0f);
                AddConfidence(scorer, 0.6f);
                foreach (var q in _celebrationJoiners) Gesture(q, GestureKind.Hug, len * 0.8f, (scorer.Pos - q.Pos).Angle);
                if (assist != null) AddConfidence(assist, 0.3f);
            }
            // the defence: the keeper and the man nearest the scorer
            var gk = defending.Goalkeeper;
            if (gk != null) { Gesture(gk, GestureKind.Disappointed, 3.5f, 0f); if (!ownGoal) AddConfidence(gk, -0.25f); }
            PlayerAgent nearest = null;
            float bd = 1e9f;
            foreach (var q in defending.Players)
            {
                if (q.SentOff || q.IsGoalkeeper) continue;
                float d = Vec2.DistanceSq(q.Pos, Ball.Pos);
                if (d < bd) { bd = d; nearest = q; }
            }
            if (nearest != null) Gesture(nearest, GestureKind.Disappointed, 3f, 0f);
            // the conceding side's leader picks the others up (a goal against hurts the team's momentum less)
            var leader = FindLeader(defending, nearest);
            if (leader != null && leader != gk) Gesture(leader, GestureKind.Encourage, 3f, 0f);
        }

        /// <summary>A shot that missed or was saved: the man who should have scored puts his hands on his head.</summary>
        private void OnShotWasted(PlayerAgent shooter, float xg)
        {
            if (shooter == null) return;
            AddConfidence(shooter, -0.1f - 0.2f * xg);
            if (xg >= 0.15f) Gesture(shooter, GestureKind.Disappointed, 1.8f, 0f);
        }

        /// <summary>A foul is given against him (or a card): now and then he protests to the referee, the hot-headed one more.</summary>
        private void MaybeProtest(PlayerAgent offender, float baseChance)
        {
            if (offender == null || offender.SentOff) return;
            float aggression = MathUtil.A(offender.Attr.Aggression), composure = MathUtil.A(offender.Attr.Composure);
            float p = baseChance * (0.5f + aggression) * (1.4f - composure);
            if (!_rng.Chance(MathUtil.Clamp(p, 0f, 0.9f))) return;
            Gesture(offender, GestureKind.Protest, 1.8f, (Referee.Pos - offender.Pos).Angle);
        }
    }
}

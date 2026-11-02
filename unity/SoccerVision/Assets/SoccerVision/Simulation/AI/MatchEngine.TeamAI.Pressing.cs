using System;
using System.Collections.Generic;
using System.Linq;

namespace SoccerVision.Simulation
{
    /// <summary>Out of possession - the ball: who presses (never more than two on the ball), cover, anticipation, shielding, intercepting.</summary>
    public sealed partial class MatchEngine
    {
        private void AssignBallDuties()
        {
            foreach (var t in Teams)
            {
                t.Chaser = null;
                t.Cover = null;
                t.Anticipator = null;
                t.Shielder = null;
                if (t.Presser != null && (t.Presser.SentOff || t.Presser.IsDown || Ball.Owner == null)) t.Presser = null;
                t.SecondPresser = null;
            }

            if (Ball.Owner != null)
            {
                var carrier = Ball.Owner;
                var def = carrier.Team.Opponent;
                if (Ball.InHands)
                {
                    def.Presser = null;
                    return;
                }
                PlayerAgent best = null, second = null;
                float t1 = float.PositiveInfinity, t2 = float.PositiveInfinity;
                Vec2 goalSide = carrier.Pos + (def.OwnGoal - carrier.Pos).Normalized * 1.2f;
                foreach (var d in def.Players)
                {
                    if (d.SentOff || d.IsGoalkeeper || d.IsDown) continue;
                    float t = d.TimeToReach(goalSide);
                    if (d == def.Presser) t *= 0.8f; // hysteresis: keep the same presser
                    // In the middle third the central midfielders go and win it (the holding one first).
                    float carrierF = def.Forward(carrier.Pos);
                    if (d.Slot.IsCentralMidfielder && carrierF > -28f && carrierF < 18f) t *= d == def.Anchor ? 0.8f : 0.9f;
                    t *= RolePressFactor(d);
                    if (d.Has(PlayerTrait.PressesHard)) t *= 0.88f;          // chases everything
                    else if (d.Has(PlayerTrait.SavesEnergy)) t *= 1.15f;    // picks his moments
                    if (d.Has(PlayerTrait.HoldsPosition)) t *= 1.2f;          // keeps to his post
                    if (t < t1) { second = best; t2 = t1; best = d; t1 = t; }
                    else if (t < t2) { second = d; t2 = t; }
                }
                // staged match: the cautioned player goes at the ball carrier (he is about to commit his second foul)
                if (_scriptPresser != null && !_scriptPresser.SentOff && !_scriptPresser.IsDown && _scriptPresser.Team == def)
                {
                    def.Presser = _scriptPresser;
                    AssignCover(def, carrier);
                    return;
                }
                var tac = def.Eff;
                // Pressing trigger: how far from his zone a player will engage the carrier (a high block engages earlier).
                float engage = 8f + 14f * tac.PressingIntensity + 6f * (tac.DefensiveLine - 0.5f);
                // Counter-press: for the first seconds after losing the ball, the players round it swarm the new carrier
                // at once (before the opponent can look up), the second man too - the best moment to win it back.
                bool counterPress = SimTime - def.LostBallAt < CounterPressWindow(def) && Vec2.Distance(carrier.Pos, def.LostBallSpot) < 22f;
                if (counterPress)
                {
                    engage += 12f;
                    if (def.CounterPressCountedFor != def.LostBallAt) { def.CounterPressCountedFor = def.LostBallAt; def.Stats.CounterPresses++; }
                }
                if (best != null)
                {
                    float fromZone = Vec2.Distance(best.ShapeTarget, carrier.Pos);
                    if (best.Has(PlayerTrait.PressesHard)) fromZone -= 4f; else if (best.Has(PlayerTrait.SavesEnergy)) fromZone += 3f;
                    bool inOwnThird = def.Forward(carrier.Pos) < -18f;
                    if (fromZone < engage || inOwnThird || t1 < 1.2f) def.Presser = best;
                    else def.Presser = null;
                }
                if (def.Presser != null && second != null && (tac.PressingIntensity > 0.3f || counterPress) && t2 < 1.4f + 1.6f * tac.PressingIntensity + (counterPress ? 1.2f : 0f))
                {
                    def.SecondPresser = second; // never more than two on the ball
                }
                AssignCover(def, carrier);
                return;
            }

            // Free ball: each team sends its fastest interceptor; the intended receiver always goes.
            foreach (var t in Teams)
            {
                PlayerAgent best = null;
                float bestT = float.PositiveInfinity;
                foreach (var p in t.Players)
                {
                    if (p.SentOff || p.IsDown) continue;
                    if (p.IsGoalkeeper && !GoalkeeperMayChase(p)) continue;
                    UpdateIntercept(p);
                    float tt = p.InterceptTime;
                    if (p.IsGoalkeeper) tt -= 0.3f; // keepers take responsibility in their area
                    if (tt < bestT) { bestT = tt; best = p; }
                }
                var receiver = Ball.IntendedReceiver;
                if (receiver != null && receiver.Team == t && !receiver.SentOff)
                {
                    UpdateIntercept(receiver);
                    t.Chaser = receiver;
                }
                else
                {
                    t.Chaser = best;
                }
            }
            AssignAnticipation();
            AssignShielding();
        }

        /// <summary>How eagerly a role goes to the ball carrier (a time multiplier: below 1 he goes sooner).</summary>
        private static float RolePressFactor(PlayerAgent d)
        {
            switch (d.PlayRole)
            {
                case PlayerRole.Destroyer: return 0.85f;
                case PlayerRole.Carrier: case PlayerRole.WideTargetMan: case PlayerRole.BoxToBoxDefensive: return 0.9f;
                case PlayerRole.AnchorMan: case PlayerRole.CoverDefender: return 1.2f;
                case PlayerRole.FalseNine: case PlayerRole.ClassicPlaymaker: case PlayerRole.TempoSetter: return 1.1f;
                case PlayerRole.Regista: return 1.15f;       // defends by reading the lanes, not by chasing
                case PlayerRole.Trequartista: return 1.35f;  // hardly defends
                case PlayerRole.Stopper: return 0.9f;
                default: return 1f;
            }
        }

        /// <summary>How long the counter-press lasts after losing the ball (sim seconds): longer for a pressing team, hardly at all for a bunker.</summary>
        private static float CounterPressWindow(TeamAgent team)
        {
            float w = 1.6f + 3f * team.Eff.PressingIntensity;
            if (team.Tactics.Defence == DefenceStyle.ParkTheBus || team.Tactics.Special == SpecialTactic.TimeWasting) w *= 0.3f;
            return w;
        }

        /// <summary>
        /// Cover (the second defender): when the ball carrier is pressed in or near our half, the nearest team-mate
        /// takes up a position diagonally behind the presser, on the inside, so that a carrier who beats the first man
        /// runs into the second. "Pressure, cover, balance" - the rest of the line stays flat and tucks in.
        /// </summary>
        private void AssignCover(TeamAgent team, PlayerAgent carrier)
        {
            var presser = team.Presser;
            if (presser == null) return;
            float carrierF = team.Forward(carrier.Pos);
            if (carrierF > 12f) return; // pressing high up the pitch: the shape behind is the cover
            Vec2 toGoal = (team.OwnGoal - carrier.Pos).Normalized;
            float lat = team.Lateral(carrier.Pos);
            Vec2 inward = MathF.Abs(lat) < 4f ? Vec2.Zero : team.DirToWorld(new Vec2(0f, -MathF.Sign(lat)));
            Vec2 spot = Pitch.ClampToField(carrier.Pos + toGoal * 6f + inward * 4.5f, -1f);
            bool deep = carrierF < 0f;
            // In shooting range the second man gets into the line of the shot instead (blocking it comes first near the box).
            float toOwnGoal = Vec2.Distance(carrier.Pos, team.OwnGoal);
            bool block = toOwnGoal < 27f && MathF.Abs(lat) < 20f;
            if (block) spot = carrier.Pos + toGoal * MathF.Min(3.2f, toOwnGoal * 0.3f);
            PlayerAgent best = null;
            float bestD = 16f;
            foreach (var p in team.Players)
            {
                if (p.SentOff || p.IsGoalkeeper || p.IsDown || p == presser || p == team.SecondPresser) continue;
                if (p.Slot.Line == Line.Attack || (deep && p.Slot.Line != Line.Defence)) continue;
                float d = Vec2.Distance(p.Pos, spot);
                if (d < bestD) { bestD = d; best = p; }
            }
            if (best == null) return;
            team.Cover = best;
            team.CoverSpot = spot;
            if (CollectDiagnostics) Count(block ? "cover.block" : "cover");
        }

        /// <summary>
        /// Reading the game: when a pass is played to an opponent, his marker (or the nearest defender) judges whether
        /// he can get to the ball first and, if so, steps in front of him to intercept instead of waiting goal-side.
        /// Better positioning and decisions mean he reads it sooner - and gambles less.
        /// </summary>
        private void AssignAnticipation()
        {
            var receiver = Ball.IntendedReceiver;
            if (receiver == null || receiver.SentOff || Ball.Owner != null) return;
            var team = receiver.Team.Opponent;
            PlayerAgent marker = null;
            float bestD = 7f;
            foreach (var p in team.Players)
            {
                if (p.SentOff || p.IsGoalkeeper || p.IsDown) continue;
                float d = Vec2.Distance(p.Pos, receiver.Pos);
                if (p.MarkTarget == receiver) d -= 3f;
                if (p.Slot.IsCentralMidfielder) d -= 2f;
                if (p.PlayRole == PlayerRole.Regista) d -= 2f; // he reads the vertical pass into the forwards
                if (d < bestD) { bestD = d; marker = p; }
            }
            if (marker == null) return;
            // In his own third a back does not gamble at all: if he misses, the striker is through on goal. He stays
            // between his man and the goal and defends the next touch.
            if (marker.Slot.Line == Line.Defence && team.Forward(receiver.Pos) < -17f) return;
            UpdateIntercept(marker);
            UpdateIntercept(receiver);
            float read = 0.5f * MathUtil.A(marker.Attr.Positioning) + 0.3f * MathUtil.A(marker.Attr.Decisions) + 0.2f * MathUtil.A(marker.Attr.Marking);
            if (marker.Slot.IsCentralMidfielder) read += marker == team.Anchor ? 0.25f : 0.12f; // midfielders live off reading passes
            if (marker.PlayRole == PlayerRole.Regista) read += 0.12f;
            // a creative passer is unpredictable: his passes are harder to read
            var passer = Ball.LastToucher;
            if (passer != null && passer.Team != team) read -= 0.3f * (MathUtil.A(passer.Attr.Creativity) - 0.5f);
            // A good reader goes when he is sure to get there first; a poor one too late, or gambles and misses.
            // He only goes when he is sure to get there first (a good reader a little earlier). A failed gamble leaves
            // his man free; in his own third it leaves a striker clean through.
            float margin = -0.1f + 0.15f * read;
            float caution = receiver.Team.Forward(receiver.Pos) > 18f ? 0.45f : 0.3f;
            if (marker.InterceptTime < receiver.InterceptTime + margin - caution)
            {
                team.Anticipator = marker;
                _diagAnticipateAt[team.Index] = SimTime;
                if (marker.AnticipatedAt < Ball.LastKickTime && CollectDiagnostics) Count("anticipate");
                marker.AnticipatedAt = SimTime;
            }
        }

        /// <summary>
        /// Shielding the ball out: a ball rolling out of play that would give us the restart (goal kick, throw-in) is
        /// not played - the nearest defender puts his body between it and the opponent chasing it and escorts it out.
        /// </summary>
        private void AssignShielding()
        {
            if (Ball.Owner != null || Ball.Z > 0.5f || Ball.LastTouchTeam == null) return;
            if (_trajectoryDirty) return; // only on a fresh prediction
            Vec2 end = _trajectory.FinalPosition;
            // Only when it is clearly going out (well past the line, and not far from it now): a ball that stops short
            // would be left for the attacker.
            bool outOver = MathF.Abs(end.X) > Pitch.HalfLength + 2.5f || MathF.Abs(end.Y) > Pitch.HalfWidth + 2.5f;
            if (!outOver) return;
            float toLine = MathF.Min(Pitch.HalfLength - MathF.Abs(Ball.Pos.X), Pitch.HalfWidth - MathF.Abs(Ball.Pos.Y));
            if (toLine > 12f) return;
            float speed = Ball.Velocity.Length;
            if (speed < 1.5f || speed > 11f) return;
            var team = Ball.LastTouchTeam.Opponent; // the team that gets the restart
            var opp = Ball.LastTouchTeam;
            // the opponent who would reach it first
            PlayerAgent chaser = null;
            float oppT = float.PositiveInfinity;
            foreach (var o in opp.Players)
            {
                if (o.SentOff || o.IsDown || (o.IsGoalkeeper && !GoalkeeperMayChase(o))) continue;
                UpdateIntercept(o);
                if (o.InterceptTime < oppT) { oppT = o.InterceptTime; chaser = o; }
            }
            if (chaser == null || oppT > 4f) return; // nobody can keep it in: just let it go
            PlayerAgent best = null;
            float bestT = float.PositiveInfinity;
            foreach (var p in team.Players)
            {
                if (p.SentOff || p.IsDown || p.IsGoalkeeper) continue;
                float t = p.TimeToReach(Ball.Pos + Ball.Velocity * 0.4f);
                if (t < bestT) { bestT = t; best = p; }
            }
            // He must be there first, and strong enough to hold the chaser off.
            if (best == null || bestT > oppT + 0.15f) return;
            team.Shielder = best;
            _diagShieldAt[team.Index] = SimTime;
            if (team.Chaser == best) team.Chaser = null;
        }

        /// <summary>Where the shielding player stands: just behind the ball, between it and the opponent chasing it.</summary>
        private Vec2 ShieldSpot(PlayerAgent p)
        {
            var opp = p.Team.Opponent;
            PlayerAgent chaser = null;
            float best = float.PositiveInfinity;
            foreach (var o in opp.Players)
            {
                if (o.SentOff || o.IsGoalkeeper) continue;
                float d = Vec2.DistanceSq(o.Pos, Ball.Pos);
                if (d < best) { best = d; chaser = o; }
            }
            Vec2 lead = Ball.Pos + Ball.Velocity * 0.25f;
            Vec2 from = chaser != null ? (chaser.Pos - lead) : -Ball.Velocity;
            if (from.LengthSq < 0.01f) from = -Ball.Velocity;
            return lead + from.Normalized * 0.75f;
        }

        private bool GoalkeeperMayChase(PlayerAgent gk)
        {
            UpdateIntercept(gk);
            Vec2 point = gk.InterceptPoint;
            if (Pitch.InPenaltyArea(point, gk.Team.OwnGoalSign)) return true;

            // Sortie: rush out of the box to sweep up a ball played in behind the line (keeper arrives first,
            // no defender is nearer, the ball is not too far from goal).
            var team = gk.Team;
            float depth = Pitch.HalfLength - point.X * team.OwnGoalSign;
            var role = gk.PlayRole;
            float range = role == PlayerRole.SweeperKeeper ? 24f : role == PlayerRole.ShotStopper ? 8f : 16f; // a sweeper-keeper comes far out, a shot-stopper stays home
            if (depth > Pitch.PenaltyAreaDepth + range || MathF.Abs(point.Y) > Pitch.PenaltyAreaHalfWidth + 4f) return false;
            if (Ball.IntendedReceiver != null && Ball.IntendedReceiver.Team == team) return false;
            float own = gk.TimeToReach(point);
            float rival = float.PositiveInfinity, mate = float.PositiveInfinity;
            foreach (var p in AllPlayers)
            {
                if (p.SentOff || p == gk) continue;
                float t = p.TimeToReach(point);
                if (p.Team == team) mate = MathF.Min(mate, t);
                else rival = MathF.Min(rival, t);
            }
            bool go = own + (role == PlayerRole.SweeperKeeper ? 0f : 0.1f) < rival && own < mate + (role == PlayerRole.SweeperKeeper ? 0.45f : 0.25f);
            if (go && SimTime - gk.LastSortieTime > 4f)
            {
                gk.LastSortieTime = SimTime;
                team.Stats.Sorties++;
            }
            return go;
        }

        /// <summary>Earliest point on the predicted ball path this player can reach (pitch-control style).</summary>
        private void UpdateIntercept(PlayerAgent p)
        {
            if (p.InterceptVersion == _trajectory.Version && p.InterceptRecalc > 0f) return;
            // Nobody reads a new ball path instantly: players who did not expect it (opponents of the
            // kicker, everyone after a deflection) keep their previous plan for their reaction time.
            bool expected = _ballChangeExpectedBy == p.Team || Ball.IntendedReceiver == p;
            if (!expected && SimTime - _ballChangedAt < p.ReactionTime && p.InterceptVersion >= 0) return;
            p.InterceptVersion = _trajectory.Version;
            p.InterceptRecalc = 0.12f;
            float elapsed = _trajectoryAge;
            float reach = p.IsGoalkeeper ? p.GoalkeeperReach : p.HeadReach;
            int count = _trajectory.Count;
            float lastT = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = _trajectory.TimeAt(i) - elapsed;
                if (t < 0f) continue;
                Vec2 pos = _trajectory.PositionAt(i);
                if (!Pitch.InsideField(pos, 0.4f))
                {
                    // Ball is going out: run to where it leaves the pitch.
                    p.InterceptPoint = Pitch.ClampToField(pos, -0.3f);
                    p.InterceptTime = MathF.Max(t, p.TimeToReach(p.InterceptPoint)) + 0.5f;
                    return;
                }
                lastT = t;
                float z = _trajectory.HeightAt(i);
                if (z > reach) continue;
                float tp = p.TimeToReach(pos);
                if (tp <= t + 0.05f)
                {
                    p.InterceptPoint = pos;
                    p.InterceptTime = t;
                    return;
                }
            }
            Vec2 final = Pitch.ClampToField(_trajectory.FinalPosition, -0.3f);
            p.InterceptPoint = final;
            p.InterceptTime = MathF.Max(lastT, p.TimeToReach(final));
        }
    }
}

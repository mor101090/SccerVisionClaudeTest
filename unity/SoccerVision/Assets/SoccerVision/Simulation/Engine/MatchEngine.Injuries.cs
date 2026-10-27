using System;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// Injuries and substitutions.
    ///
    /// Elite men's football sees roughly 20-30 time-loss injuries per 1000 match hours (UEFA Elite Club Injury Study),
    /// i.e. somewhere near one per match, most of them minor. Here a knock can come from a foul (more from behind or in
    /// a slide), a tackle, a shoulder charge or - without contact - from sprinting on tired legs (a muscle strain).
    ///
    /// - Light: the player goes down for a moment, then carries on a little slower and less precise (shown with a white
    ///   square and a red cross, like the card marker).
    /// - Serious: he stays down. The referee stops play (Law 5: serious injury), the physio treats him, he is helped off
    ///   at the nearest touchline and a substitute comes on at the halfway line (Law 3: up to five substitutions). With
    ///   no substitution left the team plays on a man short. Play restarts with the free kick if there was a foul,
    ///   otherwise with a dropped ball for the team that last touched it (Law 8).
    /// </summary>
    public sealed partial class MatchEngine
    {
        /// <summary>Sim time at which the referee stops play for a serious injury (-1: none pending).</summary>
        private float _injuryStopAt = -1f;

        /// <summary>Rolls for an injury to <paramref name="p"/> after a contact.</summary>
        private void MaybeInjure(PlayerAgent p, float pLight, float pSerious, string cause)
        {
            if (p == null || p.SentOff || p.Injury == InjuryLevel.Serious) return;
            // Fitter, stronger players get hurt a little less; tired ones more.
            float resilience = 1.15f - 0.3f * MathUtil.A(p.Attr.PhysicalPower) + 0.3f * (1f - MathUtil.Clamp01(p.Energy));
            float r = _rng.NextFloat();
            if (r < pSerious * resilience) InjureSeriously(p, cause);
            else if (r < (pSerious + pLight) * resilience && p.Injury == InjuryLevel.None) InjureLightly(p, cause);
        }

        private void InjureLightly(PlayerAgent p, string cause)
        {
            p.Injury = InjuryLevel.Light;
            p.Team.Stats.LightInjuries++;
            if (CollectDiagnostics) Count("injury.light." + cause);
            // Down for a moment, then he gets up and plays on.
            p.GroundTimer = MathF.Max(p.GroundTimer, _rng.Range(1.2f, 2.4f));
            p.StunTimer = MathF.Max(p.StunTimer, p.GroundTimer);
            Emit(MatchEventType.Injury, p.Team, p, null, p.Pos, 0f, WhistleKind.Short, 0);
        }

        private void InjureSeriously(PlayerAgent p, string cause)
        {
            if (p.IsGoalkeeper && FindSubstitute(p) == null)
            {
                // No keeper on the bench (or no change left): he plays on, hurt.
                if (p.Injury == InjuryLevel.None) InjureLightly(p, cause);
                return;
            }
            p.Injury = InjuryLevel.Serious;
            p.Team.Stats.SeriousInjuries++;
            if (CollectDiagnostics) Count("injury.serious." + cause);
            p.TreatmentTimer = Config.InjuryTreatmentSeconds * _rng.Range(0.8f, 1.3f);
            p.StunTimer = 999f;
            p.SlideTimer = 0f;
            if (Ball.Owner == p)
            {
                Ball.Owner = null;
                Ball.InHands = false;
            }
            var team = p.Team;
            if (team.Presser == p) team.Presser = null;
            if (team.SecondPresser == p) team.SecondPresser = null;
            if (team.Chaser == p) team.Chaser = null;
            team.Supporters.Remove(p);
            Emit(MatchEventType.Injury, p.Team, p, null, p.Pos, 0f, WhistleKind.Short, 1);
            // In open play the referee stops the game shortly (unless it stops anyway).
            if (Phase == MatchPhase.Live && _injuryStopAt < 0f) _injuryStopAt = SimTime + _rng.Range(0.6f, 1.6f);
        }

        /// <summary>Muscle strains: sprinting flat out on tired legs now and then pulls a hamstring.</summary>
        private void MaybeStrain(PlayerAgent p, float intensity, float clockDt)
        {
            if (p.IsGoalkeeper || intensity < 0.85f || p.Injury == InjuryLevel.Serious || Phase != MatchPhase.Live) return;
            float tired = 1.25f - MathUtil.Clamp01(p.Energy);
            float rate = 0.00006f * tired * tired * (1.3f - 0.6f * MathUtil.A(p.Attr.Stamina));
            if (!_rng.Chance(rate * clockDt)) return;
            if (_rng.Chance(0.35f)) InjureSeriously(p, "strain");
            else if (p.Injury == InjuryLevel.None) InjureLightly(p, "strain");
        }

        /// <summary>Open play: the referee stops the game for a seriously injured player (dropped ball afterwards).</summary>
        private void CheckInjuryStoppage()
        {
            if (_injuryStopAt < 0f) return;
            if (SimTime < _injuryStopAt) return;
            _injuryStopAt = -1f;
            bool pending = false;
            foreach (var p in AllPlayers) if (!p.SentOff && p.Injury == InjuryLevel.Serious) { pending = true; break; }
            if (!pending) return;
            Whistle(WhistleKind.Foul);
            var team = Ball.LastTouchTeam ?? PossessionTeam ?? Home;
            Vec2 spot = Pitch.ClampToField(Ball.Owner != null ? Ball.Owner.Pos : Ball.Pos, -0.5f);
            // Law 8: in the penalty area the ball is dropped for the defending goalkeeper.
            if (Pitch.InPenaltyArea(spot, Home.OwnGoalSign)) team = Home;
            else if (Pitch.InPenaltyArea(spot, Away.OwnGoalSign)) team = Away;
            Ball.Owner = null;
            Ball.InHands = false;
            Ball.Stop();
            Ball.ClearFlight();
            ResolvePendingPass(null);
            ClearOffside();
            if (AddedTimeExpired)
            {
                EndHalf();
                return;
            }
            BeginRestart(RestartType.DroppedBall, team, spot, null);
            _restart.CountsAsLostTime = true;
            Emit(MatchEventType.DroppedBall, team, _restart.Taker, null, spot);
        }

        /// <summary>
        /// During a stoppage: a seriously injured player is treated where he fell, then walks (is helped) off at the nearest
        /// touchline, where the change is made. Returns true when this player was handled (the caller skips him).
        /// </summary>
        private bool StepInjuredPlayer(PlayerAgent p, float dt)
        {
            if (p.SentOff || p.Injury != InjuryLevel.Serious) return false;
            if (!p.LeavingInjured)
            {
                p.TreatmentTimer -= dt;
                p.Vel *= 0.8f;
                if (p.TreatmentTimer <= 0f) p.LeavingInjured = true;
                return true;
            }
            float side = p.Pos.Y >= 0f ? 1f : -1f;
            p.Target = new Vec2(MathUtil.Clamp(p.Pos.X, -Pitch.HalfLength + 2f, Pitch.HalfLength - 2f), (Pitch.HalfWidth + 1.2f) * side);
            p.Urgency = 0.2f; // a slow walk, helped off
            p.StunTimer = 0f;
            p.Task = PlayerTask.Leave;
            p.FaceBall = false;
            MovePlayerOffPitch(p, dt);
            if (MathF.Abs(p.Pos.Y) >= Pitch.HalfWidth + 0.9f) CompleteInjuryChange(p, true);
            return true;
        }

        private void MovePlayerOffPitch(PlayerAgent p, float dt)
        {
            Vec2 to = p.Target - p.Pos;
            float d = to.Length;
            float v = 1.6f;
            if (d > 0.05f) p.Pos += to / d * MathF.Min(d, v * dt);
            p.Vel = d > 0.05f ? to / d * v : Vec2.Zero;
            if (d > 0.05f) p.Facing = (to / d).Angle;
        }

        /// <summary>Sim time until which the restart waits for a substitute who has just come on.</summary>
        private float _substituteOnUntil = -1f;

        /// <summary>True while an injured player is being treated or helped off, or his substitute is coming on.</summary>
        private bool InjuryBeingHandled()
        {
            foreach (var p in AllPlayers) if (!p.SentOff && p.Injury == InjuryLevel.Serious) return true;
            return SimTime < _substituteOnUntil;
        }

        /// <summary>The bench player for an injured player: same position group if possible (a keeper for a keeper).</summary>
        private PlayerAgent FindSubstitute(PlayerAgent injured)
        {
            var team = injured.Team;
            if (team.SubstitutionsUsed >= Config.MaxSubstitutions) return null;
            PositionGroup want = injured.IsGoalkeeper ? PositionGroup.Goalkeeper
                               : injured.Slot.Line == Line.Defence ? PositionGroup.Defender
                               : injured.Slot.Line == Line.Midfield ? PositionGroup.Midfielder : PositionGroup.Forward;
            PlayerAgent best = null;
            int bestScore = int.MinValue;
            foreach (var b in team.Bench)
            {
                var g = b.Profile.NaturalPosition;
                if (injured.IsGoalkeeper != (g == PositionGroup.Goalkeeper)) continue; // keepers only for keepers
                int score = g == want ? 100 : 0;
                score += (int)b.Profile.Attributes.Mental;
                if (score > bestScore) { bestScore = score; best = b; }
            }
            return best;
        }

        /// <summary>
        /// The injured player is off: his substitute takes over the shirt slot and comes on at the halfway line by the
        /// team's bench (or, with nobody left to bring on, the team plays on a man short).
        /// </summary>
        private void CompleteInjuryChange(PlayerAgent p, bool walkOn)
        {
            var team = p.Team;
            var sub = FindSubstitute(p);
            if (sub == null)
            {
                p.InjuredOff = true;
                p.SentOff = true; // off the pitch for good (not a dismissal)
                p.Task = PlayerTask.Leave;
                p.LeavingInjured = false;
                p.StunTimer = 0f;
                return;
            }
            team.Bench.Remove(sub);
            team.SubstitutionsUsed++;
            team.Stats.Substitutions++;
            string outLabel = p.Label;
            p.Substitute(sub.Profile);
            // Law 3: the substitute enters at the halfway line.
            float benchX = team == Home ? -6f : 6f;
            Vec2 entry = new Vec2(benchX, Pitch.HalfWidth + 0.8f);
            p.Pos = p.PrevPos = walkOn ? entry : p.ShapeTarget;
            p.Vel = Vec2.Zero;
            p.Target = p.ShapeTarget;
            if (CollectDiagnostics) Count("substitution");
            if (walkOn) _substituteOnUntil = SimTime + 2.5f * Config.MotionSpeed;
            var e = Emit(MatchEventType.Substitution, team, p, null, p.Pos);
            e.Info = p.Number + "|" + p.Profile.ShortName;
            e.Text = Commentary.Substitution(team, p, outLabel);
        }
    }
}

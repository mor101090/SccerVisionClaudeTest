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
            if (ScriptExclusive && cause != "script") return; // a staged match has only its own injuries
            // Fitter, stronger players get hurt a little less; tired ones more.
            float resilience = 1.15f - 0.3f * MathUtil.A(p.Attr.PhysicalPower) + 0.3f * (1f - MathUtil.Clamp01(p.Energy));
            resilience *= GroundInjuryFactor(p.Pos);
            // (a player already carrying a knock is more fragile: the next one is more often serious)
            if (p.Injury == InjuryLevel.Light) pSerious *= 2.2f;
            float r = _rng.NextFloat();
            if (r < pSerious * resilience) InjureSeriously(p, cause);
            else if (r < (pSerious + pLight) * resilience && p.Injury == InjuryLevel.None) InjureLightly(p, cause);
        }

        private void InjureLightly(PlayerAgent p, string cause)
        {
            p.Injury = InjuryLevel.Light;
            p.Team.Stats.LightInjuries++;
            DampMomentum(0.9f);
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
            DampMomentum(0.8f);
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

        /// <summary>
        /// The ground: a heavy, muddy, badly kept pitch twists ankles and knees (up to about +45% on a neglected pitch, more in
        /// the mud itself).
        /// </summary>
        private float GroundInjuryFactor(Vec2 pos)
        {
            float f = 1f + 0.45f * (1f - MathUtil.Clamp01(Conditions.Maintenance));
            if (Surface != null) f += 0.5f * Surface.MudAt(pos.X, pos.Y);
            return f;
        }

        /// <summary>Muscle strains: sprinting flat out on tired legs now and then pulls a hamstring.</summary>
        private void MaybeStrain(PlayerAgent p, float intensity, float clockDt)
        {
            // Injuries come from tackles. A muscle only pulls on its own when a player is exhausted (very low energy) or in poor
            // condition (low stamina, low fitness) and sprinting flat out - and even then rarely.
            if (p.IsGoalkeeper || intensity < 0.9f || p.Injury == InjuryLevel.Serious || Phase != MatchPhase.Live || ScriptExclusive) return;
            float poorShape = p.Profile.Attributes.Fitness == FitnessLevel.Low ? 1.6f : 1f;
            if (p.Energy > 0.5f && !(poorShape > 1f && p.Energy > 0.4f && p.Attr.Stamina < 55)) return;
            float tired = 1.25f - MathUtil.Clamp01(p.Energy);
            float rate = 0.00003f * tired * tired * poorShape * (1.3f - 0.6f * MathUtil.A(p.Attr.Stamina));
            rate *= GroundInjuryFactor(p.Pos) * (p.Injury == InjuryLevel.Light ? 2f : 1f);
            if (!_rng.Chance(rate * clockDt)) return;
            if (_rng.Chance(p.Injury == InjuryLevel.Light ? 0.6f : 0.35f)) InjureSeriously(p, "strain");
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
            // Helped off towards his team's bench (by the halfway line, on the dug-out side), where the substitute waits.
            p.Target = BenchSpot(p.Team);
            p.Urgency = 0.2f;
            p.StunTimer = 0f;
            p.Task = PlayerTask.Leave;
            p.FaceBall = false;
            MovePlayerOffPitch(p, dt);
            // At the touchline the change is made: the substitute comes on right there (no jump across the pitch).
            if (Vec2.Distance(p.Pos, p.Target) < 0.3f) CompleteInjuryChange(p, true);
            return true;
        }

        /// <summary>Where players leave and come on: the touchline in front of the team's bench, by the halfway line.</summary>
        private Vec2 BenchSpot(TeamAgent team) => new Vec2(team == Home ? -5f : 5f, Pitch.HalfWidth - 0.2f);

        private void MovePlayerOffPitch(PlayerAgent p, float dt)
        {
            Vec2 to = p.Target - p.Pos;
            float d = to.Length;
            float v = 2.6f; // helped off at a brisk walk
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
            if (SubstitutionsLeft(team) <= 0 || !WindowAvailable(team)) return null;
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
            team.SubbedOff.Add(p.Profile);
            team.SubstitutionsUsed++;
            team.Stats.Substitutions++;
            DampMomentum(0.85f);
            // (the change uses one of the three opportunities, unless one is already open at this stoppage)
            if (_restart != null && team.WindowRestart != _restart && !SecondHalfKickOff) { team.WindowRestart = _restart; team.WindowsUsed++; }
            string outLabel = p.Label;
            int outNumber = p.Number;
            CloseRecord(p);
            p.Substitute(sub.Profile);
            OpenRecord(p);
            // Law 3: the substitute enters at the halfway line, by his bench - where the injured player went off.
            p.Pos = p.PrevPos = walkOn ? BenchSpot(team) : p.ShapeTarget;
            p.Vel = Vec2.Zero;
            p.Target = p.ShapeTarget;
            if (CollectDiagnostics) Count("substitution");
            if (walkOn) _substituteOnUntil = SimTime + 2.5f * Config.MotionSpeed;
            var e = Emit(MatchEventType.Substitution, team, p, null, p.Pos);
            e.Info = p.Number + "|" + p.Profile.ShortName + "|" + outNumber;
            e.Text = Commentary.Substitution(team, p, outLabel);
        }
    }
}

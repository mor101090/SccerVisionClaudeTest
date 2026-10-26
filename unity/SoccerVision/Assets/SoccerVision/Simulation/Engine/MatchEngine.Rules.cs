using System;

namespace SoccerVision.Simulation
{
    internal sealed class PendingSanction
    {
        public PlayerAgent Player;
        public bool Yellow;
        public bool Red;
        public string Reason = "";
    }

    /// <summary>Laws of the Game: ball in/out of play, goals, fouls and misconduct, offside.</summary>
    public sealed partial class MatchEngine
    {
        private TeamAgent TeamDefendingGoal(int goalSign) => Home.OwnGoalSign == goalSign ? Home : Away;

        // ---- Law 9 / Law 10: ball out of play and goals ------------------------------------------------

        private void CheckBoundaries()
        {
            if (Ball.Owner != null && Ball.InHands) return;
            var b = Ball.M;
            const float r = Pitch.BallRadius;

            if (MathF.Abs(b.X) > Pitch.HalfLength + r)
            {
                int goalSign = b.X > 0f ? 1 : -1;
                bool inMouth = MathF.Abs(b.Y) < Pitch.GoalHalfWidth && b.Z < Pitch.GoalHeight;
                if (inMouth)
                {
                    OnGoal(goalSign);
                    return;
                }
                var defending = TeamDefendingGoal(goalSign);
                int ySign = b.Y >= 0f ? 1 : -1;
                if (Ball.LastTouchTeam == defending)
                {
                    var spot = new Vec2((Pitch.HalfLength - 0.5f) * goalSign, (Pitch.HalfWidth - 0.5f) * ySign);
                    BallOutOfPlay(RestartType.CornerKick, defending.Opponent, spot);
                }
                else
                {
                    var spot = new Vec2((Pitch.HalfLength - Pitch.GoalAreaDepth + 0.3f) * goalSign, ySign * _rng.Range(1.5f, 7.5f));
                    BallOutOfPlay(RestartType.GoalKick, defending, spot);
                }
                return;
            }

            if (MathF.Abs(b.Y) > Pitch.HalfWidth + r)
            {
                var team = Ball.LastTouchTeam != null ? Ball.LastTouchTeam.Opponent : PossessionTeam.Opponent;
                var spot = new Vec2(MathUtil.Clamp(b.X, -Pitch.HalfLength + 0.5f, Pitch.HalfLength - 0.5f), Pitch.HalfWidth * MathF.Sign(b.Y));
                BallOutOfPlay(RestartType.ThrowIn, team, spot);
            }
        }

        private void BallOutOfPlay(RestartType type, TeamAgent team, Vec2 spot)
        {
            if (CollectDiagnostics) Count("out." + type + "." + (SimTime - _ballChangedAt < 0.05f ? "now" : Ball.LastKickKind.ToString()) + (_ballChangeExpectedBy == null ? ".deflect" : ""));
            if (Ball.IsShot && type != RestartType.ThrowIn && Ball.Shooter != null)
            {
                Emit(MatchEventType.ShotOffTarget, Ball.Shooter.Team, Ball.Shooter, null, Ball.Pos);
            }
            Ball.Owner = null;
            Ball.InHands = false;
            Ball.ClearFlight();
            ResolvePendingPass(null);
            ClearOffside();
            _penaltyKickTime = -1f;
            FlagBallOut(spot);

            if (AddedTimeExpired)
            {
                EndHalf();
                return;
            }

            BeginRestart(type, team, spot, null);
            switch (type)
            {
                case RestartType.CornerKick:
                    team.Stats.Corners++;
                    Emit(MatchEventType.Corner, team, _restart.Taker, null, spot);
                    break;
                case RestartType.GoalKick:
                    team.Stats.GoalKicks++;
                    Emit(MatchEventType.GoalKick, team, _restart.Taker, null, spot);
                    break;
                case RestartType.ThrowIn:
                    team.Stats.ThrowIns++;
                    Emit(MatchEventType.ThrowIn, team, _restart.Taker, null, spot);
                    break;
            }
        }

        private void OnGoal(int goalSign)
        {
            var defending = TeamDefendingGoal(goalSign);
            var scoringTeam = defending.Opponent;
            var toucher = Ball.LastToucher;
            // A shot deflected in (keeper parry, defender) is still the shooter's goal.
            if (toucher != null && toucher.Team != scoringTeam && Ball.Shooter != null && Ball.Shooter.Team == scoringTeam && SimTime - _lastShotTime < 3f)
            {
                toucher = Ball.Shooter;
            }
            bool ownGoal = toucher != null && toucher.Team != scoringTeam;
            PlayerAgent scorer = toucher;
            PlayerAgent assist = null;
            if (!ownGoal && Ball.LastPasser != null && Ball.LastPasser.Team == scoringTeam && Ball.LastPasser != scorer && SimTime - Ball.LastPassTime < 12f)
            {
                assist = Ball.LastPasser;
            }

            scoringTeam.Score++;
            scoringTeam.Stats.Goals++;
            bool recentShot = Ball.Shooter != null && Ball.Shooter.Team == scoringTeam && SimTime - _lastShotTime < 3f;
            if (recentShot && !_shotCountedOnTarget) scoringTeam.Stats.ShotsOnTarget++;
            if (_penaltyKickTime >= 0f && Ball.Shooter != null && Ball.Shooter.Team == scoringTeam) scoringTeam.Stats.PenaltiesScored++;
            if (!ownGoal && scorer != null) scorer.Goals++;

            Emit(MatchEventType.Goal, scoringTeam, scorer, assist, Ball.Pos, ownGoal ? 1f : 0f);
            Whistle(WhistleKind.Long);

            Ball.InGoal = true;
            Ball.Owner = null;
            Ball.InHands = false;
            Ball.ClearFlight();
            ResolvePendingPass(null);
            ClearOffside();
            _penaltyKickTime = -1f;
            _advantageSanction = null;
            Phase = MatchPhase.GoalCelebration;
            _phaseTimer = 0f;
            _celebrationScorer = ownGoal ? null : scorer;
            Celebration = (CelebrationKind)_rng.Range(0, 5);
            _kickOffAfterGoal = defending;
            _restart = null;
            foreach (var p in AllPlayers) p.Task = PlayerTask.Celebrate;
        }

        /// <summary>Scorer's own routine for the chosen <see cref="Celebration"/>.</summary>
        private void CelebrateScorer(PlayerAgent p, TeamAgent scoring, Vec2 corner, int side)
        {
            float t = _phaseTimer;
            Vec2 goal = scoring.AttackGoal;
            switch (Celebration)
            {
                case CelebrationKind.CornerRun:
                    p.Target = corner * 0.86f;
                    p.Urgency = 0.85f;
                    break;
                case CelebrationKind.Huddle:
                    p.Target = p.Pos;
                    p.Urgency = 0f;
                    if (t > 1.2f && p.JumpZ <= 0f) p.JumpVz = 3.4f;
                    break;
                case CelebrationKind.FansRun:
                    // Sprint to the touchline in front of the fans, then stand with arms wide.
                    p.Target = new Vec2(MathUtil.Clamp(p.Pos.X, -44f, 44f), (Pitch.HalfWidth - 0.8f) * side);
                    p.Urgency = 0.95f;
                    if (t > 3f && p.JumpZ <= 0f) p.JumpVz = 3.4f;
                    break;
                case CelebrationKind.Lap:
                    {
                        // Victory lap: circle around the penalty area with the crowd on the touchline side.
                        float ang = t * 0.9f;
                        Vec2 centre = new Vec2(goal.X * 0.62f, 0f);
                        p.Target = centre + new Vec2(MathF.Cos(ang) * 11f, MathF.Sin(ang) * 14f);
                        p.Urgency = 0.8f;
                        break;
                    }
                case CelebrationKind.KneeSlide:
                    p.Target = corner * 0.8f;
                    p.Urgency = t < 2.5f ? 1f : 0f;
                    if (t > 2.2f && t < 2.5f) p.JumpVz = MathF.Max(p.JumpVz, 3.0f);
                    break;
            }
        }

        private void CelebrateTeammate(PlayerAgent p, TeamAgent scoring, Vec2 corner, int side)
        {
            var scorer = _celebrationScorer;
            switch (Celebration)
            {
                case CelebrationKind.Huddle:
                    {
                        // Everybody piles onto the scorer, then bounces on the spot.
                        float ring = 1.4f + 0.3f * (p.Index % 3);
                        Vec2 dir = (p.Pos - scorer.Pos).Normalized;
                        p.Target = scorer.Pos + dir * ring;
                        p.Urgency = 0.9f;
                        if (_phaseTimer > 2f && p.JumpZ <= 0f && Vec2.Distance(p.Pos, scorer.Pos) < 3f && ((int)(_phaseTimer * 1.6f) + p.Index) % 4 == 0) p.JumpVz = 3.0f;
                        break;
                    }
                case CelebrationKind.KneeSlide:
                    {
                        // Team-mates line up behind the sliding scorer.
                        int rank = p.Index % 11;
                        Vec2 back = (new Vec2(0f, 0f) - corner).Normalized;
                        p.Target = scorer.Pos + back * (2.5f + rank * 0.7f) + back.Perp * ((rank % 2 == 0 ? 1f : -1f) * 1.2f);
                        p.Urgency = 0.85f;
                        break;
                    }
                default:
                    {
                        Vec2 toward = scorer.Pos + (p.Pos - scorer.Pos).Normalized * 2.2f;
                        p.Target = Vec2.Distance(p.Pos, scorer.Pos) < 3f ? p.Pos : toward;
                        p.Urgency = 0.7f;
                        break;
                    }
            }
        }

        private void StepCelebration(float dt)
        {
            _phaseTimer += dt;
            BallPhysics.Step(ref Ball.M, Config, dt);
            CollideGoalFrames();
            if (MathF.Abs(Ball.Pos.X) > Pitch.HalfLength + Pitch.GoalDepth + 1f || MathF.Abs(Ball.Pos.Y) > Pitch.HalfWidth + 3f) Ball.Stop();

            var scoring = _kickOffAfterGoal.Opponent;
            int side = _celebrationScorer != null && _celebrationScorer.Pos.Y < 0f ? -1 : 1;
            Vec2 corner = Pitch.CornerPosition(scoring.AttackGoalSign, side);
            foreach (var p in AllPlayers)
            {
                if (p.SentOff) { MovePlayer(p, dt); continue; }
                if (p.Team == scoring && !p.IsGoalkeeper && _celebrationScorer != null)
                {
                    if (p == _celebrationScorer) CelebrateScorer(p, scoring, corner, side);
                    else CelebrateTeammate(p, scoring, corner, side);
                }
                else
                {
                    // Walk back into own half towards the kick-off shape.
                    p.Target = KickOffPosition(p, p.Team == _kickOffAfterGoal);
                    p.Urgency = 0.3f;
                }
                p.FaceBall = false;
                MovePlayer(p, dt);
            }

            if (_phaseTimer >= Config.GoalCelebrationSeconds)
            {
                Ball.InGoal = false;
                if (AddedTimeExpired)
                {
                    EndHalf();
                    return;
                }
                BeginRestart(RestartType.KickOff, _kickOffAfterGoal, Vec2.Zero, null);
                _restart.CountsAsLostTime = true;
            }
        }

        // ---- Law 12: fouls and misconduct -------------------------------------------------------------

        private void CommitFoul(PlayerAgent offender, PlayerAgent victim, bool fromBehind, bool playedForBall, Vec2 contact)
        {
            offender.Team.Stats.Fouls++;
            offender.FoulsCommitted++;
            bool penaltyArea = Pitch.InPenaltyArea(contact, offender.Team.OwnGoalSign);
            var sanction = DecideSanction(offender, victim, fromBehind, playedForBall, penaltyArea);
            Emit(MatchEventType.Foul, offender.Team, offender, victim, contact);

            if (Config.AllowAdvantage && !penaltyArea && (sanction == null || !sanction.Red) && Ball.Owner == victim
                && victim.Team.Forward(contact) > -5f && _rng.Chance(0.5f))
            {
                Emit(MatchEventType.Advantage, victim.Team, victim, offender, contact);
                victim.StunTimer = 0.2f;
                offender.StunTimer = MathF.Max(offender.StunTimer, 0.8f);
                if (sanction != null)
                {
                    _advantageSanction = sanction;
                    _advantageTimer = 2.5f;
                }
                return;
            }

            Whistle(penaltyArea ? WhistleKind.Long : WhistleKind.Foul);
            if (sanction != null) ApplySanction(sanction);

            Ball.Owner = null;
            Ball.InHands = false;
            Ball.Stop();
            Ball.ClearFlight();
            ResolvePendingPass(null);
            ClearOffside();
            victim.StunTimer = MathF.Max(victim.StunTimer, 0.6f);

            if (AddedTimeExpired && !penaltyArea)
            {
                EndHalf();
                return;
            }

            if (penaltyArea)
            {
                victim.Team.Stats.Penalties++;
                BeginRestart(RestartType.PenaltyKick, victim.Team, Pitch.PenaltySpot(victim.Team.AttackGoalSign), null);
                _restart.CountsAsLostTime = true;
                Emit(MatchEventType.PenaltyAwarded, victim.Team, _restart.Taker, offender, _restart.Spot);
            }
            else
            {
                victim.Team.Stats.FreeKicks++;
                Vec2 spot = Pitch.ClampToField(contact, -0.3f);
                BeginRestart(RestartType.DirectFreeKick, victim.Team, spot, null);
                _restart.CountsAsLostTime = sanction != null;
                Emit(MatchEventType.FreeKick, victim.Team, _restart.Taker, offender, spot);
            }
        }

        private PendingSanction DecideSanction(PlayerAgent offender, PlayerAgent victim, bool fromBehind, bool playedForBall, bool penaltyArea)
        {
            float aggression = MathUtil.A(offender.Attr.Aggression);
            if (IsDenyingObviousGoalScoringOpportunity(victim, offender))
            {
                // Law 12: DOGSO inside the penalty area with a genuine attempt to play the ball is a caution.
                bool yellowOnly = penaltyArea && playedForBall;
                if (CollectDiagnostics) Count(yellowOnly ? "card.dogso.yellow" : "card.dogso.red");
                return new PendingSanction { Player = offender, Yellow = yellowOnly, Red = !yellowOnly, Reason = "DOGSO" };
            }

            float pRed = 0.0004f + (fromBehind ? 0.0015f * (0.5f + aggression) : 0f);
            if (_rng.Chance(pRed))
            {
                if (CollectDiagnostics) Count("card.sfp.red");
                return new PendingSanction { Player = offender, Red = true, Reason = "serious foul play" };
            }

            bool promising = IsStoppingPromisingAttack(victim, offender);
            float pYellow = 0.022f + (fromBehind ? 0.1f : 0f) + 0.06f * aggression + (promising ? 0.5f : 0f)
                            + (offender.FoulsCommitted >= 3 ? 0.2f : 0f);
            if (!playedForBall) pYellow += 0.1f;
            if (offender.YellowCards > 0) pYellow *= 0.45f; // referees give some leeway before a second caution
            if (_rng.Chance(MathUtil.Clamp(pYellow, 0f, 0.95f)))
            {
                string reason = promising ? "stopping a promising attack" : (offender.FoulsCommitted >= 3 ? "persistent infringement" : "reckless challenge");
                return new PendingSanction { Player = offender, Yellow = true, Reason = reason };
            }
            return null;
        }

        private bool IsDenyingObviousGoalScoringOpportunity(PlayerAgent victim, PlayerAgent offender)
        {
            var attack = victim.Team;
            Vec2 goal = attack.AttackGoal;
            float dist = Vec2.Distance(victim.Pos, goal);
            if (dist > 23f) return false;
            if (MathF.Abs(victim.Pos.Y) > Pitch.PenaltyAreaHalfWidth - 3f) return false;
            if (Ball.Owner != victim) return false;
            if (Vec2.Dot(victim.Vel, (goal - victim.Pos).Normalized) < 2.5f) return false; // must be heading for goal
            float victimF = attack.Forward(victim.Pos);
            foreach (var d in offender.Team.Players)
            {
                if (d.SentOff || d == offender || d.IsGoalkeeper) continue;
                float f = attack.Forward(d.Pos);
                if (f > victimF - 0.5f && MathF.Abs(d.Pos.Y - victim.Pos.Y) < 11f) return false; // a covering defender
            }
            return true;
        }

        private bool IsStoppingPromisingAttack(PlayerAgent victim, PlayerAgent offender)
        {
            var attack = victim.Team;
            float victimF = attack.Forward(victim.Pos);
            if (victimF < 0f || Ball.Owner != victim) return false;
            int goalSide = 0;
            foreach (var d in offender.Team.Players)
            {
                if (d.SentOff || d == offender || d.IsGoalkeeper) continue;
                if (attack.Forward(d.Pos) > victimF) goalSide++;
            }
            float forwardSpeed = Vec2.Dot(victim.Vel, attack.DirToWorld(Vec2.UnitX));
            return goalSide <= 2 && forwardSpeed > 4.5f;
        }

        private void ApplySanction(PendingSanction s)
        {
            var p = s.Player;
            if (p == null || p.SentOff) return;
            if (s.Red)
            {
                p.Team.Stats.RedCards++;
                Emit(MatchEventType.RedCard, p.Team, p, null, p.Pos, 0f);
                SendOff(p);
                return;
            }
            if (s.Yellow)
            {
                p.YellowCards++;
                p.Team.Stats.YellowCards++;
                Emit(MatchEventType.YellowCard, p.Team, p, null, p.Pos, p.YellowCards);
                if (p.YellowCards >= 2)
                {
                    if (CollectDiagnostics) Count("card.secondyellow");
                    p.Team.Stats.RedCards++;
                    Emit(MatchEventType.RedCard, p.Team, p, null, p.Pos, 1f);
                    SendOff(p);
                }
            }
        }

        /// <summary>A sent-off player leaves and cannot be replaced (design doc); the team re-shapes with ten.</summary>
        private void SendOff(PlayerAgent p)
        {
            p.SentOff = true;
            p.Task = PlayerTask.Leave;
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
            if (Ball.IntendedReceiver == p) Ball.IntendedReceiver = null;

            if (p.IsGoalkeeper)
            {
                p.IsGoalkeeper = false;
                PlayerAgent best = null;
                float bestScore = float.NegativeInfinity;
                foreach (var q in team.Players)
                {
                    if (q.SentOff || q == p) continue;
                    float score = q.Attr.Goalkeeping + (q.Slot.Line == Line.Defence ? 5f : 0f) + q.Profile.HeightCm * 0.05f;
                    if (score > bestScore) { bestScore = score; best = q; }
                }
                if (best != null)
                {
                    best.IsGoalkeeper = true;
                    best.SlotIndex = 0;
                }
                return;
            }

            if (p.Slot.Line == Line.Defence)
            {
                // A central midfielder drops into the back line.
                PlayerAgent mid = null;
                float bestAbs = float.PositiveInfinity;
                foreach (var q in team.Players)
                {
                    if (q.SentOff || q.IsGoalkeeper || q.Slot.Line != Line.Midfield) continue;
                    float abs = MathF.Abs(q.Slot.BaseLateral - p.Slot.BaseLateral);
                    if (abs < bestAbs) { bestAbs = abs; mid = q; }
                }
                if (mid != null) mid.SlotIndex = p.SlotIndex;
            }
        }

        private void GoalkeeperHoldViolation(PlayerAgent gk)
        {
            // Law 12.2 (2025/26): holding the ball for more than eight seconds -> corner kick to the opponents.
            Emit(MatchEventType.GoalkeeperHoldViolation, gk.Team, gk, null, gk.Pos);
            Whistle(WhistleKind.Foul);
            Ball.Owner = null;
            Ball.InHands = false;
            Ball.ClearFlight();
            ClearOffside();
            if (AddedTimeExpired)
            {
                EndHalf();
                return;
            }
            int goalSign = gk.Team.OwnGoalSign;
            int ySign = gk.Pos.Y >= 0f ? 1 : -1;
            var opp = gk.Team.Opponent;
            opp.Stats.Corners++;
            BeginRestart(RestartType.CornerKick, opp, new Vec2((Pitch.HalfLength - 0.5f) * goalSign, (Pitch.HalfWidth - 0.5f) * ySign), null);
            Emit(MatchEventType.Corner, opp, _restart.Taker, null, _restart.Spot);
        }

        // ---- Law 11: offside --------------------------------------------------------------------------

        /// <summary>Records which team-mates are in an offside position at the moment <paramref name="toucher"/> plays the ball.</summary>
        private void SnapshotOffside(PlayerAgent toucher, bool restartException)
        {
            ClearOffside();
            if (restartException || toucher == null) return;
            var team = toucher.Team;
            float ballF = team.Forward(Ball.Pos);
            float last = float.NegativeInfinity, secondLast = float.NegativeInfinity;
            foreach (var o in team.Opponent.Players)
            {
                if (o.SentOff) continue;
                float f = team.Forward(o.Pos);
                if (f > last) { secondLast = last; last = f; }
                else if (f > secondLast) secondLast = f;
            }
            bool any = false;
            foreach (var q in team.Players)
            {
                if (q.SentOff || q == toucher) continue;
                float f = team.Forward(q.Pos);
                if (f > 0.05f && f > ballF + 0.05f && f > secondLast + 0.05f)
                {
                    _offsideFlags[q.Index] = true;
                    any = true;
                }
            }
            _offsideActive = any;
            _offsideTeam = any ? team : null;
        }

        private void ClearOffside()
        {
            if (!_offsideActive) return;
            for (int i = 0; i < _offsideFlags.Length; i++) _offsideFlags[i] = false;
            _offsideActive = false;
            _offsideTeam = null;
        }

        public bool IsFlaggedOffside(PlayerAgent p) => _offsideActive && p.Team == _offsideTeam && _offsideFlags[p.Index];

        /// <summary>Offside position test at this instant (used by AI perception and tests).</summary>
        public bool InOffsidePosition(PlayerAgent q)
        {
            var team = q.Team;
            float f = team.Forward(q.Pos);
            if (f <= 0.05f) return false;
            if (f <= team.Forward(Ball.Pos) + 0.05f) return false;
            return f > SecondLastDefenderForward(team) + 0.05f;
        }

        /// <summary>Forward coordinate (in <paramref name="attacking"/>'s frame) of the second-last opponent.</summary>
        public float SecondLastDefenderForward(TeamAgent attacking)
        {
            float last = float.NegativeInfinity, secondLast = float.NegativeInfinity;
            foreach (var o in attacking.Opponent.Players)
            {
                if (o.SentOff) continue;
                float f = attacking.Forward(o.Pos);
                if (f > last) { secondLast = last; last = f; }
                else if (f > secondLast) secondLast = f;
            }
            return secondLast;
        }

        private void OffsideOffence(PlayerAgent q)
        {
            q.Team.Stats.Offsides++;
            var assistant = Assistant1.CoverSign == MathF.Sign(q.Pos.X) ? Assistant1 : Assistant2;
            assistant.FlagTimer = 2.5f;
            Emit(MatchEventType.Offside, q.Team, q, null, q.Pos);
            Whistle(WhistleKind.Foul);
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
            BeginRestart(RestartType.IndirectFreeKick, q.Team.Opponent, Pitch.ClampToField(q.Pos, -0.3f), null);
        }

        private void FlagBallOut(Vec2 spot)
        {
            var assistant = Assistant1.CoverSign == MathF.Sign(spot.X) ? Assistant1 : Assistant2;
            assistant.FlagTimer = MathF.Max(assistant.FlagTimer, 1.2f);
        }
    }
}

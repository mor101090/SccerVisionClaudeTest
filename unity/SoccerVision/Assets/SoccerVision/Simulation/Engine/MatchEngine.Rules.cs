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
            var shielder = team.Shielder;
            if (shielder != null && type != RestartType.CornerKick && !Ball.IsShot && Vec2.Distance(shielder.Pos, Ball.Pos) < 3f)
            {
                // Escorted out with the body for our own restart.
                team.Stats.ShieldedOut++;
                PlayerAgent chaser = null;
                float best = 9f;
                foreach (var o in team.Opponent.Players)
                {
                    if (o.SentOff) continue;
                    float d = Vec2.Distance(o.Pos, Ball.Pos);
                    if (d < best) { best = d; chaser = o; }
                }
                Emit(MatchEventType.ShieldOut, team, shielder, chaser, Ball.Pos, 0f, WhistleKind.Short, type == RestartType.GoalKick ? 0 : 1);
            }
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

        internal readonly float[] _diagShieldAt = { -99f, -99f }, _diagAnticipateAt = { -99f, -99f }, _diagParryAt = { -99f, -99f }, _diagSlideAt = { -99f, -99f }, _diagAnticipateMissAt = { -99f, -99f };

        private void OnGoal(int goalSign)
        {
            var defending = TeamDefendingGoal(goalSign);
            var scoringTeam = defending.Opponent;
            var toucher = Ball.LastToucher;
            if (CollectDiagnostics)
            {
                // What the defence was doing in the build-up to the goal (to find ugly, unrealistic goals).
                if (SimTime - _diagShieldAt[defending.Index] < 3f) Count("goal.ctx.shield");
                if (SimTime - _diagAnticipateAt[defending.Index] < 3f) Count("goal.ctx.anticipate");
                if (SimTime - _diagAnticipateMissAt[defending.Index] < 8f) Count("goal.ctx.anticipateMissed");
                if (SimTime - _diagParryAt[defending.Index] < 3f) Count("goal.ctx.afterParry");
                if (SimTime - _diagSlideAt[defending.Index] < 3f) Count("goal.ctx.afterSlide");
                Count("goal.all");
            }
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

            if (defending == _chargeDownTeam && SimTime - _chargeDownTime < 8f) KeeperChargeDownGoals++;
            scoringTeam.Score++;
            scoringTeam.Stats.Goals++;
            bool recentShot = Ball.Shooter != null && Ball.Shooter.Team == scoringTeam && SimTime - _lastShotTime < 3f;
            if (recentShot && !_shotCountedOnTarget) scoringTeam.Stats.ShotsOnTarget++;
            if (_penaltyKickTime >= 0f && Ball.Shooter != null && Ball.Shooter.Team == scoringTeam) scoringTeam.Stats.PenaltiesScored++;
            if (!ownGoal && scorer != null) scorer.Goals++;
            int goalKind = !ownGoal && recentShot ? _lastShotKind : 0;
            if (goalKind == 5) scoringTeam.Stats.HeaderGoals++;
            if (goalKind == 1) scoringTeam.Stats.VolleyGoals++;
            if (goalKind == 3) scoringTeam.Stats.FreeKickGoals++;

            // No whistle: IFAB guidance says it is not needed to stop play for a clear goal (it restarts the kick-off).
            Emit(MatchEventType.Goal, scoringTeam, scorer, assist, Ball.Pos, ownGoal ? 1f : 0f, WhistleKind.Short, goalKind);

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
            _celebrationAssist = assist;
            _kickOffAfterGoal = defending;
            ChooseCelebration(scoringTeam);
            _restart = null;
            foreach (var p in AllPlayers) p.Task = PlayerTask.Celebrate;
        }

        private void ChooseCelebration(TeamAgent scoring)
        {
            // Weighted pick: everybody piling on is the exception, not the rule.
            float roll = _rng.Range(0f, 1f);
            if (roll < 0.22f) Celebration = CelebrationKind.CornerRun;
            else if (roll < 0.44f) Celebration = CelebrationKind.FansRun;
            else if (roll < 0.60f) Celebration = CelebrationKind.KneeSlide;
            else if (roll < 0.78f) Celebration = CelebrationKind.AssistHug;
            else if (roll < 0.90f) Celebration = CelebrationKind.Lap;
            else Celebration = CelebrationKind.Huddle;

            _celebrationJoiners.Clear();
            var scorer = _celebrationScorer;
            if (scorer == null) return; // own goal: no celebration, everyone walks back
            int count;
            switch (Celebration)
            {
                case CelebrationKind.CornerRun: count = _rng.Range(1, 3); break;
                case CelebrationKind.FansRun: count = 1; break;
                case CelebrationKind.KneeSlide: count = _rng.Range(2, 4); break;
                case CelebrationKind.AssistHug: count = 1; break;
                case CelebrationKind.Lap: count = 0; break;
                default: count = _rng.Range(4, 7); break;
            }
            // The assister first, then the closest team-mates (outfield only).
            if (count > 0 && _celebrationAssist != null && !_celebrationAssist.IsGoalkeeper && !_celebrationAssist.SentOff)
                _celebrationJoiners.Add(_celebrationAssist);
            while (_celebrationJoiners.Count < count)
            {
                PlayerAgent best = null;
                float bestD = float.PositiveInfinity;
                foreach (var q in scoring.Players)
                {
                    if (q == scorer || q.SentOff || q.IsGoalkeeper || _celebrationJoiners.Contains(q)) continue;
                    float d = Vec2.Distance(q.Pos, scorer.Pos);
                    if (d < bestD) { bestD = d; best = q; }
                }
                if (best == null) break;
                _celebrationJoiners.Add(best);
            }
        }

        /// <summary>Scorer's own routine for the chosen <see cref="Celebration"/>.</summary>
        private void CelebrateScorer(PlayerAgent p, TeamAgent scoring, Vec2 corner, int side)
        {
            float t = _phaseTimer;
            Vec2 goal = scoring.AttackGoal;
            switch (Celebration)
            {
                case CelebrationKind.CornerRun:
                    p.Target = corner * 0.9f;
                    p.Urgency = Vec2.Distance(p.Pos, p.Target) > 2f ? 0.85f : 0f;
                    break;
                case CelebrationKind.Huddle:
                    p.Target = p.Pos;
                    p.Urgency = 0f;
                    if (t > 1.6f && p.JumpZ <= 0f && ((int)(t * 1.4f)) % 2 == 0) StartJump(p, 0.35f);
                    break;
                case CelebrationKind.FansRun:
                    // Sprint to the touchline in front of the fans, then leap with arms wide.
                    p.Target = new Vec2(MathUtil.Clamp(p.Pos.X, -44f, 44f), (Pitch.HalfWidth - 0.8f) * side);
                    p.Urgency = Vec2.Distance(p.Pos, p.Target) > 1.5f ? 0.95f : 0f;
                    if (p.Urgency == 0f && p.JumpZ <= 0f && ((int)(t * 1.2f)) % 3 == 0) StartJump(p, 0.55f);
                    break;
                case CelebrationKind.Lap:
                    {
                        // Solo run around the penalty area, arms out to the crowd.
                        float ang = t * 0.75f;
                        Vec2 centre = new Vec2(goal.X * 0.66f, 0f);
                        p.Target = centre + new Vec2(MathF.Cos(ang) * 10f, MathF.Sin(ang) * 15f);
                        p.Urgency = 0.75f;
                        break;
                    }
                case CelebrationKind.KneeSlide:
                    p.Target = corner * 0.8f;
                    p.Urgency = t < 2.5f ? 1f : 0f;
                    break;
                case CelebrationKind.AssistHug:
                    {
                        var mate = _celebrationJoiners.Count > 0 ? _celebrationJoiners[0] : null;
                        p.Target = mate != null && Vec2.Distance(p.Pos, mate.Pos) > 1.2f ? (p.Pos + mate.Pos) * 0.5f : p.Pos;
                        p.Urgency = 0.9f;
                        if (mate != null && Vec2.Distance(p.Pos, mate.Pos) < 1.3f && t > 2f && p.JumpZ <= 0f && t < 2.5f) StartJump(p, 0.45f);
                        break;
                    }
            }
        }

        private void CelebrateTeammate(PlayerAgent p, TeamAgent scoring, Vec2 corner, int side)
        {
            var scorer = _celebrationScorer;
            int rank = _celebrationJoiners.IndexOf(p);
            if (rank < 0)
            {
                // Not part of it: clap, then jog back for the kick-off.
                p.Target = _phaseTimer < 1.5f ? p.Pos : KickOffPosition(p, false);
                p.Urgency = 0.35f;
                return;
            }
            // Joiners set off one after the other.
            if (_phaseTimer < 0.4f + 0.5f * rank)
            {
                p.Target = p.Pos;
                p.Urgency = 0f;
                return;
            }
            Vec2 around = scorer.Pos + (p.Pos - scorer.Pos).Normalized * (1.1f + 0.35f * rank);
            switch (Celebration)
            {
                case CelebrationKind.KneeSlide:
                    {
                        // Dive on the slider, one after the other.
                        bool arrived = Vec2.Distance(p.Pos, scorer.Pos) < 1.6f + 0.3f * rank;
                        p.Target = arrived ? p.Pos : around;
                        p.Urgency = 0.95f;
                        if (arrived && _phaseTimer > 3f && p.JumpZ <= 0f && _phaseTimer < 3.4f + rank * 0.4f) StartJump(p, 0.3f);
                        break;
                    }
                case CelebrationKind.AssistHug:
                    p.Target = Vec2.Distance(p.Pos, scorer.Pos) > 1.2f ? (p.Pos + scorer.Pos) * 0.5f : p.Pos;
                    p.Urgency = 0.8f;
                    break;
                case CelebrationKind.Huddle:
                    p.Target = Vec2.Distance(p.Pos, scorer.Pos) < 1.5f + 0.3f * rank ? p.Pos : around;
                    p.Urgency = 0.9f;
                    if (_phaseTimer > 2.5f && p.JumpZ <= 0f && Vec2.Distance(p.Pos, scorer.Pos) < 3f && ((int)(_phaseTimer * 1.4f) + rank) % 3 == 0) StartJump(p, 0.35f);
                    break;
                default:
                    // Chase the scorer and embrace him wherever he stops.
                    p.Target = Vec2.Distance(p.Pos, scorer.Pos) < 1.4f + 0.3f * rank ? p.Pos : around;
                    p.Urgency = 0.85f;
                    if (Celebration == CelebrationKind.FansRun && scorer.Urgency == 0f && p.JumpZ <= 0f && Vec2.Distance(p.Pos, scorer.Pos) < 2f && ((int)(_phaseTimer * 1.2f)) % 3 == 1) StartJump(p, 0.45f);
                    break;
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
                    // The conceding team trudges back into its own half towards the kick-off shape.
                    p.Target = KickOffPosition(p, p.Team == _kickOffAfterGoal);
                    p.Urgency = 0.25f;
                }
                p.FaceBall = false;
                MovePlayer(p, dt);
                // Back onto the pitch (a player may have run or slid past a line just before the goal).
                if (!p.SentOff) p.Pos = Pitch.ClampToField(p.Pos, 0.3f);
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

        /// <summary>Set while a foul from a slide tackle is being judged (a higher card risk).</summary>
        private bool _slideFoul;

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

            // The victim may be hurt: more often from behind, in a slide, and when it was a red-card challenge.
            bool redChallenge = sanction != null && sanction.Red && sanction.Reason != "DOGSO";
            MaybeInjure(victim, 0.03f + (fromBehind ? 0.025f : 0f) + (_slideFoul ? 0.035f : 0f),
                        0.006f + (fromBehind ? 0.006f : 0f) + (_slideFoul ? 0.012f : 0f) + (redChallenge ? 0.12f : 0f), "foul");

            Whistle(penaltyArea || (sanction != null && sanction.Red) ? WhistleKind.Long : WhistleKind.Foul);
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
                if (sanction != null) _restart.WaitForWhistle = true; // play restarts on the whistle after a card
                Emit(MatchEventType.FreeKick, victim.Team, _restart.Taker, offender, spot, 0f, WhistleKind.Short, _restart.Wall.Count > 0 ? 1 : 0);
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
            if (_slideFoul) pRed += fromBehind ? 0.012f : 0.003f; // excessive force: two-footed / from behind
            if (_rng.Chance(pRed))
            {
                if (CollectDiagnostics) Count("card.sfp.red");
                return new PendingSanction { Player = offender, Red = true, Reason = "serious foul play" };
            }

            bool promising = IsStoppingPromisingAttack(victim, offender);
            float pYellow = 0.06f + (fromBehind ? 0.13f : 0f) + 0.08f * aggression + (promising ? 0.55f : 0f)
                            + (offender.FoulsCommitted >= 3 ? 0.2f : 0f);
            if (!playedForBall) pYellow += 0.1f;
            if (_slideFoul) pYellow += fromBehind ? 0.15f : 0.05f; // a mistimed slide is usually reckless
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
                ShowCard(2);
                SendOff(p);
                return;
            }
            if (s.Yellow)
            {
                p.YellowCards++;
                p.Team.Stats.YellowCards++;
                Emit(MatchEventType.YellowCard, p.Team, p, null, p.Pos, p.YellowCards);
                ShowCard(1);
                if (p.YellowCards >= 2)
                {
                    // Second caution: the referee shows the yellow card first, then the red one.
                    if (CollectDiagnostics) Count("card.secondyellow");
                    _secondYellowRed = p;
                    _secondYellowRedAt = SimTime + SecondCardDelay;
                }
            }
        }

        /// <summary>Sim seconds between the second yellow card and the red card that follows it.</summary>
        private float SecondCardDelay => 1.6f * Config.MotionSpeed;
        private PlayerAgent _secondYellowRed;
        private float _secondYellowRedAt;

        /// <summary>The referee holds a card up (1 yellow, 2 red) for a moment.</summary>
        private void ShowCard(int card)
        {
            Referee.CardShown = card;
            Referee.CardTimer = (card == 2 ? 3f : 2.2f) * Config.MotionSpeed;
        }

        /// <summary>After a second yellow: the red card, and the player is off.</summary>
        private void UpdatePendingRedCard()
        {
            if (_secondYellowRed == null || SimTime < _secondYellowRedAt) return;
            var p = _secondYellowRed;
            _secondYellowRed = null;
            if (p.SentOff) return;
            p.Team.Stats.RedCards++;
            Emit(MatchEventType.RedCard, p.Team, p, null, p.Pos, 1f);
            ShowCard(2);
            SendOff(p);
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
            Whistle(WhistleKind.Light);
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
            assistant.FlagTimer = Config.OffsideFlagRealSeconds * Config.MotionSpeed;
            assistant.HoldPosition = true;
            assistant.HoldX = q.Pos.X;
            Emit(MatchEventType.Offside, q.Team, q, null, q.Pos);
            Whistle(WhistleKind.Light);
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
            q.OffsideMarkTimer = Config.OffsideFlagRealSeconds * Config.MotionSpeed;
            BeginRestart(RestartType.IndirectFreeKick, q.Team.Opponent, Pitch.ClampToField(q.Pos, -0.3f), null);
            _restart.SetupTime = MathF.Max(_restart.SetupTime, Config.OffsideRestartRealSeconds * Config.MotionSpeed);
        }

        private void FlagBallOut(Vec2 spot)
        {
            var assistant = Assistant1.CoverSign == MathF.Sign(spot.X) ? Assistant1 : Assistant2;
            assistant.FlagTimer = MathF.Max(assistant.FlagTimer, 1.2f * Config.MotionSpeed);
        }
    }
}

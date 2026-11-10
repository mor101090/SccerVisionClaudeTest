using System;
using System.Collections.Generic;
using System.Linq;

namespace SoccerVision.Simulation
{
    /// <summary>Restarts - the stoppage: players walk to legal positions, the taker goes to the ball, and the restart is taken once ready.</summary>
    public sealed partial class MatchEngine
    {
        private void StepStoppage(float dt)
        {
            var r = _restart;
            if (r == null)
            {
                StoppageId++;
                Phase = MatchPhase.Live;
                return;
            }
            if (r.PauseLeft > 0f)
            {
                // The moment after the whistle: everybody pulls up where he is, the ball runs on and stops.
                r.PauseLeft -= dt;
                BallPhysics.Step(ref Ball.M, Config, dt);
                if (!Pitch.InsideField(Ball.Pos, 3f)) Ball.Stop();
                foreach (var p in AllPlayers)
                {
                    if (!p.SentOff && StepInjuredPlayer(p, dt)) continue;
                    if (!p.SentOff)
                    {
                        p.Task = PlayerTask.SetPiece;
                        p.FaceBall = true;
                        p.Target = p.Pos + p.Vel * 0.35f;
                        p.Urgency = 0.15f;
                    }
                    MovePlayer(p, dt);
                    // (nobody drifts off the pitch while pulling up; the one who put the ball out stays where he is)
                    if (!p.SentOff) p.Pos = Pitch.ClampToField(p.Pos, -0.2f);
                }
                return;
            }
            r.Timer += dt;
            if (AddedTimeExpired && r.Type != RestartType.PenaltyKick && Clock.Running)
            {
                EndHalf();
                return;
            }

            if (!r.BallPlaced)
            {
                if (_newBallRolls)
                {
                    // a ball boy's ball, rolled in from beyond the touchline (nobody is seen): it slows and stops on the spot
                    const float roll = 1.5f;
                    float k = MathUtil.Clamp01(r.Timer / roll), e = 1f - (1f - k) * (1f - k);
                    Vec2 pos = Vec2.Lerp(_newBallFrom, r.Spot, e);
                    float v = 2f * (1f - k) * (r.Spot - _newBallFrom).Length / roll;
                    Vec2 dir = (r.Spot - _newBallFrom).Normalized;
                    Ball.M = new BallMotion { X = pos.X, Y = pos.Y, Z = Pitch.BallRadius, Vx = dir.X * v, Vy = dir.Y * v };
                    Ball.PrevPos = pos;
                    if (r.Timer >= roll)
                    {
                        Ball.Place(r.Spot);
                        r.BallPlaced = true;
                        _newBallRolls = false;
                    }
                }
                else if (r.Timer > 0.6f)
                {
                    Ball.Place(r.Spot);
                    r.BallPlaced = true;
                }
                else
                {
                    BallPhysics.Step(ref Ball.M, Config, dt);
                    if (!Pitch.InsideField(Ball.Pos, 3f)) Ball.Stop();
                }
            }

            Vec2 approach = ApproachPoint(r);
            // The taker first walks back to the start of his run-up, then runs up to the ball.
            Vec2 moveTo = r.HasRunUp && !r.RunningUp ? r.RunUpStart : approach;
            foreach (var p in AllPlayers)
            {
                if (p.SentOff)
                {
                    MovePlayer(p, dt);
                    continue;
                }
                if (StepInjuredPlayer(p, dt)) continue;
                if (IsComingOff(p)) continue; // (walking off for a change: see StepSubstitutions)
                p.Task = PlayerTask.SetPiece;
                p.FaceBall = true;
                if (CardPlayerTarget(p, out var cardTarget, out var cardUrgency))
                {
                    p.Target = cardTarget;
                    p.Urgency = cardUrgency;
                }
                else if (p == r.Taker)
                {
                    p.Target = moveTo;
                    float run = r.RunningUp ? (r.Type == RestartType.PenaltyKick ? 0.5f : 0.62f) : (Vec2.Distance(p.Pos, moveTo) > 8f ? 0.8f : 0.4f);
                    p.Urgency = run;
                }
                else
                {
                    p.Target = Pitch.ClampToField(p.ShapeTarget, -0.3f);
                    float d = Vec2.Distance(p.Pos, p.Target);
                    p.Urgency = d > 12f ? 0.7f : (d > 3f ? 0.5f : 0.3f);
                }
                MovePlayer(p, dt);
                // Everybody but the taker stays inside the field of play while a restart is set up.
                if (p != r.Taker) p.Pos = Pitch.ClampToField(p.Pos, -0.2f);
                else if (r.Type != RestartType.ThrowIn && r.Type != RestartType.CornerKick) p.Pos = Pitch.ClampToField(p.Pos, 0.3f);
                else if (r.Type == RestartType.ThrowIn) p.Pos = Pitch.ClampToField(p.Pos, 0.95f); // (he walks to the line, no further than a pace over it)
            }

            if (r.Type == RestartType.ThrowIn && r.BallPlaced && r.Taker != null && Vec2.Distance(r.Taker.Pos, approach) < 1.2f)
            {
                // Both hands, from behind and over the head.
                Ball.Place(r.Taker.Pos + r.Taker.FacingDir * 0.15f, 2.15f);
            }

            // Nobody restarts while an injured player is being treated or helped off, or a change is being made (the time is added on).
            bool changing = StepSubstitutions(dt);
            if (InjuryBeingHandled() || changing)
            {
                r.CountsAsLostTime = true;
                r.Timer = MathF.Min(r.Timer, r.SetupTime);
                if (r.Taker != null && r.Taker.Injury == InjuryLevel.Serious) r.Taker = null;
                return;
            }
            if (r.Taker == null || r.Taker.SentOff) r.Taker = ChooseTaker(r);
            // (the taker must really be at the ball: it only moves when it is kicked)
            bool takerReady = r.Taker != null && Vec2.Distance(r.Taker.Pos, moveTo) < 0.3f;
            // (nothing restarts while the referee is still showing a card: play resumes only after he has put it away)
            if (CardSequenceActive) { r.Timer = MathF.Min(r.Timer, r.SetupTime); return; } // (not counted as lost time: as before - see the CHANGELOG)
            bool ready = r.RunningUp || r.WhistleAt >= 0f || (r.BallPlaced && r.Timer >= r.SetupTime && takerReady && PlayersSettled(r));
            if (!ready && r.Timer >= Config.MaxSetupSeconds)
            {
                if (CollectDiagnostics) Count("restart.forced." + r.Type);
                if (r.Taker == null || r.Taker.SentOff) r.Taker = ChooseTaker(r);
                if (r.Taker != null)
                {
                    r.Taker.Pos = approach;
                    r.Taker.Vel = Vec2.Zero;
                }
                if (!r.BallPlaced) { Ball.Place(r.Spot); r.BallPlaced = true; }
                r.RunningUp = true; // skip the run-up: the taker was put at the ball
                ready = r.Taker != null;
            }
            if (ready && r.WaitForWhistle)
            {
                // The referee whistles; the taker waits a moment before playing the ball (players hold position).
                if (r.WhistleAt < 0f)
                {
                    r.WhistleAt = r.Timer;
                    Whistle(WhistleKind.Short);
                }
                if (r.Timer - r.WhistleAt < Config.WhistleToKickRealSeconds * Config.MotionSpeed) return;
            }
            if (ready && r.HasRunUp && !r.RunningUp)
            {
                // Whistle (if any) has been blown: now the run-up starts.
                r.RunningUp = true;
                r.RunUpBegan = r.Timer;
                return;
            }
            if (ready && r.RunningUp && r.HasRunUp && r.Taker != null && Vec2.Distance(r.Taker.Pos, approach) > 0.3f && r.Timer - r.RunUpBegan < 4f * Config.MotionSpeed) return;
            if (ready)
            {
                if (CollectDiagnostics) for (int k = 0; k < (int)r.Timer; k++) Count("restart.seconds." + r.Type);
                ExecuteRestart(r);
            }
        }

        /// <summary>Restarts wait until (nearly) everyone has reorganised: back in shape, in the box, in the wall.</summary>
        private bool PlayersSettled(Restart r)
        {
            int allowed;
            switch (r.Type)
            {
                case RestartType.KickOff: allowed = 0; break;
                case RestartType.PenaltyKick: allowed = 0; break;
                case RestartType.ThrowIn: allowed = 4; break;
                case RestartType.GoalKick: allowed = 2; break;
                case RestartType.CornerKick: allowed = 1; break;
                default: allowed = r.Wall.Count > 0 ? 1 : 3; break;
            }
            int unsettled = 0;
            foreach (var p in AllPlayers)
            {
                if (p.SentOff || p == r.Taker) continue;
                if (Vec2.Distance(p.Pos, p.Target) > 2.5f && ++unsettled > allowed) return false;
            }
            return true;
        }

        /// <summary>Referee management: anyone still illegally positioned is moved back before the kick.</summary>
        private void EnforceRestartPositions(Restart r)
        {
            var att = r.Team;
            var def = att.Opponent;
            foreach (var p in AllPlayers)
            {
                if (p.SentOff || p == r.Taker) continue;
                Vec2 pos = p.Pos;
                switch (r.Type)
                {
                    case RestartType.KickOff:
                        // Everyone in their own half; opponents outside the centre circle.
                        if (p.Team.Forward(pos) > -0.3f) pos = p.Team.ToWorld(-0.5f, p.Team.Lateral(pos));
                        if (p.Team == def && pos.Length < Pitch.CentreCircleRadius + 0.2f)
                        {
                            Vec2 dir = pos.Length > 1e-3f ? pos.Normalized : def.DirToWorld(new Vec2(-1f, 0f));
                            if (p.Team.Forward(r.Spot + dir) > 0f) dir = new Vec2(-dir.X, dir.Y);
                            pos = dir * (Pitch.CentreCircleRadius + 0.3f);
                            if (p.Team.Forward(pos) > -0.3f) pos = p.Team.ToWorld(-0.5f, MathF.Sign(p.Team.Lateral(pos) == 0f ? 1f : p.Team.Lateral(pos)) * (Pitch.CentreCircleRadius + 0.3f));
                        }
                        break;
                    case RestartType.ThrowIn:
                        if (p.Team == def && Vec2.Distance(pos, r.Spot) < Pitch.ThrowInDistance + 0.1f) pos = OutsideCircle(pos, r.Spot, Pitch.ThrowInDistance + 0.3f);
                        break;
                    case RestartType.GoalKick:
                        if (p.Team == def) pos = OutsidePenaltyArea(pos, att.OwnGoalSign, 0.3f);
                        break;
                    case RestartType.CornerKick:
                        if (p.Team == def && Vec2.Distance(pos, r.Spot) < Pitch.RestartDistance) pos = OutsideCircle(pos, r.Spot, Pitch.RestartDistance + 0.2f);
                        break;
                    case RestartType.DroppedBall:
                        // Law 8: everybody else at least 4 m from the ball.
                        if (p.Team == def && Vec2.Distance(pos, r.Spot) < Pitch.DroppedBallDistance) pos = OutsideCircle(pos, r.Spot, Pitch.DroppedBallDistance + 0.2f);
                        break;
                    case RestartType.PenaltyKick:
                        if (p == def.Goalkeeper)
                        {
                            pos = new Vec2((Pitch.HalfLength - 0.05f) * att.AttackGoalSign, MathUtil.Clamp(pos.Y, -Pitch.GoalHalfWidth + 0.3f, Pitch.GoalHalfWidth - 0.3f));
                        }
                        else
                        {
                            pos = LegalPenaltyPosition(pos, att.AttackGoalSign);
                        }
                        break;
                    default:
                        if (p.Team == def)
                        {
                            bool onGoalLine = MathF.Abs(MathF.Abs(pos.X) - Pitch.HalfLength) < 0.5f && MathF.Abs(pos.Y) < Pitch.GoalHalfWidth;
                            if (!onGoalLine && !r.Wall.Contains(p) && Vec2.Distance(pos, r.Spot) < Pitch.RestartDistance) pos = OutsideCircle(pos, r.Spot, Pitch.RestartDistance + 0.2f);
                            if (r.Wall.Contains(p) && Vec2.Distance(pos, r.Spot) < Pitch.RestartDistance) pos = OutsideCircle(pos, r.Spot, Pitch.RestartDistance + 0.05f);
                            if (Pitch.InPenaltyArea(r.Spot, def.AttackGoalSign)) pos = OutsidePenaltyArea(pos, def.AttackGoalSign, 0.2f);
                        }
                        else if (r.Wall.Count >= 3)
                        {
                            foreach (var w in r.Wall)
                            {
                                if (Vec2.Distance(pos, w.Pos) < 1.05f) pos = w.Pos + (pos - w.Pos).Normalized * 1.1f;
                            }
                        }
                        break;
                }
                if (pos != p.Pos)
                {
                    p.Pos = pos;
                    p.PrevPos = pos;
                }
            }
        }

        private float _lastRestartAt = -100f;       // (diagnostics: what the last restart was and when)
        private RestartType _lastRestartType;
        private TeamAgent _lastRestartTeam;

        private void ExecuteRestart(Restart r)
        {
            var taker = r.Taker;
            EnforceRestartPositions(r);
            _lastRestartAt = SimTime; _lastRestartType = r.Type; _lastRestartTeam = r.Team;
            RestartExecuting?.Invoke(r);
            if (r.Team != null && r.Type != RestartType.KickOff && r.Type != RestartType.GoalKick && r.Type != RestartType.DroppedBall && r.Team.Forward(r.Spot) > 0f)
                r.Team.SetPieceAt = SimTime;
            StoppageId++; // (the ball is back in play: the next stoppage is a new substitution opportunity)
            Phase = MatchPhase.Live;
            _restart = null;
            _goalkeeperHoldTimer = 0f;
            PossessionTeam = r.Team;
            if (taker == null || taker.SentOff)
            {
                taker = ChooseTaker(r);
                if (taker == null) return;
            }
            Vec2 spot = r.Spot;
            Ball.Place(spot, r.Type == RestartType.ThrowIn ? 2.15f : Pitch.BallRadius);
            Ball.Owner = taker;
            Ball.InHands = false;
            Ball.ClearFlight();
            Ball.InGoal = false;
            _restartKick = true;
            _restartOffsideException = r.Type == RestartType.ThrowIn || r.Type == RestartType.GoalKick || r.Type == RestartType.CornerKick;
            try
            {
                switch (r.Type)
                {
                    case RestartType.KickOff:
                        if (!Clock.Running)
                        {
                            Clock.Running = true;
                            _periodRealStart = RealTimeElapsed;
                        }
                        Emit(MatchEventType.KickOff, r.Team, taker, null, spot);
                        r.Team.KickOffsTaken++;
                        ExecuteKickOff(taker);
                        break;
                    case RestartType.ThrowIn:
                        ExecuteThrowIn(taker, spot, r.LongThrow);
                        break;
                    case RestartType.GoalKick:
                        ExecuteGoalKick(taker);
                        break;
                    case RestartType.CornerKick:
                        ExecuteCorner(taker, r);
                        break;
                    case RestartType.PenaltyKick:
                        ExecutePenalty(taker);
                        break;
                    default:
                        ExecuteFreeKick(taker, r);
                        break;
                }
            }
            finally
            {
                _restartKick = false;
                _restartOffsideException = false;
            }
            if (Ball.Owner == taker && Phase == MatchPhase.Live)
            {
                // Fallback: no option found, play it short forward.
                var plan = new KickPlan { Action = BallAction.Pass, Kind = KickKind.Pass, Target = spot + taker.Team.DirToWorld(Vec2.UnitX) * 8f, Speed = Roll.KickSpeedFor(8f, 4f), Heading = taker.Team.DirToWorld(Vec2.UnitX).Angle };
                _restartKick = true;
                KickGround(taker, plan, 0f);
                _restartKick = false;
            }
        }

        private void ExecuteKickOff(PlayerAgent taker)
        {
            var team = taker.Team;
            PlayerAgent best = null;
            float bestScore = float.NegativeInfinity;
            foreach (var q in team.Players)
            {
                if (q.SentOff || q == taker || q.IsGoalkeeper) continue;
                float behind = team.Forward(taker.Pos) - team.Forward(q.Pos);
                if (Config.KickOffMustGoBackwards && behind < 3f) continue;
                float d = Vec2.Distance(q.Pos, taker.Pos);
                float score = (q.Slot.IsCentralMidfielder ? 10f : 0f) - MathF.Abs(d - 13f);
                if (score > bestScore) { bestScore = score; best = q; }
            }
            if (best == null) return;
            Vec2 target = best.Pos;
            float dist = Vec2.Distance(Ball.Pos, target);
            float v0 = Roll.KickSpeedFor(dist, 4.5f);
            var plan = new KickPlan { Action = BallAction.Pass, Kind = KickKind.Pass, Receiver = best, Target = target, Speed = v0, Heading = (target - Ball.Pos).Angle };
            KickGround(taker, plan, 0f);
        }

        private KickPlan BestRestartPass(PlayerAgent taker, float maxDistance, bool allowLofted)
        {
            _options.Clear();
            EvaluatePasses(taker, Ball.Pos, 0f);
            KickPlan best = default;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < _options.Count; i++)
            {
                var o = _options[i];
                if (o.Action != BallAction.Pass || o.Receiver == null) continue;
                if (!allowLofted && o.Lofted) continue;
                if (Vec2.Distance(o.Target, Ball.Pos) > maxDistance) continue;
                float perceived = o.Utility + _rng.Gaussian(0f, 0.003f);
                if (perceived > bestScore) { bestScore = perceived; best = o; }
            }
            if (bestScore == float.NegativeInfinity) best.Action = BallAction.Hold;
            return best;
        }

        private void ExecuteThrowIn(PlayerAgent taker, Vec2 spot, bool longThrow)
        {
            var team = taker.Team;
            PlayerAgent receiver = null;
            Vec2 target;
            if (longThrow)
            {
                // Into the near side of the six-yard box for the flick across goal (polish round 2: where it comes down is the throw's
                // physics - see ReleaseLongThrow).
                LongThrowTarget(taker, spot, out target);
                target += team.DirToWorld(new Vec2(_rng.Range(-1f, 1f), _rng.Range(-1.5f, 1.5f)));
                team.Stats.LongThrows++;
                if (CollectDiagnostics) Count("throw.long");
            }
            else
            {
                var plan = BestRestartPass(taker, 26f, true);
                if (plan.Action == BallAction.Pass)
                {
                    receiver = plan.Receiver;
                    target = plan.Target;
                }
                else
                {
                    target = Pitch.ClampToField(spot + team.DirToWorld(Vec2.UnitX) * 10f + new Vec2(0f, -MathF.Sign(spot.Y) * 6f), -1f);
                }
            }
            Vec2 from = new Vec2(spot.X, spot.Y - MathF.Sign(spot.Y) * 0.05f);
            // (real: 1.43 throw-ins a match end in the penalty area, median 24.9 m - tools/calibration/throw_ins.py)
            if (CollectDiagnostics && Pitch.InPenaltyArea(target, team.AttackGoalSign)) Count(longThrow ? "throw.intoBox.long" : "throw.intoBox.short");
            if (longThrow)
            {
                ReleaseLongThrow(taker, from, target);
                return;
            }
            float d = Vec2.Distance(from, target);
            float horizontal = longThrow ? MathUtil.Clamp(9f + 0.38f * d, 12f, 17f) : MathUtil.Clamp(7f + 0.35f * d, 7f, 14f);
            Vec2 dir = (target - from).Normalized.Rotate(_rng.Gaussian(0f, longThrow ? 0.035f : 0.05f));
            float vz = KickSolver.VerticalSpeedFor(dir * horizontal, 2.15f, d, longThrow ? 1.9f : 0.6f, Config);
            KickBall(taker, from, dir * horizontal, vz, 0f, 0f, KickKind.Restart, receiver, target, true, true, 2.15f);
        }

        private void ExecuteGoalKick(PlayerAgent taker)
        {
            var team = taker.Team;
            if (_shortGoalKick)
            {
                var plan = BestRestartPass(taker, 32f, false);
                if (plan.Action == BallAction.Pass && plan.Success > 0.7f)
                {
                    plan.Kind = KickKind.Restart;
                    KickGround(taker, plan, 0f);
                    return;
                }
            }
            PlayerAgent longTarget = null;
            float bestF = float.NegativeInfinity;
            foreach (var q in team.Players)
            {
                if (q.SentOff || q == taker) continue;
                float qf = team.Forward(q.Pos) + (q.Slot.Line == Line.Attack ? 4f : 0f) + _rng.Range(0f, 6f);
                if (qf > bestF) { bestF = qf; longTarget = q; }
            }
            float kicking = MathUtil.A(taker.Attr.Kicking);
            Vec2 aim = longTarget != null ? longTarget.Pos : team.ToWorld(5f, 0f);
            float distance = MathUtil.Clamp(Vec2.Distance(Ball.Pos, aim), 35f, 42f + 22f * kicking);
            Vec2 target = Pitch.ClampToField(Ball.Pos + (aim - Ball.Pos).Normalized * distance, -3f);
            KickLofted(taker, target, 0.6f, KickKind.Restart, longTarget, 0f, kicking, false);
        }

        private void ExecuteCorner(PlayerAgent taker, Restart r)
        {
            var team = taker.Team;
            int routine = r != null && r.CornerRoutine > 0 ? r.CornerRoutine : 1;
            if (routine == 5)
            {
                // Short: to the team-mate who came towards the ball (see LayoutCorner).
                var plan = BestRestartPass(taker, 18f, false);
                if (plan.Action == BallAction.Pass)
                {
                    plan.Kind = KickKind.Restart;
                    KickGround(taker, plan, 0f);
                    if (CollectDiagnostics) Count("corner.short");
                    return;
                }
                routine = 3;
            }
            float side = MathF.Sign(team.Lateral(Ball.Pos));
            // Near post, far post, the penalty spot, or a lower ball back to the players waiting at the edge of the box.
            // (near and far post just in front of the six-yard box: inside it the keeper comes and claims it)
            Vec2 zone = routine == 1 ? new Vec2(45.8f, side * 2.6f) : routine == 2 ? new Vec2(46f, -side * 3.6f)
                      : routine == 4 ? new Vec2(36.5f, -side * 1.5f) : new Vec2(42f, side * 0.5f);
            Vec2 target = team.ToWorld(zone.X, zone.Y);
            PlayerAgent receiver = null;
            float bestD = float.PositiveInfinity;
            foreach (var q in team.Players)
            {
                if (q.SentOff || q == taker || q.IsGoalkeeper) continue;
                float d = Vec2.Distance(q.Pos, target);
                if (d < bestD) { bestD = d; receiver = q; }
            }
            // The ball bends the way the taker's foot curls it: whether that is an in-swinger (towards goal) or an
            // out-swinger (away from it) depends on his foot and on the side the corner is taken from.
            Vec2 path = target - Ball.Pos;
            Vec2 leftOfPath = new Vec2(-path.Y, path.X);
            bool inswinger = Vec2.Dot(leftOfPath * NaturalCurlSign(taker), team.AttackGoal - target) > 0f;
            team.Stats.Crosses++;
            if (CollectDiagnostics) Count("corner." + (routine == 1 ? "near" : routine == 2 ? "far" : routine == 4 ? "edge" : "middle") + (inswinger ? ".in" : ".out"));
            _crossDetail = routine == 3 ? 3 : routine;
            _crossValue = inswinger ? 1f : 2f;
            _curlBoost = 1.25f;
            KickLofted(taker, target, routine == 4 ? _rng.Range(0.28f, 0.34f) : _rng.Range(0.36f, 0.46f), KickKind.Cross, receiver, 0f,
                MathUtil.A((taker.Attr.Crossing * 2 + taker.Attr.FreeKicks) / 3), true);
            _crossDetail = 0;
            _crossValue = 0f;
            _curlBoost = 1f;
        }

        private void ExecuteFreeKick(PlayerAgent taker, Restart r)
        {
            var team = taker.Team;
            bool dangerous = IsDangerousFreeKick(r);
            float dist = Vec2.Distance(Ball.Pos, team.AttackGoal);
            if (dangerous && r.Type == RestartType.DirectFreeKick && dist < 33f && MathF.Abs(Ball.Pos.Y) < 22f)
            {
                float skill = MathUtil.A(taker.Attr.FreeKicks);
                float pShoot = MathUtil.Clamp(0.35f + 0.5f * skill - (dist - 20f) * 0.03f, 0.15f, 0.9f);
                if (_rng.Chance(pShoot))
                {
                    ExecuteFreeKickShot(taker, r);
                    return;
                }
            }
            if (dangerous)
            {
                // Deliver into the box like a corner: to one of the players waiting there (the better in the air, the likelier).
                var zoneLocal = new Vec2(_rng.Range(41f, 46f), _rng.Range(-5f, 5f));
                Vec2 target = team.ToWorld(zoneLocal.X, zoneLocal.Y);
                PlayerAgent receiver = null;
                float total = 0f;
                foreach (var q in team.Players)
                {
                    if (q.SentOff || q == taker || q.IsGoalkeeper || team.Forward(q.Pos) < 36f || MathF.Abs(team.Lateral(q.Pos)) > 18f) continue;
                    total += 1f + MathF.Max(0f, (q.Attr.Heading + q.Attr.Jumping) / 100f);
                }
                if (total > 0f)
                {
                    float pick = _rng.NextFloat() * total;
                    foreach (var q in team.Players)
                    {
                        if (q.SentOff || q == taker || q.IsGoalkeeper || team.Forward(q.Pos) < 36f || MathF.Abs(team.Lateral(q.Pos)) > 18f) continue;
                        pick -= 1f + MathF.Max(0f, (q.Attr.Heading + q.Attr.Jumping) / 100f);
                        if (pick <= 0f) { receiver = q; break; }
                    }
                    if (receiver != null) target = Pitch.ClampToField(receiver.Pos, 1f);
                }
                if (receiver == null)
                {
                    float bestD = float.PositiveInfinity;
                    foreach (var q in team.Players)
                    {
                        if (q.SentOff || q == taker || q.IsGoalkeeper) continue;
                        float d = Vec2.Distance(q.Pos, target);
                        if (d < bestD) { bestD = d; receiver = q; }
                    }
                }
                team.Stats.Crosses++;
                KickLofted(taker, target, 0.4f, KickKind.Cross, receiver, 0f, MathUtil.A((taker.Attr.Crossing + taker.Attr.FreeKicks) / 2), true);
                return;
            }
            var plan = BestRestartPass(taker, 45f, true);
            if (plan.Action == BallAction.Pass)
            {
                if (plan.Lofted) KickLofted(taker, plan.Target, plan.LaunchAngle, KickKind.LongPass, plan.Receiver, 0f, MathUtil.A(taker.Attr.Passing), false);
                else KickGround(taker, plan, 0f);
            }
        }

        private void ExecuteFreeKickShot(PlayerAgent taker, Restart r)
        {
            var team = taker.Team;
            int goalSign = team.AttackGoalSign;
            float skill = MathUtil.A((taker.Attr.FreeKicks * 2 + taker.Attr.LongShots) / 3);
            float nearSide = MathF.Abs(Ball.Pos.Y) > 0.5f ? MathF.Sign(Ball.Pos.Y) : 1f;
            // Over the wall into the corner the wall covers, or across to the keeper's side.
            float targetSide = _rng.Chance(FreeKickStyles ? FreeKickNearSide : 0.6f) ? nearSide : -nearSide;
            {
                // Right-footers bend it left, left-footers right: they aim for the side their curl brings the ball back to.
                Vec2 d0 = (new Vec2(Pitch.HalfLength * goalSign, targetSide * 2f) - Ball.Pos).Normalized;
                float needed = -MathF.Sign(Vec2.Cross(d0, new Vec2(0f, targetSide)));
                if (needed != NaturalCurlSign(taker) && _rng.Chance(FreeKickStyles && targetSide == nearSide ? FreeKickFootSwitch : 0.8f)) targetSide = -targetSide;
            }
            Vec2 aim = new Vec2(Pitch.HalfLength * goalSign, targetSide * (Pitch.GoalHalfWidth - 0.4f - 0.6f * (1f - skill)));
            float aimZ = _rng.Range(FreeKickAimLow, FreeKickAimHigh); // (polish round 6: real on-target free kicks cross the line at 1.46 m, median)
            Vec2 from = Ball.Pos;
            Vec2 dir = (aim - from).Normalized;
            float power = 22f + 5f * skill;
            float curlDraw = _rng.Range(15f, 28f);
            float curl = -MathF.Sign(Vec2.Cross(dir, new Vec2(0f, targetSide)))
                         * (Config.Kick != null ? Config.Kick.FreeKick.Spin(0.5f * (curlDraw - 15f) / 13f + 0.5f * skill, power, Config.Ball.RadiusM) : curlDraw); // (stage 7: the measured curve-kick spin)
            float topSpin = _rng.Range(8f, 16f);
            // The kind of strike (polish round 6).
            float fkDist = Vec2.Distance(from, aim);
            var style = ChooseFreeKickStyle(taker, fkDist, r.Wall.Count);
            _freeKickStyle = style;
            bool knuckle = style == FreeKickStyle.Knuckle;
            switch (style)
            {
                case FreeKickStyle.Knuckle:
                    // no spin and no curl, the ball floats and dips late
                    aim = new Vec2(aim.X, _rng.Range(-2.4f, 2.4f)); curl = _rng.Gaussian(0f, 0.4f); power = 25f + 4f * skill; aimZ = _rng.Range(0.8f, 1.8f); topSpin = 0f;
                    break;
                case FreeKickStyle.Driven:
                    // the laces: hard, a little spin, not much bend - at either side, chest to bar height
                    aim = new Vec2(aim.X, _rng.Range(-1f, 1f) * (Pitch.GoalHalfWidth - 0.7f)); power = 26f + 5f * MathUtil.A((taker.Attr.LongShots + taker.Attr.PhysicalPower) / 2);
                    curl *= 0.3f; topSpin = _rng.Range(4f, 10f); aimZ = _rng.Range(0.7f, 1.8f);
                    break;
                case FreeKickStyle.Placed:
                    // over the end of the wall into the near corner, with less pace and more care
                    targetSide = nearSide;
                    aim = new Vec2(aim.X, nearSide * (Pitch.GoalHalfWidth - 0.45f - 0.4f * (1f - skill))); power = 19f + 4f * skill;
                    curl = -MathF.Sign(Vec2.Cross((aim - from).Normalized, new Vec2(0f, nearSide))) * MathF.Abs(curl) * 0.8f; aimZ = _rng.Range(1.5f, 2.2f);
                    break;
                case FreeKickStyle.Under:
                    // low and hard along the grass where the wall stands: under it as it jumps
                    aim = new Vec2(aim.X, nearSide * _rng.Range(1.2f, Pitch.GoalHalfWidth - 0.5f)); power = 21f + 4f * skill; curl *= 0.2f; topSpin = _rng.Range(2f, 6f); aimZ = _rng.Range(0.12f, 0.3f);
                    break;
            }
            if (CollectDiagnostics) { Count("fk.style." + style); if (MathF.Abs(Ball.Pos.Y) > 3f) { Count(MathF.Sign(aim.Y) == nearSide ? "fk.side.near" : "fk.side.far");
                var gkk = team.Opponent.Goalkeeper; if (gkk != null && r.Wall.Count >= 2) { float ky = gkk.Pos.Y * nearSide; Count("fk.keeper." + (ky < -0.73f ? "a<-0.73" : ky < -0.27f ? "b-0.73..-0.27" : ky < 0.27f ? "c-0.27..0.27" : "d>0.27")); } } }
            dir = (aim - from).Normalized;
            // Aim the start line away from the target so the curl brings it back.
            Vec2 startDir = knuckle || Config.Aero != null ? dir : dir.Rotate(-MathF.Sign(curl) * 0.06f);
            float vz = KickSolver.VerticalSpeedFor(startDir * power, Pitch.BallRadius, Vec2.Distance(from, aim), aimZ, Config, curl, topSpin);
            if (!knuckle && Config.Aero != null)
            {
                // the start line that the physical curl brings back onto the aim (the same integrator as the flight)
                startDir = dir.Rotate(KickSolver.CurlYawToReach(power, Pitch.BallRadius, vz, Vec2.Distance(from, aim), Config, curl, topSpin));
            }
            float sigma = (0.012f + 0.045f * (1f - skill)) * (knuckle ? 1.5f : style == FreeKickStyle.Driven ? 1.3f : style == FreeKickStyle.Placed ? 0.8f : 1f);
            startDir = startDir.Rotate(_rng.Gaussian(0f, sigma));
            vz += _rng.Gaussian(0f, (0.25f + 0.9f * (1f - skill)) * (style == FreeKickStyle.Under ? 0.4f : FreeKickHeightError));
            if (style == FreeKickStyle.Under) vz = MathF.Min(vz, 0.8f);
            if (FreeKickStyles) ScheduleWallJumps(r);
            else foreach (var w in r.Wall) StartJump(w, 0.4f); // the wall jumps
            float xg = MathUtil.Clamp(0.09f - (Vec2.Distance(from, aim) - 18f) * 0.004f, 0.02f, 0.1f);
            KickBall(taker, from, startDir * power, vz, curl, topSpin, KickKind.Shot, null, aim, true, false);
            Ball.IsShot = true;
            Ball.ShotKnuckle = knuckle;
            if (knuckle) team.Stats.Knuckleballs++;
            Ball.Shooter = taker;
            Ball.ShotSpeed = power;
            var keeper = team.Opponent.Goalkeeper;
            Ball.ShotGoalkeeperDistance = keeper != null ? Vec2.Distance(keeper.Pos, aim) : 3f;
            Ball.ShotOnTarget = true;
            team.Stats.Shots++;
            team.Stats.FreeKickShots++;
            _lastShotTime = SimTime;
            _lastShotTeam = team;
            _lastShotXg = 0.1f;
            _shotCountedOnTarget = false;
            _lastShotKind = 3;
            team.Stats.ExpectedGoals += xg;
            taker.Shots++;
            Emit(MatchEventType.Shot, team, taker, null, from, xg, WhistleKind.Short, 3);
        }

        private void ExecutePenalty(PlayerAgent taker)
        {
            var team = taker.Team;
            var keeper = team.Opponent.Goalkeeper;
            ExecuteShot(taker, false, true, 0.76f);
            _penaltyKickTime = SimTime;
            // Predicted crossing of the goal line decides whether the keeper guessed correctly.
            var sim = Ball.M;
            float crossY = 0f;
            for (int i = 0; i < 200; i++)
            {
                BallPhysics.Step(ref sim, Config, Config.FixedStep);
                if (sim.X * team.AttackGoalSign >= Pitch.HalfLength) { crossY = sim.Y; break; }
            }
            _lastShotCrossing = new Vec2(Pitch.HalfLength * team.AttackGoalSign, crossY);
            float r = _rng.NextFloat();
            int guess = r < 0.44f ? -1 : (r < 0.88f ? 1 : 0);
            int actual = MathF.Abs(crossY) < 1.1f ? 0 : (crossY > 0f ? 1 : -1);
            _penaltyGuessCorrect = guess == actual;
            if (keeper != null)
            {
                _penaltyDiveTarget = new Vec2((Pitch.HalfLength - 0.4f) * team.AttackGoalSign, guess * 2.6f);
                _penaltyDiveUntil = SimTime + 0.8f;
                keeper.DiveTimer = guess != 0 ? 0.7f : 0f;
                Ball.ShotGoalkeeperDistance = Vec2.Distance(_penaltyDiveTarget, _lastShotCrossing);
            }
        }
    }
}

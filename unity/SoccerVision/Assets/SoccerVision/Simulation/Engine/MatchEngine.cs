using System;
using System.Collections.Generic;

namespace SoccerVision.Simulation
{
    public enum MatchPhase
    {
        /// <summary>Ball in play.</summary>
        Live,
        /// <summary>Ball out of play, a restart (kick-off, throw-in, free kick...) is being set up.</summary>
        Stoppage,
        GoalCelebration,
        HalfTimeBreak,
        FullTime,
    }

    public enum CelebrationKind
    {
        /// <summary>Scorer sprints to the corner flag; one or two team-mates follow him there.</summary>
        CornerRun,
        /// <summary>A group of the nearest team-mates piles onto the scorer and bounces together (rarer).</summary>
        Huddle,
        /// <summary>Scorer runs to the touchline in front of the fans; one team-mate joins him.</summary>
        FansRun,
        /// <summary>Scorer runs a solo lap around the box, arms out; the others applaud and jog back.</summary>
        Lap,
        /// <summary>Scorer knee-slides near the corner and two or three team-mates jump on him.</summary>
        KneeSlide,
        /// <summary>Scorer runs to the player who set him up (or the nearest team-mate) to embrace him.</summary>
        AssistHug,
    }

    public enum RestartType
    {
        None,
        KickOff,
        ThrowIn,
        GoalKick,
        CornerKick,
        DirectFreeKick,
        IndirectFreeKick,
        PenaltyKick,
    }

    public sealed class Restart
    {
        public RestartType Type;
        public TeamAgent Team;
        public Vec2 Spot;
        public PlayerAgent Taker;
        public float Timer;
        public float SetupTime;
        public bool BallPlaced;
        public bool CountsAsLostTime;
        /// <summary>The taker waits for the referee's whistle (kick-off, penalty, free kick with a wall or after a card).</summary>
        public bool WaitForWhistle;
        /// <summary>Restart timer value when the referee blew; the kick follows WhistleToKickRealSeconds later.</summary>
        public float WhistleAt = -1f;
        /// <summary>The taker walks back to RunUpStart first, then runs up to the ball (penalties, corners, free kicks, goal kicks).</summary>
        public bool HasRunUp;
        public Vec2 RunUpStart;
        public bool RunningUp;
        public float RunUpBegan;
        public readonly List<PlayerAgent> Wall = new List<PlayerAgent>();
    }

    /// <summary>
    /// Deterministic 2D football match simulation (11 v 11). Engine-independent: Unity renders it, tests and
    /// servers run it headless. Feed it real time with <see cref="Advance"/>; read players, ball, officials,
    /// clock and score; drain events for commentary, audio and UI.
    /// </summary>
    public sealed partial class MatchEngine
    {
        public readonly MatchConfig Config;
        public readonly ulong Seed;
        public readonly TeamAgent Home;
        public readonly TeamAgent Away;
        public readonly TeamAgent[] Teams;
        public readonly List<PlayerAgent> AllPlayers = new List<PlayerAgent>();
        public readonly BallState Ball = new BallState();
        public readonly MatchClock Clock = new MatchClock();
        public readonly Official Referee;
        public readonly Official Assistant1;
        public readonly Official Assistant2;
        public readonly Official[] Officials;
        public readonly GroundRollTable Roll;
        public readonly List<MatchEvent> EventLog = new List<MatchEvent>();
        /// <summary>Law checks that should never trigger (asserted by the test-suite).</summary>
        public int RuleViolations { get; private set; }
        /// <summary>Keeper distributions from the hands that an opponent blocked within 1.5 s, close to the keeper.</summary>
        public int KeeperChargeDowns { get; private set; }
        /// <summary>Goals conceded within 8 s of such a charge-down (regression guard for a corner bug).</summary>
        public int KeeperChargeDownGoals { get; private set; }
        /// <summary>Test hook: called with every restart right before the ball is put back into play.</summary>
        internal Action<Restart> RestartExecuting;

        /// <summary>Internal counters for calibration (decision mix, outcomes...). Off by default (no allocations).</summary>
        public readonly Dictionary<string, int> Diagnostics = new Dictionary<string, int>();
        public bool CollectDiagnostics;

        public MatchPhase Phase { get; private set; }
        public TeamAgent PossessionTeam { get; private set; }
        public Restart CurrentRestart => _restart;

        /// <summary>Sim seconds since the engine was created.</summary>
        public float SimTime { get; private set; }
        /// <summary>Real (viewer) seconds since the first kick-off whistle sequence started.</summary>
        public float RealTimeElapsed { get; private set; }
        public float HalftimeRemaining { get; private set; }
        public float FirstHalfRealSeconds { get; private set; }
        public float SecondHalfRealSeconds { get; private set; }
        public float HalftimeRealSeconds { get; private set; }
        public int StepCount { get; private set; }
        /// <summary>Number of times the engine had to repair an invalid state (should stay 0; tests assert it).</summary>
        public int RecoveredErrors { get; private set; }

        /// <summary>0..1 position between the previous and current physics step, for smooth rendering.</summary>
        public float InterpolationAlpha => MathUtil.Clamp01(_accumulator / Config.FixedStep);

        public bool IsFinished => Phase == MatchPhase.FullTime;

        private readonly Rng _rng;
        private readonly BallTrajectory _trajectory = new BallTrajectory();
        private readonly List<MatchEvent> _outbox = new List<MatchEvent>();
        private readonly List<IMatchCommand> _commands = new List<IMatchCommand>();
        private float _accumulator;
        private Restart _restart;
        private TeamAgent _firstHalfKickOffTeam;
        private float _phaseTimer;
        private float _periodRealStart;
        private float _addedTimeExpiredAt = -1f;
        private bool _trajectoryDirty = true;
        private float _ballChangedAt = -10f;
        private TeamAgent _ballChangeExpectedBy;
        private float _trajectoryAge;
        private float _tacticalTimer;
        private float _dutyTimer;
        private float _goalkeeperHoldTimer;
        private PlayerAgent _celebrationScorer;
        private PlayerAgent _celebrationAssist;
        private readonly List<PlayerAgent> _celebrationJoiners = new List<PlayerAgent>();
        /// <summary>Team-mates who join the scorer's celebration (the rest jog back for the kick-off).</summary>
        public IReadOnlyList<PlayerAgent> CelebrationJoiners => _celebrationJoiners;
        /// <summary>Style of the current goal celebration (chosen randomly per goal).</summary>
        public CelebrationKind Celebration { get; private set; }
        private TeamAgent _kickOffAfterGoal;
        private float _penaltyKickTime = -1f;
        private bool _offsideActive;
        private TeamAgent _offsideTeam;
        private readonly bool[] _offsideFlags;
        private PendingSanction _advantageSanction;
        private float _advantageTimer;

        public MatchEngine(TeamSetup home, TeamSetup away, ulong seed, MatchConfig config = null)
        {
            Config = (config ?? MatchConfig.CreateDefault()).Clone();
            Config.Validate();
            if (home == null) throw new ArgumentNullException(nameof(home));
            if (away == null) throw new ArgumentNullException(nameof(away));
            home.Validate();
            away.Validate();
            Seed = seed;
            _rng = new Rng(seed);
            Roll = new GroundRollTable(Config);
            Clock.RegulationHalfSeconds = Config.RegulationHalfClockSeconds;

            Home = new TeamAgent(0, home, +1);
            Away = new TeamAgent(1, away, -1);
            Home.Opponent = Away;
            Away.Opponent = Home;
            Teams = new[] { Home, Away };

            int index = 0;
            foreach (var team in Teams)
            {
                for (int i = 0; i < 11; i++)
                {
                    var agent = new PlayerAgent(index++, team.Setup.Starters[i], team, i);
                    agent.NoisePhaseA = _rng.Range(0f, MathUtil.TwoPi);
                    agent.NoisePhaseB = _rng.Range(0f, MathUtil.TwoPi);
                    agent.RunCooldown = _rng.Range(2f, 8f);
                    team.Players.Add(agent);
                    AllPlayers.Add(agent);
                }
                foreach (var benchProfile in team.Setup.Bench)
                {
                    team.Bench.Add(new PlayerAgent(-1, benchProfile, team, 0));
                }
            }
            _offsideFlags = new bool[AllPlayers.Count];

            Referee = new Official { Pos = new Vec2(-6f, 8f) };
            Assistant1 = new Official { IsAssistant = true, TouchlineSign = -1, CoverSign = +1, Pos = new Vec2(10f, -Pitch.HalfWidth - 1f) };
            Assistant2 = new Official { IsAssistant = true, TouchlineSign = +1, CoverSign = -1, Pos = new Vec2(-10f, Pitch.HalfWidth + 1f) };
            Officials = new[] { Referee, Assistant1, Assistant2 };

            _firstHalfKickOffTeam = _rng.Chance(0.5f) ? Home : Away;
            Clock.Period = MatchPeriod.FirstHalf;
            Clock.Seconds = 0f;
            Clock.Running = false;
            PossessionTeam = _firstHalfKickOffTeam;
            _periodRealStart = 0f;

            TeleportForKickOff(_firstHalfKickOffTeam);
            BeginRestart(RestartType.KickOff, _firstHalfKickOffTeam, Vec2.Zero, null);
            SavePrevious();
        }

        // ------------------------------------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------------------------------------

        /// <summary>Advances the match by real (viewer) seconds.</summary>
        public void Advance(float realDeltaSeconds)
        {
            if (IsFinished || !(realDeltaSeconds > 0f)) return;
            float realDt = MathF.Min(realDeltaSeconds, 30f);
            float step = Config.FixedStep;
            int guard = 0;
            while (realDt > 1e-7f && !IsFinished && guard++ < 1000000)
            {
                if (Phase == MatchPhase.HalfTimeBreak)
                {
                    float used = MathF.Min(realDt, HalftimeRemaining);
                    HalftimeRemaining -= used;
                    HalftimeRealSeconds += used;
                    RealTimeElapsed += used;
                    realDt -= used;
                    if (HalftimeRemaining <= 1e-6f) StartSecondHalf();
                    continue;
                }

                _accumulator += realDt * Config.MotionSpeed;
                realDt = 0f;
                while (_accumulator >= step && Phase != MatchPhase.HalfTimeBreak && !IsFinished)
                {
                    ProcessCommands();
                    Step(step);
                    _accumulator -= step;
                    RealTimeElapsed += step / Config.MotionSpeed;
                }
                if (Phase == MatchPhase.HalfTimeBreak && _accumulator > 0f)
                {
                    realDt = _accumulator / Config.MotionSpeed;
                    _accumulator = 0f;
                }
            }
        }

        /// <summary>Runs the whole match headless (tests, servers, instant results).</summary>
        public void RunToEnd(float realStep = 0.25f, int maxIterations = 10000000)
        {
            int i = 0;
            while (!IsFinished && i++ < maxIterations) Advance(realStep);
        }

        /// <summary>Returns and clears the events produced since the last call.</summary>
        public List<MatchEvent> DrainEvents()
        {
            var list = new List<MatchEvent>(_outbox);
            _outbox.Clear();
            return list;
        }

        public void DrainEvents(List<MatchEvent> into)
        {
            into.AddRange(_outbox);
            _outbox.Clear();
        }

        /// <summary>Queues a command (tactics changes, later substitutions) applied before the next step.</summary>
        public void Submit(IMatchCommand command)
        {
            if (command != null) _commands.Add(command);
        }

        public TeamAgent TeamOf(int index) => index == 0 ? Home : Away;

        public PlayerAgent PlayerByIndex(int index) => index >= 0 && index < AllPlayers.Count ? AllPlayers[index] : null;

        /// <summary>Seconds of the half-time break / real half length as configured (for UIs).</summary>
        public float RealSecondsPerClockSecond => 1f / Config.ClockSecondsPerRealSecond;

        // ------------------------------------------------------------------------------------------------
        // Main step
        // ------------------------------------------------------------------------------------------------

        private void Step(float dt)
        {
            StepCount++;
            SimTime += dt;
            SavePrevious();

            if (Clock.Running)
            {
                float clockDt = dt * Config.ClockSecondsPerSimSecond;
                Clock.Seconds += clockDt;
                if (_restart != null && _restart.CountsAsLostTime) Clock.LostSeconds += clockDt;
                if (Phase == MatchPhase.GoalCelebration) Clock.LostSeconds += clockDt;
            }

            UpdateTimers(dt);

            switch (Phase)
            {
                case MatchPhase.Live:
                    StepLive(dt);
                    break;
                case MatchPhase.Stoppage:
                    StepStoppage(dt);
                    break;
                case MatchPhase.GoalCelebration:
                    StepCelebration(dt);
                    break;
            }

            TrackKeeperRelease();
            UpdateOfficials(dt);
            CheckAddedTime();
            ValidateState();
        }

        private PlayerAgent _keeperRelease;
        private float _keeperReleaseTime;
        private Vec2 _keeperReleasePos;
        private TeamAgent _chargeDownTeam;
        private float _chargeDownTime = -100f;

        private void NoteKeeperRelease(PlayerAgent gk)
        {
            _keeperRelease = gk;
            _keeperReleaseTime = SimTime;
            _keeperReleasePos = gk.Pos;
        }

        private void TrackKeeperRelease()
        {
            if (_keeperRelease == null) return;
            if (SimTime - _keeperReleaseTime > 1.5f || Phase != MatchPhase.Live)
            {
                _keeperRelease = null;
                return;
            }
            var t = Ball.LastToucher;
            if (t != null && t.Team != _keeperRelease.Team && Vec2.Distance(Ball.Pos, _keeperReleasePos) < 6f)
            {
                KeeperChargeDowns++;
                _chargeDownTeam = _keeperRelease.Team;
                _chargeDownTime = SimTime;
                _keeperRelease = null;
            }
        }

        private void SavePrevious()
        {
            foreach (var p in AllPlayers)
            {
                p.PrevPos = p.Pos;
                p.PrevFacing = p.Facing;
                p.PrevJumpZ = p.JumpZ;
            }
            Ball.PrevPos = Ball.Pos;
            Ball.PrevZ = Ball.Z;
            foreach (var o in Officials) o.PrevPos = o.Pos;
        }

        private void UpdateTimers(float dt)
        {
            foreach (var p in AllPlayers)
            {
                if (p.TouchCooldown > 0f) p.TouchCooldown -= dt;
                if (p.TackleCooldown > 0f) p.TackleCooldown -= dt;
                if (p.DuelCooldown > 0f) p.DuelCooldown -= dt;
                if (p.StunTimer > 0f) p.StunTimer -= dt;
                if (p.KickCooldown > 0f) p.KickCooldown -= dt;
                if (p.RunTimer > 0f) p.RunTimer -= dt;
                if (p.RunCooldown > 0f) p.RunCooldown -= dt;
                if (p.DiveTimer > 0f) p.DiveTimer -= dt;
                if (p.InterceptRecalc > 0f) p.InterceptRecalc -= dt;
                if (p.OffsideMarkTimer > 0f) p.OffsideMarkTimer -= dt;
                if (p.SlideTimer > 0f)
                {
                    p.SlideTimer -= dt;
                    if (p.SlideTimer <= 0f) EndSlide(p);
                }
                else if (p.GroundTimer > 0f) p.GroundTimer -= dt;
            }
            foreach (var o in Officials) if (o.FlagTimer > 0f) o.FlagTimer -= dt;
            if (_advantageTimer > 0f)
            {
                _advantageTimer -= dt;
                if (_advantageTimer <= 0f && _advantageSanction != null)
                {
                    // Advantage played out: the referee shows the card at the next stoppage, which we
                    // approximate by showing it now without stopping play.
                    ApplySanction(_advantageSanction);
                    _advantageSanction = null;
                }
            }
        }

        private void StepLive(float dt)
        {
            if (Ball.Owner == null)
            {
                _trajectoryAge += dt;
                if (_trajectoryDirty || _trajectoryAge > 0.9f) RebuildTrajectory();
            }

            _tacticalTimer -= dt;
            if (_tacticalTimer <= 0f)
            {
                _tacticalTimer = 0.2f;
                UpdateTeamShape(Home);
                UpdateTeamShape(Away);
            }
            _dutyTimer -= dt;
            if (_dutyTimer <= 0f || _trajectoryDirty)
            {
                _dutyTimer = 0.1f;
                AssignBallDuties();
            }
            AnticipateHeaders();

            // Think: may kick the ball (the carrier releases it immediately).
            for (int i = 0; i < AllPlayers.Count; i++)
            {
                var p = AllPlayers[i];
                if (p.SentOff) continue;
                ThinkLive(p, dt);
                if (Phase != MatchPhase.Live) return;
            }

            for (int i = 0; i < AllPlayers.Count; i++) MovePlayer(AllPlayers[i], dt);

            UpdateBall(dt);
            if (Phase != MatchPhase.Live) return;

            UpdateSlides();
            if (Phase != MatchPhase.Live) return;
            ResolveShoulderDuels();
            if (Phase != MatchPhase.Live) return;
            ResolveTouches();
            if (Phase != MatchPhase.Live) return;
            ResolveTackles(dt);
            if (Phase != MatchPhase.Live) return;
            CheckBoundaries();
            if (Phase != MatchPhase.Live) return;

            if (Ball.Owner != null && Ball.InHands)
            {
                _goalkeeperHoldTimer += dt;
                if (_goalkeeperHoldTimer > Config.GoalkeeperHoldLimitSeconds) GoalkeeperHoldViolation(Ball.Owner);
            }
            if (Phase != MatchPhase.Live) return;

            if (PossessionTeam != null) PossessionTeam.Stats.PossessionSeconds += dt;
            if (_penaltyKickTime >= 0f && SimTime - _penaltyKickTime > 2.5f) _penaltyKickTime = -1f;

            CheckEndOfHalfLive();
        }

        private void RebuildTrajectory()
        {
            _trajectory.Build(Ball.M, Config);
            _trajectoryDirty = false;
            _trajectoryAge = 0f;
        }

        private void MarkBallChanged()
        {
            _trajectoryDirty = true;
        }

        private void UpdateBall(float dt)
        {
            if (Ball.Owner != null)
            {
                AttachBallToOwner(Ball.Owner);
                return;
            }

            BallPhysics.Step(ref Ball.M, Config, dt);
            CollideGoalFrames();
        }

        private void AttachBallToOwner(PlayerAgent owner)
        {
            if (Ball.InHands)
            {
                Vec2 p = owner.Pos + owner.FacingDir * 0.35f;
                Ball.M = new BallMotion { X = p.X, Y = p.Y, Z = 1.05f };
                return;
            }
            float speed = owner.Vel.Length;
            // Close control: the ball sits just ahead of the feet and is pushed further when running.
            float offset = 0.45f + MathUtil.Clamp(speed * 0.06f, 0f, 0.45f);
            float pulse = speed > 1.5f ? 0.12f * MathF.Sin(SimTime * 9f + owner.Index) : 0f;
            Vec2 pos = owner.Pos + owner.FacingDir * (offset + pulse);
            Ball.M = new BallMotion { X = pos.X, Y = pos.Y, Z = Pitch.BallRadius, Vx = owner.Vel.X, Vy = owner.Vel.Y };
        }

        /// <summary>Posts, crossbar and net. Posts are vertical cylinders, the crossbar a horizontal one.</summary>
        private void CollideGoalFrames()
        {
            ref BallMotion b = ref Ball.M;
            const float r = Pitch.BallRadius;
            for (int side = -1; side <= 1; side += 2)
            {
                float lineX = Pitch.HalfLength * side;
                float postX = lineX + Pitch.PostRadius * side;
                // Near the frame or inside the net (which is GoalDepth deep): a fast shot must not pass through the back.
                if ((b.X - lineX) * side < -1.5f || (b.X - lineX) * side > Pitch.GoalDepth + 1.5f) continue;

                // Posts
                if (b.Z < Pitch.GoalHeight + Pitch.PostRadius * 2f)
                {
                    for (int py = -1; py <= 1; py += 2)
                    {
                        var post = new Vec2(postX, (Pitch.GoalHalfWidth + Pitch.PostRadius) * py);
                        Vec2 d = b.Position2D - post;
                        float dist = d.Length;
                        float min = r + Pitch.PostRadius;
                        if (dist < min && dist > 1e-5f)
                        {
                            Vec2 n = d / dist;
                            Vec2 v = b.Velocity2D;
                            float vn = Vec2.Dot(v, n);
                            if (vn < 0f)
                            {
                                Vec2 reflected = v - n * ((1f + Config.PostRestitution) * vn);
                                b.Vx = reflected.X;
                                b.Vy = reflected.Y;
                                Vec2 fixedPos = post + n * min;
                                b.X = fixedPos.X;
                                b.Y = fixedPos.Y;
                                OnWoodwork(side, false);
                            }
                        }
                    }
                }

                // Crossbar (cylinder along Y at height GoalHeight + PostRadius)
                if (MathF.Abs(b.Y) <= Pitch.GoalHalfWidth + Pitch.PostRadius)
                {
                    float dx = b.X - postX;
                    float dz = b.Z - (Pitch.GoalHeight + Pitch.PostRadius);
                    float dist = MathF.Sqrt(dx * dx + dz * dz);
                    float min = r + Pitch.PostRadius;
                    if (dist < min && dist > 1e-5f)
                    {
                        float nx = dx / dist, nz = dz / dist;
                        float vn = b.Vx * nx + b.Vz * nz;
                        if (vn < 0f)
                        {
                            b.Vx -= (1f + Config.PostRestitution) * vn * nx;
                            b.Vz -= (1f + Config.PostRestitution) * vn * nz;
                            b.X = postX + nx * min;
                            b.Z = MathF.Max(r, Pitch.GoalHeight + Pitch.PostRadius + nz * min);
                            OnWoodwork(side, true);
                        }
                    }
                }

                // Net: the ball is inside the goal frame.
                if (Ball.InGoal || (b.X * side > Pitch.HalfLength + r && MathF.Abs(b.Y) < Pitch.GoalHalfWidth && b.Z < Pitch.GoalHeight))
                {
                    float back = Pitch.HalfLength + Pitch.GoalDepth - r;
                    float impactSpeed = b.Speed;
                    bool hit = false;
                    if (b.X * side > back) { b.X = back * side; b.Vx = -b.Vx * Config.NetRestitution; b.Vy *= 0.5f; b.Vz *= 0.5f; hit = true; }
                    float sideNet = Pitch.GoalHalfWidth - r;
                    if (b.X * side > Pitch.HalfLength && MathF.Abs(b.Y) > sideNet) { b.Y = sideNet * MathF.Sign(b.Y); b.Vy = -b.Vy * Config.NetRestitution; b.Vx *= 0.5f; hit = true; }
                    if (b.X * side > Pitch.HalfLength && b.Z > Pitch.GoalHeight - r) { b.Z = Pitch.GoalHeight - r; if (b.Vz > 0f) b.Vz = -b.Vz * Config.NetRestitution; hit = true; }
                    if (hit && Ball.InGoal && b.Speed > 1.5f) Emit(MatchEventType.NetHit, null, null, null, b.Position2D, impactSpeed);
                }
            }
        }

        private float _lastWoodworkTime = -10f;

        private void OnWoodwork(int side, bool crossbar)
        {
            MarkBallChanged();
            if (SimTime - _lastWoodworkTime < 0.3f) return;
            _lastWoodworkTime = SimTime;
            if (Phase == MatchPhase.Live)
            {
                if (Ball.LastTouchTeam != null) Ball.LastTouchTeam.Stats.WoodworkHits++;
                Emit(MatchEventType.HitWoodwork, Ball.LastTouchTeam, Ball.LastToucher, null, Ball.Pos, Ball.M.Speed, WhistleKind.Short, crossbar ? 2 : 1);
                // A rebound off the woodwork is not a new play: the shooter must wait at penalties only.
                if (_penaltyKickTime >= 0f) Ball.KickerMustWait = true;
            }
        }

        // ------------------------------------------------------------------------------------------------
        // Half time / full time
        // ------------------------------------------------------------------------------------------------

        private void CheckAddedTime()
        {
            if (Clock.AddedMinutes >= 0 || !Clock.Running) return;
            if (Clock.Seconds < Clock.PeriodRegulationEnd) return;
            int min = Clock.Period == MatchPeriod.FirstHalf ? Config.MinAddedMinutesFirstHalf : Config.MinAddedMinutesSecondHalf;
            // Goals, cards and penalties are measured; other stoppages (treatment, time-wasting) are estimated.
            float other = Clock.Period == MatchPeriod.SecondHalf ? _rng.Range(30f, 110f) : _rng.Range(0f, 50f);
            int fromLost = (int)MathF.Ceiling((Clock.LostSeconds + other) / 60f - 0.15f);
            Clock.AddedMinutes = MathUtil.Clamp(Math.Max(min, fromLost), 0, Config.MaxAddedMinutes);
            Emit(MatchEventType.AddedTime, null, null, null, Vec2.Zero, Clock.AddedMinutes);
        }

        private bool AddedTimeExpired => Clock.AddedMinutes >= 0 && Clock.Seconds >= Clock.AddedTimeEnd;

        private void CheckEndOfHalfLive()
        {
            if (!AddedTimeExpired) return;
            if (_addedTimeExpiredAt < 0f) _addedTimeExpiredAt = Clock.Seconds;
            if (_penaltyKickTime >= 0f) return; // Law 7: the half is extended until a penalty is completed
            float overrun = Clock.Seconds - _addedTimeExpiredAt;
            bool neutral = MathF.Abs(Ball.Pos.X) < 20f;
            if (!neutral && Ball.Owner != null)
            {
                // The team in possession inside its own half is not attacking: safe to blow.
                neutral = Ball.Owner.Team.Forward(Ball.Pos) < 0f;
            }
            if (neutral || overrun >= Config.EndOfHalfGraceClockSeconds) EndHalf();
        }

        private void EndHalf()
        {
            _addedTimeExpiredAt = -1f;
            _restart = null;
            _advantageSanction = null;
            Clock.Running = false;
            Ball.Owner = null;
            Ball.InHands = false;
            Ball.ClearFlight();
            Ball.Stop();
            ClearOffside();

            if (Clock.Period == MatchPeriod.FirstHalf)
            {
                FirstHalfRealSeconds = RealTimeElapsed - _periodRealStart;
                Emit(MatchEventType.Whistle, null, null, null, Ball.Pos, 0f, WhistleKind.HalfTime);
                Clock.Period = MatchPeriod.HalfTime;
                Emit(MatchEventType.HalfTime);
                Phase = MatchPhase.HalfTimeBreak;
                HalftimeRemaining = Config.HalftimeRealSeconds;
                if (HalftimeRemaining <= 0f) StartSecondHalf();
            }
            else
            {
                SecondHalfRealSeconds = RealTimeElapsed - _periodRealStart;
                Emit(MatchEventType.Whistle, null, null, null, Ball.Pos, 0f, WhistleKind.FullTime);
                Clock.Period = MatchPeriod.FullTime;
                Phase = MatchPhase.FullTime;
                foreach (var t in Teams) FinaliseStats(t);
                Emit(MatchEventType.FullTime);
            }
        }

        private void StartSecondHalf()
        {
            HalftimeRemaining = 0f;
            foreach (var t in Teams)
            {
                t.AttackDir = -t.AttackDir;
                t.LineForward = -33f;
                foreach (var p in t.Players)
                {
                    // Half-time recovery.
                    p.Energy = MathUtil.Clamp01(p.Energy + 0.07f + 0.05f * MathUtil.A(p.Attr.Stamina));
                    p.Vel = Vec2.Zero;
                    p.StunTimer = 0f;
                    p.SlideTimer = 0f;
                    p.GroundTimer = 0f;
                }
            }
            Clock.Period = MatchPeriod.SecondHalf;
            Clock.Seconds = Clock.RegulationHalfSeconds;
            Clock.AddedMinutes = -1;
            Clock.LostSeconds = 0f;
            Clock.Running = false;
            _periodRealStart = RealTimeElapsed;
            var kickOffTeam = _firstHalfKickOffTeam.Opponent;
            PossessionTeam = kickOffTeam;
            Phase = MatchPhase.Stoppage;
            TeleportForKickOff(kickOffTeam);
            BeginRestart(RestartType.KickOff, kickOffTeam, Vec2.Zero, null);
            Emit(MatchEventType.SecondHalfStart);
            SavePrevious();
        }

        private void FinaliseStats(TeamAgent t)
        {
            float metres = 0f;
            foreach (var p in t.Players) metres += p.DistanceCovered;
            // Sim distance is compressed time; scale to the represented 90 minutes.
            t.Stats.DistanceCoveredKm = metres * Config.ClockSecondsPerSimSecond / 1000f;
        }

        // ------------------------------------------------------------------------------------------------
        // Events
        // ------------------------------------------------------------------------------------------------

        private MatchEvent Emit(MatchEventType type, TeamAgent team = null, PlayerAgent player = null, PlayerAgent other = null,
                                Vec2 position = default, float value = 0f, WhistleKind whistle = WhistleKind.Short, int detail = 0)
        {
            var e = new MatchEvent
            {
                Type = type,
                Period = Clock.Period,
                ClockSeconds = Clock.Seconds,
                Minute = Clock.MinuteText,
                Team = team != null ? team.Index : (player != null ? player.Team.Index : -1),
                Player = player != null ? player.Index : -1,
                OtherPlayer = other != null ? other.Index : -1,
                X = position.X,
                Y = position.Y,
                Value = value,
                Whistle = whistle,
                Detail = detail,
            };
            e.Text = Commentary.Describe(e, this);
            EventLog.Add(e);
            _outbox.Add(e);
            return e;
        }

        private void Whistle(WhistleKind kind) => Emit(MatchEventType.Whistle, null, null, null, Ball.Pos, 0f, kind);

        // ------------------------------------------------------------------------------------------------
        // Commands
        // ------------------------------------------------------------------------------------------------

        private void ProcessCommands()
        {
            if (_commands.Count == 0) return;
            var list = new List<IMatchCommand>(_commands);
            _commands.Clear();
            foreach (var c in list) c.Apply(this);
        }

        internal void ApplyTactics(int teamIndex, TeamTactics tactics)
        {
            var team = TeamOf(teamIndex);
            var copy = tactics.Clone();
            copy.Sanitize();
            team.Tactics = copy;
        }

        // ------------------------------------------------------------------------------------------------
        // Safety net
        // ------------------------------------------------------------------------------------------------

        private void ValidateState()
        {
            bool bad = !(Ball.Pos.IsFinite && !float.IsNaN(Ball.Z) && !float.IsNaN(Ball.M.Vx) && !float.IsNaN(Ball.M.Vy) && !float.IsNaN(Ball.M.Vz));
            if (bad)
            {
                RecoveredErrors++;
                Ball.Owner = null;
                Ball.InHands = false;
                Ball.Place(Vec2.Zero);
                MarkBallChanged();
            }
            foreach (var p in AllPlayers)
            {
                if (!p.Pos.IsFinite || !p.Vel.IsFinite || float.IsNaN(p.Facing))
                {
                    RecoveredErrors++;
                    p.Pos = p.Team.ToWorld(p.Slot.BaseForward, p.Slot.BaseLateral);
                    p.Vel = Vec2.Zero;
                    p.Facing = 0f;
                }
            }
            if (Ball.Owner != null && Ball.Owner.SentOff)
            {
                RecoveredErrors++;
                Ball.Owner = null;
                Ball.InHands = false;
            }
        }

        internal Rng Random => _rng;

        private void Violation(string what)
        {
            RuleViolations++;
            Count("violation." + what);
        }

        internal void Count(string key)
        {
            Diagnostics.TryGetValue(key, out int v);
            Diagnostics[key] = v + 1;
        }
    }

    /// <summary>A queued instruction from the manager UI (tactics now; substitutions next).</summary>
    public interface IMatchCommand
    {
        void Apply(MatchEngine engine);
    }

    public sealed class ChangeTacticsCommand : IMatchCommand
    {
        public int Team;
        public TeamTactics Tactics;

        public ChangeTacticsCommand(int team, TeamTactics tactics)
        {
            Team = team;
            Tactics = tactics;
        }

        public void Apply(MatchEngine engine)
        {
            if (Tactics != null) engine.ApplyTactics(Team, Tactics);
        }
    }
}

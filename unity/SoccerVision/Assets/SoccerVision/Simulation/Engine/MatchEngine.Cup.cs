using System;
using System.Collections.Generic;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// Cup ties. A cup tie that is level after 90 minutes goes straight to penalties; a league cup tie plays extra time first
    /// (two periods of 15 minutes, ends changed in between - Law 7), in which each team may make one more substitution and has
    /// one more opportunity to make it (IFAB Law 3), and then penalties. The shoot-out (Law 10): five kicks each, taken in
    /// turn at one goal, the keepers in goal for the other side; a team that can no longer be caught has won; then one kick
    /// each until one scores and the other does not. No player kicks twice before all of his team-mates have kicked.
    /// </summary>
    public sealed partial class MatchEngine
    {
        private Action _afterBreak;

        /// <summary>Penalties scored in the shoot-out (home, away); -1 while there has been none.</summary>
        public int[] ShootoutScore = { -1, -1 };
        /// <summary>The winner of the shoot-out (null: none).</summary>
        public TeamAgent ShootoutWinner;
        /// <summary>The kicks of the shoot-out so far: team index and scored, in order.</summary>
        public readonly List<(int team, bool scored)> ShootoutKicks = new List<(int, bool)>();

        /// <summary>True from the break before extra time: one more substitution and opportunity (league cup).</summary>
        public bool ExtraTimeReached { get; private set; }

        private void BeginBreak(float realSeconds, Action after)
        {
            Clock.Running = false;
            Clock.Period = MatchPeriod.ExtraTimeBreak;
            ExtraTimeReached = true;
            Phase = MatchPhase.HalfTimeBreak;
            HalftimeRemaining = realSeconds;
            _afterBreak = after;
            if (realSeconds <= 0f) EndBreak();
        }

        private void EndBreak()
        {
            var after = _afterBreak;
            _afterBreak = null;
            if (after != null) after();
            else StartSecondHalf();
        }

        private void StartExtraTime(int period)
        {
            HalftimeRemaining = 0f;
            foreach (var t in Teams)
            {
                if (period == 2) t.AttackDir = -t.AttackDir; // ends are changed between the two periods
                t.LineForward = -33f;
                foreach (var p in t.Players)
                {
                    p.Energy = MathUtil.Clamp01(p.Energy + 0.02f + 0.02f * MathUtil.A(p.Attr.Stamina)); // (a short rest)
                    p.Vel = Vec2.Zero;
                    p.StunTimer = 0f;
                    p.SlideTimer = 0f;
                    p.GroundTimer = 0f;
                }
            }
            foreach (var p in AllPlayers)
                if (!p.SentOff && p.Injury == InjuryLevel.Serious) CompleteInjuryChange(p, false);
            Clock.Period = period == 1 ? MatchPeriod.ExtraTimeFirstHalf : MatchPeriod.ExtraTimeSecondHalf;
            Clock.Seconds = Clock.PeriodStartSeconds;
            Clock.AddedMinutes = -1;
            Clock.LostSeconds = 0f;
            Clock.Running = false;
            _periodRealStart = RealTimeElapsed;
            // a new toss for the first period; the other team kicks off the second
            var kickOffTeam = period == 1 ? (_rng.Chance(0.5f) ? Home : Away) : _extraTimeKickOff.Opponent;
            _extraTimeKickOff = kickOffTeam;
            PossessionTeam = kickOffTeam;
            Phase = MatchPhase.Stoppage;
            TeleportForKickOff(kickOffTeam);
            BeginRestart(RestartType.KickOff, kickOffTeam, Vec2.Zero, null);
            Emit(MatchEventType.ExtraTime, null, null, null, Vec2.Zero, 0f, WhistleKind.Short, period);
            SavePrevious();
        }

        private TeamAgent _extraTimeKickOff;

        // ---- the shoot-out ----------------------------------------------------------------------------------------------

        private enum ShootoutStep { Walk, RunUp, Flight, Done }
        private ShootoutStep _soStep;
        private float _soTimer, _soSign;
        private TeamAgent _soKicking, _soFirst;
        private PlayerAgent _soTaker, _soKeeper;
        private readonly List<PlayerAgent>[] _soOrder = { new List<PlayerAgent>(), new List<PlayerAgent>() };
        private readonly int[] _soTaken = new int[2];

        private void BeginShootout()
        {
            Clock.Running = false;
            Clock.Period = MatchPeriod.PenaltyShootout;
            Phase = MatchPhase.PenaltyShootout;
            _restart = null;
            ReleaseDeadBallIfAny();
            ShootoutScore[0] = ShootoutScore[1] = 0;
            _soSign = _rng.Chance(0.5f) ? 1f : -1f; // the referee picks the goal
            _soFirst = _rng.Chance(0.5f) ? Home : Away; // and the toss decides who kicks first
            foreach (var t in Teams) BuildShootoutOrder(t);
            Emit(MatchEventType.PenaltyShootout, _soFirst, null, null, new Vec2((Pitch.HalfLength - 11f) * _soSign, 0f));
            NextShootoutKick(_soFirst);
        }

        private void ReleaseDeadBallIfAny() { Ball.Owner = null; Ball.InHands = false; Ball.ClearFlight(); Ball.Stop(); }

        /// <summary>The order of the takers: the manager's penalty takers first, then the best of the rest, the keeper last.</summary>
        private void BuildShootoutOrder(TeamAgent t)
        {
            var order = _soOrder[t.Index];
            order.Clear();
            var pool = new List<PlayerAgent>();
            foreach (var p in t.Players) if (!p.SentOff && p.Injury != InjuryLevel.Serious) pool.Add(p);
            foreach (int n in t.Tactics.PenaltyTakers)
            {
                var p = pool.Find(q => q.Number == n && !q.IsGoalkeeper);
                if (p != null) { order.Add(p); pool.Remove(p); }
            }
            pool.Sort((a, b) => ShootoutSkill(b).CompareTo(ShootoutSkill(a)));
            foreach (var p in pool) if (!p.IsGoalkeeper) order.Add(p);
            foreach (var p in pool) if (p.IsGoalkeeper) order.Add(p);
        }

        private static float ShootoutSkill(PlayerAgent p) => p.Attr.Penalty * 2f + p.Attr.Composure + p.Attr.Finishing * 0.5f;

        private void NextShootoutKick(TeamAgent team)
        {
            _soKicking = team;
            var order = _soOrder[team.Index];
            _soTaker = order.Count == 0 ? null : order[_soTaken[team.Index] % order.Count];
            _soKeeper = team.Opponent.Goalkeeper;
            // both teams at the same goal: the kicking team attacks it, the other defends it
            team.AttackDir = (int)_soSign;
            team.Opponent.AttackDir = -(int)_soSign;
            Vec2 spot = new Vec2((Pitch.HalfLength - 11f) * _soSign, 0f);
            Ball.Place(spot);
            Ball.LastToucher = null;
            Ball.LastTouchTeam = null;
            // everyone else in the centre circle, the keeper of the kicking team at the edge of the area
            int k = 0;
            foreach (var p in AllPlayers)
            {
                if (p.SentOff) continue;
                p.Vel = Vec2.Zero; p.StunTimer = 0f; p.GroundTimer = 0f; p.SlideTimer = 0f; p.DiveTimer = 0f; p.TouchCooldown = 0f;
                if (p == _soTaker || p == _soKeeper) continue;
                if (p.IsGoalkeeper) { p.Target = new Vec2((Pitch.HalfLength - 16.5f) * _soSign, 11f); continue; } // (the other keeper waits at the edge of the area, close to the goal he defends next)
                // (each team in a line across the centre circle, arm in arm)
                int i = p.Team.Players.IndexOf(p);
                p.Target = new Vec2(-5.5f + i * 1.1f, p.Team == Home ? 1.6f : -1.6f);
                k++;
            }
            if (_soKeeper != null) _soKeeper.Target = new Vec2(Pitch.HalfLength * _soSign - 0.3f * _soSign, 0f);
            if (_soTaker != null) _soTaker.Target = spot - new Vec2(2.2f * _soSign, 0.6f);
            _soStep = ShootoutStep.Walk;
            _soTimer = 0f;
        }

        private void StepShootout(float dt)
        {
            _soTimer += dt;
            switch (_soStep)
            {
                case ShootoutStep.Walk:
                {
                    // the taker walks from the centre circle, the keeper takes his line
                    bool there = true;
                    foreach (var p in AllPlayers)
                    {
                        if (p.SentOff) continue;
                        bool arrived = MoveTowards(ref p.Pos, ref p.Vel, p.Target, p == _soTaker ? 3.2f : p == _soKeeper ? 4.2f : 2.4f, dt);
                        if (p == _soTaker || p == _soKeeper) there &= arrived;
                        p.Facing = p.Vel.LengthSq > 0.04f ? p.Vel.Angle : (p == _soKeeper ? (_soSign > 0 ? MathUtil.Pi : 0f) : (Ball.Pos - p.Pos).Angle);
                    }
                    if ((there && _soTimer > 2.5f) || _soTimer > 14f)
                    {
                        if (_soKeeper != null && !_soKeeper.SentOff) { _soKeeper.Pos = _soKeeper.Target; _soKeeper.Vel = Vec2.Zero; } // (exactly in the middle of his line)
                        _soStep = ShootoutStep.RunUp;
                        _soTimer = 0f;
                        Whistle(WhistleKind.Short);
                    }
                    break;
                }
                case ShootoutStep.RunUp:
                {
                    if (_soTaker == null) { ResolveShootoutKick(false); break; }
                    if (_soTimer < 0.8f) break; // (a breath after the whistle)
                    Vec2 behind = Ball.Pos - new Vec2(0.35f * _soSign, 0f);
                    bool at = MoveTowards(ref _soTaker.Pos, ref _soTaker.Vel, behind, 4.2f, dt);
                    _soTaker.Facing = new Vec2(_soSign, 0f).Angle;
                    if (at)
                    {
                        _penaltyKickTime = SimTime;
                        ExecuteShot(_soTaker, false, true, 0.76f);
                        _soTaker.TouchCooldown = 10f; // (no rebounds in a shoot-out)
                        _soStep = ShootoutStep.Flight;
                        _soTimer = 0f;
                    }
                    break;
                }
                case ShootoutStep.Flight:
                {
                    if (_soKeeper != null && !_soKeeper.SentOff)
                    {
                        ThinkLive(_soKeeper, dt);
                        MovePlayer(_soKeeper, dt);
                    }
                    UpdateBall(dt);
                    ResolveTouches();
                    var b = Ball.M;
                    if (Ball.Owner != null) { ResolveShootoutKick(false); break; } // held by the keeper
                    if (MathF.Abs(b.X) > Pitch.HalfLength + Pitch.BallRadius)
                    {
                        bool inMouth = MathF.Abs(b.Y) < Pitch.GoalHalfWidth && b.Z < Pitch.GoalHeight && MathF.Sign(b.X) == _soSign;
                        ResolveShootoutKick(inMouth);
                        break;
                    }
                    if (MathF.Abs(b.Y) > Pitch.HalfWidth || (_soTimer > 1.2f && Ball.Velocity.Length < 1.5f && b.Z < 0.3f) || _soTimer > 5f)
                        ResolveShootoutKick(false);
                    break;
                }
                case ShootoutStep.Done:
                    if (_soTimer > 2.2f) AfterShootoutKick();
                    break;
            }
        }

        private void ResolveShootoutKick(bool scored)
        {
            var team = _soKicking;
            _soTaken[team.Index]++;
            if (scored) ShootoutScore[team.Index]++;
            ShootoutKicks.Add((team.Index, scored));
            _penaltyKickTime = -1f;
            var e = Emit(MatchEventType.ShootoutKick, team, _soTaker, _soKeeper, Ball.Pos, scored ? 1f : 0f);
            e.Info = ShootoutScore[0] + "-" + ShootoutScore[1];
            _soStep = ShootoutStep.Done;
            _soTimer = 0f;
        }

        private void AfterShootoutKick()
        {
            int h = ShootoutScore[0], a = ShootoutScore[1], th = _soTaken[0], ta = _soTaken[1];
            int left0 = Math.Max(0, 5 - th), left1 = Math.Max(0, 5 - ta);
            bool decided;
            if (th <= 5 && ta <= 5 && !(th == 5 && ta == 5))
                decided = h + left0 < a || a + left1 < h; // one can no longer be caught
            else
                decided = th == ta && h != a; // sudden death: after a pair
            if (th == 5 && ta == 5 && h != a) decided = true;
            if (decided)
            {
                ShootoutWinner = h > a ? Home : Away;
                Ball.Stop();
                Emit(MatchEventType.Whistle, null, null, null, Ball.Pos, 0f, WhistleKind.FullTime);
                Clock.Period = MatchPeriod.FullTime;
                Phase = MatchPhase.FullTime;
                foreach (var t in Teams) FinaliseStats(t);
                var fe = Emit(MatchEventType.FullTime);
                fe.Info = "pens " + h + "-" + a;
                return;
            }
            NextShootoutKick(_soKicking.Opponent);
        }
    }
}

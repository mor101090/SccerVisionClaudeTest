using System;
using System.Collections.Generic;
using System.Linq;

namespace SoccerVision.Simulation
{
    /// <summary>Restarts (Laws 8, 13-17): kick-off, throw-in, goal kick, corner kick, free kicks with walls, penalties - starting one and choosing the taker.</summary>
    public sealed partial class MatchEngine
    {
        private bool _restartKick;

        private bool _restartOffsideException;

        private bool _shortGoalKick;

        private bool _penaltyGuessCorrect;

        private float _penaltyDiveUntil = -1f;

        private Vec2 _penaltyDiveTarget;

        private Vec2 _lastShotCrossing;

        private Vec2 _newBallFrom;

        private bool _newBallRolls;

        private void BeginRestart(RestartType type, TeamAgent team, Vec2 spot, PlayerAgent taker)
        {
            FlushAdvantageSanction(); // (play is stopped: a card for a foul that was played on with advantage is shown now)
            if (type != RestartType.CornerKick && type != RestartType.GoalKick && type != RestartType.ThrowIn) _newBallRolls = false;
            // the shot that ended in a goal kick: the man who shot puts his hands on his head
            if (type == RestartType.GoalKick && Ball.Shooter != null && Ball.Shooter.Team != team && SimTime - _lastShotTime < 3f) OnShotWasted(Ball.Shooter, _lastShotXg);
            OrganiseDefence(type, team, spot);
            Phase = MatchPhase.Stoppage;
            Ball.Owner = null;
            Ball.InHands = false;
            Ball.ClearFlight();
            ClearOffside();
            foreach (var t in Teams)
            {
                t.Presser = null;
                t.SecondPresser = null;
                t.Chaser = null;
                t.Supporters.Clear();
            }
            foreach (var p in AllPlayers)
            {
                p.HasPendingPlan = false;
                p.RunTimer = 0f;
                p.DiveTimer = 0f;
                // (a player who ran on over the line as play stopped is back on the pitch at once)
                if (!p.SentOff && p.Injury != InjuryLevel.Serious) p.Pos = Pitch.ClampToField(p.Pos, -0.2f);
            }

            var r = new Restart { Type = type, Team = team, Spot = spot, PreferInswing = _rng.Chance(0.62f) };
            _restart = r;
            PossessionTeam = team;
            r.Taker = taker ?? ChooseTaker(r);
            switch (type)
            {
                case RestartType.KickOff: r.SetupTime = Clock.Running ? Config.KickOffSetupSeconds : 3f; break;
                case RestartType.ThrowIn: r.SetupTime = Config.ThrowInSetupSeconds; break;
                case RestartType.GoalKick: r.SetupTime = Config.GoalKickSetupSeconds; break;
                case RestartType.CornerKick: r.SetupTime = Config.CornerSetupSeconds; break;
                case RestartType.PenaltyKick: r.SetupTime = Config.PenaltySetupSeconds; break;
                default: r.SetupTime = IsDangerousFreeKick(r) ? Config.FreeKickWithWallSetupSeconds : Config.FreeKickSetupSeconds; break;
            }
            r.SetupTime *= _rng.Range(0.85f, 1.2f);
            if (WastesTime(team)) r.SetupTime *= 1.9f; // time wasting: the restart is taken slowly (the clock runs)
            if (type != RestartType.KickOff && type != RestartType.DroppedBall) r.PauseLeft = Config.RestartPauseRealSeconds * Config.MotionSpeed;
            LayoutRestart(r);
            // IFAB: the whistle restarts kick-offs, penalties and free kicks where the 9.15 m distance is enforced;
            // throw-ins, goal kicks, corners and quick free kicks are taken without it.
            ComputeRunUp(r);
            r.WaitForWhistle = type == RestartType.KickOff || type == RestartType.PenaltyKick || r.Wall.Count > 0;
            if (type == RestartType.KickOff && Vec2.Distance(Ball.Pos, Vec2.Zero) < 0.5f) r.BallPlaced = true;
        }

        /// <summary>
        /// Real takers do not strike the ball from where they stand: they step back (4-6 m for a penalty or a free-kick shot,
        /// diagonally behind the ball for a corner, 3-4 m for a goal kick) and run up to it.
        /// </summary>
        private void ComputeRunUp(Restart r)
        {
            r.HasRunUp = false;
            r.RunningUp = false;
            if (r.Taker == null) return;
            var team = r.Team;
            Vec2 dir;
            float back;
            switch (r.Type)
            {
                case RestartType.PenaltyKick:
                    dir = (team.AttackGoal - r.Spot).Normalized;
                    back = 4.6f;
                    // A slight angle: the run comes from the side of the non-kicking foot.
                    r.RunUpStart = r.Spot - dir * back + dir.Perp * (r.Taker.Attr.StrongFoot == Foot.Right ? 1.1f : -1.1f);
                    break;
                case RestartType.CornerKick:
                {
                    // The ball goes into the box, so the taker stands behind it: he steps back off the pitch (behind the
                    // corner, on the side of his kicking foot) and runs at the ball facing the goal area.
                    dir = (team.ToWorld(44f, 0f) - r.Spot).Normalized;
                    float footSide = r.Taker.Attr.StrongFoot == Foot.Right ? 1f : -1f;
                    r.RunUpStart = r.Spot - dir * 3.3f + dir.Perp * (footSide * 0.9f);
                    r.HasRunUp = true;
                    return; // (off the pitch: not clamped to the field)
                }
                case RestartType.GoalKick:
                    dir = team.DirToWorld(Vec2.UnitX);
                    r.RunUpStart = r.Spot - dir * 3.6f;
                    break;
                case RestartType.DirectFreeKick:
                case RestartType.IndirectFreeKick:
                    if (r.Wall.Count > 0)
                    {
                        dir = (team.AttackGoal - r.Spot).Normalized;
                        back = IsDangerousFreeKick(r) && r.Type == RestartType.DirectFreeKick ? 5.5f : 3.5f;
                    }
                    else
                    {
                        dir = team.DirToWorld(Vec2.UnitX);
                        back = 2.5f;
                    }
                    r.RunUpStart = r.Spot - dir * back + dir.Perp * (r.Taker.Attr.StrongFoot == Foot.Right ? 0.6f : -0.6f);
                    break;
                default:
                    return; // throw-ins and kick-offs: no run-up
            }
            r.RunUpStart = Pitch.ClampToField(r.RunUpStart, 0.5f);
            r.HasRunUp = Vec2.Distance(r.RunUpStart, r.Spot) > 1.5f;
        }

        private bool IsDangerousFreeKick(Restart r)
        {
            if (r.Type != RestartType.DirectFreeKick && r.Type != RestartType.IndirectFreeKick) return false;
            float dist = Vec2.Distance(r.Spot, r.Team.AttackGoal);
            return dist < 36f && r.Team.Forward(r.Spot) > 12f;
        }

        /// <summary>The manager's priority list for this restart (corners and free kicks at goal by the side of the pitch), or empty.</summary>
        private int[] TakerList(Restart r)
        {
            var t = r.Team.Tactics;
            bool right = r.Team.Lateral(r.Spot) < 0f; // (a team's right flank is negative lateral)
            if (r.Type == RestartType.CornerKick) return right ? t.CornerTakersRight : t.CornerTakersLeft;
            if (r.Type == RestartType.PenaltyKick) return t.PenaltyTakers;
            if (r.Type != RestartType.KickOff && r.Type != RestartType.ThrowIn && r.Type != RestartType.GoalKick && IsDangerousFreeKick(r)) return right ? t.FreeKickTakersRight : t.FreeKickTakersLeft;
            return null;
        }

        /// <summary>Position of a player in the manager's list (0 first choice), -1 if he is not on it. A player who has been substituted, sent off or hurt is not on the pitch, so the next name on the list takes it.</summary>
        private int TakerRank(Restart r, PlayerAgent p)
        {
            var list = TakerList(r);
            if (list == null || p.IsGoalkeeper) return -1;
            return Array.IndexOf(list, p.Number);
        }

        private PlayerAgent ChooseTaker(Restart r)
        {
            var team = r.Team;
            PlayerAgent best = null;
            float bestScore = float.NegativeInfinity;
            foreach (var p in team.Players)
            {
                if (p.SentOff || p.Injury == InjuryLevel.Serious) continue;
                float score;
                switch (r.Type)
                {
                    case RestartType.KickOff:
                        score = p.Slot.Line == Line.Attack ? 100f - Vec2.Distance(p.Pos, r.Spot) : (p.Slot.Line == Line.Midfield ? 40f : -100f);
                        if (p.IsGoalkeeper) score = -1000f;
                        break;
                    case RestartType.GoalKick:
                        score = p.IsGoalkeeper ? 1000f : -Vec2.Distance(p.Pos, r.Spot);
                        break;
                    case RestartType.ThrowIn:
                        if (p.IsGoalkeeper) { score = -1000f; break; }
                        score = -Vec2.Distance(p.Pos, r.Spot) + (p.Slot.IsFullBack || p.Slot.IsWideMidfielder ? 8f : 0f)
                                + (MathF.Sign(team.Lateral(r.Spot)) == MathF.Sign(p.Slot.BaseLateral) ? 6f : 0f);
                        // within his range of the box the long-throw specialist comes over to take it (polish round 2: who can reach is the physics)
                        if (p.Has(PlayerTrait.LongThrowSpecialist) && Vec2.Distance(p.Pos, r.Spot) < LongThrowWalk && CanThrowIntoBox(p, r.Spot)) score += 60f;
                        break;
                    case RestartType.CornerKick:
                        if (p.IsGoalkeeper) { score = -1000f; break; }
                        score = p.Attr.Crossing + 0.3f * p.Attr.FreeKicks - (p.Slot.IsCentreBack ? 40f : 0f) - (p.Slot.Line == Line.Attack ? 15f : 0f);
                        {
                            // The foot decides the swing: the team wants an in-swinger (towards goal) for most corners and an
                            // out-swinger (away from the keeper) for the rest, and picks a taker whose foot gives it.
                            Vec2 toBox = (team.ToWorld(46f, 0f) - r.Spot).Normalized;
                            Vec2 toGoal = (team.AttackGoal - r.Spot).Normalized;
                            bool gives = MathF.Sign(Vec2.Cross(toBox, toGoal)) == NaturalCurlSign(p);
                            if (gives == r.PreferInswing) score += 14f;
                        }
                        break;
                    case RestartType.PenaltyKick:
                        if (p.IsGoalkeeper) { score = -1000f; break; }
                        score = p.Attr.Penalty * 2f + p.Attr.Composure + p.Attr.Finishing * 0.5f;
                        break;
                    default:
                        if (IsDangerousFreeKick(r))
                        {
                            if (p.IsGoalkeeper) { score = -1000f; break; }
                            score = p.Attr.FreeKicks * 2f + p.Attr.Crossing * 0.5f - (p.Slot.IsCentreBack ? 30f : 0f);
                        }
                        else
                        {
                            bool ownBox = Pitch.InPenaltyArea(r.Spot, team.OwnGoalSign);
                            score = p.IsGoalkeeper ? (ownBox ? 50f : -1000f) : -Vec2.Distance(p.Pos, r.Spot) + (p.Slot.Line == Line.Defence && team.Forward(r.Spot) < -20f ? 4f : 0f);
                        }
                        break;
                }
                // the manager's set-piece takers come first (corners, free kicks at goal, penalties)
                int rank = TakerRank(r, p);
                if (rank >= 0) score = 100000f - 1000f * rank; // (the first on the manager's list who is on the pitch and fit)
                if (score > bestScore)
                {
                    bestScore = score;
                    best = p;
                }
            }
            return best;
        }

        /// <summary>Which way the taker of a restart stands: into the field for a throw-in, at the box for a corner, upfield for a goal kick, at goal for a free kick or a penalty.</summary>
        private bool RestartFacing(Restart r, PlayerAgent p, out float facing)
        {
            facing = 0f;
            var team = r.Team;
            switch (r.Type)
            {
                case RestartType.ThrowIn: facing = new Vec2(0f, -MathF.Sign(r.Spot.Y)).Angle; return true;
                case RestartType.CornerKick: facing = (Pitch.PenaltySpot(team.AttackGoalSign) - p.Pos).Angle; return true;
                case RestartType.GoalKick: facing = team.DirToWorld(Vec2.UnitX).Angle; return true;
                case RestartType.PenaltyKick: facing = (team.AttackGoal - p.Pos).Angle; return true;
                case RestartType.DirectFreeKick:
                case RestartType.IndirectFreeKick:
                    if (Vec2.Distance(p.Pos, r.Spot) > 6f) return false; // still walking back for his run-up
                    facing = (IsDangerousFreeKick(r) ? team.AttackGoal - r.Spot : team.DirToWorld(Vec2.UnitX) * 10f).Angle;
                    return true;
                default: return false;
            }
        }

        private Vec2 ApproachPoint(Restart r)
        {
            var team = r.Team;
            Vec2 forward = team.DirToWorld(Vec2.UnitX);
            switch (r.Type)
            {
                case RestartType.ThrowIn:
                    return new Vec2(r.Spot.X, r.Spot.Y + MathF.Sign(r.Spot.Y) * 0.35f);
                case RestartType.CornerKick:
                    // Just behind the ball, on the line of the run-up: the ball is in front of him.
                    if (r.HasRunUp) return r.Spot + (r.RunUpStart - r.Spot).Normalized * 0.6f;
                    return r.Spot + new Vec2(MathF.Sign(r.Spot.X) * 0.45f, MathF.Sign(r.Spot.Y) * 0.45f);
                case RestartType.PenaltyKick:
                    return r.Spot - (team.AttackGoal - r.Spot).Normalized * 0.5f; // he strikes it from right behind the ball
                case RestartType.KickOff:
                    return r.Spot - forward * 0.45f + new Vec2(0f, 0.15f);
                default:
                    Vec2 aim = IsDangerousFreeKick(r) ? team.AttackGoal : r.Spot + forward * 10f;
                    return r.Spot - (aim - r.Spot).Normalized * 0.8f;
            }
        }
    }
}

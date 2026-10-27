using System;
using System.Collections.Generic;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// Restarts (Laws 8, 13-17): kick-off, throw-in, goal kick, corner kick, free kicks with walls, penalties.
    /// Players walk to legal positions, the taker goes to the ball, and the restart is taken once ready.
    /// </summary>
    public sealed partial class MatchEngine
    {
        private bool _restartKick;
        private bool _restartOffsideException;
        private bool _shortGoalKick;
        private bool _penaltyGuessCorrect;
        private float _penaltyDiveUntil = -1f;
        private Vec2 _penaltyDiveTarget;
        private Vec2 _lastShotCrossing;

        private void BeginRestart(RestartType type, TeamAgent team, Vec2 spot, PlayerAgent taker)
        {
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
            }

            var r = new Restart { Type = type, Team = team, Spot = spot };
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

        // ---- Takers ---------------------------------------------------------------------------------------

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
                        break;
                    case RestartType.CornerKick:
                        if (p.IsGoalkeeper) { score = -1000f; break; }
                        score = p.Attr.Crossing + 0.3f * p.Attr.FreeKicks - (p.Slot.IsCentreBack ? 40f : 0f) - (p.Slot.Line == Line.Attack ? 15f : 0f);
                        {
                            // The foot that swings the ball towards goal (in-swinger) is preferred.
                            Vec2 toBox = (team.ToWorld(46f, 0f) - r.Spot).Normalized;
                            Vec2 toGoal = (team.AttackGoal - r.Spot).Normalized;
                            if (MathF.Sign(Vec2.Cross(toBox, toGoal)) == NaturalCurlSign(p)) score += 14f;
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
                if (score > bestScore)
                {
                    bestScore = score;
                    best = p;
                }
            }
            return best;
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
                    return r.Spot - (team.AttackGoal - r.Spot).Normalized * 2.2f;
                case RestartType.KickOff:
                    return r.Spot - forward * 0.45f + new Vec2(0f, 0.15f);
                default:
                    Vec2 aim = IsDangerousFreeKick(r) ? team.AttackGoal : r.Spot + forward * 10f;
                    return r.Spot - (aim - r.Spot).Normalized * 0.8f;
            }
        }

        // ---- Layouts ----------------------------------------------------------------------------------------

        private void LayoutRestart(Restart r)
        {
            var att = r.Team;
            var def = att.Opponent;
            r.Wall.Clear();
            switch (r.Type)
            {
                case RestartType.KickOff:
                    foreach (var p in AllPlayers) p.ShapeTarget = KickOffPosition(p, p.Team == att);
                    break;
                case RestartType.ThrowIn:
                    LayoutThrowIn(r);
                    break;
                case RestartType.GoalKick:
                    LayoutGoalKick(r);
                    break;
                case RestartType.CornerKick:
                    LayoutCorner(r);
                    break;
                case RestartType.PenaltyKick:
                    LayoutPenalty(r);
                    break;
                case RestartType.DroppedBall:
                    LayoutShapeAround(r.Spot, att);
                    foreach (var p in def.Players)
                        if (!p.SentOff) p.ShapeTarget = OutsideCircle(p.ShapeTarget, r.Spot, Pitch.DroppedBallDistance + 0.5f);
                    break;
                default:
                    if (IsDangerousFreeKick(r)) LayoutDangerousFreeKick(r);
                    else LayoutShapeAround(r.Spot, att);
                    break;
            }
            if (r.Taker != null) r.Taker.ShapeTarget = ApproachPoint(r);
        }

        /// <summary>Both teams take their normal shapes as if the ball were at <paramref name="ballPos"/>.</summary>
        private void LayoutShapeAround(Vec2 ballPos, TeamAgent inPossession)
        {
            foreach (var t in Teams)
            {
                bool poss = t == inPossession;
                var tac = t.Tactics;
                float ballF = t.Forward(ballPos);
                float ballL = t.Lateral(ballPos);
                float line = poss ? MathUtil.Clamp(ballF * 0.62f - 17f + 10f * (tac.Mentality - 0.5f), -41f, 3f)
                                  : MathUtil.Clamp(MathF.Min(ballF * 0.6f - 21f + 12f * (tac.DefensiveLine - 0.5f), ballF - 2f), -42f, -2f + 12f * (tac.DefensiveLine - 0.5f));
                t.LineForward = MathF.Max(line, -47.5f);
                float offsideLine = MathF.Max(MathF.Max(SecondLastDefenderForward(t), ballF), 0f);
                foreach (var p in t.Players)
                {
                    if (p.SentOff) continue;
                    if (p.IsGoalkeeper)
                    {
                        Vec2 goal = t.OwnGoal;
                        Vec2 dir = (ballPos - goal).Normalized;
                        float depth = poss && ballF > 0f ? 12f : MathUtil.Clamp(1f + 0.07f * Vec2.Distance(ballPos, goal), 1f, 6f);
                        p.ShapeTarget = goal + dir * depth;
                        continue;
                    }
                    p.ShapeTarget = ComputeShapeTarget(p, t, poss, ballF, ballL, offsideLine);
                }
            }
        }

        private Vec2 KickOffPosition(PlayerAgent p, bool kicking)
        {
            var team = p.Team;
            if (p.IsGoalkeeper) return team.ToWorld(-50.5f, 0f);
            var slot = p.Slot;
            float f = MathF.Min(slot.BaseForward * 0.85f - 3f, -1.5f);
            float l = slot.BaseLateral;
            if (slot.Line == Line.Attack)
            {
                f = kicking ? -1.2f : -10.6f;
                l = kicking ? slot.BaseLateral * 0.9f : slot.BaseLateral * 0.6f;
            }
            Vec2 pos = team.ToWorld(f, l);
            if (!kicking && pos.Length < Pitch.CentreCircleRadius + 0.8f)
            {
                pos = pos.Normalized * (Pitch.CentreCircleRadius + 0.8f);
            }
            return pos;
        }

        private void TeleportForKickOff(TeamAgent kicking)
        {
            Ball.Owner = null;
            Ball.InHands = false;
            Ball.InGoal = false;
            Ball.Place(Vec2.Zero);
            foreach (var p in AllPlayers)
            {
                if (p.SentOff)
                {
                    p.Pos = new Vec2(p.Pos.X, (Pitch.HalfWidth + 6f) * (p.Pos.Y >= 0f ? 1f : -1f));
                    continue;
                }
                p.Pos = KickOffPosition(p, p.Team == kicking);
                p.Vel = Vec2.Zero;
                p.Facing = p.Team.AttackDir > 0 ? 0f : MathUtil.Pi;
                p.JumpZ = 0f;
                p.JumpVz = 0f;
                p.ShapeTarget = p.Pos;
                p.Target = p.Pos;
            }
            foreach (var t in Teams) t.LineForward = -33f;
        }

        private void LayoutThrowIn(Restart r)
        {
            var att = r.Team;
            var def = att.Opponent;
            LayoutShapeAround(r.Spot, att);
            float inward = -MathF.Sign(r.Spot.Y);
            Vec2 forward = att.DirToWorld(Vec2.UnitX);
            Vec2 offerA = Pitch.ClampToField(r.Spot + forward * 6f + new Vec2(0f, inward * 6f), -1.5f);
            Vec2 offerB = Pitch.ClampToField(r.Spot - forward * 5f + new Vec2(0f, inward * 8.5f), -1.5f);
            var used = new HashSet<PlayerAgent> { r.Taker };
            var a = NearestTo(att, offerA, used);
            if (a != null) { a.ShapeTarget = offerA; used.Add(a); }
            var b = NearestTo(att, offerB, used);
            if (b != null) { b.ShapeTarget = offerB; used.Add(b); }
            var defUsed = new HashSet<PlayerAgent>();
            foreach (var mark in new[] { a, b })
            {
                if (mark == null) continue;
                var m = NearestTo(def, mark.ShapeTarget, defUsed);
                if (m == null) continue;
                defUsed.Add(m);
                m.ShapeTarget = mark.ShapeTarget + (def.OwnGoal - mark.ShapeTarget).Normalized * 1.6f;
            }
        }

        private void LayoutGoalKick(Restart r)
        {
            var team = r.Team;
            var opp = team.Opponent;
            float tempo = team.Tactics.Tempo;
            float kicking = r.Taker != null ? MathUtil.A(r.Taker.Attr.Kicking) : 0.5f;
            float pShort = MathUtil.Clamp(0.35f + 0.35f * (1f - tempo) + 0.2f * (1f - opp.Tactics.PressingIntensity) - 0.25f * kicking, 0.1f, 0.85f);
            _shortGoalKick = _rng.Chance(pShort);
            LayoutShapeAround(r.Spot, team);
            foreach (var p in team.Players)
            {
                if (p.SentOff || p == r.Taker) continue;
                var s = p.Slot;
                float side = MathF.Sign(s.BaseLateral);
                Vec2 local;
                if (_shortGoalKick)
                {
                    if (s.IsCentreBack) local = new Vec2(-41f, side * 13f);
                    else if (s.IsFullBack) local = new Vec2(-30f, side * 27f);
                    else if (s.IsCentralMidfielder) local = new Vec2(-25f, side * 7f);
                    else if (s.IsWideMidfielder) local = new Vec2(-10f, side * 26f);
                    else local = new Vec2(3f, side * 8f);
                }
                else
                {
                    if (s.IsCentreBack) local = new Vec2(-35f, side * 10f);
                    else if (s.IsFullBack) local = new Vec2(-22f, side * 26f);
                    else if (s.IsCentralMidfielder) local = new Vec2(-6f, side * 8f);
                    else if (s.IsWideMidfielder) local = new Vec2(4f, side * 25f);
                    else local = new Vec2(13f, side * 5f);
                }
                p.ShapeTarget = team.ToWorld(local.X, local.Y);
            }
            // Opponents must be outside the penalty area until the ball is in play.
            foreach (var o in opp.Players)
            {
                if (o.SentOff || o.IsGoalkeeper) continue;
                o.ShapeTarget = OutsidePenaltyArea(o.ShapeTarget, team.OwnGoalSign, 1.2f);
            }
        }

        private void LayoutCorner(Restart r)
        {
            var att = r.Team;
            var def = att.Opponent;
            LayoutShapeAround(r.Spot, att);
            float side = MathF.Sign(att.Lateral(r.Spot));
            var zones = new[]
            {
                new Vec2(46.8f, side * 2.6f),   // near post
                new Vec2(47.4f, -side * 3.2f),  // far post
                new Vec2(42f, side * 0.5f),     // penalty spot
                new Vec2(48.6f, -side * 0.3f),  // centre of the six-yard box
                new Vec2(35.5f, -side * 2f),    // edge of the box
                new Vec2(32f, side * 9f),       // recycling / rebound
            };

            // Best headers attack the box (centre-backs go up), the rest guard against the counter.
            var candidates = new List<PlayerAgent>();
            foreach (var p in att.Players) if (!p.SentOff && !p.IsGoalkeeper && p != r.Taker) candidates.Add(p);
            candidates.Sort((x, y) => (y.Attr.Heading + y.Attr.Jumping + (y.Profile.HeightCm - 180) * 1.2f).CompareTo(x.Attr.Heading + x.Attr.Jumping + (x.Profile.HeightCm - 180) * 1.2f));
            int zoneIndex = 0;
            var attackersInBox = new List<PlayerAgent>();
            int stayBack = Math.Max(2, candidates.Count - zones.Length);
            for (int i = 0; i < candidates.Count; i++)
            {
                var p = candidates[i];
                bool guard = i >= candidates.Count - stayBack && (p.Slot.IsFullBack || p.Slot.Line == Line.Attack || zoneIndex >= zones.Length);
                if (!guard && zoneIndex < zones.Length)
                {
                    var z = zones[zoneIndex++];
                    p.ShapeTarget = att.ToWorld(z.X, z.Y + _rng.Range(-0.6f, 0.6f));
                    if (zoneIndex <= 4) attackersInBox.Add(p);
                }
                else
                {
                    float lat = (i % 2 == 0 ? 1f : -1f) * 12f;
                    p.ShapeTarget = att.ToWorld(2f, lat);
                }
            }

            // Defenders: keeper, zonal near post and six-yard line, man-marking the main threats.
            float dSide = MathF.Sign(def.Lateral(r.Spot));
            var defenders = new List<PlayerAgent>();
            foreach (var p in def.Players) if (!p.SentOff && !p.IsGoalkeeper) defenders.Add(p);
            defenders.Sort((x, y) => (y.Attr.Heading + y.Attr.Jumping + y.Attr.Marking).CompareTo(x.Attr.Heading + x.Attr.Jumping + x.Attr.Marking));
            var gk = def.Goalkeeper;
            if (gk != null) gk.ShapeTarget = def.ToWorld(-51.6f, -dSide * 0.6f);
            var zonal = new[] { new Vec2(-51.6f, dSide * 3.3f), new Vec2(-47.6f, dSide * 1.5f), new Vec2(-47.6f, -dSide * 2.5f) };
            int di = 0;
            var defUsed = new HashSet<PlayerAgent>();
            foreach (var attacker in attackersInBox)
            {
                if (di >= defenders.Count) break;
                var m = NearestTo(def, attacker.ShapeTarget, defUsed);
                if (m == null) break;
                defUsed.Add(m);
                m.ShapeTarget = attacker.ShapeTarget + (def.OwnGoal - attacker.ShapeTarget).Normalized * 0.9f;
                di++;
            }
            int zi = 0;
            foreach (var d in defenders)
            {
                if (defUsed.Contains(d)) continue;
                if (zi < zonal.Length)
                {
                    d.ShapeTarget = def.ToWorld(zonal[zi].X, zonal[zi].Y);
                    zi++;
                }
                else if (d.Slot.Line == Line.Attack)
                {
                    d.ShapeTarget = def.ToWorld(-6f, d.Slot.BaseLateral);
                }
                else
                {
                    d.ShapeTarget = def.ToWorld(-36f, -dSide * 4f + _rng.Range(-4f, 4f));
                }
                defUsed.Add(d);
            }
            foreach (var d in def.Players)
            {
                if (d.SentOff) continue;
                d.ShapeTarget = OutsideCircle(d.ShapeTarget, r.Spot, Pitch.RestartDistance + 0.3f);
            }
        }

        private void LayoutDangerousFreeKick(Restart r)
        {
            var att = r.Team;
            var def = att.Opponent;
            LayoutShapeAround(r.Spot, att);
            float dist = Vec2.Distance(r.Spot, att.AttackGoal);
            int wallCount = dist < 20f ? 5 : (dist < 25f ? 4 : (dist < 30f ? 3 : 2));
            if (MathF.Abs(r.Spot.Y) > 18f) wallCount = Math.Max(1, wallCount - 2);
            if (r.Type == RestartType.IndirectFreeKick) wallCount = Math.Max(1, wallCount - 1);

            int goalSign = att.AttackGoalSign;
            float nearSide = MathF.Abs(r.Spot.Y) > 0.5f ? MathF.Sign(r.Spot.Y) : 1f;
            Vec2 nearPostAim = new Vec2(Pitch.HalfLength * goalSign, nearSide * 1.6f);
            Vec2 dir = (nearPostAim - r.Spot).Normalized;
            Vec2 centre = r.Spot + dir * (Pitch.RestartDistance + 0.1f);
            Vec2 perp = dir.Perp;

            var pool = new List<PlayerAgent>();
            foreach (var p in def.Players) if (!p.SentOff && !p.IsGoalkeeper) pool.Add(p);
            pool.Sort((x, y) => Vec2.Distance(x.Pos, centre).CompareTo(Vec2.Distance(y.Pos, centre)));
            for (int i = 0; i < wallCount && i < pool.Count; i++)
            {
                float offset = (i - (wallCount - 1) * 0.5f) * 0.62f;
                // The wall leans to cover the near post side.
                Vec2 pos = centre + perp * (offset * (Vec2.Cross(dir, new Vec2(0f, nearSide)) >= 0f ? 1f : -1f));
                pool[i].ShapeTarget = pos;
                r.Wall.Add(pool[i]);
            }
            var gk = def.Goalkeeper;
            if (gk != null) gk.ShapeTarget = new Vec2((Pitch.HalfLength - 0.7f) * goalSign, -nearSide * 0.9f);

            LayoutFreeKickBox(r, centre);
        }

        /// <summary>
        /// The box at a free kick in the attacking third, as at a corner: the best headers are spread over the near post,
        /// far post, penalty spot and the edges of the area, each with a defender goal-side of him (a pair, shoulder to
        /// shoulder), a couple of players at the edge of the box for the second ball, and the rest guarding the counter.
        /// Nobody stands in a straight line.
        /// </summary>
        private void LayoutFreeKickBox(Restart r, Vec2 wallCentre)
        {
            var att = r.Team;
            var def = att.Opponent;
            float dist = Vec2.Distance(r.Spot, att.AttackGoal);
            bool shooting = r.Type == RestartType.DirectFreeKick && dist < 33f && MathF.Abs(r.Spot.Y) < 22f;
            float side = MathF.Abs(att.Lateral(r.Spot)) > 1f ? MathF.Sign(att.Lateral(r.Spot)) : (_rng.Chance(0.5f) ? 1f : -1f);
            var zones = new[]
            {
                new Vec2(47.0f, side * 2.6f),    // near post
                new Vec2(47.2f, -side * 3.6f),   // far post
                new Vec2(42.0f, side * 0.8f),    // penalty spot
                new Vec2(43.5f, -side * 8.5f),   // far side of the area
                new Vec2(44.0f, side * 9.0f),    // near side of the area
                new Vec2(38.5f, -side * 3.5f),   // edge of the box, second ball
            };
            int nBox = shooting ? 3 : (dist > 28f ? 4 : 5);

            var candidates = new List<PlayerAgent>();
            foreach (var p in att.Players) if (!p.SentOff && !p.IsGoalkeeper && p != r.Taker) candidates.Add(p);
            float Aerial(PlayerAgent p) => p.Attr.Heading + p.Attr.Jumping + (p.Profile.HeightCm - 180) * 1.2f;
            candidates.Sort((x, y) => Aerial(y).CompareTo(Aerial(x)));
            // One centre-back (the weakest in the air) and the full-backs stay back against the counter.
            PlayerAgent stayCb = null;
            foreach (var p in candidates) if (p.Slot.IsCentreBack) stayCb = p;
            var inBox = new List<PlayerAgent>();
            var rest = new List<PlayerAgent>();
            foreach (var p in candidates)
            {
                if (inBox.Count < nBox && !p.Slot.IsFullBack && p != stayCb && p.Slot.Line != Line.Goalkeeper) inBox.Add(p);
                else rest.Add(p);
            }
            int zi = 0;
            foreach (var p in inBox)
            {
                Vec2 z = zones[zi % zones.Length];
                for (int guard = 0; guard < zones.Length && Vec2.Distance(att.ToWorld(z.X, z.Y), r.Spot) < 4.5f; guard++)
                {
                    zi++;
                    z = zones[zi % zones.Length];
                }
                zi++;
                p.ShapeTarget = att.ToWorld(z.X + _rng.Range(-1.0f, 1.0f), z.Y + _rng.Range(-0.8f, 0.8f));
            }
            int recycle = 0;
            foreach (var p in rest)
            {
                if (p.Slot.IsFullBack) p.ShapeTarget = att.ToWorld(_rng.Range(4f, 12f), (recycle++ % 2 == 0 ? 1f : -1f) * _rng.Range(10f, 16f));
                else if (p == stayCb) p.ShapeTarget = att.ToWorld(_rng.Range(10f, 15f), _rng.Range(-4f, 4f));
                else if (recycle < 4) p.ShapeTarget = att.ToWorld(_rng.Range(31f, 35f), (recycle++ % 2 == 0 ? 1f : -1f) * _rng.Range(8f, 14f));
            }

            // Defenders: pair up goal-side of each attacker; the others cover the edge of the box and the second ball.
            var pairUsed = new HashSet<PlayerAgent>(r.Wall);
            foreach (var p in inBox)
            {
                var m = NearestTo(def, p.ShapeTarget, pairUsed);
                if (m == null) break;
                pairUsed.Add(m);
                Vec2 toGoal = (def.OwnGoal - p.ShapeTarget).Normalized;
                // some mark tight (man to man), some stand off in a zone
                float gap = m.Attr.Marking > 55 ? 0.95f : 1.7f;
                m.ShapeTarget = p.ShapeTarget + toGoal * gap + toGoal.Perp * _rng.Range(-0.4f, 0.4f);
            }
            int edge = 0;
            foreach (var d in def.Players)
            {
                if (d.SentOff || d.IsGoalkeeper || pairUsed.Contains(d)) continue;
                if (d.Slot.Line == Line.Attack) continue; // strikers stay high for the counter
                if (edge < 3)
                {
                    d.ShapeTarget = def.ToWorld(_rng.Range(-37f, -34f) , (edge % 2 == 0 ? 1f : -1f) * (_rng.Range(5f, 13f)) * (edge == 2 ? 0.4f : 1f));
                    edge++;
                }
            }

            // Keep everyone legal: defenders 9.15 m from the ball, attackers 1.3 m from the wall and onside at the kick.
            foreach (var p in def.Players)
            {
                if (p.SentOff || r.Wall.Contains(p)) continue;
                p.ShapeTarget = OutsideCircle(p.ShapeTarget, r.Spot, Pitch.RestartDistance + 0.4f);
            }
            float second = float.NegativeInfinity, last = float.NegativeInfinity;
            foreach (var p in def.Players)
            {
                if (p.SentOff) continue;
                float f = att.Forward(p.ShapeTarget);
                if (f > last) { second = last; last = f; }
                else if (f > second) second = f;
            }
            float ballF = att.Forward(r.Spot);
            foreach (var p in att.Players)
            {
                if (p.SentOff || p == r.Taker) continue;
                float f = att.Forward(p.ShapeTarget);
                if (f > ballF && f > second - 0.3f) p.ShapeTarget = att.ToWorld(MathF.Max(ballF, second - 0.4f), att.Lateral(p.ShapeTarget));
                foreach (var w in r.Wall)
                {
                    if (Vec2.Distance(p.ShapeTarget, w.ShapeTarget) < 1.3f) p.ShapeTarget = w.ShapeTarget + (p.ShapeTarget - w.ShapeTarget).Normalized * 1.4f;
                }
            }
        }

        private void LayoutPenalty(Restart r)
        {
            var att = r.Team;
            var def = att.Opponent;
            int goalSign = att.AttackGoalSign;
            var gk = def.Goalkeeper;
            if (gk != null) gk.ShapeTarget = new Vec2((Pitch.HalfLength - 0.1f) * goalSign, 0f);
            // Everyone else: outside the area and arc, behind the penalty mark.
            float[] laterals = { -9f, 9f, -14f, 14f, -18.5f, 18.5f, -4.5f, 4.5f };
            int ai = 0, di = 0;
            foreach (var p in att.Players)
            {
                if (p.SentOff || p == r.Taker || p.IsGoalkeeper) continue;
                if (ai < 4)
                {
                    float lat = laterals[(ai * 2) % laterals.Length] * (ai % 2 == 0 ? 1f : -1f);
                    p.ShapeTarget = att.ToWorld(34.3f, lat);
                    ai++;
                }
                else
                {
                    p.ShapeTarget = att.ToWorld(_rng.Range(12f, 22f), p.Slot.BaseLateral * 0.8f);
                }
            }
            foreach (var p in def.Players)
            {
                if (p.SentOff || p.IsGoalkeeper) continue;
                if (di < 5)
                {
                    float lat = laterals[(di * 2 + 1) % laterals.Length] * (di % 2 == 0 ? 1f : -1f) * 0.92f;
                    p.ShapeTarget = att.ToWorld(33.6f, lat);
                    di++;
                }
                else
                {
                    p.ShapeTarget = att.ToWorld(_rng.Range(15f, 26f), -p.Slot.BaseLateral * 0.6f);
                }
            }
            foreach (var p in AllPlayers)
            {
                if (p.SentOff || p == r.Taker || p == gk) continue;
                p.ShapeTarget = LegalPenaltyPosition(p.ShapeTarget, goalSign);
            }
        }

        private PlayerAgent NearestTo(TeamAgent team, Vec2 point, HashSet<PlayerAgent> exclude)
        {
            PlayerAgent best = null;
            float bestD = float.PositiveInfinity;
            foreach (var p in team.Players)
            {
                if (p.SentOff || p.IsGoalkeeper || (exclude != null && exclude.Contains(p))) continue;
                float d = Vec2.DistanceSq(p.Pos, point);
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        private static Vec2 OutsideCircle(Vec2 p, Vec2 centre, float radius)
        {
            Vec2 d = p - centre;
            float len = d.Length;
            if (len >= radius) return p;
            Vec2 dir = len > 1e-3f ? d / len : new Vec2(-MathF.Sign(centre.X == 0f ? 1f : centre.X), 0f);
            // Stay on the pitch: if the radial spot is beyond a line (free kicks near the corner or the goal line), swing round
            // the circle to the nearest spot that is inside the field of play.
            float baseAngle = dir.Angle;
            for (int k = 0; k <= 24; k++)
            {
                float swing = ((k + 1) / 2) * 0.2618f * (k % 2 == 0 ? 1f : -1f); // 0, +15, -15, +30, -30 degrees ...
                Vec2 c = centre + Vec2.FromAngle(baseAngle + swing) * radius;
                if (Pitch.ClampToField(c, -0.3f).Equals(c)) return c;
            }
            return Pitch.ClampToField(centre + dir * radius, -0.3f);
        }

        private static Vec2 OutsidePenaltyArea(Vec2 p, int goalSign, float margin)
        {
            if (!Pitch.InPenaltyArea(p, goalSign) && !(MathF.Abs(p.Y) <= Pitch.PenaltyAreaHalfWidth + margin && (Pitch.HalfLength - p.X * goalSign) <= Pitch.PenaltyAreaDepth + margin)) return p;
            float x = (Pitch.HalfLength - Pitch.PenaltyAreaDepth - margin) * goalSign;
            return new Vec2(x, p.Y);
        }

        private static Vec2 LegalPenaltyPosition(Vec2 p, int goalSign)
        {
            Vec2 spot = Pitch.PenaltySpot(goalSign);
            // Behind the penalty mark.
            if (p.X * goalSign > spot.X * goalSign - 0.5f) p = new Vec2(spot.X - 0.5f * goalSign, p.Y);
            // Outside the area.
            if (Pitch.InPenaltyArea(p, goalSign)) p = new Vec2((Pitch.HalfLength - Pitch.PenaltyAreaDepth - 0.6f) * goalSign, p.Y);
            // Outside the arc (9.15 m from the mark).
            if (Vec2.Distance(p, spot) < Pitch.PenaltyArcRadius + 0.4f)
            {
                Vec2 dir = (p - spot).Normalized;
                if (dir.X * goalSign > -0.1f) dir = new Vec2(-0.1f * goalSign, MathF.Sign(dir.Y == 0f ? 1f : dir.Y)).Normalized;
                p = spot + dir * (Pitch.PenaltyArcRadius + 0.5f);
            }
            return p;
        }

        // ---- Stoppage step --------------------------------------------------------------------------------

        private void StepStoppage(float dt)
        {
            var r = _restart;
            if (r == null)
            {
                Phase = MatchPhase.Live;
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
                if (r.Timer > 0.6f)
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
                p.Task = PlayerTask.SetPiece;
                p.FaceBall = true;
                if (p == r.Taker)
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
            }

            if (r.Type == RestartType.ThrowIn && r.BallPlaced && r.Taker != null && Vec2.Distance(r.Taker.Pos, approach) < 1.2f)
            {
                // Both hands, from behind and over the head.
                Ball.Place(r.Taker.Pos + r.Taker.FacingDir * 0.15f, 2.15f);
            }

            // Nobody restarts while an injured player is being treated or helped off (the time is added on).
            if (InjuryBeingHandled())
            {
                r.CountsAsLostTime = true;
                r.Timer = MathF.Min(r.Timer, r.SetupTime);
                if (r.Taker != null && r.Taker.Injury == InjuryLevel.Serious) r.Taker = null;
                return;
            }
            if (r.Taker == null || r.Taker.SentOff) r.Taker = ChooseTaker(r);
            bool takerReady = r.Taker != null && Vec2.Distance(r.Taker.Pos, moveTo) < 0.7f;
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
            if (ready && r.RunningUp && r.HasRunUp && r.Taker != null && Vec2.Distance(r.Taker.Pos, approach) > 0.5f && r.Timer - r.RunUpBegan < 4f * Config.MotionSpeed) return;
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

        // ---- Execution ------------------------------------------------------------------------------------

        private void ExecuteRestart(Restart r)
        {
            var taker = r.Taker;
            EnforceRestartPositions(r);
            RestartExecuting?.Invoke(r);
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
                        ExecuteThrowIn(taker, spot);
                        break;
                    case RestartType.GoalKick:
                        ExecuteGoalKick(taker);
                        break;
                    case RestartType.CornerKick:
                        ExecuteCorner(taker);
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

        private void ExecuteThrowIn(PlayerAgent taker, Vec2 spot)
        {
            var team = taker.Team;
            float f = team.Forward(spot);
            PlayerAgent receiver = null;
            Vec2 target;
            bool longThrow = f > 33f && taker.Attr.PhysicalPower > 70 && _rng.Chance(0.6f);
            if (longThrow)
            {
                target = team.ToWorld(44f, -MathF.Sign(team.Lateral(spot)) * _rng.Range(0f, 4f));
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
            float d = Vec2.Distance(from, target);
            float horizontal = MathUtil.Clamp(7f + 0.35f * d, 7f, longThrow ? 15f : 14f);
            Vec2 dir = (target - from).Normalized.Rotate(_rng.Gaussian(0f, 0.05f));
            float vz = KickSolver.VerticalSpeedFor(dir * horizontal, 2.15f, d, 0.6f, Config);
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

        private void ExecuteCorner(PlayerAgent taker)
        {
            var team = taker.Team;
            if (_rng.Chance(0.12f))
            {
                var plan = BestRestartPass(taker, 16f, false);
                if (plan.Action == BallAction.Pass)
                {
                    plan.Kind = KickKind.Restart;
                    KickGround(taker, plan, 0f);
                    return;
                }
            }
            float side = MathF.Sign(team.Lateral(Ball.Pos));
            float r = _rng.NextFloat();
            Vec2 zone = r < 0.36f ? new Vec2(46.8f, side * 2.6f) : (r < 0.68f ? new Vec2(42f, side * 0.5f) : new Vec2(47.4f, -side * 3.2f));
            Vec2 target = team.ToWorld(zone.X, zone.Y);
            PlayerAgent receiver = null;
            float bestD = float.PositiveInfinity;
            foreach (var q in team.Players)
            {
                if (q.SentOff || q == taker || q.IsGoalkeeper) continue;
                float d = Vec2.Distance(q.Pos, target);
                if (d < bestD) { bestD = d; receiver = q; }
            }
            team.Stats.Crosses++;
            KickLofted(taker, target, _rng.Range(0.36f, 0.46f), KickKind.Cross, receiver, 0f, MathUtil.A((taker.Attr.Crossing * 2 + taker.Attr.FreeKicks) / 3), true);
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
            float targetSide = _rng.Chance(0.6f) ? nearSide : -nearSide;
            {
                // Right-footers bend it left, left-footers right: they aim for the side their curl brings the ball back to.
                Vec2 d0 = (new Vec2(Pitch.HalfLength * goalSign, targetSide * 2f) - Ball.Pos).Normalized;
                float needed = -MathF.Sign(Vec2.Cross(d0, new Vec2(0f, targetSide)));
                if (needed != NaturalCurlSign(taker) && _rng.Chance(0.8f)) targetSide = -targetSide;
            }
            Vec2 aim = new Vec2(Pitch.HalfLength * goalSign, targetSide * (Pitch.GoalHalfWidth - 0.4f - 0.6f * (1f - skill)));
            float aimZ = _rng.Range(1.5f, 2.2f);
            Vec2 from = Ball.Pos;
            Vec2 dir = (aim - from).Normalized;
            float power = 22f + 5f * skill;
            float curl = -MathF.Sign(Vec2.Cross(dir, new Vec2(0f, targetSide))) * _rng.Range(15f, 28f);
            // Aim the start line away from the target so the curl brings it back.
            Vec2 startDir = dir.Rotate(-MathF.Sign(curl) * 0.06f);
            float topSpin = _rng.Range(8f, 16f);
            float vz = KickSolver.VerticalSpeedFor(startDir * power, Pitch.BallRadius, Vec2.Distance(from, aim), aimZ, Config, curl, topSpin);
            float sigma = 0.012f + 0.045f * (1f - skill);
            startDir = startDir.Rotate(_rng.Gaussian(0f, sigma));
            vz += _rng.Gaussian(0f, 0.25f + 0.9f * (1f - skill));
            foreach (var w in r.Wall) StartJump(w, 0.4f); // the wall jumps
            float xg = MathUtil.Clamp(0.09f - (Vec2.Distance(from, aim) - 18f) * 0.004f, 0.02f, 0.1f);
            KickBall(taker, from, startDir * power, vz, curl, topSpin, KickKind.Shot, null, aim, true, false);
            Ball.IsShot = true;
            Ball.Shooter = taker;
            Ball.ShotSpeed = power;
            var keeper = team.Opponent.Goalkeeper;
            Ball.ShotGoalkeeperDistance = keeper != null ? Vec2.Distance(keeper.Pos, aim) : 3f;
            Ball.ShotOnTarget = true;
            team.Stats.Shots++;
            team.Stats.FreeKickShots++;
            _lastShotTime = SimTime;
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

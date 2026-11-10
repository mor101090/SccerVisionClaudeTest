using System;
using System.Collections.Generic;
using System.Linq;

namespace SoccerVision.Simulation
{
    /// <summary>Restarts - where everybody stands: the walls, the box at a corner, the legal positions at a penalty.</summary>
    public sealed partial class MatchEngine
    {
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
                var tac = t.Eff;
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
            // A long throw (a weapon against a deep, compact block): only when the thrower can reach the near post from here (polish
            // round 2: his release speed and the flight decide it) - and now and then a short one instead, when they expect the long one.
            r.LongThrow = r.Taker != null && att.Forward(r.Spot) > 12f && CanThrowIntoBox(r.Taker, r.Spot)
                          && _rng.Chance(r.Taker.Has(PlayerTrait.LongThrowSpecialist) ? LongThrowChanceSpecialist : LongThrowChanceOther);
            if (r.LongThrow) { LayoutLongThrow(r); return; }
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

        /// <summary>
        /// Long throw: one or two at the near post to flick it on across the six-yard box, a group at the far post for the
        /// flick and the rebound, one blocking the keeper, one at the edge of the box for the second ball. The defenders
        /// pack the box and pick them up goal-side.
        /// </summary>
        private void LayoutLongThrow(Restart r)
        {
            var att = r.Team;
            var def = att.Opponent;
            float side = MathF.Sign(att.Lateral(r.Spot));
            var spots = new[]
            {
                LongThrowTarget(r.Taker, r.Spot, out var aim) ? att.ToLocal(aim) - new Vec2(0f, side * 0.3f) : new Vec2(LongThrowTargetForward, side * LongThrowTargetLateral), // where the throw comes down: the flick-on
                new Vec2(47.6f, -side * 3.6f),   // far post
                new Vec2(43.5f, -side * 1.2f),   // arriving late in the middle
                new Vec2(50.2f, side * 0.4f),    // on the keeper (the block)
                new Vec2(36.5f, -side * 4f),     // edge of the box: the second ball
            };
            var cands = new List<PlayerAgent>();
            foreach (var p in att.Players) if (!p.SentOff && !p.IsGoalkeeper && p != r.Taker) cands.Add(p);
            cands.Sort((x, y) => (y.Attr.Heading + y.Attr.Jumping + (y.Profile.HeightCm - 180) * 1.2f).CompareTo(x.Attr.Heading + x.Attr.Jumping + (x.Profile.HeightCm - 180) * 1.2f));
            var box = new List<PlayerAgent>();
            for (int i = 0; i < cands.Count && i < spots.Length; i++)
            {
                // (the second-ball man is the best shooter of the rest, not the fifth header)
                var p = i == spots.Length - 1 ? cands.Skip(spots.Length - 1).OrderByDescending(q => q.Attr.LongShots + q.Attr.Passing).First() : cands[i];
                p.ShapeTarget = att.ToWorld(spots[i].X + _rng.Range(-0.5f, 0.5f), spots[i].Y + _rng.Range(-0.6f, 0.6f));
                if (i < 4) box.Add(p);
            }
            var defUsed = new HashSet<PlayerAgent>();
            foreach (var a in box)
            {
                var m = NearestTo(def, a.ShapeTarget, defUsed);
                if (m == null) break;
                defUsed.Add(m);
                m.ShapeTarget = a.ShapeTarget + (def.OwnGoal - a.ShapeTarget).Normalized * 1.1f;
            }
            // two more defenders zonal at the near post and on the six-yard line
            var z1 = NearestTo(def, def.ToWorld(-49.5f, -side * 2.5f), defUsed);
            if (z1 != null) { defUsed.Add(z1); z1.ShapeTarget = def.ToWorld(-49.5f, -side * 2.5f); }
            var z2 = NearestTo(def, def.ToWorld(-46.5f, 0f), defUsed);
            if (z2 != null) { defUsed.Add(z2); z2.ShapeTarget = def.ToWorld(-46.5f, 0f); }
        }

        private void LayoutGoalKick(Restart r)
        {
            var team = r.Team;
            var opp = team.Opponent;
            float tempo = team.Eff.Tempo;
            float kicking = r.Taker != null ? MathUtil.A(r.Taker.Attr.Kicking) : 0.5f;
            float pShort = MathUtil.Clamp(0.35f + 0.35f * (1f - tempo) + 0.2f * (1f - opp.Eff.PressingIntensity) - 0.25f * kicking
                                          + (r.Taker != null && r.Taker.PlayRole == PlayerRole.SweeperKeeper ? 0.25f : 0f), 0.1f, 0.85f); // (a sweeper-keeper plays it out from the back)
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

            // The routine: near post, far post, penalty spot, pulled back to the edge, or short (varied, like a real team).
            float pr = _rng.NextFloat();
            r.CornerRoutine = pr < 0.3f ? 1 : pr < 0.56f ? 2 : pr < 0.72f ? 3 : pr < 0.86f ? 4 : 5;
            // the manager's routine for this side (0: varied, as a real team)
            bool rightSide = att.Lateral(r.Spot) < 0f;
            int chosenRoutine = rightSide ? att.Tactics.CornerRoutineRight : att.Tactics.CornerRoutineLeft;
            if (chosenRoutine >= 1 && chosenRoutine <= 5) r.CornerRoutine = chosenRoutine;
            var zoneNumbers = rightSide ? att.Tactics.CornerZonesRight : att.Tactics.CornerZonesLeft;

            // Best headers attack the box (centre-backs go up), the rest guard against the counter.
            var candidates = new List<PlayerAgent>();
            foreach (var p in att.Players) if (!p.SentOff && !p.IsGoalkeeper && p != r.Taker) candidates.Add(p);
            // (the best header first - but only the best centre-back goes up with the strikers and the midfield, the other stays back)
            PlayerAgent bestCb = null;
            foreach (var c in candidates) if (c.Slot.IsCentreBack && (bestCb == null || c.Attr.Heading + c.Attr.Jumping > bestCb.Attr.Heading + bestCb.Attr.Jumping)) bestCb = c;
            float Header(PlayerAgent y) => y.Attr.Heading + y.Attr.Jumping + (y.Profile.HeightCm - 180) * 1.2f - (y.Slot.IsCentreBack && y != bestCb ? 45f : 0f);
            candidates.Sort((x, y) => Header(y).CompareTo(Header(x)));
            int zoneIndex = 0;
            var attackersInBox = new List<PlayerAgent>();
            // The manager's placing first: each zone with a name goes to that player, if he is on the pitch (and not the taker).
            var placed = new HashSet<PlayerAgent>();
            if (zoneNumbers != null)
                for (int zk = 0; zk < zoneNumbers.Length && zk < zones.Length; zk++)
                {
                    if (zoneNumbers[zk] <= 0) continue;
                    var chosen = candidates.Find(c => c.Number == zoneNumbers[zk]);
                    if (chosen == null) continue; // (substituted, injured, sent off or the taker: the zone is filled as usual)
                    chosen.ShapeTarget = att.ToWorld(zones[zk].X, zones[zk].Y + _rng.Range(-0.4f, 0.4f));
                    placed.Add(chosen);
                    if (zk < 4) attackersInBox.Add(chosen);
                }
            int stayBack = Math.Max(2, candidates.Count - zones.Length);
            for (int i = 0; i < candidates.Count; i++)
            {
                var p = candidates[i];
                if (placed.Contains(p)) continue;
                // (zones the manager filled are skipped)
                while (zoneIndex < zones.Length && zoneNumbers != null && zoneIndex < zoneNumbers.Length && zoneNumbers[zoneIndex] > 0 && candidates.Exists(c => c.Number == zoneNumbers[zoneIndex])) zoneIndex++;
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

            // A short corner: one man comes towards the taker to receive; a ball to the edge of the box: someone waits there.
            if (r.CornerRoutine == 5 || r.CornerRoutine == 4)
            {
                PlayerAgent pick = null;
                foreach (var p in candidates) if (!attackersInBox.Contains(p) && (pick == null || p.Attr.Passing + p.Attr.LongShots > pick.Attr.Passing + pick.Attr.LongShots)) pick = p;
                if (pick != null)
                    pick.ShapeTarget = r.CornerRoutine == 5 ? att.ToWorld(43.5f, side * 25.5f) : att.ToWorld(36.5f, -side * 1.5f + _rng.Range(-1f, 1f));
            }

            // The manager's own set-up (the taker stays at the flag): his spots, mirrored for a corner on the other side.
            float aSide = MathF.Sign(att.Lateral(r.Spot));
            if (att.Tactics.CornerSetupAttack.Length > 0)
            {
                foreach (var s in att.Tactics.CornerSetupAttack)
                {
                    var p = att.Players.Find(q => q.Number == s.Number && !q.SentOff && !q.IsGoalkeeper && q != r.Taker);
                    if (p == null) continue;
                    p.ShapeTarget = att.ToWorld(s.Forward, s.Lateral * aSide);
                }
                attackersInBox.Clear();
                foreach (var p in att.Players)
                    if (!p.SentOff && p != r.Taker && !p.IsGoalkeeper && Pitch.InPenaltyArea(p.ShapeTarget, att.AttackGoalSign)) attackersInBox.Add(p);
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
            foreach (var s in def.Tactics.CornerSetupDefence)
            {
                var d = def.Players.Find(q => q.Number == s.Number && !q.SentOff);
                if (d != null) d.ShapeTarget = def.ToWorld(s.Forward, s.Lateral * dSide);
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
    }
}

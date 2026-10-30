using System;
using System.Collections.Generic;
using System.Linq;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// Team behaviour: formation block that slides with the ball, defensive line, pressing (never more than two
    /// players on the ball, per the design document), supporting players, runs in behind and marking.
    /// </summary>
    public sealed partial class MatchEngine
    {
        // ---- Team shape ---------------------------------------------------------------------------------

        private void UpdateTeamShape(TeamAgent team)
        {
            var tac = team.Eff;
            var tac0 = team.Tactics;
            bool inPossession = PossessionTeam == team;
            float ballF = team.Forward(Ball.Pos);
            float ballL = team.Lateral(Ball.Pos);

            float lineTarget;
            if (inPossession)
            {
                // (the whole team plays higher with a high line, in possession too)
                lineTarget = ballF * 0.62f - 17f + 10f * (tac.Mentality - 0.5f) + 16f * (tac.DefensiveLine - 0.5f);
                lineTarget = MathUtil.Clamp(lineTarget, -41f, 3f);
            }
            else
            {
                // the defensive line: from a deep block on the edge of the box (0) to a high line near halfway (1)
                lineTarget = ballF * 0.45f - 22f + 22f * (tac.DefensiveLine - 0.5f);
                lineTarget = MathUtil.Clamp(lineTarget, -42f, -7f + 18f * (tac.DefensiveLine - 0.5f));
                // Step up together (squeeze the space, leave attackers offside) when the opponent has to turn back:
                // a carrier facing his own goal under pressure, or a ball played back towards their goal.
                float chem = team.DefensiveChemistry;
                var opp = Ball.Owner != null && Ball.Owner.Team != team && !Ball.InHands ? Ball.Owner : null;
                Vec2 ourAttack = team.DirToWorld(Vec2.UnitX);
                bool highBackline = tac0.Defence == DefenceStyle.HighBackline;
                float stepUp = highBackline ? 1.6f : 1f;
                if (opp != null && Vec2.Dot(opp.FacingDir, ourAttack) > 0.3f && Pressure(opp, out _) > 0.45f && ballF > lineTarget + 12f)
                    lineTarget += 5f * chem * stepUp;
                else if (Ball.Owner == null && Ball.LastTouchTeam != team && Vec2.Dot(Ball.Velocity, ourAttack) > 4f && ballF > lineTarget + 10f)
                    lineTarget += 6f * chem * stepUp;
                // Stay goal-side of the ball.
                lineTarget = MathF.Min(lineTarget, ballF - 2f);
                // Drop off when the ball carrier has time to play a ball over the top.
                if (Ball.Owner != null && Ball.Owner.Team != team && !Ball.InHands)
                {
                    float pressure = Pressure(Ball.Owner, out _);
                    if (pressure < 0.3f && ballF < lineTarget + 40f) lineTarget -= (tac0.Defence == DefenceStyle.OffsideTrap ? 2f : tac0.Defence == DefenceStyle.HighBackline ? 3.5f : 7f) * (1f - 0.6f * tac.DefensiveLine);
                }
                // A ball in the air over the line: everybody drops.
                if (Ball.Owner == null && Ball.LastTouchTeam != team && Ball.Z > 2.5f) lineTarget -= 6f;
                if (tac0.Defence == DefenceStyle.OffsideTrap)
                {
                    // The trap: the whole line takes a step forward together whenever the ball is in front of it, to leave the
                    // forwards offside; it does not drop off when the carrier has time. (Now and then one man misses the step.)
                    if (ballF > lineTarget + 6f) lineTarget += 5f;
                    lineTarget = MathF.Min(lineTarget, ballF - 3f);
                    if (team.TrapBrokenUntil < SimTime && ballF > lineTarget + 8f && _rng.Chance(0.012f))
                    {
                        var back = team.Players.Where(q => q.Slot.Line == Line.Defence && !q.SentOff && !q.IsGoalkeeper).ToList();
                        if (back.Count > 0) { team.TrapBreaker = back[_rng.Range(0, back.Count)]; team.TrapBrokenUntil = SimTime + 3.2f; if (CollectDiagnostics) Count("trap.broken"); }
                    }
                }
                if (highBackline)
                {
                    // Mistimed step-up: now and then one defender is late and splits the line (the price of a high line)
                    if (team.TrapBrokenUntil < SimTime && ballF > lineTarget + 8f && _rng.Chance(0.014f))
                    {
                        var back = team.Players.Where(q => q.Slot.Line == Line.Defence && !q.SentOff && !q.IsGoalkeeper).ToList();
                        if (back.Count > 0) { team.TrapBreaker = back[_rng.Range(0, back.Count)]; team.TrapBrokenUntil = SimTime + 3.4f; if (CollectDiagnostics) Count("highline.broken"); }
                    }
                    // A high backline: the back four squeeze right up towards halfway so the whole team is compact (about
                    // 25-30 m from front to back) - the pitch is shortened and the opponent has no time between the lines.
                    // The keeper sweeps the space behind it; the risk is the ball over the top and a slow defender in a race.
                    // (reference: the back four stand on the halfway line, the midfield 10-15 m ahead of them, the forwards
                    // up to 30 m ahead - "high line = high pressure")
                    float squeeze = -3f + 12f * (tac.DefensiveLine - 0.5f);
                    if (ballF > squeeze + 6f) lineTarget = MathF.Max(lineTarget, squeeze);
                    lineTarget = MathF.Min(lineTarget, ballF - 3f);
                }
            }
            lineTarget = MathF.Max(lineTarget, -47.5f);
            // The line moves like a unit: rate limited (steps up / drops at ~4.5 m/s).
            // A well-drilled line moves faster as one.
            team.LineForward = MathUtil.MoveTowards(team.LineForward, lineTarget, (3.6f + 1.8f * team.DefensiveChemistry) * 0.2f * (tac0.Defence == DefenceStyle.OffsideTrap ? 1.7f : tac0.Defence == DefenceStyle.HighBackline ? 1.45f : 1f));

            float offsideLine = MathF.Max(MathF.Max(SecondLastDefenderForward(team), ballF), 0f);

            foreach (var p in team.Players)
            {
                if (p.SentOff || p.IsGoalkeeper) continue;
                p.ShapeTarget = ComputeShapeTarget(p, team, inPossession, ballF, ballL, offsideLine);
            }
            // (in the opponent's half; a team keeping the ball builds on a stable base from further back)
            if (inPossession && ballF > (team.Tactics.Special == SpecialTactic.Possession ? -8f : 6f)) ApplyRestDefence(team, ballF);
            bool overload = inPossession && IsWideOverload(team, ballF, ballL);
            if (overload && !team.OverloadOn) team.Stats.Overloads++;
            team.OverloadOn = overload;

            if (inPossession) AssignSupport(team);
            else team.Supporters.Clear();
            AssignMarking(team, inPossession, ballF);
        }

        private readonly List<PlayerAgent> _rest = new List<PlayerAgent>(11);

        /// <summary>
        /// Rest defence: while the team attacks, enough players stay behind the ball to stop the counter the moment it is
        /// lost - normally five (a 2+3 or 3+2: the centre-backs, the holding man and the full-backs or a midfielder, the
        /// full-backs tucked into the half-spaces), fewer in a siege or a counter, more when protecting a lead.
        /// </summary>
        private void ApplyRestDefence(TeamAgent team, float ballF)
        {
            var s = team.Tactics;
            int need = 5;
            if (s.Special == SpecialTactic.Siege) need = 3;
            else if (team.CounterUntil > SimTime || team.Eff.Mentality > 0.78f) need = 4;
            if (s.Defence == DefenceStyle.ParkTheBus || s.Special == SpecialTactic.TimeWasting) need = 6;
            float behind = ballF - 7f;
            int have = 0;
            _rest.Clear();
            foreach (var p in team.Players)
            {
                if (p.SentOff || p.IsGoalkeeper || p == Ball.Owner) continue;
                if (team.Forward(p.ShapeTarget) < behind) { have++; continue; }
                if (p.Slot.Line == Line.Attack || p.OneTwoUntil > SimTime || p.RunTimer > 0f || p.ThirdManUntil > SimTime || team.Supporters.Contains(p)) continue;
                _rest.Add(p);
            }
            if (have >= need || _rest.Count == 0) return;
            // the deepest players in the formation hold first (centre-backs, full-backs, the holding man)
            var anchor = team.Anchor;
            _rest.Sort((a, b) => (a.Slot.Depth - (a == anchor ? 0.25f : 0f)).CompareTo(b.Slot.Depth - (b == anchor ? 0.25f : 0f)));
            for (int i = 0; i < _rest.Count && have < need; i++, have++)
            {
                var q = _rest[i];
                Vec2 loc = team.ToLocal(q.ShapeTarget);
                float l = q.Slot.IsFullBack ? MathF.Sign(loc.Y) * MathF.Min(MathF.Abs(loc.Y), 18f) : loc.Y; // (full-backs tuck into the half-spaces)
                q.ShapeTarget = team.ToWorld(MathF.Min(loc.X, behind - 2f), l);
                if (CollectDiagnostics) Count("rest.hold");
            }
        }

        /// <summary>The ball on a flank in the middle or final third, in our possession: the wide overload is on.</summary>
        private bool IsWideOverload(TeamAgent team, float ballF, float ballL) =>
            MathF.Abs(ballL) > 14f && ballF > -12f && ballF < 34f && Ball.Owner != null && Ball.Owner.Team == team;

        private readonly List<PlayerAgent> _markPool = new List<PlayerAgent>(11);
        private readonly List<PlayerAgent> _threats = new List<PlayerAgent>(11);

        /// <summary>Zone radius in zonal marking: an opponent inside it is this player's responsibility.</summary>
        private static float ZoneRadius(PlayerAgent p) => p.Slot.Line == Line.Defence ? 9f : 10.5f;

        /// <summary>One marker per attacker (no two defenders on the same man), by the team's marking system.</summary>
        private void AssignMarking(TeamAgent team, bool inPossession, float ballF)
        {
            foreach (var p in team.Players) p.PrevMarkTarget = p.MarkTarget;
            foreach (var p in team.Players) p.MarkTarget = null;
            if (inPossession)
            {
                // A new defensive phase gets new man-to-man assignments.
                foreach (var p in team.Players) p.ManAssignment = null;
                return;
            }
            if (team.Tactics.Marking == MarkingStyle.ManToMan) AssignManToMan(team, ballF);
            else AssignZonal(team, ballF);
            foreach (var p in team.Players)
                if (p.PrevMarkTarget != null && p.MarkTarget != null && p.PrevMarkTarget != p.MarkTarget) team.Stats.MarkingSwitches++;
        }

        /// <summary>
        /// Zonal marking: every defender and midfielder owns an area around his place in the block (which shifts with the
        /// ball). He takes the most dangerous opponent inside it and lets him go when he leaves - the next zone picks him up.
        /// </summary>
        private void AssignZonal(TeamAgent team, float ballF)
        {
            if (ballF > 12f) return; // ball deep in their half: hold the shape, no marking
            _threats.Clear();
            foreach (var o in team.Opponent.Players)
            {
                if (o.SentOff || o.IsGoalkeeper || o == Ball.Owner) continue;
                if (team.Forward(o.Pos) < 5f) _threats.Add(o);
            }
            Vec2 goal = team.OwnGoal;
            _threats.Sort((a, b) => Vec2.DistanceSq(a.Pos, goal).CompareTo(Vec2.DistanceSq(b.Pos, goal)));
            _markPool.Clear();
            foreach (var p in team.Players)
            {
                if (p.SentOff || p.IsGoalkeeper || p.IsDown || p == team.Presser || p == team.SecondPresser) continue;
                if (p.Slot.Line == Line.Attack) continue;
                _markPool.Add(p);
            }
            foreach (var a in _threats)
            {
                PlayerAgent best = null;
                float bestD = float.PositiveInfinity;
                foreach (var d in _markPool)
                {
                    // whose zone is he in?
                    float dist = Vec2.Distance(d.ShapeTarget, a.Pos);
                    if (dist < ZoneRadius(d) && dist < bestD) { bestD = dist; best = d; }
                }
                if (best == null) continue; // in between zones and nobody's: the block's shape covers him
                best.MarkTarget = a;
                _markPool.Remove(best);
                if (_markPool.Count == 0) break;
            }
        }

        /// <summary>
        /// Man-to-man marking: when the ball is lost, the four defenders take the four most advanced opponents and the
        /// midfielders the next ones (nearest first); each keeps his man for the whole defensive phase and follows him out
        /// of position. The strikers do not mark (they stay up and press the centre-backs).
        /// </summary>
        private void AssignManToMan(TeamAgent team, float ballF)
        {
            bool needBuild = true;
            foreach (var p in team.Players) if (p.ManAssignment != null) { needBuild = false; break; }
            var opponents = new List<PlayerAgent>();
            foreach (var o in team.Opponent.Players) if (!o.SentOff && !o.IsGoalkeeper) opponents.Add(o);
            // most advanced (closest to our goal) first
            opponents.Sort((a, b) => team.Forward(a.Pos).CompareTo(team.Forward(b.Pos)));
            if (needBuild)
            {
                var taken = new HashSet<PlayerAgent>();
                foreach (var line in new[] { Line.Defence, Line.Midfield })
                {
                    var markers = new List<PlayerAgent>();
                    foreach (var p in team.Players) if (!p.SentOff && !p.IsGoalkeeper && p.Slot.Line == line) markers.Add(p);
                    var group = new List<PlayerAgent>();
                    foreach (var o in opponents) { if (taken.Contains(o)) continue; group.Add(o); if (group.Count == markers.Count) break; }
                    // greedy nearest pairs inside the group
                    while (markers.Count > 0 && group.Count > 0)
                    {
                        PlayerAgent bm = null, bo = null;
                        float bd = float.PositiveInfinity;
                        foreach (var m in markers)
                            foreach (var o in group)
                            {
                                float d = Vec2.Distance(m.Pos, o.Pos) + 0.5f * MathF.Abs(team.Lateral(m.ShapeTarget) - team.Lateral(o.Pos));
                                if (d < bd) { bd = d; bm = m; bo = o; }
                            }
                        bm.ManAssignment = bo;
                        taken.Add(bo);
                        markers.Remove(bm);
                        group.Remove(bo);
                    }
                }
            }
            foreach (var p in team.Players)
            {
                if (p.SentOff || p.IsGoalkeeper || p.Slot.Line == Line.Attack) continue;
                var man = p.ManAssignment;
                if (man == null || man.SentOff)
                {
                    // his man was sent off (or none was left): pick up the nearest opponent nobody marks
                    p.ManAssignment = null;
                    float bd = 25f;
                    foreach (var o in opponents)
                    {
                        bool marked = false;
                        foreach (var q in team.Players) if (q.ManAssignment == o) { marked = true; break; }
                        if (marked) continue;
                        float d = Vec2.Distance(p.Pos, o.Pos);
                        if (d < bd) { bd = d; p.ManAssignment = o; }
                    }
                    man = p.ManAssignment;
                }
                // the ball deep in their half: hold the shape (the assignment is kept for when they come forward)
                if (ballF > 15f || man == null || p.IsDown || p == team.Presser || p == team.SecondPresser || man == Ball.Owner) continue;
                p.MarkTarget = man;
            }
        }

        private Vec2 ComputeShapeTarget(PlayerAgent p, TeamAgent team, bool inPossession, float ballF, float ballL, float offsideLine)
        {
            var tac = team.Eff;
            var slot = p.Slot;
            float line = team.LineForward;
            float blockLength = inPossession ? 34f + 10f * tac.Mentality + 5f * tac.Tempo : (team.Tactics.Defence == DefenceStyle.HighBackline ? 21f : 26f) + 5f * tac.DefensiveLine;
            // tactical width: how far apart the players spread across the pitch (narrow 0 .. wide 1)
            float widthScale = inPossession ? 1.12f + 0.55f * (tac.Width - 0.5f) : 0.74f + 0.28f * (tac.Width - 0.5f);
            float shift = inPossession ? 0.2f : 0.36f;

            float f = line + slot.Depth * blockLength;
            float l = slot.BaseLateral * widthScale + ballL * shift;

            if (slot.Line == Line.Defence)
            {
                bool invertedFullBacks = team.Tactics.Defence == DefenceStyle.InvertedFullBacks;
                if (inPossession && slot.IsFullBack && invertedFullBacks)
                {
                    // Inverted full-back: in possession he comes off the flank into midfield beside the holding man (a
                    // double pivot - with both inverted, a midfield three): numbers in the middle for the build-up and,
                    // when the ball is lost, already central to stop the counter. The winger keeps the width on his own.
                    float side = MathF.Sign(slot.BaseLateral);
                    f = MathUtil.Clamp(ballF - 11f, line + 7f, 14f);
                    l = side * (8f + 2.5f * tac.Width) + ballL * 0.15f;
                }
                else if (inPossession && slot.IsFullBack)
                {
                    // Fullbacks overlap the winger on the ball side (design doc) and push up anyway. How far they go is
                    // the team's choice (mentality) and the player's: a quick, fit full-back who crosses well goes forward
                    // far more, and leaves a bigger hole behind him.
                    float side = MathF.Sign(slot.BaseLateral);
                    bool ballSide = MathF.Sign(ballL) == side && MathF.Abs(ballL) > 6f;
                    float drive = FullBackDrive(p);
                    float push = (0.35f + 0.65f * tac.Mentality) * (0.55f + 0.45f * drive);   // 0 .. 1
                    f += 4f + (ballSide && ballF > -5f ? 12f * (0.5f + tac.Mentality) * (0.65f + 0.7f * drive) : 0f);
                    l = side * (22f + 11f * tac.Width);
                    if (ballSide && ballF > 22f && push > 0.5f)
                    {
                        // Attacking full-back in the final third: up beside the winger and beyond the ball, into the by-line.
                        f = MathF.Max(f, MathF.Min(ballF + 3f + 5f * push, 38f + 8f * push));
                        l = side * (24f + 3f * tac.Width);
                    }
                    else if (!ballSide && ballF > 8f)
                    {
                        // Far-side full-back tucks in and holds the line (balance): he is the safety for the counter.
                        f = MathF.Min(f, ballF - 10f + 8f * push);
                        l = side * (17f + 3f * tac.Width);
                    }
                }
                if (inPossession && slot.IsCentreBack) f = MathF.Min(f, 0f); // CBs: no further than halfway
                // (with the full-backs inside, the centre-backs split wider to cover the flanks behind them)
                if (inPossession && slot.IsCentreBack && invertedFullBacks) l = MathF.Sign(slot.BaseLateral == 0f ? 1f : slot.BaseLateral) * (11f + 3f * tac.Width) + ballL * 0.2f;
                if (!inPossession && slot.IsFullBack) f += 1.5f * (1f - team.DefensiveChemistry); // a well-drilled line is flat
            }
            else if (slot.Line == Line.Midfield)
            {
                if (inPossession && slot.IsWideMidfielder)
                {
                    f += 6f;
                    l = MathF.Sign(slot.BaseLateral) * (24f + 10f * tac.Width);
                    // Inverted winger (strong foot on the inside) tucks in when the ball is in the final third.
                    bool inverted = (slot.BaseLateral > 0f) == (p.Attr.StrongFoot == Foot.Right);
                    if (team.Tactics.Defence == DefenceStyle.InvertedFullBacks) l = MathF.Sign(slot.BaseLateral) * (27f + 4f * tac.Width); // he is the width now
                    else if (inverted && ballF > 15f) l *= 0.72f;
                }
                if (!inPossession && slot.IsWideMidfielder) f -= 1.5f;
                // Out of possession the midfield stays goal-side of the ball (screens the back four).
                if (!inPossession) f = MathF.Min(f, MathF.Max(ballF + 2.5f, line + 4f));
                if (p == team.Anchor)
                {
                    if (!inPossession)
                    {
                        // The holding midfielder sits just in front of the back four, in the middle, towards the ball.
                        f = MathF.Min(f, line + 7f);
                        l = MathUtil.Clamp(ballL * 0.45f, -10f, 10f);
                    }
                    else
                    {
                        // ...and in possession he stays behind the ball as the safe pass back and the pivot of the build-up.
                        f = MathF.Min(f, MathF.Max(ballF - 7f, line + 5f));
                        l = MathUtil.Clamp(ballL * 0.35f, -9f, 9f);
                    }
                }
            }
            else if (slot.Line == Line.Attack)
            {
                if (inPossession)
                {
                    f = MathF.Max(f, ballF - 4f);
                    float hug = 1.5f * MathF.Sin(SimTime * 0.9f + p.NoisePhaseA * 3f) * (1.3f - MathUtil.A(p.Attr.OffTheBall));
                    f = MathF.Min(f, offsideLine - 0.25f + hug);
                    l = slot.BaseLateral * (0.9f + 0.5f * tac.Width) + ballL * 0.25f;
                }
                else
                {
                    // Strikers stay higher to start counters and screen the opponent's build-up. The block moves as one:
                    // with a high line the forwards press up near the opponents' defenders and the midfield follows
                    // (a compact high block, about 30 m from back to front); with a deep block they drop in front of
                    // the midfield.
                    float high = tac.DefensiveLine - 0.5f;
                    f = MathF.Max(f, -14f + 26f * high);
                    f = MathF.Max(f, ballF - 14f + 5f * high);
                    f = MathF.Min(f, line + 36f);
                    l = slot.BaseLateral * 0.9f + ballL * 0.3f;
                }
            }

            // The player's role on the shape.
            float sideOf = MathF.Sign(slot.BaseLateral == 0f ? 1f : slot.BaseLateral);
            switch (p.PlayRole)
            {
                case PlayerRole.CoverDefender:
                    // a couple of metres behind his partner, sweeping up behind the line; never far up in possession
                    if (!inPossession) f -= 2.2f; else f = MathF.Min(f, line + 1f);
                    break;
                case PlayerRole.DefensiveFullBack:
                    if (inPossession) { f = MathF.Min(f, line + 8f); l = sideOf * MathF.Min(MathF.Abs(l), 24f); }
                    break;
                case PlayerRole.InvertedFullBack:
                    if (inPossession) { f = MathUtil.Clamp(ballF - 11f, line + 7f, 14f); l = sideOf * (8f + 2.5f * tac.Width) + ballL * 0.15f; }
                    break;
                case PlayerRole.WingBack:
                    if (inPossession) { f += 8.5f; l = sideOf * (25f + 6f * tac.Width); } // (high and wide, the whole flank is his)
                    break;
                case PlayerRole.AnchorMan:
                    f = MathF.Min(f, line + (inPossession ? 9f : 6f));
                    l = MathUtil.Clamp(l, -9f, 9f);
                    break;
                case PlayerRole.DeepLyingPlaymaker:
                    if (inPossession) { f = MathF.Min(f, MathF.Max(ballF - 9f, line + 4f)); l = MathUtil.Clamp(ballL * 0.3f, -8f, 8f); }
                    break;
                case PlayerRole.BoxToBoxDefensive:
                    if (inPossession && ballF > -5f) f = MathF.Max(f, MathF.Min(ballF - 1f, offsideLine - 3f));
                    break;
                case PlayerRole.Carrier:
                    if (inPossession) f += 3f;
                    break;
                case PlayerRole.TempoSetter:
                    if (inPossession) f = MathF.Min(f, ballF - 3f);
                    break;
                case PlayerRole.WidePlaymaker:
                    if (inPossession) l = (MathF.Abs(ballL) > 6f ? MathF.Sign(ballL) : sideOf) * 15f;
                    break;
                case PlayerRole.ShadowStriker:
                    if (inPossession) f = MathF.Max(f, MathF.Min(ballF + 5f, offsideLine - 0.8f));
                    break;
                case PlayerRole.ClassicPlaymaker:
                    if (inPossession) f = MathF.Min(MathF.Max(f, ballF + 2f), offsideLine - 8f); // between the lines
                    break;
                case PlayerRole.ClassicWinger:
                    if (inPossession) l = sideOf * (28f + 3f * tac.Width);
                    break;
                case PlayerRole.InvertedWinger:
                    if (inPossession && ballF > 5f) l = sideOf * MathF.Min(MathF.Abs(l), 15f + 3f * tac.Width);
                    break;
                case PlayerRole.WideTargetMan:
                    if (!inPossession) f -= 4f; // he works back
                    break;
                case PlayerRole.TargetForward:
                    if (inPossession) f = MathF.Max(f, offsideLine - 1.2f); // always the highest, the outlet for the long ball
                    break;
                case PlayerRole.SecondStriker:
                    if (inPossession) f = MathF.Min(f, offsideLine - 7f);
                    break;
                case PlayerRole.FalseNine:
                    if (inPossession) f = MathF.Min(f, MathF.Max(ballF - 2f, offsideLine - 16f)); // drops into midfield and drags a centre-back with him
                    break;
            }

            // Box presence when a cross is on (ball wide in the final third).
            bool crossy = team.Tactics.Attack == AttackStyle.CrossesIntoBox || team.Tactics.Special == SpecialTactic.Siege;
            if (inPossession && ballF > (crossy ? 18f : 24f) && MathF.Abs(ballL) > (crossy ? 9f : 12f) && Ball.Owner != null && Ball.Owner.Team == team && Ball.Owner != p)
            {
                float side = MathF.Sign(ballL);
                if (slot.Line == Line.Attack)
                {
                    bool near = MathF.Sign(slot.BaseLateral) == side;
                    f = near ? 46.5f : 43f;
                    l = near ? side * 3f : -side * 2f;
                    f = MathF.Min(f, offsideLine - 0.6f);
                }
                else if (slot.IsWideMidfielder && MathF.Sign(slot.BaseLateral) != side)
                {
                    f = MathF.Min(46f, offsideLine - 0.6f);
                    l = -side * 5f;
                }
                else if (slot.IsCentralMidfielder && MathF.Sign(slot.BaseLateral) == side)
                {
                    f = 34f;
                    l = side * 4f;
                }
            }

            // Wide overload: with the ball on a flank the ball-side central midfielder joins the full-back and the winger in
            // the half-space (3 v 2 there, the third man for combinations), while the far winger holds the far touchline
            // for the switch - overload one side to isolate him one against one on the other.
            if (inPossession && Ball.Owner != p && IsWideOverload(team, ballF, ballL))
            {
                float side = MathF.Sign(ballL);
                if (slot.IsCentralMidfielder && p != team.Anchor && MathF.Sign(slot.BaseLateral) == side)
                {
                    f = MathUtil.Clamp(ballF + 1f, line + 8f, offsideLine - 1.5f);
                    l = side * 14f;
                }
                else if (slot.IsWideMidfielder && MathF.Sign(slot.BaseLateral) == -side)
                {
                    f = MathF.Max(f, MathF.Min(ballF - 4f, offsideLine - 1f));
                    l = -side * (27f + 3f * tac.Width);
                }
            }

            // The chosen styles on the shape.
            var style = team.Tactics;
            if (style.Defence == DefenceStyle.ParkTheBus)
            {
                // A bunker: nobody ahead of the ball, the midfield on the back line's shoulder, one forward left up.
                if (!inPossession) f = MathF.Min(f, line + (slot.Line == Line.Midfield ? 9f : slot.Line == Line.Attack ? 17f : 6f));
                else f = MathF.Min(f, MathF.Max(ballF - 5f, line + 22f));
            }
            if (style.Special == SpecialTactic.Siege && inPossession && ballF > 14f)
            {
                // Relentless pressure: everybody up round the box, the centre-backs at halfway, the midfield at the edge of the box.
                if (slot.Line == Line.Midfield && !slot.IsWideMidfielder) f = MathF.Max(f, MathF.Min(ballF - 2f, offsideLine - 2f));
                if (slot.IsFullBack) f += 6f;
                if (slot.IsCentreBack) f = MathF.Max(f, MathF.Min(6f, ballF - 16f));
            }
            if (team.CounterUntil > SimTime && inPossession && !slot.IsCentreBack && !slot.IsFullBack)
            {
                // The counter: the players ahead of the ball break forward at once and spread out.
                f = MathF.Min(MathF.Max(f, ballF + (slot.Line == Line.Attack ? 6f : 2f)), offsideLine - 0.3f);
            }

            // Balance: out of possession the far-side fullback tucks in, so there is no gap through the middle.
            if (!inPossession && slot.IsFullBack && MathF.Abs(ballL) > 8f && MathF.Sign(ballL) != MathF.Sign(slot.BaseLateral))
                l = MathF.Sign(slot.BaseLateral) * MathF.Min(MathF.Abs(l), 13f + 4f * tac.Width);

            // Slow organic wobble so lines don't look robotic (+/- ~1.5 m). A back line out of possession stays flat -
            // the better the understanding between the defenders, the flatter.
            float depthWobble = !inPossession && slot.Line == Line.Defence ? 1.6f * (1f - team.DefensiveChemistry) : 1.4f;
            f += depthWobble * MathF.Sin(SimTime * 0.23f + p.NoisePhaseA);
            l += 1.6f * MathF.Sin(SimTime * 0.19f + p.NoisePhaseB);

            f = MathUtil.Clamp(f, -50.5f, 50f);
            l = MathUtil.Clamp(l, -32.5f, 32.5f);
            return team.ToWorld(f, l);
        }

        // ---- Supporting players -----------------------------------------------------------------------

        private void AssignSupport(TeamAgent team)
        {
            team.Supporters.Clear();
            var carrier = Ball.Owner != null && Ball.Owner.Team == team ? Ball.Owner : null;
            Vec2 focus = carrier != null ? carrier.Pos : Ball.Pos;
            PlayerAgent first = null, second = null;
            float d1 = float.PositiveInfinity, d2 = float.PositiveInfinity;
            foreach (var p in team.Players)
            {
                if (p.SentOff || p.IsGoalkeeper || p == carrier || p == Ball.IntendedReceiver) continue;
                float d = Vec2.DistanceSq(p.Pos, focus);
                // Central midfielders always want the ball: they come to show for it from further away.
                if (p.Slot.IsCentralMidfielder) d *= 0.4f;
                if (d < d1) { second = first; d2 = d1; first = p; d1 = d; }
                else if (d < d2) { second = p; d2 = d; }
            }
            if (first != null) team.Supporters.Add(first);
            if (second != null) team.Supporters.Add(second);
        }

        /// <summary>
        /// Best support spot near the carrier (after Buckland's "Simple Soccer" support-spot calculator):
        /// safe passing lane, good distance, space, threat (xT) and onside.
        /// </summary>
        private Vec2 BestSupportSpot(PlayerAgent supporter, PlayerAgent carrier, Vec2 avoid)
        {
            var team = supporter.Team;
            Vec2 cLocal = team.ToLocal(carrier.Pos);
            float offsideLine = MathF.Max(MathF.Max(SecondLastDefenderForward(team), team.Forward(Ball.Pos)), 0f);
            Vec2 best = supporter.ShapeTarget;
            float bestScore = float.NegativeInfinity;
            Vec2 shapeLocal = team.ToLocal(supporter.ShapeTarget);
            for (int ring = 0; ring < 2; ring++)
            {
                float radius = ring == 0 ? 11f : 18f;
                for (int k = 0; k < 8; k++)
                {
                    float ang = -1.35f + k * (2.7f / 7f); // forward-facing fan, local frame
                    Vec2 local = cLocal + new Vec2(MathF.Cos(ang), MathF.Sin(ang)) * radius;
                    // Bias the fan towards the supporter's own zone.
                    local = Vec2.Lerp(local, shapeLocal, 0.25f);
                    if (local.X > offsideLine - 0.8f) local = new Vec2(offsideLine - 0.8f, local.Y);
                    if (MathF.Abs(local.Y) > 32f || local.X < -50f || local.X > 50f) continue;
                    Vec2 world = team.ToWorld(local.X, local.Y);

                    float nearestOpp = NearestOpponentDistance(team, world);
                    float laneClear = LaneClearance(team, carrier.Pos, world);
                    float value = ExpectedThreat.At(local.X, local.Y);
                    float distFromShape = Vec2.Distance(local, shapeLocal);
                    float score = value * 60f
                                  + MathUtil.Clamp(laneClear, 0f, 6f) * 0.35f
                                  + MathUtil.Clamp(nearestOpp, 0f, 10f) * 0.2f
                                  - MathF.Max(0f, distFromShape - 10f) * 0.12f
                                  - (Vec2.Distance(world, avoid) < 6f ? 2f : 0f);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = world;
                    }
                }
            }
            return best;
        }

        private float NearestOpponentDistance(TeamAgent team, Vec2 point)
        {
            float best = float.PositiveInfinity;
            foreach (var o in team.Opponent.Players)
            {
                if (o.SentOff) continue;
                float d = Vec2.Distance(o.Pos, point);
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>Smallest distance from an opponent to the segment from-&gt;to (excluding its very ends).</summary>
        private float LaneClearance(TeamAgent team, Vec2 from, Vec2 to)
        {
            float best = float.PositiveInfinity;
            foreach (var o in team.Opponent.Players)
            {
                if (o.SentOff) continue;
                float d = Vec2.DistanceToSegment(o.Pos, from, to, out float t);
                if (t < 0.05f || t > 0.97f) continue;
                if (d < best) best = d;
            }
            return best;
        }

        // ---- Ball duties: chaser, presser, receivers ---------------------------------------------------

        private void AssignBallDuties()
        {
            foreach (var t in Teams)
            {
                t.Chaser = null;
                t.Cover = null;
                t.Anticipator = null;
                t.Shielder = null;
                if (t.Presser != null && (t.Presser.SentOff || t.Presser.IsDown || Ball.Owner == null)) t.Presser = null;
                t.SecondPresser = null;
            }

            if (Ball.Owner != null)
            {
                var carrier = Ball.Owner;
                var def = carrier.Team.Opponent;
                if (Ball.InHands)
                {
                    def.Presser = null;
                    return;
                }
                PlayerAgent best = null, second = null;
                float t1 = float.PositiveInfinity, t2 = float.PositiveInfinity;
                Vec2 goalSide = carrier.Pos + (def.OwnGoal - carrier.Pos).Normalized * 1.2f;
                foreach (var d in def.Players)
                {
                    if (d.SentOff || d.IsGoalkeeper || d.IsDown) continue;
                    float t = d.TimeToReach(goalSide);
                    if (d == def.Presser) t *= 0.8f; // hysteresis: keep the same presser
                    // In the middle third the central midfielders go and win it (the holding one first).
                    float carrierF = def.Forward(carrier.Pos);
                    if (d.Slot.IsCentralMidfielder && carrierF > -28f && carrierF < 18f) t *= d == def.Anchor ? 0.8f : 0.9f;
                    t *= RolePressFactor(d);
                    if (t < t1) { second = best; t2 = t1; best = d; t1 = t; }
                    else if (t < t2) { second = d; t2 = t; }
                }
                // staged match: the cautioned player goes at the ball carrier (he is about to commit his second foul)
                if (_scriptPresser != null && !_scriptPresser.SentOff && !_scriptPresser.IsDown && _scriptPresser.Team == def)
                {
                    def.Presser = _scriptPresser;
                    AssignCover(def, carrier);
                    return;
                }
                var tac = def.Eff;
                // Pressing trigger: how far from his zone a player will engage the carrier (a high block engages earlier).
                float engage = 8f + 14f * tac.PressingIntensity + 6f * (tac.DefensiveLine - 0.5f);
                // Counter-press: for the first seconds after losing the ball, the players round it swarm the new carrier
                // at once (before the opponent can look up), the second man too - the best moment to win it back.
                bool counterPress = SimTime - def.LostBallAt < CounterPressWindow(def) && Vec2.Distance(carrier.Pos, def.LostBallSpot) < 22f;
                if (counterPress)
                {
                    engage += 12f;
                    if (def.CounterPressCountedFor != def.LostBallAt) { def.CounterPressCountedFor = def.LostBallAt; def.Stats.CounterPresses++; }
                }
                if (best != null)
                {
                    float fromZone = Vec2.Distance(best.ShapeTarget, carrier.Pos);
                    bool inOwnThird = def.Forward(carrier.Pos) < -18f;
                    if (fromZone < engage || inOwnThird || t1 < 1.2f) def.Presser = best;
                    else def.Presser = null;
                }
                if (def.Presser != null && second != null && (tac.PressingIntensity > 0.3f || counterPress) && t2 < 1.4f + 1.6f * tac.PressingIntensity + (counterPress ? 1.2f : 0f))
                {
                    def.SecondPresser = second; // never more than two on the ball
                }
                AssignCover(def, carrier);
                return;
            }

            // Free ball: each team sends its fastest interceptor; the intended receiver always goes.
            foreach (var t in Teams)
            {
                PlayerAgent best = null;
                float bestT = float.PositiveInfinity;
                foreach (var p in t.Players)
                {
                    if (p.SentOff || p.IsDown) continue;
                    if (p.IsGoalkeeper && !GoalkeeperMayChase(p)) continue;
                    UpdateIntercept(p);
                    float tt = p.InterceptTime;
                    if (p.IsGoalkeeper) tt -= 0.3f; // keepers take responsibility in their area
                    if (tt < bestT) { bestT = tt; best = p; }
                }
                var receiver = Ball.IntendedReceiver;
                if (receiver != null && receiver.Team == t && !receiver.SentOff)
                {
                    UpdateIntercept(receiver);
                    t.Chaser = receiver;
                }
                else
                {
                    t.Chaser = best;
                }
            }
            AssignAnticipation();
            AssignShielding();
        }

        /// <summary>How eagerly a role goes to the ball carrier (a time multiplier: below 1 he goes sooner).</summary>
        private static float RolePressFactor(PlayerAgent d)
        {
            switch (d.PlayRole)
            {
                case PlayerRole.Destroyer: return 0.85f;
                case PlayerRole.Carrier: case PlayerRole.WideTargetMan: case PlayerRole.BoxToBoxDefensive: return 0.9f;
                case PlayerRole.AnchorMan: case PlayerRole.CoverDefender: return 1.2f;
                case PlayerRole.FalseNine: case PlayerRole.ClassicPlaymaker: case PlayerRole.TempoSetter: return 1.1f;
                case PlayerRole.Stopper: return 0.9f;
                default: return 1f;
            }
        }

        /// <summary>How long the counter-press lasts after losing the ball (sim seconds): longer for a pressing team, hardly at all for a bunker.</summary>
        private static float CounterPressWindow(TeamAgent team)
        {
            float w = 1.6f + 3f * team.Eff.PressingIntensity;
            if (team.Tactics.Defence == DefenceStyle.ParkTheBus || team.Tactics.Special == SpecialTactic.TimeWasting) w *= 0.3f;
            return w;
        }

        /// <summary>
        /// Cover (the second defender): when the ball carrier is pressed in or near our half, the nearest team-mate
        /// takes up a position diagonally behind the presser, on the inside, so that a carrier who beats the first man
        /// runs into the second. "Pressure, cover, balance" - the rest of the line stays flat and tucks in.
        /// </summary>
        private void AssignCover(TeamAgent team, PlayerAgent carrier)
        {
            var presser = team.Presser;
            if (presser == null) return;
            float carrierF = team.Forward(carrier.Pos);
            if (carrierF > 12f) return; // pressing high up the pitch: the shape behind is the cover
            Vec2 toGoal = (team.OwnGoal - carrier.Pos).Normalized;
            float lat = team.Lateral(carrier.Pos);
            Vec2 inward = MathF.Abs(lat) < 4f ? Vec2.Zero : team.DirToWorld(new Vec2(0f, -MathF.Sign(lat)));
            Vec2 spot = Pitch.ClampToField(carrier.Pos + toGoal * 6f + inward * 4.5f, -1f);
            bool deep = carrierF < 0f;
            PlayerAgent best = null;
            float bestD = 16f;
            foreach (var p in team.Players)
            {
                if (p.SentOff || p.IsGoalkeeper || p.IsDown || p == presser || p == team.SecondPresser) continue;
                if (p.Slot.Line == Line.Attack || (deep && p.Slot.Line != Line.Defence)) continue;
                float d = Vec2.Distance(p.Pos, spot);
                if (d < bestD) { bestD = d; best = p; }
            }
            if (best == null) return;
            team.Cover = best;
            team.CoverSpot = spot;
            if (CollectDiagnostics) Count("cover");
        }

        /// <summary>
        /// Reading the game: when a pass is played to an opponent, his marker (or the nearest defender) judges whether
        /// he can get to the ball first and, if so, steps in front of him to intercept instead of waiting goal-side.
        /// Better positioning and decisions mean he reads it sooner - and gambles less.
        /// </summary>
        private void AssignAnticipation()
        {
            var receiver = Ball.IntendedReceiver;
            if (receiver == null || receiver.SentOff || Ball.Owner != null) return;
            var team = receiver.Team.Opponent;
            PlayerAgent marker = null;
            float bestD = 7f;
            foreach (var p in team.Players)
            {
                if (p.SentOff || p.IsGoalkeeper || p.IsDown) continue;
                float d = Vec2.Distance(p.Pos, receiver.Pos);
                if (p.MarkTarget == receiver) d -= 3f;
                if (p.Slot.IsCentralMidfielder) d -= 2f;
                if (d < bestD) { bestD = d; marker = p; }
            }
            if (marker == null) return;
            // In his own third a back does not gamble at all: if he misses, the striker is through on goal. He stays
            // between his man and the goal and defends the next touch.
            if (marker.Slot.Line == Line.Defence && team.Forward(receiver.Pos) < -17f) return;
            UpdateIntercept(marker);
            UpdateIntercept(receiver);
            float read = 0.5f * MathUtil.A(marker.Attr.Positioning) + 0.3f * MathUtil.A(marker.Attr.Decisions) + 0.2f * MathUtil.A(marker.Attr.Marking);
            if (marker.Slot.IsCentralMidfielder) read += marker == team.Anchor ? 0.25f : 0.12f; // midfielders live off reading passes
            // a creative passer is unpredictable: his passes are harder to read
            var passer = Ball.LastToucher;
            if (passer != null && passer.Team != team) read -= 0.3f * (MathUtil.A(passer.Attr.Creativity) - 0.5f);
            // A good reader goes when he is sure to get there first; a poor one too late, or gambles and misses.
            // He only goes when he is sure to get there first (a good reader a little earlier). A failed gamble leaves
            // his man free; in his own third it leaves a striker clean through.
            float margin = -0.1f + 0.15f * read;
            float caution = receiver.Team.Forward(receiver.Pos) > 18f ? 0.45f : 0.3f;
            if (marker.InterceptTime < receiver.InterceptTime + margin - caution)
            {
                team.Anticipator = marker;
                _diagAnticipateAt[team.Index] = SimTime;
                if (marker.AnticipatedAt < Ball.LastKickTime && CollectDiagnostics) Count("anticipate");
                marker.AnticipatedAt = SimTime;
            }
        }

        /// <summary>
        /// Shielding the ball out: a ball rolling out of play that would give us the restart (goal kick, throw-in) is
        /// not played - the nearest defender puts his body between it and the opponent chasing it and escorts it out.
        /// </summary>
        private void AssignShielding()
        {
            if (Ball.Owner != null || Ball.Z > 0.5f || Ball.LastTouchTeam == null) return;
            if (_trajectoryDirty) return; // only on a fresh prediction
            Vec2 end = _trajectory.FinalPosition;
            // Only when it is clearly going out (well past the line, and not far from it now): a ball that stops short
            // would be left for the attacker.
            bool outOver = MathF.Abs(end.X) > Pitch.HalfLength + 2.5f || MathF.Abs(end.Y) > Pitch.HalfWidth + 2.5f;
            if (!outOver) return;
            float toLine = MathF.Min(Pitch.HalfLength - MathF.Abs(Ball.Pos.X), Pitch.HalfWidth - MathF.Abs(Ball.Pos.Y));
            if (toLine > 12f) return;
            float speed = Ball.Velocity.Length;
            if (speed < 1.5f || speed > 11f) return;
            var team = Ball.LastTouchTeam.Opponent; // the team that gets the restart
            var opp = Ball.LastTouchTeam;
            // the opponent who would reach it first
            PlayerAgent chaser = null;
            float oppT = float.PositiveInfinity;
            foreach (var o in opp.Players)
            {
                if (o.SentOff || o.IsDown || (o.IsGoalkeeper && !GoalkeeperMayChase(o))) continue;
                UpdateIntercept(o);
                if (o.InterceptTime < oppT) { oppT = o.InterceptTime; chaser = o; }
            }
            if (chaser == null || oppT > 4f) return; // nobody can keep it in: just let it go
            PlayerAgent best = null;
            float bestT = float.PositiveInfinity;
            foreach (var p in team.Players)
            {
                if (p.SentOff || p.IsDown || p.IsGoalkeeper) continue;
                float t = p.TimeToReach(Ball.Pos + Ball.Velocity * 0.4f);
                if (t < bestT) { bestT = t; best = p; }
            }
            // He must be there first, and strong enough to hold the chaser off.
            if (best == null || bestT > oppT + 0.15f) return;
            team.Shielder = best;
            _diagShieldAt[team.Index] = SimTime;
            if (team.Chaser == best) team.Chaser = null;
        }

        /// <summary>Where the shielding player stands: just behind the ball, between it and the opponent chasing it.</summary>
        private Vec2 ShieldSpot(PlayerAgent p)
        {
            var opp = p.Team.Opponent;
            PlayerAgent chaser = null;
            float best = float.PositiveInfinity;
            foreach (var o in opp.Players)
            {
                if (o.SentOff || o.IsGoalkeeper) continue;
                float d = Vec2.DistanceSq(o.Pos, Ball.Pos);
                if (d < best) { best = d; chaser = o; }
            }
            Vec2 lead = Ball.Pos + Ball.Velocity * 0.25f;
            Vec2 from = chaser != null ? (chaser.Pos - lead) : -Ball.Velocity;
            if (from.LengthSq < 0.01f) from = -Ball.Velocity;
            return lead + from.Normalized * 0.75f;
        }

        private bool GoalkeeperMayChase(PlayerAgent gk)
        {
            UpdateIntercept(gk);
            Vec2 point = gk.InterceptPoint;
            if (Pitch.InPenaltyArea(point, gk.Team.OwnGoalSign)) return true;

            // Sortie: rush out of the box to sweep up a ball played in behind the line (keeper arrives first,
            // no defender is nearer, the ball is not too far from goal).
            var team = gk.Team;
            float depth = Pitch.HalfLength - point.X * team.OwnGoalSign;
            var role = gk.PlayRole;
            float range = role == PlayerRole.SweeperKeeper ? 24f : role == PlayerRole.ShotStopper ? 8f : 16f; // a sweeper-keeper comes far out, a shot-stopper stays home
            if (depth > Pitch.PenaltyAreaDepth + range || MathF.Abs(point.Y) > Pitch.PenaltyAreaHalfWidth + 4f) return false;
            if (Ball.IntendedReceiver != null && Ball.IntendedReceiver.Team == team) return false;
            float own = gk.TimeToReach(point);
            float rival = float.PositiveInfinity, mate = float.PositiveInfinity;
            foreach (var p in AllPlayers)
            {
                if (p.SentOff || p == gk) continue;
                float t = p.TimeToReach(point);
                if (p.Team == team) mate = MathF.Min(mate, t);
                else rival = MathF.Min(rival, t);
            }
            bool go = own + (role == PlayerRole.SweeperKeeper ? 0f : 0.1f) < rival && own < mate + (role == PlayerRole.SweeperKeeper ? 0.45f : 0.25f);
            if (go && SimTime - gk.LastSortieTime > 4f)
            {
                gk.LastSortieTime = SimTime;
                team.Stats.Sorties++;
            }
            return go;
        }

        /// <summary>Earliest point on the predicted ball path this player can reach (pitch-control style).</summary>
        private void UpdateIntercept(PlayerAgent p)
        {
            if (p.InterceptVersion == _trajectory.Version && p.InterceptRecalc > 0f) return;
            // Nobody reads a new ball path instantly: players who did not expect it (opponents of the
            // kicker, everyone after a deflection) keep their previous plan for their reaction time.
            bool expected = _ballChangeExpectedBy == p.Team || Ball.IntendedReceiver == p;
            if (!expected && SimTime - _ballChangedAt < p.ReactionTime && p.InterceptVersion >= 0) return;
            p.InterceptVersion = _trajectory.Version;
            p.InterceptRecalc = 0.12f;
            float elapsed = _trajectoryAge;
            float reach = p.IsGoalkeeper ? p.GoalkeeperReach : p.HeadReach;
            int count = _trajectory.Count;
            float lastT = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = _trajectory.TimeAt(i) - elapsed;
                if (t < 0f) continue;
                Vec2 pos = _trajectory.PositionAt(i);
                if (!Pitch.InsideField(pos, 0.4f))
                {
                    // Ball is going out: run to where it leaves the pitch.
                    p.InterceptPoint = Pitch.ClampToField(pos, -0.3f);
                    p.InterceptTime = MathF.Max(t, p.TimeToReach(p.InterceptPoint)) + 0.5f;
                    return;
                }
                lastT = t;
                float z = _trajectory.HeightAt(i);
                if (z > reach) continue;
                float tp = p.TimeToReach(pos);
                if (tp <= t + 0.05f)
                {
                    p.InterceptPoint = pos;
                    p.InterceptTime = t;
                    return;
                }
            }
            Vec2 final = Pitch.ClampToField(_trajectory.FinalPosition, -0.3f);
            p.InterceptPoint = final;
            p.InterceptTime = MathF.Max(lastT, p.TimeToReach(final));
        }

        // ---- Per-player thinking ----------------------------------------------------------------------

        private void ThinkLive(PlayerAgent p, float dt)
        {
            p.FaceBall = true;
            if (p.StunTimer > 0f)
            {
                p.Target = p.Pos + p.Vel * 0.3f;
                p.Urgency = 0.2f;
                return;
            }

            if (Ball.Owner == p)
            {
                ThinkOnBall(p, dt);
                return;
            }

            if (p.IsGoalkeeper)
            {
                ThinkGoalkeeper(p, dt);
                return;
            }

            var team = p.Team;
            bool free = Ball.Owner == null;

            if (free && team.Shielder == p)
            {
                p.Task = PlayerTask.Shield;
                p.Target = ShieldSpot(p);
                p.Urgency = 0.9f;
                return;
            }

            if (free && (team.Chaser == p || Ball.IntendedReceiver == p || team.Anticipator == p))
            {
                UpdateIntercept(p);
                p.Task = Ball.IntendedReceiver == p ? PlayerTask.Receive : PlayerTask.Chase;
                p.Target = p.InterceptPoint;
                p.Urgency = 1f;
                return;
            }

            if (!free && Ball.Owner.Team != team && (team.Presser == p || team.SecondPresser == p))
            {
                var carrier = Ball.Owner;
                Vec2 toGoal = (team.OwnGoal - carrier.Pos).Normalized;
                // Jockey goal-side; step in tighter when the carrier faces away from our goal.
                bool facingAway = Vec2.Dot(carrier.FacingDir, toGoal) < -0.2f;
                bool nearGoal = Vec2.Distance(carrier.Pos, team.OwnGoal) < 30f;
                float standOff = team.Presser == p ? (facingAway || nearGoal ? 0.95f : 1.6f) : 2.6f;
                Vec2 lateral = team.SecondPresser == p ? toGoal.Perp * (MathF.Sign(Vec2.Cross(toGoal, p.Pos - carrier.Pos)) * 2.5f) : Vec2.Zero;
                p.Task = PlayerTask.Press;
                Vec2 approach = toGoal;
                if (team.Presser == p && !nearGoal)
                {
                    // Cover shadow: the presser curves his run so that his body also blocks the carrier's best forward
                    // pass (to the nearest team-mate ahead of him) - pressing and cutting the lane at once.
                    PlayerAgent option = null;
                    float best = 22f;
                    float carrierF = carrier.Team.Forward(carrier.Pos);
                    foreach (var q in carrier.Team.Players)
                    {
                        if (q == carrier || q.SentOff || q.IsGoalkeeper || carrier.Team.Forward(q.Pos) < carrierF + 3f) continue;
                        float d = Vec2.Distance(q.Pos, carrier.Pos);
                        if (d < best) { best = d; option = q; }
                    }
                    if (option != null) approach = (toGoal * 0.65f + (option.Pos - carrier.Pos).Normalized * 0.35f).Normalized;
                }
                p.Target = carrier.Pos + carrier.Vel * 0.35f + approach * standOff + lateral;
                // Near goal the second man blocks the dribbling lane straight in front of the carrier.
                if (team.SecondPresser == p && nearGoal) p.Target = carrier.Pos + toGoal * 2.6f + carrier.Vel * 0.25f;
                p.Urgency = Vec2.Distance(p.Pos, carrier.Pos) > 4f ? 0.95f : 0.75f;
                return;
            }

            if (!free && Ball.Owner.Team != team && team.Cover == p)
            {
                p.Task = PlayerTask.Cover;
                p.Target = team.CoverSpot;
                p.Urgency = Vec2.Distance(p.Pos, team.CoverSpot) > 3f ? 0.85f : 0.55f;
                return;
            }

            // Free ball heading to an opponent: the nearest player closes the receiver down.
            if (free && Ball.IntendedReceiver != null && Ball.IntendedReceiver.Team != team && team.Presser == null && team.Chaser != p)
            {
                // handled by shape/marking below
            }

            ThinkOffBall(p, dt);
        }

        private void ThinkOffBall(PlayerAgent p, float dt)
        {
            var team = p.Team;
            bool inPossession = PossessionTeam == team;
            Vec2 target = p.ShapeTarget;
            bool wasSupporting = p.Task == PlayerTask.Support;
            p.Task = PlayerTask.Shape;
            float urgency = 0.5f;

            if (inPossession)
            {
                var carrier = Ball.Owner != null && Ball.Owner.Team == team ? Ball.Owner : null;
                if (carrier != null && team.Supporters.Contains(p))
                {
                    Vec2 avoid = team.Supporters.Count > 1 && team.Supporters[0] != p ? team.Supporters[0].SupportSpot : new Vec2(999f, 999f);
                    if (p.DecisionTimer <= 0f || !wasSupporting)
                    {
                        p.SupportSpot = BestSupportSpot(p, carrier, avoid);
                        p.DecisionTimer = 0.6f;
                    }
                    p.DecisionTimer -= dt;
                    target = p.SupportSpot;
                    p.Task = PlayerTask.Support;
                    urgency = 0.6f;
                }

                // Give-and-go: the passer bursts past his man for the ball straight back.
                if (p.OneTwoUntil > SimTime && p.OneTwoWith != null && (Ball.Owner == p.OneTwoWith || Ball.IntendedReceiver == p.OneTwoWith || Ball.IntendedReceiver == p))
                {
                    p.Target = p.OneTwoSpot;
                    p.Task = PlayerTask.Run;
                    p.Urgency = 1f;
                    return;
                }

                // Third-man run: the ball is going into (or is with) the team-mate who will lay it off - go now, in behind,
                // while the defenders watch the ball.
                if (p.ThirdManUntil > SimTime && p.ThirdManVia != null && (Ball.Owner == p.ThirdManVia || Ball.IntendedReceiver == p.ThirdManVia || Ball.IntendedReceiver == p))
                {
                    float offsideLine = MathF.Max(MathF.Max(SecondLastDefenderForward(team), team.Forward(Ball.Pos)), 0f);
                    // (onside until the lay-off: hold on the shoulder while the ball goes in, then go)
                    float f = Ball.Owner == p.ThirdManVia ? offsideLine + p.RunPlan.X + 4f : offsideLine - 0.8f;
                    p.Target = team.ToWorld(MathUtil.Clamp(f, -10f, 49f), p.RunPlan.Y);
                    p.Task = PlayerTask.Run;
                    p.Urgency = 1f;
                    return;
                }

                // Runs in behind (strikers / wide players), timed on the last defender's shoulder.
                if (carrier != null && IsRunner(p))
                {
                    float carrierF = team.Forward(carrier.Pos);
                    float pressure = Pressure(carrier, out _);
                    if (p.RunTimer <= 0f && p.RunCooldown <= 0f && carrierF > -25f && pressure < 0.6f)
                    {
                        float willing = 0.2f + 0.4f * MathUtil.A(p.Attr.OffTheBall) + 0.25f * team.Eff.Mentality;
                        if (team.Tactics.Attack == AttackStyle.RunsInBehind) willing *= 1.6f;
                        if (team.CounterUntil > SimTime) willing *= 1.8f;
                        // Central midfielders arrive late from deep (third-man runs), only around the box; the role decides.
                        willing *= RoleRunFactor(p, team.Forward(carrier.Pos));
                        if (p.PlayRole != PlayerRole.FalseNine && FalseNineOn(team)) willing *= 1.25f; // the space a false nine leaves is there to be run into
                        // Timing (a dynamic advantage): the run is made when the carrier can play it - facing forward, head up.
                        // Too early and he cannot use it; a run into a carrier with his back to goal is wasted.
                        bool headUp = Vec2.Dot(carrier.FacingDir, team.DirToWorld(Vec2.UnitX)) > 0.25f && pressure < 0.4f;
                        willing *= headUp ? 1.3f : 0.65f;
                        if (_rng.Chance(willing * 0.45f))
                        {
                            p.RunTimer = _rng.Range(2.2f, 3.6f);
                            // Counter-movement: a clever forward first comes short (as if for the ball at his feet) to drag his
                            // marker up, then spins in behind into the space the marker left.
                            if ((p.Slot.Line == Line.Attack || p.Slot.IsWideMidfielder) && _rng.Chance(0.15f + 0.5f * MathUtil.A(p.Attr.OffTheBall)))
                            {
                                p.RunCheckUntil = SimTime + _rng.Range(0.6f, 1.0f);
                                p.RunTimer += 0.8f;
                                team.Stats.CounterMovements++;
                            }
                            // Imperfect timing: low off-the-ball players drift offside more often.
                            float timingError = _rng.Gaussian(0.9f, 2.0f * (1.4f - MathUtil.A(p.Attr.OffTheBall)));
                            float lateral = MathUtil.Clamp(team.Lateral(p.Pos) * 0.8f + _rng.Range(-6f, 6f), -26f, 26f);
                            p.RunPlan = new Vec2(timingError, lateral); // (overshoot beyond the line, lateral) in local space
                        }
                        p.RunCooldown = _rng.Range(4f, 9f) * (team.Tactics.Attack == AttackStyle.RunsInBehind || team.CounterUntil > SimTime ? 0.5f : 1f);
                    }
                    if (p.RunTimer > 0f && p.RunCheckUntil > SimTime)
                    {
                        // (the check: a few steps towards the ball)
                        target = p.Pos + (carrier.Pos - p.Pos).Normalized * 3.5f;
                        p.Task = PlayerTask.Run;
                        urgency = 0.7f;
                    }
                    else if (p.RunTimer > 0f)
                    {
                        float offsideLine = MathF.Max(MathF.Max(SecondLastDefenderForward(team), team.Forward(Ball.Pos)), 0f);
                        float f = MathUtil.Clamp(offsideLine - 0.5f + p.RunPlan.X, -10f, 49f);
                        target = team.ToWorld(f, p.RunPlan.Y);
                        p.Task = PlayerTask.Run;
                        urgency = 0.85f;
                    }
                }
            }
            else
            {
                p.OneTwoUntil = -1f;
                // Out of possession: goal-side of the man this player is marking (see AssignMarking).
                float ballF = team.Forward(Ball.Pos);
                var mark = p.MarkTarget;
                if (mark != null && !mark.SentOff)
                {
                    bool inBox = Pitch.InPenaltyArea(mark.Pos, team.OwnGoalSign);
                    Vec2 lead = mark.Pos + mark.Vel * 0.3f;
                    float marking = 0.65f + 0.35f * MathUtil.A(p.Attr.Marking);
                    if (team.Tactics.Marking == MarkingStyle.ManToMan)
                    {
                        // Tight, goal-side, and he goes wherever his man goes (looser when the ball is far away).
                        bool ballFar = Vec2.Distance(mark.Pos, Ball.Pos) > 30f;
                        // (a poor marker gives his man more room)
                        float gap = (inBox ? 0.9f : (ballFar ? 2.6f : 1.3f)) * (2f - marking);
                        Vec2 goalSide = lead + (team.OwnGoal - lead).Normalized * gap;
                        // never followed deep into the opponents' half
                        if (team.Forward(goalSide) > 8f) goalSide = team.ToWorld(8f, team.Lateral(goalSide));
                        target = goalSide;
                        p.Task = PlayerTask.Mark;
                        float behind = Vec2.Distance(p.Pos, goalSide);
                        urgency = behind > 4f || inBox ? 0.85f : behind > 1.5f ? 0.65f : 0.5f;
                    }
                    else
                    {
                        // Zonal: step towards the man in my zone, but never out of it - he is handed over when he leaves.
                        Vec2 goalSide = lead + (team.OwnGoal - lead).Normalized * (inBox ? 1.0f : 1.8f);
                        float r = ZoneRadius(p) * 0.75f;
                        Vec2 fromZone = goalSide - p.ShapeTarget;
                        if (fromZone.Length > r) goalSide = p.ShapeTarget + fromZone.Normalized * r;
                        float weight = (p.Slot.Line == Line.Defence ? 0.7f : 0.55f) + (inBox ? 0.25f : 0f);
                        target = Vec2.Lerp(p.ShapeTarget, goalSide, MathUtil.Clamp01(weight * marking));
                        p.Task = PlayerTask.Mark;
                        if (inBox || Vec2.Distance(p.Pos, goalSide) > 4f) urgency = 0.85f;
                    }
                }
                // Hold the line: a defender does not drop off to follow a forward standing beyond the line while the
                // ball is still in front of it and under pressure - he is left offside. How reliably the line holds depends on the
                // understanding between the defenders (DefensiveChemistry) and the player's own positioning.
                if (p.Slot.Line == Line.Defence && Ball.Owner != null && Ball.Owner.Team != team && !Ball.InHands)
                {
                    float chem = team.DefensiveChemistry;
                    float line = team.LineForward;
                    // Only with pressure on the ball: an unpressured passer can play it over the top, so then the
                    // line drops with the runners ("ball pressured - hold, ball free - drop").
                    bool ballInFront = team.Forward(Ball.Pos) > line + 4f && Pressure(Ball.Owner, out _) > 0.3f;
                    bool trusts = chem * (0.6f + 0.4f * MathUtil.A(p.Attr.Positioning)) + 0.12f * MathF.Sin(SimTime * 0.31f + p.NoisePhaseB) > 0.55f;
                    float slack = 0.4f + 2.2f * (1f - chem);
                    bool trap = team.Tactics.Defence == DefenceStyle.OffsideTrap || team.Tactics.Defence == DefenceStyle.HighBackline;
                    bool broken = trap && p == team.TrapBreaker && team.TrapBrokenUntil > SimTime;
                    if (trap) { trusts = !broken; slack = 0.3f; ballInFront = team.Forward(Ball.Pos) > line + 4f; } // the trap holds the line whatever the pressure
                    if (broken) target = team.ToWorld(MathF.Min(team.Forward(target), line - 6.5f), team.Lateral(target)); // one man is left behind: nobody is offside
                    if (ballInFront && trusts && !Pitch.InPenaltyArea(target, team.OwnGoalSign) && team.Forward(target) < line - slack)
                    {
                        target = team.ToWorld(line - slack, team.Lateral(target));
                        if (CollectDiagnostics) Count("line.hold");
                    }
                    // ...and in a zonal back four nobody steps out of the line to follow a man into midfield: the
                    // midfield picks him up. (Man-markers do follow their man - that is the point of man-marking.)
                    float stepOut = 2.5f + 5f * (1f - chem);
                    if (p.PlayRole == PlayerRole.Stopper) stepOut += 4f;        // he follows his man into midfield to win it early
                    else if (p.PlayRole == PlayerRole.CoverDefender) stepOut = 0.5f; // he never leaves the line
                    if (team.Tactics.Marking == MarkingStyle.Zonal && trusts && team.Forward(target) > line + stepOut)
                        target = team.ToWorld(line + stepOut, team.Lateral(target));
                }

                // Recovery runs: players caught ahead of the ball sprint back.
                // How hard he works back for his team-mates is his teamwork.
                if (team.Forward(p.Pos) > ballF + 5f && p.Slot.Line != Line.Attack) urgency = 0.78f + 0.2f * MathUtil.A(p.Attr.Teamwork);
            }

            float dist = Vec2.Distance(p.Pos, target);
            // The back line out of possession moves as a unit with the line (steps up and drops together) instead of
            // strolling the last couple of metres - the better the understanding, the sharper.
            bool lineDefender = !inPossession && p.Slot.Line == Line.Defence && (p.Task == PlayerTask.Shape || p.Task == PlayerTask.Mark);
            if (lineDefender && MathF.Abs(team.Forward(target) - team.Forward(p.Pos)) > 0.8f)
                urgency = MathF.Max(urgency, 0.55f + 0.25f * team.DefensiveChemistry);
            if (dist > 12f) urgency = MathF.Max(urgency, 0.8f);
            // (a marker keeps pace with his man even when close to him)
            else if (dist < 2.5f && !lineDefender && !(p.Task == PlayerTask.Mark && p.MarkTarget != null && p.MarkTarget.Vel.Length > 2f)) urgency = MathF.Min(urgency, 0.35f);
            p.Target = target;
            p.Urgency = urgency;
        }

        /// <summary>How readily the player's role makes runs in behind (1 = as a forward does).</summary>
        private static float RoleRunFactor(PlayerAgent p, float carrierF)
        {
            switch (p.PlayRole)
            {
                case PlayerRole.ShadowStriker: return 1.3f;
                case PlayerRole.BoxToBoxDefensive: return carrierF > 0f ? 0.9f : 0.3f;
                case PlayerRole.Carrier: return carrierF > 5f ? 0.6f : 0f;
                case PlayerRole.AnchorMan: case PlayerRole.DeepLyingPlaymaker: case PlayerRole.Destroyer: case PlayerRole.TempoSetter: return 0f;
                case PlayerRole.TargetForward: return 0.6f;
                case PlayerRole.FalseNine: return 0.4f;
                case PlayerRole.SecondStriker: return 0.8f;
                case PlayerRole.ClassicWinger: return 1.2f;
            }
            if (p.Slot.IsCentralMidfielder) return carrierF > 4f ? 1.4f : 0f; // (late arrivals from midfield into the box: shots and goals from midfield)
            return 1f;
        }

        private static bool FalseNineOn(TeamAgent team)
        {
            foreach (var q in team.Players) if (!q.SentOff && q.PlayRole == PlayerRole.FalseNine) return true;
            return false;
        }

        private static bool IsRunner(PlayerAgent p)
        {
            var slot = p.Slot;
            return slot.Line == Line.Attack || slot.IsWideMidfielder || slot.IsCentralMidfielder;
        }

        /// <summary>Pressure on a player: 0 (free) .. 1 (closed down by several opponents).</summary>
        private float Pressure(PlayerAgent p, out float nearest)
        {
            nearest = float.PositiveInfinity;
            float pressure = 0f;
            foreach (var o in p.Team.Opponent.Players)
            {
                if (o.SentOff) continue;
                float d = Vec2.Distance(o.Pos, p.Pos);
                if (d < nearest) nearest = d;
                if (d < 6f)
                {
                    float w = 1f - d / 6f;
                    pressure += w * w * 1.3f;
                }
            }
            return MathUtil.Clamp01(pressure);
        }

        // ---- Locomotion -------------------------------------------------------------------------------

        private void MovePlayer(PlayerAgent p, float dt)
        {
            if (!p.SentOff && p.IsDown)
            {
                // Sliding: carried along the ground by the lunge; then lying there for a moment and getting up.
                if (p.SlideTimer > 0f) p.Vel = p.SlideDir * p.SlideSpeed;
                else p.Vel = p.Vel.ClampLength(MathF.Max(0f, p.Vel.Length - 14f * dt));
                p.Pos = Pitch.ClampToField(p.Pos + p.Vel * dt, 1.5f);
                p.DistanceCovered += p.Vel.Length * dt;
                UpdateEnergy(p, p.Vel.Length, dt);
                return;
            }
            if (p.SentOff)
            {
                // Walk off the pitch over the nearest touchline.
                p.Target = new Vec2(p.Pos.X, (Pitch.HalfWidth + 6f) * (p.Pos.Y >= 0f ? 1f : -1f));
                p.Urgency = 0.25f;
            }
            else if (Phase == MatchPhase.Live && Ball.InHands && Ball.Owner != null && Ball.Owner.Team != p.Team)
            {
                // Opponents drift away from a goalkeeper holding the ball: they may not challenge him or block the release.
                var gk = Ball.Owner;
                const float keep = 10f;
                Vec2 away = p.Target - gk.Pos;
                if (away.Length < keep)
                {
                    Vec2 dir = away.Length > 0.5f ? away.Normalized : (p.Pos - gk.Pos).Length > 0.5f ? (p.Pos - gk.Pos).Normalized : gk.Team.DirToWorld(Vec2.UnitX);
                    p.Target = gk.Pos + dir * keep;
                    p.Urgency = MathF.Max(p.Urgency, 0.4f);
                }
            }

            Vec2 toTarget = p.Target - p.Pos;
            float dist = toTarget.Length;
            float u = MathUtil.Clamp01(p.Urgency);
            float vmax = p.MaxSpeed;
            // Wet grass, snow and mud: less grip to accelerate, brake and turn on, and a little less pace.
            float footing = Footing(p.Pos);
            if (footing < 1f) vmax *= 1f - 0.45f * (1f - footing);
            float accelBoost = 1f;
            if (p.DiveTimer > 0f)
            {
                // Explosive dive: the keeper launches sideways faster than he can run.
                vmax *= 1.3f + 0.15f * MathUtil.A(p.Attr.Reflexes);
                accelBoost = 2.6f;
            }
            const float walk = 1.5f, jog = 3.9f;
            float cap = u < 0.5f ? MathUtil.Lerp(walk, jog, u / 0.5f) : MathUtil.Lerp(jog, vmax, (u - 0.5f) / 0.5f);
            if (Ball.Owner == p && !Ball.InHands)
            {
                cap = MathF.Min(cap, p.DribbleSpeed);
                // Physical pressure: defenders tight on the carrier slow him down (shoulder-to-shoulder).
                int tight = 0;
                foreach (var o in p.Team.Opponent.Players)
                {
                    if (!o.SentOff && o.StunTimer <= 0f && Vec2.DistanceSq(o.Pos, p.Pos) < 1.3f * 1.3f) tight++;
                }
                if (tight > 0) cap *= MathF.Max(0.55f, 1f - 0.22f * tight * (1.15f - 0.3f * MathUtil.A(p.Attr.PhysicalPower)));
            }
            if (p.HasPendingPlan) cap = MathF.Min(cap, 3.5f);
            if (p.StunTimer > 0f) cap = MathF.Min(cap, 1.5f);

            float decel = p.MaxDecel;
            float desiredSpeed = dist < 0.25f ? 0f : MathF.Min(cap, MathF.Sqrt(2f * decel * 0.55f * dist));
            Vec2 desired = dist > 1e-4f ? toTarget / dist * desiredSpeed : Vec2.Zero;

            // Separation keeps players from overlapping (teammates more, opponents less so duels still happen).
            if (!p.SentOff && Phase != MatchPhase.HalfTimeBreak)
            {
                Vec2 push = Vec2.Zero;
                foreach (var q in AllPlayers)
                {
                    if (q == p || q.SentOff) continue;
                    float radius = q.Team == p.Team ? 1.4f : 0.85f;
                    float strength = 3f;
                    if (q.Team != p.Team && q.Task == PlayerTask.Shield)
                    {
                        // A player shielding the ball holds the opponent off with his body (arm out, side on).
                        radius = 1.3f;
                        strength = 3f * MathUtil.Clamp(1.2f + 2f * (BodyStrength(q) - BodyStrength(p)), 0.5f, 2f);
                    }
                    Vec2 d = p.Pos - q.Pos;
                    float dl = d.LengthSq;
                    if (dl < radius * radius && dl > 1e-6f)
                    {
                        float len = MathF.Sqrt(dl);
                        push += d / len * ((radius - len) / radius) * strength;
                    }
                }
                desired += push;
            }

            // Airborne players cannot change direction: no ground contact until they land.
            if (p.JumpZ > 0f && p.DiveTimer <= 0f) desired = p.Vel;
            Vec2 dv = desired - p.Vel;
            bool accelerating = Vec2.Dot(desired, p.Vel) >= p.Vel.LengthSq * 0.95f;
            float maxDv = (accelerating ? p.MaxAccel : decel) * accelBoost * dt;
            if (footing < 1f)
            {
                maxDv *= 0.5f + 0.5f * footing;
                MaybeSlip(p, footing, p.Vel.Length, vmax, dv.Length, dt);
            }
            p.Vel += dv.ClampLength(maxDv);
            float speed = p.Vel.Length;
            if (speed > vmax) { p.Vel = p.Vel / speed * vmax; speed = vmax; }
            p.Pos += p.Vel * dt;
            // (a corner taker steps back off the pitch for his run-up)
            p.Pos = Pitch.ClampToField(p.Pos, p.SentOff ? 8f : (Phase == MatchPhase.Stoppage && _restart != null && _restart.Taker == p ? 3.5f : 1.5f));
            p.DistanceCovered += speed * dt;

            if (p.KickTimer > 0f) p.KickTimer -= dt;
            // Facing: run direction when moving, otherwise the ball. A pending kick turns the body first.
            float targetFacing;
            float turnRate;
            if (Phase == MatchPhase.Stoppage && _restart != null && _restart.Taker == p && RestartFacing(_restart, p, out float restartFacing))
            {
                // the player taking a throw-in, corner, goal kick or free kick faces the way he will play it, into the field
                targetFacing = restartFacing;
                turnRate = 7f;
            }
            else if (p.HasPendingPlan)
            {
                targetFacing = p.PendingPlan.Heading;
                turnRate = 14f;
            }
            else if (speed > 1.2f && !(p.FaceBall && speed < 2.5f))
            {
                targetFacing = p.Vel.Angle;
                turnRate = 9f;
            }
            else if (Ball.Owner != p)
            {
                targetFacing = (Ball.Pos - p.Pos).Angle;
                turnRate = 6f;
            }
            else
            {
                targetFacing = p.Facing;
                turnRate = 6f;
            }
            if (footing < 1f) turnRate *= 0.55f + 0.45f * footing;
            p.Facing = MathUtil.MoveTowardsAngle(p.Facing, targetFacing, turnRate * dt);

            // Jump: ballistic flight of the body (headers, goalkeeper takes, walls, celebrations).
            if (p.JumpZ > 0f || p.JumpVz > 0f)
            {
                p.JumpZ += p.JumpVz * dt;
                p.JumpVz -= JumpGravity * dt;
                if (p.JumpZ <= 0f) { p.JumpZ = 0f; p.JumpVz = 0f; }
            }

            UpdateEnergy(p, speed, dt);
        }

        /// <summary>
        /// Gravity for the body's jump, in sim time. Players move at MotionSpeed x real time, so true gravity would make
        /// a leap last a blink on screen; dividing by MotionSpeed gives a visible hang time of about 0.45-0.6 real
        /// seconds for a 0.4-0.7 m leap (a real one lasts 0.55-0.75 s).
        /// </summary>
        private float JumpGravity => Config.Gravity / Config.MotionSpeed;

        /// <summary>Highest standing leap of the centre of mass (m) from the Jumping attribute (0.35-0.75 m).</summary>
        private static float MaxJumpHeight(PlayerAgent p) => 0.35f + 0.4f * MathUtil.A(p.Attr.Jumping);

        /// <summary>Take off for a leap of <paramref name="height"/> metres: v0 = sqrt(2 g h).</summary>
        private void StartJump(PlayerAgent p, float height)
        {
            if (p.JumpZ > 0f || p.SentOff) return;
            float h = MathUtil.Clamp(height, 0.1f, MaxJumpHeight(p));
            p.JumpVz = MathF.Sqrt(2f * JumpGravity * h);
            p.JumpZ = 0.001f;
            if (p.DiveTimer <= 0f) p.Vel *= 0.55f; // plant and take off
        }

        /// <summary>Time from take-off to the top of a leap of the given height.</summary>
        private float TimeToApex(float height) => MathF.Sqrt(2f * height / JumpGravity);

        /// <summary>
        /// Anticipated leaps: a player under a high ball takes off early so that the top of his jump meets the ball,
        /// like a real header, instead of hopping after the contact.
        /// </summary>
        private void AnticipateHeaders()
        {
            if (Ball.Owner != null || Ball.InGoal || Ball.Z < 1.2f && Ball.M.Vz <= 0f) return;
            Vec2 bv = new Vec2(Ball.M.Vx, Ball.M.Vy);
            float bv2 = bv.LengthSq;
            if (bv2 < 4f) return;
            foreach (var p in AllPlayers)
            {
                if (p.SentOff || p.IsGoalkeeper || p.JumpZ > 0f || p.StunTimer > 0f) continue;
                Vec2 rel = p.Pos - Ball.Pos;
                float tc = Vec2.Dot(rel, bv) / bv2;
                if (tc <= 0f || tc > 1.2f) continue;
                Vec2 closest = Ball.Pos + bv * tc;
                if (Vec2.Distance(closest, p.Pos + p.Vel * tc) > 1.1f) continue;
                // Ball height at that moment (drag ignored over such a short time).
                float z = Ball.Z + Ball.M.Vz * tc - 0.5f * Config.Gravity * tc * tc;
                if (z < 1.75f || z > p.HeadReach + 0.15f) continue;
                float need = MathUtil.Clamp(z - 1.75f + 0.15f, 0.2f, MaxJumpHeight(p));
                if (tc <= TimeToApex(need) + 0.02f) StartJump(p, need);
            }
        }

        /// <summary>How attacking a full-back is by nature (0..1): crossing, pace, stamina and running off the ball.</summary>
        public static float FullBackDrive(PlayerAgent p) =>
            MathUtil.Clamp01((p.Attr.Crossing + p.Attr.Speed + p.Attr.Stamina + p.Attr.OffTheBall) / 400f);

        /// <summary>
        /// How hard a position works, as a multiplier on the energy drain. Defenders run all the time (full-backs up and
        /// down the flank, centre-backs stepping out and recovering), wide midfielders and central midfielders cover the
        /// most ground, strikers press in bursts, and a goalkeeper hardly tires.
        /// </summary>
        public static float WorkRate(PlayerAgent p)
        {
            var slot = p.Slot;
            if (p.IsGoalkeeper) return 0.35f;
            if (slot.IsFullBack) return 1.25f;
            if (slot.IsWideMidfielder) return 1.15f;
            if (slot.IsCentralMidfielder) return 1.1f;
            if (slot.IsCentreBack) return 1.05f;
            return 0.95f;
        }

        private void UpdateEnergy(PlayerAgent p, float speed, float dt)
        {
            float clockDt = dt * Config.ClockSecondsPerSimSecond;
            float intensity = speed / MathF.Max(1f, p.MaxSpeed);
            float staminaFactor = 1.35f - 0.7f * MathUtil.A(p.Attr.Stamina);
            float drain = (0.00005f + 0.00066f * intensity * intensity * intensity) * staminaFactor;
            drain *= WorkRate(p) * p.Team.Tactics.EnergyDrain * (p.Team == Away ? 1f + 0.03f * Config.HomeAdvantage : 1f); // (the travel)
            float recover = speed < 2.2f ? 0.00004f : 0f;
            p.Energy = MathUtil.Clamp(p.Energy - (drain - recover) * clockDt, 0.25f, 1f);
            MaybeStrain(p, intensity, clockDt);
        }
    }
}

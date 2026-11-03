using System;
using System.Collections.Generic;
using System.Linq;

namespace SoccerVision.Simulation
{
    /// <summary>Team behaviour: the formation block that slides with the ball, the defensive line, rest defence and the supporting players.</summary>
    public sealed partial class MatchEngine
    {
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
                {
                    lineTarget += 5f * chem * stepUp;
                    CallStepUp(team);
                }
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
            // After a clearance the whole block pushes out together, 10-15 m (squeezes the space for the second ball and
            // leaves anyone still in the box offside) - not while the ball is still dropping near our box.
            if (team.PushUpUntil > SimTime && ballF > lineTarget + 16f)
            {
                lineTarget = MathF.Min(lineTarget + 8f, ballF - 8f);
                if (CollectDiagnostics) Count("line.pushUp");
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

            if (inPossession) { AssignSupport(team); UpdateSituationalTempo(team); }
            else { team.Supporters.Clear(); team.TempoShift = 0f; }
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
                case PlayerRole.Regista:
                    // the pocket just in front of the centre-backs (between them when they split), always a safe option
                    if (inPossession) { f = MathF.Min(f, MathF.Max(ballF - 14f, line + 1.5f)); l = MathUtil.Clamp(ballL * 0.2f, -6f, 6f); }
                    else { f = MathF.Min(f, line + 7f); l = MathUtil.Clamp(l, -10f, 10f); } // screens the space in front of the defence
                    break;
                case PlayerRole.Trequartista:
                    // free between the lines, drifting to the half-space nearest the ball; out of possession he stays up
                    if (inPossession) { f = MathF.Min(MathF.Max(f, ballF + 4f), offsideLine - 6f); l = MathUtil.Clamp(ballL * 0.45f, -15f, 15f); }
                    else f += 7f;
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

            // Box roles when a cross is on (ball wide in the final third). Real goals from crosses and cut-backs are scored
            // round the penalty spot far more than on the keeper's toes, so each man attacks his own zone and nobody stands
            // in the six-yard box waiting: the near post (just outside the six-yard box, to get across the first defender),
            // the far post, the penalty spot (the late arrival - he only goes when the cross is about to come), the cut-back
            // zone at the edge of the box and the second ball behind it. (StatsBomb: 7% of shots from the six-yard box.)
            bool crossy = team.Tactics.Attack == AttackStyle.CrossesIntoBox || team.Tactics.Special == SpecialTactic.Siege;
            if (inPossession && ballF > (crossy ? 18f : 24f) && MathF.Abs(ballL) > (crossy ? 9f : 12f) && Ball.Owner != null && Ball.Owner.Team == team && Ball.Owner != p)
            {
                float side = MathF.Sign(ballL);
                bool late = ballF > 36f; // (the carrier is near the by-line: the cross or the cut-back is coming now)
                string role = null;
                if (slot.Line == Line.Attack)
                {
                    int forwards = 0;
                    foreach (var q in team.Players) if (!q.SentOff && q.Slot.Line == Line.Attack && q != Ball.Owner) forwards++;
                    bool near = MathF.Sign(slot.BaseLateral) == side || slot.BaseLateral == 0f;
                    if (forwards <= 1) { f = 44f; l = side * 1.5f; role = "lone"; }        // (one forward: between the near post and the spot)
                    else if (near) { f = 45f; l = side * 2.5f; role = "nearPost"; }
                    else { f = 42.5f; l = -side * 4.5f; role = "farPost"; }
                    f = MathF.Min(f, offsideLine - 0.6f);
                }
                else if (slot.IsWideMidfielder && MathF.Sign(slot.BaseLateral) != side)
                {
                    f = MathF.Min(41.5f, offsideLine - 0.6f); // (beyond the far post, for the ball that clears everyone)
                    l = -side * 8f;
                    role = "backPost";
                }
                else if (slot.IsCentralMidfielder && p != team.Anchor && MathF.Sign(slot.BaseLateral) == side)
                {
                    f = 36.5f; // the cut-back: the edge of the box, level with the near post
                    l = side * 6f;
                    role = "cutBack";
                }
                else if ((slot.IsCentralMidfielder || p.Kind == PositionKind.AttackingMidfielder) && p != team.Anchor)
                {
                    // the late run: waits at the edge (and picks up the second ball) until the delivery is on, then arrives
                    // at the penalty spot - the man nobody has picked up
                    if (late) { f = MathF.Min(40.5f, offsideLine - 0.6f); l = -side * 1f; role = "lateRun"; }
                    else { f = 31f; l = -side * 2f; role = "edge"; }
                }
                if (role != null && CollectDiagnostics && p.Number % 4 == 0) Count("box.role." + role);
            }
            else if (inPossession && slot.Line == Line.Attack && p.RunTimer <= 0f)
                f = MathF.Min(f, 44.5f); // (a forward does not stand on the keeper: he waits where the ball can find him)

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
                // (and the back four stay home even with the ball: no full-back bombing forward, the bunker is kept for when it is lost)
                if (inPossession && slot.Line == Line.Defence) f = MathF.Min(f, line + 7f);
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
            var presser = Presser(carrier);
            // (a carrier facing his own goal or squeezed: the second man offers the way out behind him too)
            bool dropOff = avoid.X < 900f && NeedsDropOff(carrier);
            for (int ring = 0; ring < 3; ring++)
            {
                if (ring == 2 && !dropOff) break;
                float radius = ring == 0 ? 11f : ring == 1 ? 18f : 10f;
                for (int k = 0; k < 8; k++)
                {
                    float ang = ring == 2 ? 1.9f + k * (2.5f / 7f) : -1.35f + k * (2.7f / 7f); // forward-facing fan (the drop-off: behind), local frame
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
                                  - (Vec2.Distance(world, avoid) < 6f ? 2f : 0f)
                                  + SplitScore(supporter, carrier, presser, world, avoid);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = world;
                    }
                }
            }
            if (CollectDiagnostics && presser != null && avoid.X < 900f)
                Count(SideOf(carrier.Pos, presser.Pos, best) != SideOf(carrier.Pos, presser.Pos, avoid) ? "support.split" : "support.sameSide");
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
    }
}

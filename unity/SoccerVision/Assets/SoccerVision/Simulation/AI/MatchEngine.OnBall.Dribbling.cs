using System;
using System.Collections.Generic;
using System.Linq;

namespace SoccerVision.Simulation
{
    /// <summary>In possession - running with the ball, crossing and clearing, and the pieces of skill (fake shot, backheel).</summary>
    public sealed partial class MatchEngine
    {
        private static readonly float[] DribbleAngles = { 0f, 0.45f, -0.45f, 0.9f, -0.9f, 1.5f, -1.5f };

        private void EvaluateDribbles(PlayerAgent p, Vec2 pos, float pressure, float lossNow)
        {
            var team = p.Team;
            float f = team.Forward(pos);
            float dribbling = MathUtil.A(p.Attr.Dribbling);
            Vec2 forward = team.DirToWorld(Vec2.UnitX);
            Vec2 toGoal = (team.AttackGoal - pos).Normalized;
            Vec2 baseDir = f > 25f ? Vec2.Lerp(forward, toGoal, 0.6f).Normalized : forward;
            bool cutIn = IsCutInsidePlayer(p);
            float cutInSkill = MathUtil.A((p.Attr.Dribbling + p.Attr.LongShots + p.Attr.Finishing) / 3);
            bool inverted = p.Slot.IsWideMidfielder && ((p.Slot.BaseLateral > 0f) == (p.Attr.StrongFoot == Foot.Right));
            const float length = 6.5f;
            float tMe = length / MathF.Max(1f, p.DribbleSpeed);

            foreach (float a in DribbleAngles)
            {
                Vec2 dir = baseDir.Rotate(a);
                Vec2 target = pos + dir * length;
                Vec2 clamped = new Vec2(MathUtil.Clamp(target.X, -Pitch.HalfLength + 1.5f, Pitch.HalfLength - 1.5f),
                                        MathUtil.Clamp(target.Y, -Pitch.HalfWidth + 1.8f, Pitch.HalfWidth - 1.8f));
                if (Vec2.Distance(clamped, pos) < 3f) continue;
                target = clamped;
                Vec2 mid = Vec2.Lerp(pos, target, 0.5f);

                float threat = 0f;
                bool takeOn = false;
                int crowd = 0;
                foreach (var o in team.Opponent.Players)
                {
                    if (o.SentOff) continue;
                    if (Vec2.DistanceSq(o.Pos, target) < 16f) crowd++;
                    float tMid = o.TimeToReach(mid);
                    float tEnd = o.TimeToReach(target);
                    float r = MathF.Max(MathUtil.Clamp01(0.5f + (tMe * 0.5f - tMid) / 0.6f), MathUtil.Clamp01(0.5f + (tMe - tEnd) / 0.6f));
                    if (r > threat) threat = r;
                    if (Vec2.DistanceToSegment(o.Pos, pos, target, out _) < 1.4f) takeOn = true;
                }
                float keep = MathUtil.Clamp(0.94f - 0.55f * threat + 0.15f * (dribbling - 0.5f), 0.08f, 0.96f);
                if (takeOn) keep *= 0.55f + 0.4f * dribbling;
                keep *= MathF.Pow(0.82f, crowd); // dribbling into a crowd rarely works
                float value = ExpectedThreat.At(team, target);
                bool inward = MathF.Abs(team.Lateral(target)) < MathF.Abs(team.Lateral(pos)) - 2f;
                if (cutIn && inward && f > 14f && MathF.Abs(team.Lateral(pos)) > 14f) value += (p.Has(PlayerTrait.CutsInsideToShoot) || p.PlayRole == PlayerRole.InvertedWinger ? 0.14f : 0.11f) * (0.4f + 0.6f * cutInSkill); // (the winger with his strong foot inside: the run in towards goal is what he is for)
                else if (inverted && team.Forward(pos) > 15f && inward) value += 0.003f;
                // Wide players (wingers, attacking full-backs) love to take it down the line towards the by-line,
                // where they can deliver; the ones who cut in (strong foot inside) prefer to come inside instead.
                bool wideRunner = (p.Slot.IsWideMidfielder || p.Slot.IsFullBack || (p.Slot.Line == Line.Attack && MathF.Abs(p.Slot.BaseLateral) > 12f))
                                  && !(cutIn && inverted);
                if (wideRunner && f > 5f && MathF.Abs(team.Lateral(pos)) > 19f && MathF.Abs(team.Lateral(target)) > 21f
                    && team.Forward(target) - team.Forward(pos) > 3f)
                    value += 0.006f + 0.008f * dribbling;
                bool progressive = team.Forward(target) - team.Forward(pos) > 3f;
                float stale = MathF.Max(0f, p.HoldTime - (progressive ? 3f : 1.5f)) * 0.005f;
                float utility = keep * value - (1f - keep) * (lossNow + _riskPremium) - stale;
                float like = DribbleFactor(p, progressive);
                utility = utility > 0f ? utility * like : utility / like;
                _options.Add(new KickPlan { Action = BallAction.Dribble, DribbleTarget = target, Utility = utility, Success = keep });
            }
        }

        /// <summary>How much the player wants to run with the ball: his individual instruction and his role.</summary>
        private static float DribbleFactor(PlayerAgent p, bool progressive)
        {
            float f = PlayerAgent.Scale(p.Orders.Dribbling, 0.55f, 1.4f);
            if (p.Has(PlayerTrait.LovesToDribble)) f *= 1.35f;
            if (p.Has(PlayerTrait.Unselfish)) f *= 0.9f;
            switch (p.PlayRole)
            {
                case PlayerRole.Carrier: f *= 1.3f; break;
                case PlayerRole.Trequartista: f *= 1.15f; break; // short bursts past the press
                case PlayerRole.Regista: f *= 0.8f; break;
                case PlayerRole.BallPlayingDefender: if (progressive) f *= 1.15f; break;
                case PlayerRole.ClassicWinger: case PlayerRole.InvertedWinger: f *= 1.12f; break;
                case PlayerRole.TempoSetter: case PlayerRole.TargetForward: case PlayerRole.AnchorMan: f *= 0.8f; break;
            }
            return f;
        }

        private void EvaluateCrosses(PlayerAgent p, Vec2 pos)
        {
            var team = p.Team;
            float side = MathF.Sign(team.Lateral(pos));
            var zones = new[]
            {
                // (to the zones the runners attack - see the box roles in ComputeShapeTarget - in front of the six-yard box,
                // where the keeper cannot come and claim it)
                new Vec2(45f, side * 2.5f),   // near post
                new Vec2(41f, 0f),            // penalty spot
                new Vec2(43f, -side * 4.5f),  // far post
                new Vec2(41.5f, -side * 8f),  // beyond the far post
            };
            float crossing = MathUtil.A(p.Attr.Crossing);
            var keeper = team.Opponent.Goalkeeper;
            foreach (var z in zones)
            {
                Vec2 target = team.ToWorld(z.X, z.Y);
                float d = Vec2.Distance(pos, target);
                if (d < 8f) continue;
                PlayerAgent bestMate = null;
                float bestMateT = float.PositiveInfinity;
                foreach (var q in team.Players)
                {
                    if (q.SentOff || q == p || q.IsGoalkeeper) continue;
                    float t = q.TimeToReach(target);
                    if (t < bestMateT) { bestMateT = t; bestMate = q; }
                }
                if (bestMate == null) continue;
                float bestOppT = float.PositiveInfinity;
                PlayerAgent bestOpp = null;
                foreach (var o in team.Opponent.Players)
                {
                    if (o.SentOff) continue;
                    float t = o.TimeToReach(target);
                    if (o == keeper)
                    {
                        if (!Pitch.InGoalArea(target, team.AttackGoalSign) && Vec2.Distance(o.Pos, target) > 7f) continue;
                        t -= 0.4f * MathUtil.A(o.Attr.AerialAbility);
                    }
                    if (t < bestOppT) { bestOppT = t; bestOpp = o; }
                }
                float flight = d / 17f + 0.4f;
                float aerialMate = (bestMate.Attr.Jumping + bestMate.Attr.Heading + bestMate.Attr.PhysicalPower) / 3f + (bestMate.Profile.HeightCm - 180) * 0.3f;
                float aerialOpp = bestOpp != null ? (bestOpp.IsGoalkeeper ? bestOpp.Attr.AerialAbility + 10f : (bestOpp.Attr.Jumping + bestOpp.Attr.Heading + bestOpp.Attr.PhysicalPower) / 3f + (bestOpp.Profile.HeightCm - 180) * 0.3f) : 0f;
                float pWin = MathUtil.Sigmoid((bestOppT - MathF.Max(bestMateT, flight)) / 0.45f + (aerialMate - aerialOpp) / 35f);
                float delivery = 0.35f + 0.4f * crossing; // (most crosses in the top leagues do not find a team-mate: about one in five does)
                float headerXg = ExpectedGoals.FromLocation(target, team.AttackGoalSign) * 0.55f * (0.65f + 0.7f * MathUtil.A(bestMate.Attr.Heading));
                float utility = pWin * delivery * (headerXg + 0.01f) - (1f - pWin * delivery) * (LossCost(team, target) * 0.25f + _riskPremium * 0.6f);
                // the manager wants crosses: into the box from everywhere wide (crosses style, relentless pressure)
                if (team.Tactics.Attack == AttackStyle.CrossesIntoBox) utility = utility > 0f ? utility * 1.7f + 0.0015f : utility * 0.6f;
                else if (team.Tactics.Special == SpecialTactic.Siege) utility = utility > 0f ? utility * 1.25f : utility;
                // the classic winger lives to cross, the inverted one would rather come inside
                float crosser = p.PlayRole == PlayerRole.ClassicWinger ? 1.3f : p.PlayRole == PlayerRole.WingBack ? 1.15f : p.PlayRole == PlayerRole.InvertedWinger ? 0.8f : 1f;
                if (p.PlayRole == PlayerRole.Auto && p.Slot.IsWideMidfielder && !IsCutInsidePlayer(p)) crosser *= 1.3f; // (a winger on his strong side goes to the by-line and crosses)
                if (utility > 0f) utility *= crosser * 0.75f; // (a cross is one option among others, not the first thing a wide man thinks of)
                _options.Add(new KickPlan
                {
                    Action = BallAction.Cross,
                    Kind = KickKind.Cross,
                    Receiver = bestMate,
                    Target = target,
                    Lofted = true,
                    LaunchAngle = d > 30f ? 0.42f : 0.36f,
                    Heading = (target - pos).Angle,
                    Utility = utility,
                    Success = pWin * delivery,
                });
            }
        }

        private void EvaluateClearance(PlayerAgent p, Vec2 pos, float pressure)
        {
            var team = p.Team;
            float f = team.Forward(pos);
            float l = team.Lateral(pos);
            float side = MathF.Abs(l) > 4f ? MathF.Sign(l) : (_rng.Chance(0.5f) ? 1f : -1f);
            float lateral = MathF.Abs(l) > 22f && pressure > 0.6f ? _rng.Range(30f, 44f) : _rng.Range(14f, 32f);
            Vec2 target = team.ToWorld(MathUtil.Clamp(f + 38f, -10f, 20f), side * lateral);
            float utility = 0.45f * ExpectedThreat.At(team, target) - 0.55f * LossCost(team, target) + 0.004f * pressure;
            _options.Add(new KickPlan
            {
                Action = BallAction.Clear,
                Kind = KickKind.Clearance,
                Target = target,
                Lofted = true,
                LaunchAngle = 0.62f,
                Heading = (target - pos).Angle,
                Utility = utility,
            });
        }

        /// <summary>Under pressure by the touchline: put it out (concede a throw-in rather than lose it in play).</summary>
        private void EvaluateKickOut(PlayerAgent p, Vec2 pos, float pressure)
        {
            var team = p.Team;
            float side = MathF.Sign(team.Lateral(pos));
            Vec2 target = team.ToWorld(team.Forward(pos) + _rng.Range(4f, 14f), side * 40f);
            Vec2 throwSpot = new Vec2(MathUtil.Clamp((pos.X + target.X) * 0.5f, -52f, 52f), Pitch.HalfWidth * MathF.Sign(target.Y));
            float utility = -LossCost(team, throwSpot) * 0.45f + 0.002f * pressure;
            _options.Add(new KickPlan
            {
                Action = BallAction.Clear,
                Kind = KickKind.Clearance,
                Target = target,
                Lofted = true,
                LaunchAngle = 0.35f,
                Heading = (target - pos).Angle,
                Utility = utility,
            });
        }

        /// <summary>
        /// A piece of skill instead of the plain action: the fake shot (shapes to shoot, the defender throws himself in the
        /// way, he cuts past him) and the backheel pass to a team-mate behind him when he has no time to turn. Only the
        /// creative, technical players do it, and not often.
        /// </summary>
        private KickPlan MaybeSkill(PlayerAgent p, KickPlan chosen, Vec2 pos, float pressure)
        {
            var team = p.Team;
            float flair = MathUtil.A((p.Attr.Creativity + p.Attr.Dribbling + p.Attr.Composure) / 3);
            bool showboat = p.Has(PlayerTrait.Showboat);
            if (showboat) flair = MathF.Min(1f, flair + 0.2f);
            if (chosen.Action == BallAction.Shoot && flair > 0.55f)
            {
                Vec2 toGoal = (team.AttackGoal - pos).Normalized;
                PlayerAgent blocker = null;
                foreach (var o in team.Opponent.Players)
                {
                    if (o.SentOff || o.IsGoalkeeper || o.StunTimer > 0f) continue;
                    Vec2 d = o.Pos - pos;
                    float len = d.Length;
                    if (len < 3.6f && len > 0.3f && Vec2.Dot(d / len, toGoal) > 0.4f) { blocker = o; break; }
                }
                if (blocker != null && _rng.Chance((0.7f * (flair - 0.55f) / 0.45f + 0.1f) * (showboat ? 1.5f : 1f)))
                {
                    // the cut: away from the side the defender committed to, still towards goal
                    float side = Vec2.Cross(toGoal, blocker.Pos - pos) > 0f ? -1f : 1f;
                    Vec2 across = new Vec2(-toGoal.Y, toGoal.X) * side;
                    blocker.StunTimer = MathF.Max(blocker.StunTimer, 1.0f); // sold: committed to the block, he is a second out of it
                    // the shooting motion - the whole swing, the foot goes back and through - and the ball moves a little with it:
                    // nudged to the side he is about to go (in or out), then carried on
                    p.KickTimer = PlayerAgent.KickSwing;
                    p.KickLeft = side > 0f;
                    p.FakeDir = across; p.FakeStart = SimTime; p.FakeUntil = SimTime + 0.55f;
                    Emit(MatchEventType.Skill, team, p, blocker, pos, 0f, WhistleKind.Short, 1);
                    if (CollectDiagnostics) Count("skill.fakeShot");
                    return new KickPlan { Action = BallAction.Dribble, DribbleTarget = Pitch.ClampToField(pos + across * 3.2f + toGoal * 1.2f, -1f), Utility = chosen.Utility };
                }
            }
            if (chosen.Action == BallAction.Dribble && TryNutmeg(p, ref chosen, pos, pressure)) return chosen;
            if (chosen.Action == BallAction.Pass && !chosen.Lofted && chosen.Receiver != null && pressure > 0.25f)
            {
                Vec2 to = chosen.Target - pos;
                float len = to.Length;
                float technique = MathUtil.A((p.Attr.BallControl + p.Attr.Creativity) / 2) + (showboat ? 0.2f : 0f);
                if (len > 3f && len < 14f && Vec2.Dot(to / len, p.FacingDir) < -0.15f && technique > 0.55f // (behind him, or beside him and a little back: a heel flick)
                    && _rng.Chance((0.4f * (technique - 0.55f) / 0.45f + 0.03f) * (showboat ? 1.6f : 1f) * (Vec2.Dot(to / len, p.FacingDir) < -0.55f ? 1f : 0.3f))) // (to the side: much rarer than straight back)
                {
                    Emit(MatchEventType.Skill, team, p, chosen.Receiver, pos, 0f, WhistleKind.Short, 2);
                    chosen.Heel = true;
                    if (CollectDiagnostics) Count("skill.backheel");
                }
            }
            return chosen;
        }

        /// <summary>
        /// The nutmeg: a defender closing him down head on, close enough, and a dribbler with the nerve and the touch - he pushes the
        /// ball through the defender's legs and goes round to collect it. Flair players (the trick-lovers, the dribblers, the
        /// creative ones) try it far more than the others; it does not always work - the ball can hit a leg and go to the defender.
        /// </summary>
        private bool TryNutmeg(PlayerAgent p, ref KickPlan chosen, Vec2 pos, float pressure)
        {
            var team = p.Team;
            if (team.Forward(pos) < -20f || Ball.Owner != p) return false;
            Vec2 dir = (chosen.DribbleTarget - pos).Normalized;
            if (dir.LengthSq < 0.01f) dir = p.FacingDir;
            PlayerAgent v = null; float vd = 99f;
            foreach (var o in team.Opponent.Players)
            {
                if (o.SentOff || o.IsGoalkeeper || o.StunTimer > 0f) continue;
                Vec2 rel = o.Pos - pos; float d = rel.Length;
                if (d < 1.1f || d > 2.7f || Vec2.Dot(rel / d, dir) < 0.88f) continue;
                if (d < vd) { vd = d; v = o; }
            }
            if (v == null) return false;
            float flair = MathUtil.A((p.Attr.Dribbling * 2 + p.Attr.Creativity + p.Attr.Composure) / 4);
            if (flair < 0.45f && !p.Has(PlayerTrait.Showboat)) return false;
            float chance = 0.03f + 0.22f * MathUtil.Clamp01((flair - 0.45f) / 0.5f);
            if (p.Has(PlayerTrait.Showboat)) chance *= 2.2f;
            if (p.Has(PlayerTrait.LovesToDribble)) chance *= 1.6f;
            chance *= MathUtil.Lerp(1f, 0.5f, MathUtil.Clamp01(pressure));
            if (!_rng.Chance(MathUtil.Clamp(chance * _nutmegRate, 0f, 0.6f))) return false;
            // the ball goes through his legs and on, about two and a half metres beyond him
            Vec2 landing = Pitch.ClampToField(v.Pos + dir * 2.4f, -1.5f);
            float dist = Vec2.Distance(pos, landing);
            float speed = Roll.KickSpeedFor(dist, 2.8f);
            float pWork = MathUtil.Clamp(0.4f + 0.5f * flair + (p.Has(PlayerTrait.Showboat) ? 0.05f : 0f) - 0.2f * pressure, 0.25f, 0.9f);
            bool works = _rng.Chance(pWork);
            KickBall(p, Ball.Pos, dir * speed, 0f, 0f, 0f, KickKind.Nutmeg, null, landing, false, false);
            Emit(MatchEventType.Skill, team, p, v, pos, 0f, WhistleKind.Short, 4);
            team.Stats.Nutmegs++;
            if (CollectDiagnostics) Count("skill.nutmeg" + (works ? ".works" : ".fails"));
            if (works)
            {
                v.StunTimer = MathF.Max(v.StunTimer, 1.1f);     // fooled: he cannot get a foot to it
                v.NutmegVictimUntil = SimTime + 0.9f;            // (his legs are drawn open)
                p.NutmegChaseUntil = SimTime + 3f;
            }
            // he goes round the defender for his own ball
            chosen = new KickPlan { Action = BallAction.Dribble, DribbleTarget = landing, Utility = chosen.Utility };
            return true;
        }

        private float _nutmegRate = 1f;

        /// <summary>A target man with his back to goal in the final third: he holds it up and lays it off.</summary>
        private static bool BackToGoalNow(PlayerAgent p)
        {
            if (!p.Has(PlayerTrait.BackToGoal)) return false;
            var team = p.Team;
            if (team.Forward(p.Pos) < 12f) return false;
            Vec2 toGoal = (team.AttackGoal - p.Pos).Normalized;
            return Vec2.Dot(p.FacingDir, toGoal) < -0.2f;
        }

        private static bool IsCutInsidePlayer(PlayerAgent p)
        {
            var slot = p.Slot;
            if (!(slot.IsWideMidfielder || (slot.Line == Line.Attack && MathF.Abs(slot.BaseLateral) > 12f))) return false;
            var role = p.PlayRole;
            if (role == PlayerRole.ClassicWinger || role == PlayerRole.WideTargetMan) return false; // he stays wide
            bool inverted = (slot.BaseLateral > 0f) == (p.Attr.StrongFoot == Foot.Right);
            // (the trait: a winger with his strong foot inside, or a two-footed one, always looks to come in)
            if (p.Has(PlayerTrait.CutsInsideToShoot) && (inverted || p.Attr.WeakFootRating >= 2)) return true;
            float skill = (p.Attr.Dribbling + p.Attr.LongShots + p.Attr.Finishing + p.Attr.BallControl) / 4f;
            if (role == PlayerRole.InvertedWinger) return true; // the manager asked him to come inside: he does, whatever his skill (that only decides how well)
            if (!inverted) return skill >= 62f && p.Attr.WeakFootRating >= 2; // a good two-footed winger cuts in too
            return skill >= 50f;
        }
    }
}

using System;
using System.Collections.Generic;

namespace SoccerVision.Simulation
{
    internal enum BallAction { Hold, Dribble, Pass, Shoot, Cross, Clear }

    /// <summary>How a shot is struck.</summary>
    public enum ShotTechnique { Power, PlacedNear, PlacedFar, Curler, Volley, Chip, OutsideFoot, WeakFoot, Header, Penalty }

    /// <summary>A fully specified kick (direction, speed, loft, spin) plus what it is meant to achieve.</summary>
    internal struct KickPlan
    {
        public BallAction Action;
        public KickKind Kind;
        public PlayerAgent Receiver;
        public Vec2 Target;
        public bool Lofted;
        public float Speed;        // ground passes: kick speed; lofted: launch speed
        public float LaunchAngle;  // lofted only
        public float Utility;
        public float Success;
        public Vec2 DribbleTarget;
        public float Heading;      // direction of the kick (radians)
        public float ExpectedGoals;
        /// <summary>A pass played into space ahead of a running team-mate rather than to his feet.</summary>
        public bool IntoSpace;
        /// <summary>A long diagonal / lateral ball to the other flank that relieves pressure (a switch of play).</summary>
        public bool IsSwitch;
    }

    /// <summary>
    /// Decisions in possession (design doc: pass, through ball, cross, dribble, shoot) evaluated as
    /// success probability x value - risk, with xT as value function and xG for shots. Weighted noise
    /// scaled by Decisions/Composure avoids robotic choices.
    /// </summary>
    public sealed partial class MatchEngine
    {
        private readonly List<KickPlan> _options = new List<KickPlan>(64);
        private float _lastShotTime = -10f;
        /// <summary>How the last shot was struck: 0 foot, 1 volley, 2 cut inside, 3 direct free kick, 4 penalty, 5 header.</summary>
        private int _lastShotKind;
        private ShotTechnique _lastShotTechnique;
        private float _riskPremium;
        private bool _shotCountedOnTarget;
        private string _lastPassKey;
        private bool _pendingThrough, _pendingIntoSpace, _pendingSwitch;
        private bool _nextPassIntoSpace;

        private void ThinkOnBall(PlayerAgent p, float dt)
        {
            if (Ball.InHands)
            {
                ThinkGoalkeeperHold(p, dt);
                return;
            }
            p.Task = PlayerTask.OnBall;
            p.HoldTime += dt;
            p.DecisionTimer -= dt;

            if (p.HasPendingPlan)
            {
                if (p.DecisionTimer <= 0f) ExecutePlan(p, p.PendingPlan);
                return;
            }
            if (p.DecisionTimer > 0f) return;

            var plan = p.IsGoalkeeper ? ChooseGoalkeeperFeetAction(p) : ChooseAction(p);
            switch (plan.Action)
            {
                case BallAction.Hold:
                case BallAction.Dribble:
                    p.Target = plan.DribbleTarget;
                    p.Urgency = plan.Action == BallAction.Dribble ? 0.85f : 0.3f;
                    p.DecisionTimer = (plan.Action == BallAction.Dribble ? 0.42f : 0.32f) * (1.25f - 0.5f * p.Team.Tactics.Tempo) * _rng.Range(0.8f, 1.2f);
                    break;
                default:
                    float turn = MathF.Abs(MathUtil.AngleDiff(p.Facing, plan.Heading));
                    float delay = turn / MathUtil.Pi * 0.32f;
                    if (delay < 0.05f)
                    {
                        ExecutePlan(p, plan);
                    }
                    else
                    {
                        p.PendingPlan = plan;
                        p.HasPendingPlan = true;
                        p.DecisionTimer = delay;
                        p.Target = p.Pos + p.Vel * 0.2f;
                    }
                    break;
            }
        }

        // ---- Action selection -------------------------------------------------------------------------

        private KickPlan ChooseAction(PlayerAgent p)
        {
            var team = p.Team;
            Vec2 pos = Ball.Pos;
            float f = team.Forward(pos);
            float l = team.Lateral(pos);
            float pressure = Pressure(p, out float nearestOpp);
            float xtNow = ExpectedThreat.At(f, l);
            float lossNow = LossCost(team, pos);
            // Losing the ball also forfeits what we have now; cautious teams weigh that more.
            _riskPremium = xtNow * (0.6f + 0.8f * (1f - team.Tactics.Mentality)) * (f < -17.5f ? 1.3f : 1f);
            _options.Clear();

            // HOLD / shield
            {
                Vec2 away = Vec2.Zero;
                foreach (var o in team.Opponent.Players)
                {
                    if (o.SentOff) continue;
                    Vec2 d = p.Pos - o.Pos;
                    float len = d.Length;
                    if (len < 5f && len > 1e-3f) away += d / len * (5f - len);
                }
                float holdPenalty = MathF.Max(0f, p.HoldTime - 1.2f) * 0.006f;
                _options.Add(new KickPlan
                {
                    Action = BallAction.Hold,
                    DribbleTarget = Pitch.ClampToField(p.Pos + away.ClampLength(2f), -1.5f),
                    Utility = xtNow * 0.95f - pressure * lossNow * 0.8f - holdPenalty,
                });
            }

            // SHOOT
            float distGoal = Vec2.Distance(pos, team.AttackGoal);
            if (distGoal < 34f && f > 18f)
            {
                float xg = EstimateShotXG(p, pos);
                float tendency = p.Slot.Line == Line.Attack ? 1.25f : (p.Slot.Line == Line.Midfield ? 1.05f : 0.8f);
                if (p.Slot.IsFullBack && f > 30f) tendency *= 1.3f + 0.4f * MathUtil.A(p.Attr.LongShots); // an attacking full-back at the edge of the box has a go
                tendency *= 0.9f + 0.4f * team.Tactics.Mentality;
                if (IsCutInsidePlayer(p) && MathF.Abs(l) < 21f && f > 27f && p.CutInUntil > SimTime) { tendency *= 2.3f; xg *= 1.1f; }
                if (distGoal > 22f) tendency *= 0.6f + 0.6f * MathUtil.A(p.Attr.LongShots);
                // A shot that does not score usually hands the ball over (goal kick, keeper, block).
                float afterShotLoss = 0.006f + 0.25f * _riskPremium;
                _options.Add(new KickPlan
                {
                    Action = BallAction.Shoot,
                    Kind = KickKind.Shot,
                    Target = team.AttackGoal,
                    Heading = (team.AttackGoal - pos).Angle,
                    ExpectedGoals = xg,
                    Utility = xg * tendency - (1f - xg) * afterShotLoss,
                });
            }

            EvaluatePasses(p, pos, pressure);
            EvaluateDribbles(p, pos, pressure, lossNow);
            if (f > 20f && MathF.Abs(l) > 13f) EvaluateCrosses(p, pos);
            if (f < -24f && pressure > 0.45f) EvaluateClearance(p, pos, pressure);
            if (MathF.Abs(l) > 27f && pressure > 0.55f) EvaluateKickOut(p, pos, pressure);

            // Weighted randomness: perception noise grows with poor decisions and pressure on low composure.
            float decisions = MathUtil.A(p.Attr.Decisions);
            float composure = MathUtil.A(p.Attr.Composure);
            float sigma = (0.0015f + 0.006f * (1f - decisions)) * (1f + pressure * (1.1f - composure)) * p.FatigueError;
            int best = 0;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < _options.Count; i++)
            {
                var o = _options[i];
                float perceived = o.Utility * (1f + _rng.Gaussian(0f, 0.12f * (1.2f - decisions))) + _rng.Gaussian(0f, sigma);
                if (perceived > bestScore)
                {
                    bestScore = perceived;
                    best = i;
                }
            }
            var chosen = _options[best];
            if (CollectDiagnostics) Count("decide." + chosen.Action + (chosen.Action == BallAction.Pass ? "." + chosen.Kind + (chosen.Lofted ? ".lofted" : "") : ""));
            if (chosen.Action != BallAction.Dribble && chosen.Action != BallAction.Hold) p.HoldTime = 0f;
            if (chosen.Action == BallAction.Dribble && IsCutInsidePlayer(p) && MathF.Abs(team.Lateral(chosen.DribbleTarget)) < MathF.Abs(l) - 2f && f > 14f && MathF.Abs(l) > 14f)
                p.CutInUntil = SimTime + 3.5f;
            return chosen;
        }

        /// <summary>Value the opponent gets if they win the ball here (their xT).</summary>
        private static float LossCost(TeamAgent team, Vec2 world)
        {
            return ExpectedThreat.At(-team.Forward(world), -team.Lateral(world)) * 1.05f;
        }

        private float EstimateShotXG(PlayerAgent p, Vec2 pos, bool header = false)
        {
            var team = p.Team;
            float xg = ExpectedGoals.FromLocation(pos, team.AttackGoalSign);
            float dist = Vec2.Distance(pos, team.AttackGoal);
            float skill = header ? MathUtil.A(p.Attr.Heading) : (dist > 20f ? MathUtil.A(p.Attr.LongShots) : MathUtil.A(p.Attr.Finishing));
            xg *= 0.65f + 0.7f * skill;
            if (header) xg *= 0.6f;

            // Bodies in the shooting cone block shots.
            Vec2 postA = new Vec2(Pitch.HalfLength * team.AttackGoalSign, Pitch.GoalHalfWidth);
            Vec2 postB = new Vec2(Pitch.HalfLength * team.AttackGoalSign, -Pitch.GoalHalfWidth);
            foreach (var o in team.Opponent.Players)
            {
                if (o.SentOff || o.IsGoalkeeper) continue;
                float d = Vec2.Distance(o.Pos, pos);
                if (d > 12f) continue;
                if (PointInTriangle(o.Pos, pos, postA, postB)) xg *= d < 3f ? 0.62f : 0.8f;
                else if (d < 1.6f) xg *= 0.8f;
            }
            return xg;
        }

        private static bool PointInTriangle(Vec2 p, Vec2 a, Vec2 b, Vec2 c)
        {
            float d1 = Vec2.Cross(b - a, p - a);
            float d2 = Vec2.Cross(c - b, p - b);
            float d3 = Vec2.Cross(a - c, p - c);
            bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNeg && hasPos);
        }

        // ---- Passing ------------------------------------------------------------------------------------

        private float PassAngleSigma(PlayerAgent p, float pressure, bool lofted)
        {
            float skill = MathUtil.A(p.Attr.Passing);
            float sigma = (0.018f + 0.075f * (1f - skill)) * (1f + 0.6f * pressure * (1.2f - MathUtil.A(p.Attr.Composure))) * p.FatigueError;
            if (lofted) sigma *= 1.35f;
            return sigma;
        }

        private void EvaluatePasses(PlayerAgent p, Vec2 from, float pressure)
        {
            var team = p.Team;
            float fromF = team.Forward(from);
            float offsideLine = MathF.Max(MathF.Max(SecondLastDefenderForward(team), fromF), 0f);
            float noticeOffside = 0.3f + 0.45f * MathUtil.A(p.Attr.Decisions);

            foreach (var q in team.Players)
            {
                if (q == p || q.SentOff) continue;
                float dist = Vec2.Distance(q.Pos, from);
                if (dist < 4f || dist > 58f) continue;
                if (q.IsGoalkeeper && (fromF > -8f || pressure < 0.35f)) continue;

                // Perceived offside: the passer judges the line with an error (better decision makers see it better).
                float margin = team.Forward(q.Pos) - offsideLine;
                float perceivedMargin = margin + _rng.Gaussian(0f, 0.5f + 1.4f * (1f - MathUtil.A(p.Attr.Decisions)));
                bool offside = perceivedMargin > 0f && _rng.Chance(noticeOffside); // what the passer believes
                if (offside) continue;

                Vec2 lead = q.Vel * 0.45f;
                Vec2 feet = Pitch.ClampToField(q.Pos + lead, -1f);
                AddGroundPass(p, q, from, feet, KickKind.Pass, pressure);
                if (dist > 22f) AddLoftedPass(p, q, from, Pitch.ClampToField(q.Pos + lead * 1.6f, -1.5f), pressure, KickKind.LongPass);

                // Through balls into space behind the line for runners / attackers who are onside now - aimed through
                // the widest gap in the back line near the runner (between centre-backs, or full-back and centre-back).
                bool attacker = q.Slot.Line == Line.Attack || q.Slot.IsWideMidfielder || (q.Slot.IsFullBack && team.Forward(q.Pos) > 10f)
                                || (q.Slot.IsCentralMidfielder && q.RunTimer > 0f);
                bool onTheLine = team.Forward(q.Pos) > offsideLine - 4f;
                float vision = MathUtil.A(p.Attr.Creativity) * 0.7f + MathUtil.A(p.Attr.Decisions) * 0.3f;
                bool sees = _rng.Chance(0.25f + 0.6f * vision);
                if (attacker && !offside && sees && (q.RunTimer > 0f || onTheLine) && team.Forward(q.Pos) > fromF - 6f)
                {
                    float qLat = team.Lateral(q.Pos);
                    float gapLat = BestGapInLine(team, offsideLine, qLat);
                    for (int k = 0; k < 2; k++)
                    {
                        float depth = k == 0 ? 6f : 13f;
                        float tf = MathF.Min(offsideLine + depth, 49f);
                        if (tf < team.Forward(q.Pos) + 3f) continue;
                        // the run bends towards the gap
                        float tl = MathUtil.Clamp(MathUtil.Lerp(qLat * 0.85f, gapLat, 0.6f), -30f, 30f);
                        Vec2 target = team.ToWorld(tf, tl);
                        AddGroundPass(p, q, from, target, KickKind.ThroughBall, pressure);
                        if (Vec2.Distance(target, from) > 18f) AddLoftedPass(p, q, from, target, pressure, KickKind.ThroughBall);
                    }
                }

                // Into space: a team-mate already running (a run, an overlap, a wide man going down the line) gets the
                // ball played ahead of him, into the space he is running into, so he can take it in his stride.
                float qSpeed = q.Vel.Length;
                if (!q.IsGoalkeeper && qSpeed > 3.4f && _rng.Chance(0.25f + 0.45f * vision))
                {
                    Vec2 run = q.Vel / qSpeed;
                    if (Vec2.Dot(run, team.DirToWorld(Vec2.UnitX)) > 0.2f)
                    {
                        float ahead = MathUtil.Clamp(qSpeed * 1.2f, 4.5f, 9f);
                        Vec2 target = Pitch.ClampToField(q.Pos + run * ahead, -2f);
                        bool behindLine = team.Forward(target) > offsideLine;
                        if (!behindLine && SpaceAround(team, target) > 5.5f && SpaceAround(team, q.Pos) > 2.5f)
                        {
                            _nextPassIntoSpace = true;
                            AddGroundPass(p, q, from, target, KickKind.ThroughBall, pressure);
                            _nextPassIntoSpace = false;
                        }
                    }
                }
            }
        }

        private void AddGroundPass(PlayerAgent p, PlayerAgent q, Vec2 from, Vec2 target, KickKind kind, float pressure)
        {
            var team = p.Team;
            Vec2 delta = target - from;
            float d = delta.Length;
            if (d < 3f) return;
            Vec2 dir = delta / d;

            float tRecv = q.TimeToReach(target, -0.08f);
            float maxPower = 22f + 9f * MathUtil.A(p.Attr.Passing) + 4f * MathUtil.A(p.Attr.PhysicalPower);
            float v0;
            if (kind == KickKind.ThroughBall)
            {
                v0 = Roll.KickSpeedForTime(d, tRecv * 0.92f, 2.5f);
            }
            else
            {
                float arrival = MathUtil.Clamp(5f + 0.09f * d, 5f, 10.5f) * (0.9f + 0.25f * team.Tactics.Tempo);
                v0 = Roll.KickSpeedFor(d, arrival);
            }
            if (v0 > maxPower) return;
            float tArrive = Roll.TimeToDistance(v0, d);
            if (float.IsInfinity(tArrive)) return;
            if (kind == KickKind.Pass && tRecv > tArrive + 0.9f) return;

            float pIntercept = LaneInterceptionRisk(team, from, dir, d, v0, kind == KickKind.Pass ? 1.6f : 0.5f, MathF.Max(tRecv, 0f), out Vec2 riskPoint);
            float oppAtTarget = float.PositiveInfinity;
            foreach (var o in team.Opponent.Players)
            {
                if (o.SentOff) continue;
                float t = o.TimeToReach(target);
                if (t < oppAtTarget) oppAtTarget = t;
            }
            float arrivalTime = MathF.Max(tArrive, tRecv);
            float receiverPressure = MathUtil.Clamp01((arrivalTime + 0.9f - oppAtTarget) / 1.2f);
            float arrivalSpeed = Roll.SpeedAfterDistance(v0, d);
            float pControl = MathUtil.Clamp(0.985f - 0.018f * MathF.Max(0f, arrivalSpeed - 7f) * (1.3f - MathUtil.A(q.Attr.BallControl)) - 0.25f * receiverPressure, 0.45f, 0.99f);
            if (kind == KickKind.ThroughBall)
            {
                // The runner must win the race to the ball, keepers included.
                float margin = oppAtTarget - tRecv;
                pControl *= MathUtil.Clamp01(0.5f + margin / 0.8f);
            }

            float sigma = PassAngleSigma(p, pressure, false);
            float lateralSd = MathF.Max(0.05f, d * sigma);
            float capture = 1.2f + 0.05f * d + (kind == KickKind.ThroughBall ? 1.5f : 0f);
            float pAccuracy = 1f - MathF.Exp(-0.635f * capture * capture / (lateralSd * lateralSd));

            float success = (1f - pIntercept) * pControl * pAccuracy;
            // A ball into space must also be weighted perfectly - harder than the lane model alone says.
            if (_nextPassIntoSpace) success *= 0.85f;
            // Through balls behind the line: keepers sweep, defenders turn and recover - the race is harder to win
            // than the simple estimate suggests (measured: well below the predicted success).
            else if (kind == KickKind.ThroughBall) success *= 0.68f;
            float value = ExpectedThreat.At(team, target) * (1f - 0.3f * receiverPressure);
            float relief = SwitchRelief(p, from, target, pressure);
            value += relief;
            // Cut-back: from the by-line, low and square to a team-mate arriving in the middle of the box.
            if (team.Forward(from) > 38f && MathF.Abs(team.Lateral(from)) > 13f && team.Forward(target) > 34f && team.Forward(target) < team.Forward(from) - 2f
                && MathF.Abs(team.Lateral(target)) < 11f && kind == KickKind.Pass)
                value *= 1.5f;
            float progress = team.Forward(target) - team.Forward(from);
            value += MathF.Max(0f, progress) * 0.00022f * (0.6f + 0.8f * team.Tactics.Tempo);
            if (progress < -5f) value -= 0.0006f * (team.Tactics.Tempo - 0.3f) * (-progress / 10f);
            float loss = LossCost(team, pIntercept > 0.3f ? riskPoint : target) + _riskPremium;
            float utility = success * value - (1f - success) * loss;

            _options.Add(new KickPlan
            {
                Action = BallAction.Pass,
                Kind = kind,
                Receiver = q,
                Target = target,
                Lofted = false,
                Speed = v0,
                Heading = dir.Angle,
                Success = success,
                Utility = utility,
                IntoSpace = _nextPassIntoSpace,
                IsSwitch = relief > 0.014f && kind != KickKind.ThroughBall,
            });
        }

        /// <summary>
        /// The value of a switch of play: when the carrier is hemmed in on one flank (a crowd of opponents round him, or
        /// real pressure), a ball to a free team-mate on the other side takes the pressure off. A full-back doing it
        /// from the touchline is the classic case. Zero for anything that is not a lateral ball to open space.
        /// </summary>
        private float SwitchRelief(PlayerAgent p, Vec2 from, Vec2 target, float pressure)
        {
            var team = p.Team;
            if (MathF.Abs(team.Lateral(target) - team.Lateral(from)) < 22f) return 0f;
            if (team.Forward(target) < team.Forward(from) - 10f) return 0f;
            int crowd = 0;
            foreach (var o in team.Opponent.Players)
            {
                if (!o.SentOff && Vec2.DistanceSq(o.Pos, from) < 13f * 13f) crowd++;
            }
            if (crowd < 2 && pressure < 0.35f) return 0f;
            float free = MathUtil.Clamp01((SpaceAround(team, target) - 4f) / 8f);
            float vision = 0.6f + 0.6f * MathUtil.Clamp01((p.Attr.Passing + p.Attr.Decisions) / 200f);
            return (0.005f * MathF.Min(crowd, 4) + 0.012f * pressure) * free * vision * (p.Slot.IsFullBack ? 1.8f : 1f);
        }

        /// <summary>Distance from a point to the nearest opponent outfield player (how much space there is).</summary>
        private float SpaceAround(TeamAgent team, Vec2 point)
        {
            float best = float.PositiveInfinity;
            foreach (var o in team.Opponent.Players)
            {
                if (o.SentOff || o.IsGoalkeeper) continue;
                float d = Vec2.Distance(o.Pos, point);
                if (d < best) best = d;
            }
            return best;
        }

        private readonly List<float> _lineLaterals = new List<float>(11);

        /// <summary>
        /// The lateral position (in the attacking team's frame) of the widest gap between the opponents holding the
        /// last line, weighted towards the runner: where a through ball splits the defence.
        /// </summary>
        private float BestGapInLine(TeamAgent team, float offsideLine, float runnerLat)
        {
            _lineLaterals.Clear();
            foreach (var o in team.Opponent.Players)
            {
                if (o.SentOff || o.IsGoalkeeper) continue;
                if (team.Forward(o.Pos) > offsideLine - 9f) _lineLaterals.Add(team.Lateral(o.Pos));
            }
            if (_lineLaterals.Count == 0) return runnerLat;
            _lineLaterals.Sort();
            float best = runnerLat, bestScore = float.NegativeInfinity;
            float prev = -Pitch.HalfWidth;
            for (int i = 0; i <= _lineLaterals.Count; i++)
            {
                float next = i < _lineLaterals.Count ? _lineLaterals[i] : Pitch.HalfWidth;
                float width = next - prev;
                float mid = (prev + next) * 0.5f;
                // touchline "gaps" are only half gaps (the ball runs out)
                if (i == 0 || i == _lineLaterals.Count) width *= 0.5f;
                float score = width - 0.35f * MathF.Abs(mid - runnerLat) - 0.1f * MathF.Abs(mid);
                if (width > 5f && score > bestScore) { bestScore = score; best = mid; }
                prev = next;
            }
            return best;
        }

        private void AddLoftedPass(PlayerAgent p, PlayerAgent q, Vec2 from, Vec2 target, float pressure, KickKind kind)
        {
            var team = p.Team;
            Vec2 delta = target - from;
            float d = delta.Length;
            if (d < 12f || d > 62f) return;
            float angle = kind == KickKind.ThroughBall ? 0.62f : (d > 35f ? 0.52f : 0.46f);
            // Drag-corrected ballistic estimate (exact solve happens only when the pass is executed).
            float v = MathF.Sqrt(d * Config.Gravity / MathF.Sin(2f * angle)) * (1f + 0.0032f * d);
            float maxPower = 25f + 8f * MathUtil.A(p.Attr.Passing) + 5f * MathUtil.A(p.Attr.PhysicalPower);
            if (v > maxPower) return;
            float flight = 2f * v * MathF.Sin(angle) / Config.Gravity * 0.93f;

            float tRecv = q.TimeToReach(target, -0.05f);
            float oppT = float.PositiveInfinity;
            PlayerAgent oppKeeper = team.Opponent.Goalkeeper;
            foreach (var o in team.Opponent.Players)
            {
                if (o.SentOff) continue;
                float t = o.TimeToReach(target);
                if (o == oppKeeper && Pitch.InPenaltyArea(target, team.AttackGoalSign)) t -= 0.35f;
                if (t < oppT) oppT = t;
            }
            float arrive = MathF.Max(flight, tRecv);
            float pWin = MathUtil.Sigmoid((oppT - arrive) / 0.35f);
            float pControl = 0.78f + 0.16f * MathUtil.A(q.Attr.BallControl);
            float sigma = PassAngleSigma(p, pressure, true);
            float sd = MathF.Max(0.1f, d * sigma);
            float capture = 2.2f + 0.04f * d;
            float pAccuracy = 1f - MathF.Exp(-0.635f * capture * capture / (sd * sd));
            float success = pWin * pControl * pAccuracy;
            if (kind == KickKind.ThroughBall) success *= 0.8f; // the keeper and the recovering defenders (see AddGroundPass)
            float value = ExpectedThreat.At(team, target);
            float relief = SwitchRelief(p, from, target, pressure);
            value += relief;
            float progress = team.Forward(target) - team.Forward(from);
            value += MathF.Max(0f, progress) * 0.0002f * (0.6f + 0.8f * team.Tactics.Tempo);
            float utility = success * value - (1f - success) * (LossCost(team, target) * 0.85f + _riskPremium);

            _options.Add(new KickPlan
            {
                Action = BallAction.Pass,
                Kind = kind == KickKind.ThroughBall ? KickKind.ThroughBall : KickKind.LongPass,
                Receiver = q,
                Target = target,
                Lofted = true,
                LaunchAngle = angle,
                Speed = v,
                Heading = delta.Angle,
                Success = success,
                Utility = utility,
                IsSwitch = relief > 0.014f && kind != KickKind.ThroughBall,
            });
        }

        /// <summary>
        /// Probability that an opponent intercepts a ground pass: for points along the lane compare when the
        /// ball gets there with when each opponent can (reaction + acceleration + top speed).
        /// </summary>
        private float LaneInterceptionRisk(TeamAgent team, Vec2 from, Vec2 dir, float d, float v0, float skipEnd, float receiverTime, out Vec2 riskPoint)
        {
            float maxRisk = 0f, secondRisk = 0f;
            riskPoint = from + dir * (d * 0.5f);
            float totalTime = Roll.TimeToDistance(v0, d);
            var keeper = team.Opponent.Goalkeeper;
            foreach (var o in team.Opponent.Players)
            {
                if (o.SentOff) continue;
                float seg = Vec2.DistanceToSegment(o.Pos, from, from + dir * d, out _);
                if (seg > 2f + o.MaxSpeed * (totalTime + 0.2f)) continue;
                float risk = 0f;
                Vec2 rp = riskPoint;
                // Body block: an opponent standing on the line blocks it without having to react.
                float laneDist = Vec2.DistanceToSegment(o.Pos, from, from + dir * MathF.Max(0.5f, d - skipEnd), out float laneT);
                if (laneDist < 0.95f)
                {
                    risk = 0.85f - laneDist * 0.4f;
                    rp = from + dir * (laneT * MathF.Max(0.5f, d - skipEnd));
                }
                for (float s = 0.6f; s <= d - skipEnd + 0.01f; s += s < 4f ? 1.2f : 2.5f)
                {
                    Vec2 pt = from + dir * s;
                    float tb = Roll.TimeToDistance(v0, s);
                    float to = o.TimeToReach(pt);
                    if (o == keeper && Pitch.InPenaltyArea(pt, team.AttackGoalSign)) to -= 0.25f;
                    if (s > d - 2.5f) tb = MathF.Min(tb, receiverTime + 0.15f);
                    float r = MathUtil.Clamp01(0.5f + (tb - to) / 0.5f);
                    if (r > risk) { risk = r; rp = pt; }
                    if (risk >= 0.99f) break;
                }
                if (risk > maxRisk) { secondRisk = maxRisk; maxRisk = risk; riskPoint = rp; }
                else if (risk > secondRisk) secondRisk = risk;
            }
            return 1f - (1f - maxRisk) * (1f - 0.35f * secondRisk);
        }

        // ---- Dribbling ----------------------------------------------------------------------------------

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
                if (cutIn && inward && f > 14f && MathF.Abs(team.Lateral(pos)) > 14f) value += 0.03f * cutInSkill;
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
                _options.Add(new KickPlan { Action = BallAction.Dribble, DribbleTarget = target, Utility = utility, Success = keep });
            }
        }

        // ---- Crossing and clearing -----------------------------------------------------------------------

        private void EvaluateCrosses(PlayerAgent p, Vec2 pos)
        {
            var team = p.Team;
            float side = MathF.Sign(team.Lateral(pos));
            var zones = new[]
            {
                new Vec2(46.5f, side * 3f),   // near post
                new Vec2(41.5f, 0f),          // penalty spot
                new Vec2(47f, -side * 3.5f),  // far post
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
                float delivery = 0.45f + 0.4f * crossing;
                float headerXg = ExpectedGoals.FromLocation(target, team.AttackGoalSign) * 0.55f * (0.65f + 0.7f * MathUtil.A(bestMate.Attr.Heading));
                float utility = pWin * delivery * (headerXg + 0.01f) - (1f - pWin * delivery) * (LossCost(team, target) * 0.25f + _riskPremium * 0.6f);
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

        // ---- Execution ---------------------------------------------------------------------------------

        private void ExecutePlan(PlayerAgent p, KickPlan plan)
        {
            p.HasPendingPlan = false;
            if (Ball.Owner != p) return; // lost the ball while turning
            if (plan.Action == BallAction.Pass && CollectDiagnostics)
            {
                string k = "exec." + plan.Kind + (plan.Lofted ? ".lofted" : "");
                Count(k + ".n");
                Diagnostics.TryGetValue(k + ".predicted%", out int predicted);
                Diagnostics[k + ".predicted%"] = predicted + (int)(plan.Success * 100f);
                _lastPassKey = k;
            }
            if (plan.Action == BallAction.Pass)
            {
                _pendingThrough = plan.Kind == KickKind.ThroughBall && !plan.IntoSpace;
                _pendingIntoSpace = plan.IntoSpace;
                _pendingSwitch = plan.IsSwitch;
                if (_pendingSwitch)
                {
                    p.Team.Stats.Switches++;
                    if (CollectDiagnostics) Count("pass.switch" + (p.Slot.IsFullBack ? ".fullback" : ""));
                }
                if (_pendingThrough) p.Team.Stats.ThroughBalls++;
                if (_pendingIntoSpace) p.Team.Stats.PassesIntoSpace++;
                if (CollectDiagnostics && plan.IntoSpace) Count("pass.intospace");
            }
            else _pendingThrough = _pendingIntoSpace = _pendingSwitch = false;
            float pressure = Pressure(p, out _);
            switch (plan.Action)
            {
                case BallAction.Shoot:
                    ExecuteShot(p, false, false, plan.ExpectedGoals);
                    break;
                case BallAction.Cross:
                    p.Team.Stats.Crosses++;
                    if (p.Slot.IsFullBack) p.Team.Stats.FullBackCrosses++;
                    else if (p.Slot.IsWideMidfielder || (p.Slot.Line == Line.Attack && MathF.Abs(p.Slot.BaseLateral) > 12f)) p.Team.Stats.WingerCrosses++;
                    KickLofted(p, plan.Target, plan.LaunchAngle, KickKind.Cross, plan.Receiver, pressure, MathUtil.A(p.Attr.Crossing), curl: true);
                    break;
                case BallAction.Clear:
                    KickLofted(p, plan.Target, plan.LaunchAngle, KickKind.Clearance, null, pressure, 0.35f + 0.3f * MathUtil.A(p.Attr.Passing), curl: false);
                    break;
                case BallAction.Pass:
                    if (!plan.Lofted && plan.Kind == KickKind.Pass && p.Team.Forward(Ball.Pos) > 38f && MathF.Abs(p.Team.Lateral(Ball.Pos)) > 13f
                        && p.Team.Forward(plan.Target) > 34f && MathF.Abs(p.Team.Lateral(plan.Target)) < 11f && p.Team.Forward(plan.Target) < p.Team.Forward(Ball.Pos) - 2f)
                        p.Team.Stats.CutBacks++;
                    if (plan.Lofted) KickLofted(p, plan.Target, plan.LaunchAngle, plan.Kind, plan.Receiver, pressure, MathUtil.A(p.Attr.Passing), curl: false);
                    else KickGround(p, plan, pressure);
                    break;
            }
        }

        /// <summary>+1 when the kicker's natural curl bends the ball to the left of its path (right-footers), -1 for left-footers.</summary>
        private static float NaturalCurlSign(PlayerAgent p) => p.Attr.StrongFoot == Foot.Right ? 1f : -1f;

        private float WeakFootFactor(PlayerAgent p, Vec2 direction)
        {
            // Kicking across the body to the strong-foot side is natural; otherwise the weak foot is likely used.
            float cross = Vec2.Cross(p.FacingDir, direction);
            bool needsLeft = cross > 0.15f;
            bool needsRight = cross < -0.15f;
            int rating;
            if (needsLeft) rating = p.Attr.LeftFoot;
            else if (needsRight) rating = p.Attr.RightFoot;
            else rating = p.Attr.StrongFootRating;
            if (_rng.Chance(0.35f)) rating = p.Attr.StrongFootRating;
            return 1f + (3 - MathUtil.Clamp(rating, 1, 3)) * 0.22f;
        }

        private void KickGround(PlayerAgent p, KickPlan plan, float pressure)
        {
            Vec2 from = Ball.Pos;
            Vec2 dir = Vec2.FromAngle(plan.Heading);
            float sigma = PassAngleSigma(p, pressure, false) * WeakFootFactor(p, dir);
            float angle = plan.Heading + _rng.Gaussian(0f, sigma);
            float speedError = (plan.Kind == KickKind.ThroughBall ? 0.07f : 0.04f) + 0.07f * (1f - MathUtil.A(p.Attr.Passing));
            float speed = plan.Speed * (1f + _rng.Gaussian(0f, speedError));
            Vec2 v = Vec2.FromAngle(angle) * speed;
            KickBall(p, from, v, 0f, 0f, 0f, plan.Kind, plan.Receiver, plan.Target, false, false);
            EmitPass(p, plan.Receiver, plan.Kind);
        }

        /// <summary>Lofted kick (long pass, cross, clearance, goal kick) solved to land on <paramref name="target"/>.</summary>
        private void KickLofted(PlayerAgent p, Vec2 target, float launchAngle, KickKind kind, PlayerAgent receiver, float pressure, float skill, bool curl, float startZ = Pitch.BallRadius)
        {
            Vec2 from = Ball.Pos;
            float d = Vec2.Distance(from, target);
            float speed = KickSolver.LoftSpeedFor(MathF.Max(2f, d), launchAngle, Config, out _);
            Vec2 dir = (target - from).Normalized;
            float sigma = (0.02f + 0.08f * (1f - skill)) * (1f + 0.6f * pressure) * p.FatigueError * WeakFootFactor(p, dir);
            if (kind == KickKind.Clearance) sigma *= 1.6f;
            float angle = dir.Angle + _rng.Gaussian(0f, sigma);
            speed *= 1f + _rng.Gaussian(0f, 0.035f + 0.05f * (1f - skill));
            float horizontal = speed * MathF.Cos(launchAngle);
            Vec2 v = Vec2.FromAngle(angle) * horizontal;
            float vz = speed * MathF.Sin(launchAngle);
            float spin = 0f;
            if (curl)
            {
                // The ball bends the way the kicking foot naturally curls it: right-footers bend it to the left, left-footers
                // to the right. From the left wing that is an in-swinger for a right-footer, from the right wing an out-swinger.
                spin = NaturalCurlSign(p) * _rng.Range(10f, 30f);
            }
            KickBall(p, from, v, vz, spin, 0f, kind, receiver, target, false, false, startZ);
            if (kind == KickKind.Cross) Emit(MatchEventType.Cross, p.Team, p, receiver, from);
            else if (kind == KickKind.Clearance) Emit(MatchEventType.Clearance, p.Team, p, null, from);
            else EmitPass(p, receiver, kind);
        }

        private void EmitPass(PlayerAgent p, PlayerAgent receiver, KickKind kind)
        {
            float value = _pendingSwitch ? 3f : (kind != KickKind.ThroughBall ? 0f : _pendingIntoSpace ? 2f : 1f);
            Emit(kind == KickKind.LongPass ? MatchEventType.LongBall : MatchEventType.Pass, p.Team, p, receiver, Ball.Pos, value);
        }

        /// <summary>Shot at goal: aims at a corner away from the keeper, then applies skill-based error.</summary>
        private void ExecuteShot(PlayerAgent p, bool header, bool penalty, float xg, bool volley = false)
        {
            var team = p.Team;
            int goalSign = team.AttackGoalSign;
            Vec2 from = Ball.Pos;
            float startZ = Ball.Z;
            float dist = Vec2.Distance(from, team.AttackGoal);
            var keeper = team.Opponent.Goalkeeper;

            float skill;
            if (penalty) skill = MathUtil.A((p.Attr.Penalty * 2 + p.Attr.Composure) / 3);
            else if (header) skill = MathUtil.A(p.Attr.Heading);
            else if (dist > 20f) skill = MathUtil.A(p.Attr.LongShots);
            else skill = MathUtil.A(p.Attr.Finishing);

            // How he strikes it: picked from the situation (distance, angle, where the keeper is, which foot the ball is
            // on) and the player (technique, composure, how good his weaker foot is).
            var tech = header ? ShotTechnique.Header : penalty ? ShotTechnique.Penalty : volley ? ShotTechnique.Volley
                     : ChooseShotTechnique(p, from, dist, keeper);
            float keeperY = keeper != null ? keeper.Pos.Y : 0f;
            float nearSide = MathF.Abs(from.Y) > 1f ? MathF.Sign(from.Y) : (_rng.Chance(0.5f) ? 1f : -1f);
            // Default aim: far side from the keeper (or a random side when he is central).
            float side = MathF.Abs(keeperY) > 0.6f ? -MathF.Sign(keeperY) : (_rng.Chance(0.5f) ? 1f : -1f);
            float naturalCurl = NaturalCurlSign(p);
            switch (tech)
            {
                case ShotTechnique.Penalty: side = _rng.Chance(0.5f) ? 1f : -1f; break;
                case ShotTechnique.PlacedNear: side = nearSide; break;
                case ShotTechnique.PlacedFar:
                case ShotTechnique.Curler: side = -nearSide; break;
                case ShotTechnique.OutsideFoot:
                    // The outside of the strong foot bends the ball the other way: aim at the corner that curl comes back to.
                    side = NeededCurl(from, goalSign, 1f) == -naturalCurl ? 1f : -1f;
                    break;
            }
            float margin = penalty ? 0.45f + 0.5f * (1f - skill) + _rng.Range(0f, 0.4f) : 0.35f + 0.9f * (1f - skill) + _rng.Range(0f, 0.5f);
            if (tech == ShotTechnique.PlacedNear || tech == ShotTechnique.PlacedFar) margin = 0.3f + 0.6f * (1f - skill) + _rng.Range(0f, 0.35f);
            float aimY = side * MathF.Max(0.2f, Pitch.GoalHalfWidth - margin);
            if (penalty && _rng.Chance(0.12f)) aimY = _rng.Range(-0.8f, 0.8f);
            float r = _rng.NextFloat();
            float aimZ = header ? _rng.Range(0.3f, 1.4f) : (r < 0.58f ? _rng.Range(0.2f, 0.6f) : (r < 0.78f ? _rng.Range(0.7f, 1.4f) : _rng.Range(1.5f, 2.15f)));

            float power = header ? _rng.Range(11f, 15f) + 3f * MathUtil.A(p.Attr.PhysicalPower)
                                 : 20f + 6f * skill + 4f * MathUtil.A(p.Attr.PhysicalPower) + _rng.Range(-1.5f, 1.5f);
            if (penalty) power = _rng.Range(21f, 27f);
            float curlSign = 0f, curlMag = 0f, accuracy = 1f;
            switch (tech)
            {
                case ShotTechnique.PlacedNear:
                case ShotTechnique.PlacedFar:
                    // Side-foot "pass into the net": less pace, much more control, kept low.
                    power = 15f + 5f * skill + _rng.Range(-1f, 1f);
                    aimZ = _rng.Range(0.15f, 0.7f);
                    accuracy = 0.85f;
                    break;
                case ShotTechnique.Curler:
                    power = 19f + 5f * skill + _rng.Range(-1f, 1f);
                    aimZ = _rng.Range(0.5f, 1.6f);
                    curlSign = naturalCurl;
                    curlMag = _rng.Range(9f, 17f) * (0.7f + 0.5f * skill);
                    accuracy = 1.5f; // a fine margin
                    break;
                case ShotTechnique.OutsideFoot:
                    power = 19f + 5f * skill + _rng.Range(-1f, 1f);
                    aimZ = _rng.Range(0.4f, 1.6f);
                    curlSign = -naturalCurl;
                    curlMag = _rng.Range(7f, 13f) * (0.7f + 0.5f * skill);
                    accuracy = 1.7f; // hard to get right
                    break;
                case ShotTechnique.Chip:
                    // Scooped up over the keeper who has come off his line, dropping under the bar.
                    aimY = -MathF.Sign(keeperY == 0f ? 1f : keeperY) * _rng.Range(0f, 1.6f);
                    aimZ = _rng.Range(1.3f, 2.1f);
                    power = MathUtil.Clamp(6.5f + 0.36f * dist, 9f, 15f);
                    accuracy = 1.25f;
                    break;
                case ShotTechnique.WeakFoot:
                    // On the weaker foot: less pace and less control the poorer that foot is.
                    power *= 0.86f + 0.05f * p.Attr.WeakFootRating;
                    accuracy = 1f + (3 - MathUtil.Clamp(p.Attr.WeakFootRating, 1, 3)) * 0.3f;
                    break;
            }
            Vec2 aim = new Vec2(Pitch.HalfLength * goalSign, aimY);
            Vec2 dir = (aim - from).Normalized;
            float distToAim = Vec2.Distance(from, aim);
            if (curlMag > 0f)
            {
                // Start wide of the target and let the curl bring it back in (the same shape as a free kick).
                dir = dir.Rotate(-curlSign * (0.04f + 0.02f * curlMag / 10f));
            }
            Vec2 horizontal = dir * power;
            float vz = KickSolver.VerticalSpeedFor(horizontal, startZ, distToAim, aimZ, Config, curlSign * curlMag, 0f);

            float pressure = penalty ? 0f : Pressure(p, out _);
            float composure = MathUtil.A(p.Attr.Composure);
            float stake = penalty ? 0.25f : 0f;
            float sigmaA = (0.085f + 0.16f * (1f - skill)) * (1f + 0.75f * pressure * (1.2f - composure) + stake) * p.FatigueError;
            if (penalty) sigmaA *= 0.2f;
            // (the techniques for the weaker side already price in which foot is used)
            if (tech == ShotTechnique.Power || tech == ShotTechnique.PlacedNear || tech == ShotTechnique.PlacedFar) sigmaA *= WeakFootFactor(p, dir);
            if (volley) sigmaA *= 1.35f;
            sigmaA *= accuracy;
            float sigmaZ = (0.6f + 1.8f * (1f - skill)) * (1f + 0.5f * pressure) * p.FatigueError;
            if (penalty) sigmaZ *= 0.35f;
            if (tech == ShotTechnique.PlacedNear || tech == ShotTechnique.PlacedFar) sigmaZ *= 0.6f;
            if (tech == ShotTechnique.Chip) sigmaZ *= 0.5f; // a soft lob: errors are in length, not height
            // Technique error that does not shrink with distance (snatched/skied finishes at close range).
            float flight = MathF.Max(0.15f, distToAim / MathF.Max(5f, power));
            float fixedLateral = (penalty ? 0.15f : 1.0f) * (1.3f - skill) * (1f + pressure);
            float fixedHeight = (penalty ? 0.15f : 0.55f + 1.0f * (1f - skill)) * (1f + 0.6f * pressure);
            if (tech == ShotTechnique.Chip) fixedHeight *= 0.3f;
            float angle = dir.Angle + _rng.Gaussian(0f, sigmaA) + _rng.Gaussian(0f, fixedLateral / MathF.Max(3f, distToAim));
            vz += _rng.Gaussian(0f, sigmaZ) + _rng.Gaussian(0f, fixedHeight / flight);
            Vec2 v = Vec2.FromAngle(angle) * power;
            float curl = header ? 0f : (curlMag > 0f ? curlSign * curlMag + _rng.Gaussian(0f, 2.5f) : _rng.Gaussian(0f, tech == ShotTechnique.Chip ? 1f : 6f));
            if (CollectDiagnostics) Count("shot.tech." + tech);
            _lastShotTechnique = tech;

            KickBall(p, from, v, vz, curl, 0f, header ? KickKind.Header : KickKind.Shot, null, aim, penalty, false, startZ);
            Ball.IsShot = true;
            Ball.Shooter = p;
            Ball.ShotSpeed = power;
            _lastShotTime = SimTime;
            _shotCountedOnTarget = false;
            team.Stats.Shots++;
            team.Stats.ExpectedGoals += xg;
            if (p.Slot.IsFullBack) team.Stats.FullBackShots++;
            else if (p.Slot.IsWideMidfielder || (p.Slot.Line == Line.Attack && MathF.Abs(p.Slot.BaseLateral) > 12f)) team.Stats.WingerShots++;
            p.Shots++;
            if (CollectDiagnostics) Count("shot." + (header ? "header" : penalty ? "penalty" : "foot") + ".d" + ((int)(dist / 6f) * 6));

            // A defender tight on the shooter and goal-side can throw himself in the way.
            if (!penalty)
            {
                foreach (var d in team.Opponent.Players)
                {
                    if (d.SentOff || d.IsGoalkeeper || d.StunTimer > 0f) continue;
                    Vec2 rel = d.Pos - from;
                    float gap = rel.Length;
                    if (gap > 2.2f || Vec2.Dot(rel, dir) <= 0.2f * gap) continue;
                    float pBlock = 0.42f * (1f - gap / 2.2f) + 0.12f * MathUtil.A(d.Attr.Tackling) + 0.06f * MathUtil.A(d.Attr.Positioning);
                    if (_rng.Chance(pBlock))
                    {
                        Emit(MatchEventType.Shot, team, p, null, from, xg);
                        team.Stats.ShotsBlocked++;
                        Emit(MatchEventType.ShotBlocked, d.Team, d, p, from);
                        Ball.IsShot = false;
                        Vec2 bdir = dir.Rotate(_rng.Range(-1.4f, 1.4f));
                        SetDeflection(d, bdir * _rng.Range(4f, 11f), _rng.Range(0f, 4f));
                        Ball.Shooter = p;
                        return;
                    }
                }
            }

            // Predict where it crosses the goal line: on target?
            var sim = Ball.M;
            bool onTarget = false;
            Vec2 crossing = aim;
            for (int i = 0; i < 400; i++)
            {
                BallPhysics.Step(ref sim, Config, Config.FixedStep);
                if (sim.X * goalSign >= Pitch.HalfLength)
                {
                    onTarget = MathF.Abs(sim.Y) < Pitch.GoalHalfWidth - Pitch.BallRadius * 0.5f && sim.Z < Pitch.GoalHeight;
                    crossing = sim.Position2D;
                    break;
                }
                if (sim.HorizontalSpeed < 1f) break;
            }
            Ball.ShotOnTarget = onTarget;
            Ball.ShotGoalkeeperDistance = keeper != null ? Vec2.DistanceToSegment(keeper.Pos, from, crossing, out _) : 3f;
            bool cutIn = !header && !penalty && !volley && p.CutInUntil > SimTime;
            if (cutIn && tech == ShotTechnique.Curler && CollectDiagnostics) Count("shot.cutin.curl");
            if (volley) team.Stats.Volleys++;
            if (cutIn) team.Stats.CutInShots++;
            if (header) team.Stats.Headers++;
            p.CutInUntil = -1f;
            _lastShotKind = header ? 5 : penalty ? 4 : volley ? 1 : (cutIn ? 2 : ShotDetail(tech));
            Emit(MatchEventType.Shot, team, p, null, from, xg, WhistleKind.Short, header ? 0 : _lastShotKind);
        }

        /// <summary>
        /// Picks the way a shot is struck:
        /// - a chip when the keeper has come off his line and stands between the shooter and the goal;
        /// - close in, usually a side-footed "pass" into the near or far corner (finesse) rather than a blast;
        /// - from the edge of the box at an angle, a curler into the far corner if the strong foot's natural curl brings
        ///   it back in (a cutting-in winger always tries it);
        /// - with the ball on the weaker side: a technical player with a poor weaker foot uses the outside of his strong
        ///   foot, everybody else (and anyone whose weaker foot is decent) hits it with the weaker foot;
        /// - otherwise a driven shot.
        /// </summary>
        private ShotTechnique ChooseShotTechnique(PlayerAgent p, Vec2 from, float dist, PlayerAgent keeper)
        {
            var team = p.Team;
            int goalSign = team.AttackGoalSign;
            float technique = MathUtil.Clamp01((p.Attr.BallControl + p.Attr.Dribbling + p.Attr.Finishing + p.Attr.Composure) / 400f);
            if (keeper != null && !keeper.SentOff)
            {
                float off = Pitch.HalfLength - keeper.Pos.X * goalSign; // keeper's distance from his goal line
                float toKeeper = Vec2.Distance(from, keeper.Pos);
                float inLine = Vec2.DistanceToSegment(keeper.Pos, from, team.AttackGoal, out _);
                if (off > 6f && inLine < 2.5f && toKeeper > 5f && toKeeper < 15f && dist > 11f && dist < 30f
                    && _rng.Chance((0.05f + 0.3f * technique) * MathUtil.Clamp01((off - 6f) / 6f + 0.3f)))
                    return ShotTechnique.Chip;
            }
            Vec2 dir = (team.AttackGoal - from).Normalized;
            if (BallOnWeakSide(p, dir))
            {
                // The trivela is a rare, showy skill: only the most technical players, and mostly those whose weaker
                // foot is poor, try it; everybody else just hits it with the foot the ball is on.
                bool technical = technique > 0.7f;
                int weak = p.Attr.WeakFootRating;
                float trivela = !technical ? 0f : weak <= 1 ? 0.3f : weak == 2 ? 0.12f : 0f;
                return _rng.Chance(trivela * (0.5f + technique)) ? ShotTechnique.OutsideFoot : ShotTechnique.WeakFoot;
            }
            bool wide = MathF.Abs(from.Y) > 3f;
            if (p.CutInUntil > SimTime && wide && dist > 12f && NeededCurl(from, goalSign, -MathF.Sign(from.Y)) == NaturalCurlSign(p))
                return ShotTechnique.Curler;
            if (dist < 17f && _rng.Chance(0.45f + 0.35f * MathUtil.A(p.Attr.Composure)))
            {
                // Near post when the angle is tight or the keeper leaves it open, otherwise across him to the far post.
                float keeperShift = keeper != null ? keeper.Pos.Y * MathF.Sign(from.Y == 0f ? 1f : from.Y) : 0f;
                bool near = MathF.Abs(from.Y) > 9f ? _rng.Chance(0.55f) : keeperShift < -0.4f ? _rng.Chance(0.7f) : _rng.Chance(0.3f);
                return near ? ShotTechnique.PlacedNear : ShotTechnique.PlacedFar;
            }
            if (dist >= 15f && dist <= 30f && wide && NeededCurl(from, goalSign, -MathF.Sign(from.Y)) == NaturalCurlSign(p)
                && _rng.Chance(0.25f + 0.45f * technique))
                return ShotTechnique.Curler;
            return ShotTechnique.Power;
        }

        /// <summary>The curl (in NaturalCurlSign terms) that brings a ball from <paramref name="from"/> back into the corner on <paramref name="side"/>.</summary>
        private static float NeededCurl(Vec2 from, int goalSign, float side)
        {
            Vec2 d = (new Vec2(Pitch.HalfLength * goalSign, side * 2f) - from).Normalized;
            return -MathF.Sign(Vec2.Cross(d, new Vec2(0f, side)));
        }

        /// <summary>Whether the ball sits for the weaker foot for a kick in <paramref name="direction"/> (he cannot always adjust).</summary>
        private bool BallOnWeakSide(PlayerAgent p, Vec2 direction)
        {
            // About a fifth of shots in top football are struck with the weaker foot: the ball is simply where it is.
            // Turning across the body makes it likelier; good close control lets a player shift it onto his strong foot.
            float cross = Vec2.Cross(p.FacingDir, direction);
            Foot? needs = cross > 0.15f ? Foot.Left : cross < -0.15f ? Foot.Right : (Foot?)null;
            float chance = 0.22f - 0.1f * MathUtil.A(p.Attr.BallControl);
            if (needs != null) chance += needs == p.Attr.StrongFoot ? -0.12f : 0.25f;
            return _rng.Chance(MathUtil.Clamp01(chance));
        }

        private static int ShotDetail(ShotTechnique t)
        {
            switch (t)
            {
                case ShotTechnique.PlacedNear: return 6;
                case ShotTechnique.PlacedFar: return 7;
                case ShotTechnique.Curler: return 8;
                case ShotTechnique.Chip: return 9;
                case ShotTechnique.OutsideFoot: return 10;
                case ShotTechnique.WeakFoot: return 11;
                default: return 0;
            }
        }

        /// <summary>
        /// A technical player on the wing whose strong foot is on the inside (right-footed left winger, or the
        /// mirror): he cuts inside and shoots rather than going for the by-line.
        /// </summary>
        private static bool IsCutInsidePlayer(PlayerAgent p)
        {
            var slot = p.Slot;
            if (!(slot.IsWideMidfielder || (slot.Line == Line.Attack && MathF.Abs(slot.BaseLateral) > 12f))) return false;
            bool inverted = (slot.BaseLateral > 0f) == (p.Attr.StrongFoot == Foot.Right);
            if (!inverted) return false;
            float skill = (p.Attr.Dribbling + p.Attr.LongShots + p.Attr.Finishing + p.Attr.BallControl) / 4f;
            return skill >= 55f;
        }

        /// <summary>Releases the ball from a player with the given velocity and updates all bookkeeping.</summary>
        private void KickBall(PlayerAgent p, Vec2 from, Vec2 horizontalVelocity, float vz, float sideSpin, float topSpin,
                              KickKind kind, PlayerAgent receiver, Vec2 target, bool restartKick, bool offsideException,
                              float startZ = Pitch.BallRadius)
        {
            Ball.Owner = null;
            Ball.InHands = false;
            Ball.M = new BallMotion
            {
                X = from.X,
                Y = from.Y,
                Z = MathF.Max(Pitch.BallRadius, startZ),
                Vx = horizontalVelocity.X,
                Vy = horizontalVelocity.Y,
                Vz = vz,
                SideSpin = sideSpin,
                TopSpin = topSpin,
            };
            if (Ball.M.Z > Pitch.BallRadius + 0.001f && Ball.M.Vz == 0f) Ball.M.Vz = -0.01f;
            Ball.PrevPos = from;
            Ball.LastToucher = p;
            Ball.LastTouchTeam = p.Team;
            Ball.LastKicker = p;
            Ball.LastKickKind = kind;
            Ball.LastKickTime = SimTime;
            restartKick |= _restartKick;
            offsideException |= _restartOffsideException;
            Ball.KickerMustWait = restartKick;
            Ball.ClearFlight();
            Ball.IntendedReceiver = receiver;
            Ball.IntendedTarget = target;
            bool deliberateKick = kind == KickKind.Pass || kind == KickKind.ThroughBall || kind == KickKind.LongPass
                                  || kind == KickKind.Cross || kind == KickKind.Clearance || kind == KickKind.Restart;
            Ball.HandlingForbiddenFor = deliberateKick ? p.Team : null;
            if (kind == KickKind.Pass || kind == KickKind.ThroughBall || kind == KickKind.LongPass || kind == KickKind.Cross
                || kind == KickKind.GoalkeeperDistribution || kind == KickKind.Restart)
            {
                Ball.PendingPassFrom = p;
                Ball.LastPasser = p;
                Ball.LastPassTime = SimTime;
                p.Team.Stats.Passes++;
                p.PassesAttempted++;
            }
            PossessionTeam = p.Team;
            p.KickCooldown = 0.3f;
            p.TouchCooldown = 0.3f;
            p.HasPendingPlan = false;
            p.HoldTime = 0f;
            p.Touches++;
            SnapshotOffside(p, offsideException);
            if (CollectDiagnostics && receiver != null && IsFlaggedOffside(receiver)) Count("pass.to.offside");
            MarkBallChanged();
            RebuildTrajectory();
            _ballChangedAt = SimTime;
            _ballChangeExpectedBy = p.Team;
            if (receiver != null) receiver.Task = PlayerTask.Receive;
            AssignBallDuties();
        }

        private void ResolvePendingPass(PlayerAgent controller)
        {
            var passer = Ball.PendingPassFrom;
            if (passer == null) return;
            Ball.PendingPassFrom = null;
            bool through = _pendingThrough, intoSpace = _pendingIntoSpace, wasSwitch = _pendingSwitch;
            _pendingThrough = _pendingIntoSpace = _pendingSwitch = false;
            string key = _lastPassKey;
            if (controller == null)
            {
                if (CollectDiagnostics) Count("pass.lost.dead");
                _lastPassKey = null;
                return;
            }
            if (controller.Team == passer.Team)
            {
                if (controller != passer)
                {
                    passer.Team.Stats.PassesCompleted++;
                    passer.PassesCompleted++;
                    if (through) passer.Team.Stats.ThroughBallsCompleted++;
                    if (intoSpace && CollectDiagnostics) Count("pass.intospace.ok");
                    if (wasSwitch) passer.Team.Stats.SwitchesCompleted++;
                    if (CollectDiagnostics)
                    {
                        Count("pass.completed");
                        if (key != null) Count(key + ".ok");
                    }
                }
            }
            else
            {
                controller.Team.Stats.Interceptions++;
                bool read = controller.AnticipatedAt >= Ball.LastKickTime && Ball.IntendedReceiver != null && Ball.IntendedReceiver.Team == passer.Team;
                if (read) controller.Team.Stats.AnticipatedInterceptions++;
                if (CollectDiagnostics)
                {
                    float pf = passer.Team.Forward(controller.Pos);
                    Count("pass.intercepted" + (controller.IsGoalkeeper ? ".gk" : ""));
                    Count("pass.intercepted.zone" + (pf < -17.5f ? "1own" : pf < 17.5f ? "2mid" : "3fin") + "." + (key ?? "other"));
                }
                if (read) Emit(MatchEventType.Interception, controller.Team, controller, Ball.IntendedReceiver, controller.Pos, 0f, WhistleKind.Short, 1);
                else Emit(MatchEventType.Interception, controller.Team, controller, passer, controller.Pos);
            }
            _lastPassKey = null;
        }
    }
}

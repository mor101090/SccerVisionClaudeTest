using System;
using System.Collections.Generic;

namespace SoccerVision.Simulation
{
    internal enum BallAction { Hold, Dribble, Pass, Shoot, Cross, Clear }

    /// <summary>How a shot is struck.</summary>
    public enum ShotTechnique { Power, PlacedNear, PlacedFar, Curler, Volley, Chip, OutsideFoot, WeakFoot, Header, Penalty, HalfVolley, Knuckleball, Bicycle, Backheel }

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
        /// <summary>The return ball of a one-two (give-and-go) into the passer's run.</summary>
        public bool OneTwo;
        /// <summary>A third-man lay-off: first time into the run of the man the first passer could not reach.</summary>
        public bool ThirdMan;
        /// <summary>A heel flick: played with the back of the heel to a team-mate behind or beside him, without turning.</summary>
        public bool Heel;
    }

    /// <summary>Decisions in possession (design doc: pass, through ball, cross, dribble, shoot): the options are evaluated, the best is chosen and executed.</summary>
    public sealed partial class MatchEngine
    {
        private readonly List<KickPlan> _options = new List<KickPlan>(64);

        private float _riskPremium;
        /// <summary>The risk premium before a team's style (possession) or a player's role (tempo setter) made it larger: what a shot costs. Keeping the ball does not make a team shoot less.</summary>
        private float _shotRiskPremium;
        private bool _nextKickHeel;

        private string _lastPassKey;

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
                    p.DecisionTimer = (plan.Action == BallAction.Dribble ? 0.42f : 0.32f) * (1.25f - 0.5f * TempoNow(p.Team)) * _rng.Range(0.8f, 1.2f);
                    break;
                default:
                    float turn = MathF.Abs(MathUtil.AngleDiff(p.Facing, plan.Heading));
                    float delay = plan.Heel ? 0f : turn / MathUtil.Pi * 0.32f; // (a heel flick is played where he stands: he does not turn for it)
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
            _riskPremium = xtNow * (0.6f + 0.8f * (1f - team.Eff.Mentality)) * (f < -17.5f ? 1.3f : 1f);
            float keepingTheBall = team.Tactics.Special == SpecialTactic.Possession ? 1.65f : 1f;
            _riskPremium *= keepingTheBall; // keeping the ball: no gambles
            if (p.PlayRole == PlayerRole.TempoSetter) _riskPremium *= 1.3f;             // he keeps it moving, safely
            else if (p.PlayRole == PlayerRole.DeepLyingPlaymaker || p.PlayRole == PlayerRole.Regista) _riskPremium *= 1.15f;
            _shotRiskPremium = _riskPremium / keepingTheBall; // (a shot costs what it always did: keeping the ball does not make a team shoot less)
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
                if (WastesTime(team)) holdPenalty = MathF.Max(0f, p.HoldTime - 4f) * 0.002f - 0.003f; // time wasting: he holds it, shields it, takes his time
                _options.Add(new KickPlan
                {
                    Action = BallAction.Hold,
                    DribbleTarget = Pitch.ClampToField(p.Pos + away.ClampLength(2f), -1.5f),
                    Utility = xtNow * 0.95f - pressure * lossNow * 0.8f - holdPenalty
                              + (BackToGoalNow(p) ? 0.004f + 0.004f * pressure : 0f), // the target man holds it up under pressure
                });
            }

            // SHOOT
            float distGoal = Vec2.Distance(pos, team.AttackGoal);
            bool longShots = team.Tactics.Attack == AttackStyle.LongShots || p.Orders.LongShots == Amount.More;
            // An open goal: the keeper is out of the way (off his line and not between the ball and the goal) and nobody
            // blocks the lane - he shoots, he does not dribble on until the chance is gone.
            bool openGoal = false;
            if (distGoal < 26f && f > 22f)
            {
                var gk = team.Opponent.Goalkeeper;
                bool keeperBeaten = KeeperCannotGetThere(team, pos, gk);
                if (keeperBeaten)
                {
                    openGoal = true;
                    foreach (var o in team.Opponent.Players)
                    {
                        if (o.SentOff || o == gk) continue;
                        if (Vec2.DistanceToSegment(o.Pos, pos, team.AttackGoal, out _) < 1.2f) { openGoal = false; break; }
                    }
                }
            }
            if (distGoal < 34f && f > (longShots || p.Has(PlayerTrait.ShootsFromDistance) ? 12f : 18f) || openGoal)
            {
                float xg = EstimateShotXG(p, pos);
                if (openGoal) xg = MathF.Max(xg, MathUtil.Clamp(0.95f - distGoal * 0.018f, 0.5f, 0.92f));
                float tendency = p.Slot.Line == Line.Attack ? (distGoal > 13f ? 0.45f : 0.55f)
                                : p.Slot.IsCentralMidfielder ? (distGoal < 22f ? 1.7f : 0.9f)
                                : (p.Slot.Line == Line.Midfield ? 0.95f : 0.6f);
                if (p.Slot.IsFullBack && f > 30f) tendency *= 1.3f + 0.4f * MathUtil.A(p.Attr.LongShots); // an attacking full-back at the edge of the box has a go
                tendency *= 0.9f + 0.4f * team.Eff.Mentality;
                tendency *= 1.15f - 0.3f * MathUtil.A(p.Attr.Teamwork); // (a team player looks for the better-placed man; a selfish one has a go)
                if (IsCutInsidePlayer(p) && MathF.Abs(l) < 24f && f > 24f && p.CutInUntil > SimTime) { tendency *= (p.PlayRole == PlayerRole.InvertedWinger ? 5.4f : 4.4f); xg *= 1.1f; }
                if (distGoal > 22f) tendency *= 0.6f + 0.6f * MathUtil.A(p.Attr.LongShots);
                // (real football: four shots in ten come from outside the box - the edge of the box is a place to have a go from)
                if (distGoal > 17f && distGoal < 30f) tendency *= 1.4f;
                // long shots: the manager wants a shot on sight - from the edge of the box out to about 30 m
                if (longShots && distGoal > 12f && distGoal < 33f) tendency *= 2.6f;
                if (team.Tactics.Special == SpecialTactic.Siege) tendency *= 1.2f;
                // the individual instruction: shoot from distance more / less (inside the box he shoots as usual)
                if (distGoal > 18f) tendency *= PlayerAgent.Scale(p.Orders.LongShots, 0.35f, 1.75f);
                if (p.PlayRole == PlayerRole.ShadowStriker && distGoal < 18f) tendency *= 1.15f;
                if (p.PlayRole == PlayerRole.Trequartista && distGoal > 15f && distGoal < 25f) tendency *= 1.25f; // the shot from the edge
                // traits: a shot on sight from range; the cut inside and shoot; the unselfish one looks for a pass first
                if (p.Has(PlayerTrait.ShootsFromDistance) && distGoal > 18f) tendency *= 2.2f; // (a shot on sight from range: the trait must show)
                if (p.Has(PlayerTrait.CutsInsideToShoot) && p.CutInUntil > SimTime) tendency *= 1.15f;
                if (p.Has(PlayerTrait.Unselfish) && !openGoal && !(p.CutInUntil > SimTime) && p.PlayRole != PlayerRole.InvertedWinger) tendency *= 0.62f; // (the winger cutting inside carries on: his shot is his signature)
                // (the habits shift which shots a player takes, not how many a team takes: the balance for the squad's mix)
                tendency *= p.CutInUntil > SimTime ? 0.92f : 0.72f; // (the cut inside and shoot is the winger's signature) // (the cut inside and shoot is the winger's signature: left as it was)
                if (p.FirstTimeUntil > SimTime) tendency *= 2.6f; // (set to hit it first time)
                if (openGoal) { tendency = MathF.Max(tendency, 3f); if (CollectDiagnostics) Count("shot.openGoal.considered"); }
                { Patience(team, out float styleOnly); if (styleOnly > 0f) tendency *= 1f + PatientShotBoost * styleOnly; } // (a team that has worked the ball into the box takes its chances)
                // A shot that does not score usually hands the ball over (goal kick, keeper, block).
                float afterShotLoss = 0.011f + 0.4f * _shotRiskPremium; // (a shot that does not go in hands the ball over: a poor one is a ball given away)
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
            bool crossy = team.Tactics.Attack == AttackStyle.CrossesIntoBox;
            if (crossy ? f > 15f && MathF.Abs(l) > 9f : f > 20f && MathF.Abs(l) > 13f) EvaluateCrosses(p, pos);
            if (f < -24f && pressure > 0.45f) EvaluateClearance(p, pos, pressure);
            if (MathF.Abs(l) > 27f && pressure > 0.55f) EvaluateKickOut(p, pos, pressure);

            // Basic football sense: when a good forward pass is on (a team-mate free in front, a clear lane, a pass that
            // really gets the attack going) and he is not under pressure, he plays it - he does not stall the attack with a
            // back pass or by sitting on the ball. (Under pressure the safe ball is still the right one.)
            if (pressure < 0.55f)
            {
                bool forwardOn = false;
                foreach (var o in _options)
                    if (o.Action == BallAction.Pass && o.Success >= 0.62f && o.Utility > 0f && team.Forward(o.Target) - f >= 12f && team.Forward(o.Target) > -5f) { forwardOn = true; break; }
                if (forwardOn)
                    for (int i = 0; i < _options.Count; i++)
                    {
                        var o = _options[i];
                        if (o.Action == BallAction.Pass && o.Success >= 0.62f && o.Utility > 0f && team.Forward(o.Target) - f >= 12f) o.Utility *= 1.3f;
                        else if (o.Action == BallAction.Hold || (o.Action == BallAction.Pass && team.Forward(o.Target) - f < 3f)) o.Utility = o.Utility > 0f ? o.Utility * 0.6f : o.Utility * 1.25f;
                        else continue;
                        _options[i] = o;
                    }
            }

            // Team play and flair: a team player passes up a shot when a mate is in a clearly better spot; a creative one
            // sees (and values) the through ball and the pass into space that others do not think of.
            {
                float teamwork = MathUtil.A(p.Attr.Teamwork), creativity = MathUtil.A(p.Attr.Creativity);
                bool unselfish = p.Has(PlayerTrait.Unselfish), layOff = BackToGoalNow(p);
                if (unselfish) teamwork = MathF.Min(1f, teamwork + 0.35f);
                float ownXg = f > 24f ? EstimateShotXG(p, pos) : 0f;
                // the last man with only the keeper to beat: no back pass, no sitting on it - he goes at goal
                bool lastMan = f > 12f && IsLastMan(p);
                for (int i = 0; i < _options.Count; i++)
                {
                    var o = _options[i];
                    if (lastMan && ((o.Action == BallAction.Pass && team.Forward(o.Target) < f - 1f) || o.Action == BallAction.Hold))
                    {
                        o.Utility = MathF.Min(o.Utility, 0f) - 0.05f;
                        _options[i] = o;
                        continue;
                    }
                    if (o.Action != BallAction.Pass || o.Utility <= 0f) continue;
                    if (o.Kind == KickKind.ThroughBall || o.IntoSpace) o.Utility *= 0.85f + 0.35f * creativity;
                    if (unselfish && o.Receiver != null && team.Forward(o.Receiver.Pos) > f - 4f) o.Utility *= 1.08f;
                    // back to goal: the lay-off to a team-mate facing the play
                    if (layOff && !o.Lofted && team.Forward(o.Target) < f && Vec2.Distance(o.Target, pos) < 14f) o.Utility *= 1.25f;
                    if (o.IsSwitch && p.Has(PlayerTrait.SwitchesPlay)) o.Utility *= 1.3f;
                    // the midfielder arriving late into the box is the man nobody picks up: find him
                    if (o.Receiver != null && (o.Receiver.Slot.IsCentralMidfielder || o.Receiver.Kind == PositionKind.AttackingMidfielder) && team.Forward(o.Receiver.Pos) > 28f)
                        o.Utility *= (o.Receiver.RunTimer > 0f ? 1.35f : 1.05f) + 0.2f * teamwork;
                    if (o.Receiver != null && f > 24f && team.Forward(o.Receiver.Pos) > 30f && !o.OneTwo)
                    {
                        float theirXg = EstimateShotXG(o.Receiver, o.Receiver.Pos);
                        if (theirXg > ownXg * 1.6f + 0.05f) o.Utility += (0.2f + 0.6f * teamwork) * 0.15f * (theirXg - ownXg) * o.Success;
                    }
                    _options[i] = o;
                }
            }

            // In and around the box the question is simple: is there a better shot to be had by carrying it on? A dribble
            // there is worth the shot it leads to (the xG from where he would be, if he keeps the ball, a moment later) - not
            // the "progress" it makes; sitting on the ball is worth less still. So a striker with a sight of goal hits it, and
            // he does not run on until he is standing on the keeper. An open goal is always a shot.
            if ((distGoal < 20f && f > 30f) || openGoal)
            {
                int shootAt = -1;
                for (int i = 0; i < _options.Count; i++) if (_options[i].Action == BallAction.Shoot) { shootAt = i; break; }
                if (shootAt >= 0)
                {
                    var so = _options[shootAt];
                    float xgNow = so.ExpectedGoals;
                    if (openGoal) { so.Utility = 10f; _options[shootAt] = so; if (CollectDiagnostics) Count("shot.openGoal.forced"); }
                    else
                    {
                        // (a cool head may wait a beat; the unselfish one looks up first; and a shot that will not go in still gives
                        // the ball away - a poor chance in the box is not worth more than keeping the ball for a better one)
                        float boxShot = xgNow * (1.05f - 0.15f * MathUtil.A(p.Attr.Composure)) * (p.Has(PlayerTrait.Unselfish) ? 0.8f : 1f)
                                        - (1f - xgNow) * (0.017f + 0.3f * _shotRiskPremium) * (p.FirstTimeUntil > SimTime ? 0.5f : 1f);
                        so.Utility = MathF.Max(so.Utility, boxShot);
                        _options[shootAt] = so;
                        for (int i = 0; i < _options.Count; i++)
                        {
                            var o = _options[i];
                            if (o.Action == BallAction.Dribble)
                            {
                                if (p.FirstTimeUntil > SimTime) { o.Utility *= 0.4f; _options[i] = o; continue; } // (set to hit it first time: no time to carry it)
                                if (p.CutInUntil > SimTime) continue; // (the winger cutting inside carries on: his curler comes from the run)
                                float later = EstimateShotXG(p, o.DribbleTarget, false, KeeperWhenThere(team, pos, o.DribbleTarget)) * MathUtil.Clamp01(o.Success) * 0.72f;
                                // (into the six-yard box with the keeper there: he smothers it at the feet - a forward hits it before that)
                                var kp = team.Opponent.Goalkeeper;
                                if (Pitch.InGoalArea(o.DribbleTarget, team.AttackGoalSign) && kp != null && !kp.SentOff && !kp.IsDown && Vec2.Distance(kp.Pos, o.DribbleTarget) < 6f) later *= 0.5f;
                                // (running on to a keeper who is coming out: by the time he gets there the keeper is on top of him)
                                if (kp != null && !kp.SentOff && !kp.IsDown && Vec2.Distance(kp.Pos, o.DribbleTarget) < 4.5f) later *= 0.55f;
                                // (and in the time it takes the defenders close in: every one near where he would shoot from takes some of it)
                                foreach (var dq in team.Opponent.Players)
                                    if (!dq.SentOff && !dq.IsGoalkeeper && Vec2.Distance(dq.Pos, o.DribbleTarget) < 4.5f) later *= 0.8f;
                                if (o.Utility > later) { o.Utility = later; _options[i] = o; }
                            }
                            else if (o.Action == BallAction.Hold && xgNow > 0.06f && o.Utility > xgNow * 0.4f) { o.Utility = xgNow * 0.4f; _options[i] = o; }
                            // the pass to a team-mate in a better spot (the cut-back, the square ball) is worth his shot
                            else if (o.Action == BallAction.Pass && o.Receiver != null && !o.Lofted && team.Forward(o.Target) > 30f)
                            {
                                float theirs = EstimateShotXG(o.Receiver, o.Target) * MathUtil.Clamp01(o.Success) * 0.9f;
                                if (theirs > o.Utility) { o.Utility = theirs; _options[i] = o; }
                            }
                        }
                    }
                }
            }

            // Do not take the ball on to the keeper. Real forwards shoot before the keeper is on them: only about one foot shot
            // in 25 is struck with the keeper within 3 m (StatsBomb). A carry that ends beside a keeper who is set on his line
            // or coming out is a ball he smothers - it is weighed very low (going round him is still possible, rarely), and
            // a man already that close to him and the goal hits it or squares it now.
            {
                var kp = team.Opponent.Goalkeeper;
                if (kp != null && !kp.SentOff && !kp.IsDown && f > 30f)
                {
                    float kNow = Vec2.Distance(kp.Pos, pos);
                    for (int i = 0; i < _options.Count; i++)
                    {
                        var o = _options[i];
                        if (o.Action != BallAction.Dribble || o.Utility <= 0f) continue;
                        float kThen = Vec2.Distance(kp.Pos + kp.Vel * 0.5f, o.DribbleTarget);
                        if (kThen < 5f && kThen < kNow)
                        {
                            o.Utility *= 0.12f + 0.12f * MathUtil.A(p.Attr.Dribbling) * MathUtil.Clamp01((kThen - 1.5f) / 3.5f);
                            _options[i] = o;
                            if (CollectDiagnostics) Count("dribble.ontoKeeper.cut");
                        }
                    }
                }
            }

            // Weighted randomness: perception noise grows with poor decisions and pressure on low composure.
            float decisions = MathUtil.A(p.Attr.Decisions);
            float composure = MathUtil.A(p.Attr.Composure);
            float sigma = (0.0015f + 0.006f * (1f - decisions)) * (1f + pressure * (1.1f - composure)) * p.FatigueError;
            // Phase 4 - natural variation. Two players with the same numbers do not play the same way: each has his own
            // leanings (a little more of a shooter, a dribbler, a passer or a crosser - fixed for the match, from his own
            // make-up), and a good decision does not mean always the same decision: among the options that are nearly as
            // good he picks one with a soft-max - a clear-headed player almost always the best, a poor decision-maker
            // more loosely. The clearly worse options are never picked this way (only through misreading them, above).
            if (_perceived.Length < _options.Count) _perceived = new float[_options.Count * 2];
            float top = float.NegativeInfinity;
            int best = 0;
            for (int i = 0; i < _options.Count; i++)
            {
                var o = _options[i];
                float u = o.Utility;
                if (u > 0f && !(openGoal && o.Action == BallAction.Shoot)) u *= StyleLean(p, o.Action);
                float perceived = u * (1f + _rng.Gaussian(0f, 0.16f * (1.15f - decisions))) + _rng.Gaussian(0f, sigma);
                _perceived[i] = perceived;
                if (perceived > top) { top = perceived; best = i; }
            }
            if (!openGoal && _options.Count > 1 && top > 0f)
            {
                float temp = (0.0008f + 0.0035f * (1f - decisions)) * (0.8f + 0.4f * MathUtil.A(p.Attr.Creativity)) * (p.Has(PlayerTrait.Showboat) ? 1.3f : 1f) + 0.04f * top * (1f - decisions);
                float margin = 2.5f * temp, total = 0f;
                for (int i = 0; i < _options.Count; i++) if (_perceived[i] >= top - margin && _perceived[i] > 0f) total += MathF.Exp((_perceived[i] - top) / temp);
                float pick = _rng.NextFloat() * total;
                for (int i = 0; i < _options.Count; i++)
                {
                    if (!(_perceived[i] >= top - margin && _perceived[i] > 0f)) continue;
                    pick -= MathF.Exp((_perceived[i] - top) / temp);
                    if (pick <= 0f) { if (CollectDiagnostics && i != best) Count("decide.variation"); best = i; break; }
                }
            }
            var chosen = _options[best];
            if (!(openGoal && chosen.Action == BallAction.Shoot)) chosen = MaybeSkill(p, chosen, pos, pressure);
            if (CollectDiagnostics)
            {
                // is a good forward pass on, and did he play something else (a back or square pass, or sat on it)?
                bool forwardOn = false;
                foreach (var o in _options)
                    if (o.Action == BallAction.Pass && o.Success >= 0.62f && o.Utility > 0f && team.Forward(o.Target) - f >= 12f && team.Forward(o.Target) > -5f) { forwardOn = true; break; }
                if (forwardOn)
                {
                    Count("fwd.on");
                    bool wasForward = chosen.Action == BallAction.Shoot || chosen.Action == BallAction.Cross || chosen.Action == BallAction.Dribble || (chosen.Action == BallAction.Pass && team.Forward(chosen.Target) - f >= 6f);
                    if (!wasForward) Count(chosen.Action == BallAction.Pass ? "fwd.missed.passBack" : "fwd.missed." + chosen.Action);
                }
            }
            if (CollectDiagnostics && chosen.Action == BallAction.Dribble && distGoal < 9f && f > 40f) Count("dribble.atKeeper");
            if (CollectDiagnostics && distGoal < 18f) Count("box.decide." + chosen.Action);
            if (CollectDiagnostics) Count("decide." + chosen.Action + (chosen.Action == BallAction.Pass ? "." + chosen.Kind + (chosen.Lofted ? ".lofted" : "") : ""));
            if (chosen.Action != BallAction.Dribble && chosen.Action != BallAction.Hold) p.HoldTime = 0f;
            if (chosen.Action == BallAction.Dribble && IsCutInsidePlayer(p) && MathF.Abs(team.Lateral(chosen.DribbleTarget)) < MathF.Abs(l) - 2f && f > 14f && MathF.Abs(l) > 14f)
                p.CutInUntil = SimTime + 3.5f;
            return chosen;
        }

        private float[] _perceived = new float[64];

        /// <summary>
        /// His own leaning for an action (0.88-1.12, fixed for the match): taken from his make-up (the phases that also give
        /// his movement its wobble), so it costs no random draw and is the same every time the match is replayed.
        /// </summary>
        private static float StyleLean(PlayerAgent p, BallAction a)
        {
            float k = a == BallAction.Shoot ? 1.3f : a == BallAction.Dribble ? 2.9f : a == BallAction.Cross ? 4.7f : a == BallAction.Pass ? 6.1f : 0f;
            return k == 0f ? 1f : 1f + 0.12f * MathF.Sin(p.NoisePhaseA * k + p.NoisePhaseB * (k * 0.37f + 1f));
        }

        /// <summary>Value the opponent gets if they win the ball here (their xT).</summary>
        private static float LossCost(TeamAgent team, Vec2 world)
        {
            return ExpectedThreat.At(-team.Forward(world), -team.Lateral(world)) * 1.05f;
        }

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
                _pendingGround = !plan.Lofted;
                if (_pendingSwitch)
                {
                    p.Team.Stats.Switches++;
                    if (CollectDiagnostics) Count("pass.switch" + (p.Slot.IsFullBack ? ".fullback" : ""));
                }
                _pendingOneTwo = plan.OneTwo;
                if (plan.OneTwo) p.Team.Stats.OneTwos++;
                _pendingThirdMan = plan.ThirdMan;
                if (plan.ThirdMan) p.Team.Stats.ThirdManPasses++;
                if (_pendingThrough) p.Team.Stats.ThroughBalls++;
                if (_pendingIntoSpace) p.Team.Stats.PassesIntoSpace++;
                if (CollectDiagnostics && plan.IntoSpace) Count("pass.intospace");
            }
            else _pendingThrough = _pendingIntoSpace = _pendingSwitch = _pendingOneTwo = _pendingThirdMan = false;
            float pressure = Pressure(p, out _);
            if (plan.Action == BallAction.Pass && !plan.Lofted && plan.Kind == KickKind.Pass && plan.Receiver != null)
            {
                MaybeStartOneTwo(p, plan.Receiver);
                MaybeStartThirdMan(p, plan.Receiver);
            }
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
                    p.Team.PushUpUntil = SimTime + 3f;
                    CallStepUp(p.Team);
                    KickLofted(p, plan.Target, plan.LaunchAngle, KickKind.Clearance, null, pressure, 0.35f + 0.3f * MathUtil.A(p.Attr.Passing), curl: false);
                    break;
                case BallAction.Pass:
                    if (!plan.Lofted && plan.Kind == KickKind.Pass && p.Team.Forward(Ball.Pos) > 38f && MathF.Abs(p.Team.Lateral(Ball.Pos)) > 13f
                        && p.Team.Forward(plan.Target) > 34f && MathF.Abs(p.Team.Lateral(plan.Target)) < 11f && p.Team.Forward(plan.Target) < p.Team.Forward(Ball.Pos) - 2f)
                        p.Team.Stats.CutBacks++;
                    if (plan.Lofted) KickLofted(p, plan.Target, plan.LaunchAngle, plan.Kind, plan.Receiver, pressure, MathUtil.A(p.Attr.Passing), curl: false);
                    else
                    {
                        if (plan.Heel) { _nextKickHeel = true; p.HeelUntil = SimTime + 0.7f; }
                        KickGround(p, plan, pressure);
                    }
                    break;
            }
        }

        /// <summary>A team that wastes time (the time wasting tactic, or parking the bus): slow restarts, the keeper takes his time, the ball is held.</summary>
        private static bool WastesTime(TeamAgent team) => team.Tactics.Special == SpecialTactic.TimeWasting || team.Tactics.Defence == DefenceStyle.ParkTheBus;

        /// <summary>
        /// 0..1: how much room the opponents leave behind their back line - nothing for a line on the edge of its box, a
        /// lot for a high line near halfway. A ball in behind a high line is the classic way to beat it: the runner has
        /// space to run into and the defenders have to turn and chase.
        /// </summary>
        private float SpaceBehindLine(TeamAgent team) => MathUtil.Clamp01((team.Opponent.LineForward + Pitch.HalfLength - 30f) / 16f);

        /// <summary>
        /// The team's passing style: short combinations value the short ball to feet, direct play the ball in behind and the
        /// long ball. Mixed changes nothing.
        /// </summary>
        private static float StyleFactor(TeamAgent team, KickKind kind, float d, bool lofted)
        {
            var s = team.Eff.Passing;
            if (s == PassingStyle.Mixed) return 1f;
            bool shortBall = !lofted && kind == KickKind.Pass && d < 22f;
            bool longBall = lofted || kind == KickKind.ThroughBall || d > 30f;
            // (a team keeping the ball plays it short to feet even more: the passes are the point)
            if (s == PassingStyle.Short) return shortBall ? (team.Tactics.Special == SpecialTactic.Possession ? 1.2f : 1.12f) : longBall ? 0.75f : 1f;
            return shortBall ? 0.92f : longBall ? 1.3f : 1f;
        }

        /// <summary>How much the team's passing style favours short combinations: 1.6 short, 1 mixed, 0.45 direct.</summary>
        private static float StyleShort(TeamAgent team) =>
            team.Eff.Passing == PassingStyle.Short ? 1.6f : team.Eff.Passing == PassingStyle.Direct ? 0.45f : 1f;

        /// <summary>
        /// A technical player on the wing whose strong foot is on the inside (right-footed left winger, or the
        /// mirror): he cuts inside and shoots rather than going for the by-line.
        /// </summary>
        /// <summary>
        /// Against a low block (the opponent's line deep near its box) the way through is to draw it over to one side and
        /// switch quickly to the far side, where the winger is one against one: the switch is worth more.
        /// </summary>
        private static float LowBlockSwitch(TeamAgent team) =>
            team.Opponent.Tactics.Defence == DefenceStyle.ParkTheBus || team.Opponent.Eff.DefensiveLine < 0.3f || team.Opponent.LineForward < -38f ? 1.35f : 1f;

        /// <summary>Releases the ball from a player with the given velocity and updates all bookkeeping.</summary>
        private void KickBall(PlayerAgent p, Vec2 from, Vec2 horizontalVelocity, float vz, float sideSpin, float topSpin,
                              KickKind kind, PlayerAgent receiver, Vec2 target, bool restartKick, bool offsideException,
                              float startZ = Pitch.BallRadius)
        {
            if (CollectDiagnostics) ContactCheck(p, from, startZ, "kick." + kind + (_restartKick ? ".restart" + (_restart != null ? _restart.Type.ToString() : "") : "") + (Ball.Owner == p ? ".owned" : ".loose"));
            horizontalVelocity = AllowForWind(p, from, startZ, horizontalVelocity, vz, sideSpin, topSpin, kind);
            // the swing of the leg (seen in the replay): the foot that kicks it - across the body to the left foot, to the right
            // foot, otherwise his strong one
            {
                float cross = Vec2.Cross(p.FacingDir, horizontalVelocity.Normalized);
                p.KickLeft = cross > 0.15f ? true : cross < -0.15f ? false : p.Attr.StrongFoot == Foot.Left;
                p.KickTimer = PlayerAgent.KickSwing;
                p.KickHeel = _nextKickHeel; _nextKickHeel = false;
                if (p.KickHeel) p.KickLeft = cross < 0f; // (the heel flick goes back with the foot on the far side of the target)
            }
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
            if (kind == KickKind.Pass || kind == KickKind.ThroughBall || kind == KickKind.LongPass || kind == KickKind.Cross || kind == KickKind.Restart
                || kind == KickKind.GoalkeeperDistribution || (kind == KickKind.Header && receiver != null))
                _lastTeamPassKind[p.Team.Index] = kind == KickKind.ThroughBall && _pendingIntoSpace ? "Pass" : kind.ToString(); // (for the goal breakdown: what the last pass of the move was; a ball into a running man's path is a pass, as StatsBomb has it)
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
    }
}

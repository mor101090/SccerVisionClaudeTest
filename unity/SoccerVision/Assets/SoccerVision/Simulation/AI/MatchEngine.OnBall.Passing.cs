using System;
using System.Collections.Generic;
using System.Linq;

namespace SoccerVision.Simulation
{
    /// <summary>In possession - passing: ground and lofted passes, through balls, switches, one-twos and third-man runs, the kick itself.</summary>
    public sealed partial class MatchEngine
    {
        private bool _pendingThrough, _pendingIntoSpace, _pendingSwitch, _pendingOneTwo, _pendingThirdMan, _pendingGround;

        // A corner routine for the commentary (Cross event Detail: 1 near post, 2 far post, 3 middle, 4 edge of the box;
        // Value: 1 in-swinger, 2 out-swinger) and how much harder a corner taker bends it.
        private int _crossDetail;

        private float _crossValue;

        private float _curlBoost = 1f;

        private bool _nextPassIntoSpace, _nextOneTwo;

        /// <summary>
        /// The chance the runner beats the keeper to a ball played in behind (1 when the keeper is far away). The keeper starts
        /// as the ball is played, needs only to get a hand to it and is a little quicker off his line than the plain time to
        /// the spot says; how well the passer judges it is his decisions (a poor one overrates his runner's chance).
        /// </summary>
        private float KeeperRace(PlayerAgent passer, TeamAgent team, Vec2 target, float runnerArrives)
        {
            var gk = team.Opponent.Goalkeeper;
            if (gk == null || gk.SentOff) return 1f;
            float gkT = gk.TimeToReach(target) - 0.45f - 0.25f * MathUtil.A(gk.Attr.OneOnOne);
            float misjudge = 0.35f * (1f - MathUtil.A(passer.Attr.Decisions));
            return MathUtil.Clamp01(0.5f + (gkT - runnerArrives + misjudge) / 0.7f);
        }

        /// <summary>The pace of a team's passing, for the match statistics: open-play passes, their length, and the runs of passes within one possession.</summary>
        private void NotePassTempo(TeamAgent team, float length)
        {
            var st = team.Stats;
            st.OpenPlayPasses++; st.PassLengthSum += length;
            if (length < 15f) st.ShortPasses++; else if (length >= 32f) st.LongPasses++;
            float now = SimTime * Config.ClockSecondsPerSimSecond;
            if (st.PossessionSpells == st.LastPassSpell && now - st.LastPassClock < 30f) { st.PassGapSum += now - st.LastPassClock; st.PassGapCount++; }
            else st.PassRuns++;
            st.LastPassSpell = st.PossessionSpells; st.LastPassClock = now;
        }

        /// <summary>
        /// How much a team wants to keep the ball (the possession round): its style (+1 possession, +0.5 short passing, -0.5 direct passing, -0.5 counter-attack,
        /// -0.5 park the bus), how much better its passers are than the other team's (up to +-1.8; nothing for a gap of less than 4 points), and how hard the other team
        /// presses it (a high press -0.6, and -2 for every point of pressing intensity above the default). A team at 0 plays as the engine always did.
        /// <paramref name="style"/> is the style part alone.
        /// </summary>
        /// <summary>What a safe pass is worth to a team at patience 1, in its own half (fading to nothing at the halfway line; in the other half the chance decides). Measured: 0.025 takes a
        /// possession-style team to about two thirds of the ball against an ordinary one.</summary>
        private const float RetainValue = 0.025f;
        /// <summary>A team that works the ball patiently takes its chances: the shot tendency is multiplied by 1 + this x the team's style patience.</summary>
        private const float PatientShotBoost = 1.2f;

        private static float Patience(TeamAgent team, out float style)
        {
            var t = team.Tactics;
            style = (t.Special == SpecialTactic.Possession ? 1f : 0f) + (t.Passing == PassingStyle.Short ? 0.5f : t.Passing == PassingStyle.Direct ? -0.5f : 0f)
                    + (t.Special == SpecialTactic.CounterAttack ? -0.5f : 0f) + (t.Defence == DefenceStyle.ParkTheBus ? -0.5f : 0f);
            float gap = PassingQuality(team) - PassingQuality(team.Opponent);
            float quality = MathUtil.Clamp(MathF.Sign(gap) * MathF.Max(0f, MathF.Abs(gap) - 4f) / 6f, -1.8f, 1.8f);
            var o = team.Opponent.Tactics; // (what the manager chose, not the moment's adjustments: a team on its defaults is at 0)
            float pressed = MathUtil.Clamp((o.Defence == DefenceStyle.HighPress ? 0.6f : 0f) + 2f * (o.PressingIntensity - 0.5f), -0.4f, 1f);
            int d = team.Score - team.Opponent.Score; // (in front: no need to keep it; behind: the ball is needed - see TeamAgent.RefreshEff)
            float score = d == 0 ? 0f : -ScorePatience * TeamAgent.ScoreEffect * MathF.Sign(d) * (MathF.Abs(d) == 1 ? 1f : 0.4f);
            return style + quality - pressed + score;
        }
        /// <summary>
        /// The extra time (sim s) a patient team takes on the ball after controlling it, per point of style patience (possession + short passing = 1.5).
        /// Real passing (see tools/calibration/passing_tempo.py and CLAIMS.md): a possession side passes about 1.7 times as often as an ordinary one -
        /// Guardiola's Barcelona about 747 passes a match against about 434 for a Premier League side (secondary source); without it the engine's
        /// possession side passed 2.4 times as often.
        /// </summary>
        public static float PatientTouch = 0.6f;
        /// <summary>The score's part of a team's patience at one goal apart (see <see cref="Patience"/>).</summary>
        public static float ScorePatience = 1f;

        /// <summary>The starting eleven's mean of passing, ball control, composure and decisions (0-100), worked out once per match (a red card or an injury does not change a team's character).</summary>
        private static float PassingQuality(TeamAgent team)
        {
            if (team.PassingQualityValue >= 0f) return team.PassingQualityValue;
            float sum = 0f; int n = 0;
            foreach (var q in team.Players) { var a = q.Attr; sum += (a.Passing + a.BallControl + a.Composure + a.Decisions) * 0.25f; n++; }
            return team.PassingQualityValue = n == 0 ? 50f : sum / n;
        }

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
                int firstOption = _options.Count;
                try {
                float dist = Vec2.Distance(q.Pos, from);
                if (dist < 4f || dist > 58f) continue;
                if (q.IsGoalkeeper && (fromF > -8f || pressure < 0.35f)) continue;

                // Perceived offside: the passer judges the line with an error (better decision makers see it better).
                float margin = team.Forward(q.Pos) - offsideLine;
                float perceivedMargin = margin + _rng.Gaussian(0f, 0.5f + 1.4f * (1f - MathUtil.A(p.Attr.Decisions)));
                bool offside = perceivedMargin > 0f && _rng.Chance(noticeOffside); // what the passer believes
                if (offside) continue;

                // One-two: q gave me the ball and is running past for it back - play it into his run, first time.
                if (q.OneTwoWith == p && q.OneTwoUntil > SimTime && !CarryingBeforeTheReturn(p))
                {
                    Vec2 toSpot = q.OneTwoSpot - q.Pos;
                    float left = toSpot.Length;
                    Vec2 runDir = left > 0.5f ? toSpot / left : team.DirToWorld(Vec2.UnitX);
                    // (just ahead of him, into his stride: a wall pass is short and simple, not a long ball into space)
                    Vec2 into = Pitch.ClampToField(q.Pos + runDir * MathUtil.Clamp(MathF.Max(left * 0.5f, q.Vel.Length * 0.7f), 2f, 6f), -2f);
                    int before = _options.Count;
                    _nextPassIntoSpace = true;
                    _nextOneTwo = true; // (the wall pass is played first time into a run both players expect: a simple ball)
                    AddGroundPass(p, q, from, into, KickKind.ThroughBall, pressure);
                    _nextPassIntoSpace = false;
                    _nextOneTwo = false;
                    // (polish round 4: a man in the lane - the return can be clipped over him into the run)
                    if (LaneClearance(team, from, into) < OneTwoChipLane) AddLoftedPass(p, q, from, into, pressure, KickKind.ThroughBall, OneTwoChipMinDistance);
                    float pull = 0.009f * StyleShort(team);
                    for (int i = before; i < _options.Count; i++)
                    {
                        var o = _options[i];
                        o.OneTwo = true;
                        o.Success *= 0.9f; // (measured: the lane model is optimistic about balls played into a run)
                        if (o.Success < 0.4f) { _options[i] = o; continue; } // a wall pass into a closed lane is not on: no bonus
                        o.Utility = o.Utility * (o.Utility > 0f ? 2.6f : 0.5f) + pull * 1.8f;
                        if (o.Success >= 0.55f) o.Utility = MathF.Max(o.Utility, 0.003f * StyleShort(team)); // (a wall pass that is on is worth playing)
                        _options[i] = o;
                    }
                }

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
                finally
                {
                    // Third man: q set off as the ball came into me - the lay-off into his run is the pass the defence did
                    // not see coming (they were watching the ball into me, facing the wrong way).
                    if (q.ThirdManVia == p && q.ThirdManUntil > SimTime && CollectDiagnostics) Count(_options.Count > firstOption ? "thirdman.options" : "thirdman.nooption");
                    if (q.ThirdManVia == p && q.ThirdManUntil > SimTime)
                        for (int i = firstOption; i < _options.Count; i++)
                        {
                            var o = _options[i];
                            if (o.OneTwo || team.Forward(o.Target) < fromF + 3f) continue;
                            // (a team keeping the ball plays it into his feet, not over the top)
                            if (team.Tactics.Special == SpecialTactic.Possession && (o.Lofted || o.Kind == KickKind.ThroughBall)) continue;
                            o.ThirdMan = true;
                            o.Success = MathF.Min(0.97f, o.Success * 1.15f);
                            if (o.Success > 0.4f) o.Utility = o.Utility * (o.Utility > 0f ? 1.8f : 0.6f) + 0.012f;
                            _options[i] = o;
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
            float maxPower = 22f + 9f * MathUtil.A(p.Attr.Passing) + 4f * MathUtil.A(p.Attr.Passing); // (his foot, not his build)
            float v0;
            if (kind == KickKind.ThroughBall)
            {
                // (a wall pass is weighted to arrive softly into the runner's stride - a hard ball would be a heavy touch)
                v0 = Roll.KickSpeedForTime(d, tRecv * (_nextOneTwo ? 1.12f : 0.92f), _nextOneTwo ? 1.8f : 2.5f);
            }
            else
            {
                float arrival = MathUtil.Clamp(5f + 0.09f * d, 5f, 10.5f) * (0.9f + 0.25f * TempoNow(team));
                v0 = Roll.KickSpeedFor(d, arrival);
            }
            if (v0 > maxPower) return;
            float tArrive = Roll.TimeToDistance(v0, d);
            if (float.IsInfinity(tArrive)) return;
            if (kind == KickKind.Pass && tRecv > tArrive + 0.9f) return;

            float pIntercept = LaneInterceptionRisk(team, from, dir, d, v0, kind == KickKind.Pass ? 1.6f : 0.5f, MathF.Max(tRecv, 0f), out Vec2 riskPoint);
            // A wall pass is played first time into a run both players expect, and the defender who followed the runner is
            // now out of the lane: it is intercepted less often than a ball into space (when it fails it is an interception or
            // a return that is too hard to control - the heavy touch is in pControl below).
            if (_nextOneTwo) { pIntercept *= 0.55f; if (LaneClearance(team, from, target) < 2.2f) pIntercept = MathF.Max(pIntercept, 0.45f); } // (a wall pass into a crowded lane is not on)
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
            if (_nextPassIntoSpace && !_nextOneTwo) success *= 0.85f;
            // Through balls behind the line: keepers sweep, defenders turn and recover - the race is harder to win
            // than the simple estimate suggests (measured: well below the predicted success).
            else if (kind == KickKind.ThroughBall && !_nextOneTwo) success *= 0.68f + 0.5f * SpaceBehindLine(team);
            // ...and the keeper: he reads the ball from the moment it is played and comes off his line, and he may use his
            // hands - a ball he can reach first is his. A passer who reads the game sees it (he plays it shorter, or not at all).
            if (kind == KickKind.ThroughBall && !_nextOneTwo && !_nextPassIntoSpace) success *= KeeperRace(p, team, target, MathF.Max(tArrive, tRecv));
            // (against a high backline the space behind it is there for the taking)
            float styleBoost = (kind == KickKind.ThroughBall && team.Opponent.Tactics.Defence == DefenceStyle.HighBackline ? 1.2f : 1f)
                               * (kind == KickKind.ThroughBall && team.Tactics.Attack == AttackStyle.RunsInBehind ? 1.28f : 1f)
                               * (team.CounterUntil > SimTime && team.Forward(target) > team.Forward(from) + 8f ? 1.45f : 1f);
            float value = ExpectedThreat.At(team, target) * (1f - 0.3f * receiverPressure) * StyleFactor(team, kind, d, false) * styleBoost
                          * RolePassFactor(p, q, kind, d, false, team.Forward(target) - team.Forward(from));
            float relief = SwitchRelief(p, from, target, pressure) * (p.Has(PlayerTrait.SwitchesPlay) ? 1.6f : 1f) * LowBlockSwitch(team);
            value += relief;
            // Cut-back: from the by-line, low and square to a team-mate arriving in the middle of the box.
            if (team.Forward(from) > 38f && MathF.Abs(team.Lateral(from)) > 13f && team.Forward(target) > 34f && team.Forward(target) < team.Forward(from) - 2f
                && MathF.Abs(team.Lateral(target)) < 11f && kind == KickKind.Pass)
                value *= 1.5f;
            // Deep completion: a ground ball into the space in front of the goal or a half-space, within ~20 m - the
            // better his creativity, the more he looks for it (a controlled ball there is worth more than a hopeful cross).
            if (kind != KickKind.LongPass && IsDeepZone(team, target) && !IsDeepZone(team, from))
                value *= 1.06f + 0.12f * MathUtil.A(p.Attr.Creativity);
            float progress = team.Forward(target) - team.Forward(from);
            // Central midfielders are the ones who move the game on: they look for the forward pass.
            if (p.Slot.IsCentralMidfielder && progress > 5f) value *= 1.12f;
            // ...and the build-up goes through them: defenders look for the midfielder first.
            if (q.Slot.IsCentralMidfielder && p.Slot.Line == Line.Defence && kind == KickKind.Pass) value *= 1.15f;
            if (kind == KickKind.Pass) { float pat = Patience(team, out _); if (pat != 0f) value += RetainValue * (pat > 0f ? pat : pat * 0.5f) * MathUtil.Clamp01(-team.Forward(from) / 25f); } // (recycling in the middle of the pitch; in the final third the chance decides)
            value += MathF.Max(0f, progress) * 0.00022f * (0.6f + 0.8f * TempoNow(team));
            if (progress < -5f) value -= 0.0006f * (TempoNow(team) - 0.3f) * (-progress / 10f);
            float loss = LossCost(team, pIntercept > 0.3f ? riskPoint : target) + _riskPremium;
            float utility = success * value - (1f - success) * loss;
            // A ball in behind is a gamble a good team takes when it is on, not the default: patience over a hopeful ball
            // (a long ball the runner has little chance of reaching is not even considered).
            if (kind == KickKind.ThroughBall && !_nextOneTwo && !_nextPassIntoSpace)
            {
                if (success < 0.3f) return;
                utility = success * value * 0.75f - (1f - success) * loss;
            }

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
            // Overload to isolate: the block has shifted over to the ball - a wide man left one against one on the far side
            // is exactly what the overload was for.
            int nearTarget = 0;
            foreach (var o in team.Opponent.Players)
                if (!o.SentOff && !o.IsGoalkeeper && Vec2.DistanceSq(o.Pos, target) < 12f * 12f) nearTarget++;
            float isolate = nearTarget <= 1 && crowd >= 3 ? 1.35f : 1f;
            return (0.005f * MathF.Min(crowd, 4) + 0.012f * pressure) * free * vision * isolate * (p.Slot.IsFullBack ? 1.8f : 1f);
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

        private void AddLoftedPass(PlayerAgent p, PlayerAgent q, Vec2 from, Vec2 target, float pressure, KickKind kind, float minDistance = 12f)
        {
            var team = p.Team;
            Vec2 delta = target - from;
            float d = delta.Length;
            if (d < minDistance || d > 62f) return;
            float angle = kind == KickKind.ThroughBall ? 0.62f : (d > 35f ? 0.52f : 0.46f);
            // Drag-corrected ballistic estimate (exact solve happens only when the pass is executed).
            float v = MathF.Sqrt(d * Config.Gravity / MathF.Sin(2f * angle)) * (1f + 0.0032f * d);
            float maxPower = 25f + 8f * MathUtil.A(p.Attr.Passing) + 5f * MathUtil.A(p.Attr.Passing);
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
            if (kind == KickKind.ThroughBall) success *= 0.78f + 0.3f * SpaceBehindLine(team); // the keeper and the recovering defenders (see AddGroundPass)
            if (kind == KickKind.ThroughBall) success *= KeeperRace(p, team, target, arrive);
            float value = ExpectedThreat.At(team, target) * StyleFactor(team, kind, d, true) * RolePassFactor(p, q, kind, d, true, team.Forward(target) - team.Forward(from));
            float relief = SwitchRelief(p, from, target, pressure) * (p.Has(PlayerTrait.SwitchesPlay) ? 1.6f : 1f) * LowBlockSwitch(team);
            value += relief;
            float progress = team.Forward(target) - team.Forward(from);
            value += MathF.Max(0f, progress) * 0.0002f * (0.6f + 0.8f * TempoNow(team));
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

        /// <summary>How the roles of passer and receiver weigh a pass: playmakers look for the ball in behind, a target man is the outlet for the long ball.</summary>
        private static float RolePassFactor(PlayerAgent p, PlayerAgent q, KickKind kind, float d, bool lofted, float progress)
        {
            float m = 1f;
            switch (p.PlayRole)
            {
                case PlayerRole.TempoSetter: m *= d < 22f && !lofted ? 1.1f : 0.8f; break;
                case PlayerRole.DeepLyingPlaymaker: if (d > 25f || kind == KickKind.ThroughBall) m *= 1.25f; break;
                case PlayerRole.ClassicPlaymaker: if (kind == KickKind.ThroughBall) m *= 1.3f; break;
                // the regista: the diagonal switch and the clipped ball over the first line, the vertical pass into the forwards
                case PlayerRole.Regista: if (d > 25f || (lofted && progress > 10f)) m *= 1.3f; else if (progress > 12f) m *= 1.15f; break;
                case PlayerRole.Trequartista: if (kind == KickKind.ThroughBall) m *= 1.35f; else if (d < 14f && progress > 0f) m *= 1.1f; break;
                case PlayerRole.BallPlayingDefender: if (progress > 8f) m *= 1.3f; if (lofted && d > 30f) m *= 1.15f; break;
                case PlayerRole.SecondStriker: case PlayerRole.WidePlaymaker: if (kind == KickKind.ThroughBall) m *= 1.15f; break;
            }
            var r = q.PlayRole;
            if (lofted && (r == PlayerRole.TargetForward || r == PlayerRole.WideTargetMan)) m *= 1.35f;
            return m;
        }

        /// <summary>
        /// Give-and-go: after a short pass the passer may burst past his marker into the space ahead, for the ball straight
        /// back (the receiver plays it first time, see EvaluatePasses). Players who read the game and move well off the
        /// ball do it more, and a short-passing team much more.
        /// </summary>
        private void MaybeStartOneTwo(PlayerAgent p, PlayerAgent q)
        {
            var team = p.Team;
            if (p.IsGoalkeeper || p.Slot.IsCentreBack || q.IsGoalkeeper) return;
            float f = team.Forward(p.Pos), d = Vec2.Distance(p.Pos, q.Pos);
            if (d < 4f || d > 30f || f < -34f || f > 45f) return;
            float offsideLine = MathF.Max(MathF.Max(SecondLastDefenderForward(team), team.Forward(Ball.Pos)), 0f);
            float ahead = MathF.Min(f + 13f, MathF.Max(offsideLine + 1.5f, f + 6f));
            if (ahead < f + 5f) return;
            float lat = team.Lateral(p.Pos), qLat = team.Lateral(q.Pos);
            // past the man on the side away from the receiver's marker: drift a little towards the receiver's line
            Vec2 spot = Pitch.ClampToField(team.ToWorld(ahead, MathUtil.Clamp(lat + (qLat - lat) * 0.3f, -30f, 30f)), -2f);
            if (SpaceAround(team, spot) < 3f) return;
            float chance = OneTwoRunChance * (0.75f + 0.5f * MathUtil.A(p.Attr.OffTheBall) + 0.3f * MathUtil.A(p.Attr.Teamwork)) * StyleShort(team) * (0.8f + 0.4f * team.Eff.Mentality);
            if (!_rng.Chance(chance)) return;
            p.OneTwoWith = q;
            p.OneTwoUntil = SimTime + 3.6f;
            p.OneTwoSpot = spot;
            if (CollectDiagnostics) Count("onetwo.run");
        }

        /// <summary>
        /// Third-man principle: A plays into B, and C - the man A could not reach (the lane to him is blocked, or he is
        /// beyond the next line) - sets off as the ball travels, so that B can lay it first time into his run (A-B-C).
        /// Players who read the game (B's decisions and passing, C's movement) make it more often.
        /// </summary>
        private void MaybeStartThirdMan(PlayerAgent a, PlayerAgent b)
        {
            var team = a.Team;
            if (b.IsGoalkeeper || b.Slot.IsCentreBack) return;
            float bF = team.Forward(b.Pos);
            if (bF < -22f || bF > 40f) return;
            float offsideLine = MathF.Max(MathF.Max(SecondLastDefenderForward(team), team.Forward(Ball.Pos)), 0f);
            PlayerAgent best = null;
            float bestScore = 0f;
            foreach (var c in team.Players)
            {
                if (c == a || c == b || c.SentOff || c.IsGoalkeeper || !IsRunner(c) || c.OneTwoUntil > SimTime || c.ThirdManUntil > SimTime) continue;
                float cF = team.Forward(c.Pos);
                if (cF < bF + 2f || cF > offsideLine + 0.3f) continue;
                float d = Vec2.Distance(c.Pos, b.Pos);
                if (d < 6f || d > 30f) continue;
                bool blocked = LaneClearance(team, a.Pos, c.Pos) < 2.5f;
                float score = (blocked ? 1.4f : 0.6f) * (0.5f + MathUtil.A(c.Attr.OffTheBall)) - 0.015f * d;
                if (score > bestScore) { bestScore = score; best = c; }
            }
            if (best == null) return;
            float chance = (0.12f + 0.2f * MathUtil.A(b.Attr.Decisions) + 0.2f * MathUtil.A(best.Attr.OffTheBall)) * (0.7f + 0.3f * StyleShort(team)) * (0.8f + 0.4f * team.Eff.Mentality);
            if (!_rng.Chance(chance)) return;
            float lane = BestGapInLine(team, offsideLine, team.Lateral(best.Pos));
            best.RunTimer = 3.4f; // (he counts as a runner for the through ball)
            best.RunCheckUntil = -1f;
            best.RunPlan = new Vec2(_rng.Gaussian(1f, 0.8f * (1.4f - MathUtil.A(best.Attr.OffTheBall))), MathUtil.Clamp(MathUtil.Lerp(team.Lateral(best.Pos), lane, 0.5f), -28f, 28f));
            best.ThirdManVia = b;
            best.ThirdManUntil = SimTime + 3.4f;
            team.Stats.ThirdManRuns++;
            if (CollectDiagnostics) Count("thirdman.run");
        }

        private float WeakFootFactor(PlayerAgent p, Vec2 direction)
        {
            // Kicking across the body to the strong-foot side is natural; otherwise the weak foot is likely used.
            float cross = Vec2.Cross(p.FacingDir, direction);
            bool needsLeft = cross > 0.15f;
            bool needsRight = cross < -0.15f;
            int stars;
            if (needsLeft) stars = p.Attr.LeftFoot;
            else if (needsRight) stars = p.Attr.RightFoot;
            else stars = p.Attr.StrongFootRating;
            // Weak foot (stars 1-5): 5 two strong feet, 3 he can use the other foot, 1-2 he hardly can - he tries to get the ball onto his
            // strong foot (more often managing it), and when he cannot, the kick is a poor one.
            if (p.Has(PlayerTrait.UsesWeakFoot)) stars = Math.Max(stars, 3); // he practises with it and is happy to use it (never as good as the strong one)
            else if (_rng.Chance(stars <= 2 ? 0.5f : 0.35f)) stars = p.Attr.StrongFootRating;
            // (the error multiplier: 1 at level 3, 1.25 at 2, 1.8 at 1 - as on the old 1-3 scale - and in between for 2 and 4 stars)
            float level = PlayerAttributes.FootLevel(stars);
            return level >= 2f ? 1f + 0.25f * (3f - level) : 1.25f + 0.55f * (2f - level);
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
        /// <summary>
        /// The speed and the aim-off (yaw) of a lofted kick to <paramref name="target"/>: the curl bends the flight, so a kicker who curls it aims off to
        /// the side it comes back from. <paramref name="draw"/> is the strength of the curl (10-30); the cross's lane check uses the same numbers.
        /// </summary>
        private float LoftedLaunch(PlayerAgent p, Vec2 from, Vec2 target, float launchAngle, float skill, bool curl, float draw, out float yaw)
        {
            float d = Vec2.Distance(from, target);
            float speed = KickSolver.LoftSpeedFor(MathF.Max(2f, d), launchAngle, Config, out _);
            float spin = 0f;
            yaw = 0f;
            if (curl)
            {
                // The ball bends the way the kicking foot naturally curls it: right-footers bend it to the left, left-footers
                // to the right. From the left wing that is an in-swinger for a right-footer, from the right wing an out-swinger.
                spin = NaturalCurlSign(p) * (Config.Kick != null
                    ? Config.Kick.CurledPass.Spin(MathF.Min(1f, (0.5f * (draw - 10f) / 20f + 0.5f * skill) * _curlBoost), speed, Config.Ball.RadiusM) // (stage 7: measured spin)
                    : draw * _curlBoost);
            }
            if (spin != 0f && Config.Aero != null)
            {
                // The physical Magnus force bends the flight by metres, so a kicker who curls it aims off to the side it comes back from, and
                // strikes it for the distance of the curved path (the same integrator as the flight, so the ball comes down on the target).
                speed = KickSolver.LoftSpeedWithSpin(MathF.Max(2f, d), launchAngle, Config, spin, 0f, out _);
                yaw = KickSolver.CurlYaw(speed, launchAngle, Config, spin, 0f);
            }
            _loftedSpin = spin;
            return speed;
        }
        private float _loftedSpin;

        private void KickLofted(PlayerAgent p, Vec2 target, float launchAngle, KickKind kind, PlayerAgent receiver, float pressure, float skill, bool curl, float startZ = Pitch.BallRadius, float curlDraw = -1f)
        {
            Vec2 from = Ball.Pos;
            float d = Vec2.Distance(from, target);
            Vec2 dir = (target - from).Normalized;
            float sigma = (0.02f + 0.08f * (1f - skill)) * (1f + 0.6f * pressure) * p.FatigueError * WeakFootFactor(p, dir);
            if (kind == KickKind.Clearance) sigma *= 1.6f;
            if (kind == KickKind.Cross) sigma *= 1.3f; // (a ball into a crowded box on the run: hard to put on a head)
            float angleNoise = _rng.Gaussian(0f, sigma);
            float speedNoise = _rng.Gaussian(0f, 0.035f + 0.05f * (1f - skill));
            float draw = curl ? (curlDraw >= 0f ? curlDraw : _rng.Range(10f, 30f)) : 0f;
            float speed = LoftedLaunch(p, from, target, launchAngle, skill, curl, draw, out float yaw);
            float spin = _loftedSpin;
            if (CollectDiagnostics && kind == KickKind.Cross && !_restartKick)
            {
                float ay = MathF.Abs(yaw) * 57.2958f;
                Count("cross.yaw." + (ay < 3f ? "a<3" : ay < 6f ? "b3-6" : ay < 10f ? "c6-10" : ay < 15f ? "d10-15" : "e15+"));
            }
            float angle = dir.Angle + yaw + angleNoise;
            speed *= 1f + speedNoise;
            float horizontal = speed * MathF.Cos(launchAngle);
            Vec2 v = Vec2.FromAngle(angle) * horizontal;
            float vz = speed * MathF.Sin(launchAngle);
            KickBall(p, from, v, vz, spin, 0f, kind, receiver, target, false, false, startZ);
            if (kind == KickKind.Cross) Emit(MatchEventType.Cross, p.Team, p, receiver, from, _crossValue, WhistleKind.Short, _crossDetail);
            else if (kind == KickKind.Clearance) Emit(MatchEventType.Clearance, p.Team, p, null, from);
            else EmitPass(p, receiver, kind);
        }

        private void EmitPass(PlayerAgent p, PlayerAgent receiver, KickKind kind)
        {
            float value = _pendingOneTwo ? 4f : _pendingSwitch ? 3f : (kind != KickKind.ThroughBall ? 0f : _pendingIntoSpace ? 2f : 1f);
            Emit(kind == KickKind.LongPass ? MatchEventType.LongBall : MatchEventType.Pass, p.Team, p, receiver, Ball.Pos, value);
        }

        private void ResolvePendingPass(PlayerAgent controller)
        {
            var passer = Ball.PendingPassFrom;
            if (passer == null) return;
            Ball.PendingPassFrom = null;
            bool through = _pendingThrough, intoSpace = _pendingIntoSpace, wasSwitch = _pendingSwitch, oneTwo = _pendingOneTwo, thirdMan = _pendingThirdMan, ground = _pendingGround;
            _pendingThrough = _pendingIntoSpace = _pendingSwitch = _pendingOneTwo = _pendingThirdMan = false;
            string key = _lastPassKey;
            if (oneTwo && CollectDiagnostics)
                Count(controller == null ? "onetwo.fail.dead" : controller.Team != passer.Team ? "onetwo.fail.intercepted" : controller == passer ? "onetwo.fail.backToPasser" : controller.OneTwoWith == passer ? "onetwo.ok" : "onetwo.ok.other");
            if (controller == null)
            {
                if (CollectDiagnostics) Count("pass.lost.dead");
                _lastPassKey = null;
                return;
            }
            if (controller.Team == passer.Team)
            {
                if (CollectDiagnostics)
                    foreach (var d in passer.Team.Opponent.Players)
                        if (d.AnticipatedAt >= Ball.LastKickTime) { _diagAnticipateMissAt[passer.Team.Opponent.Index] = SimTime; Count("anticipate.missed"); break; }
                if (controller != passer)
                {
                    passer.Team.Stats.PassesCompleted++;
                    passer.PassesCompleted++;
                    if (through) passer.Team.Stats.ThroughBallsCompleted++;
                    if (intoSpace && CollectDiagnostics) Count("pass.intospace.ok");
                    if (wasSwitch) passer.Team.Stats.SwitchesCompleted++;
                    if (oneTwo && controller.OneTwoWith == passer) passer.Team.Stats.OneTwosCompleted++;
                    if (thirdMan) passer.Team.Stats.ThirdManCompleted++;
                    if (ground && IsDeepZone(passer.Team, controller.Pos)) passer.Team.Stats.DeepCompletions++;
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
                controller.Interceptions++;
                bool read = controller.AnticipatedAt >= Ball.LastKickTime && Ball.IntendedReceiver != null && Ball.IntendedReceiver.Team == passer.Team;
                // the pass that never reached him: now and then the man it was meant for throws his hands up at the passer
                var meant = Ball.IntendedReceiver;
                if (meant != null && meant.Team == passer.Team && meant != passer && !meant.SentOff && Phase == MatchPhase.Live
                    && _rng.Chance(0.12f + 0.2f * MathUtil.A(meant.Attr.Aggression)))
                    Gesture(meant, GestureKind.Complain, 1.4f, (passer.Pos - meant.Pos).Angle);
                AddConfidence(passer, -0.05f);
                if (read) controller.Team.Stats.AnticipatedInterceptions++;
                if (CollectDiagnostics)
                {
                    float pf = passer.Team.Forward(controller.Pos);
                    Count("pass.intercepted" + (controller.IsGoalkeeper ? ".gk" : ""));
                    if (controller.IsGoalkeeper) Count("pass.gk." + (intoSpace ? "intospace" : key ?? "other"));
                    Count("pass.intercepted.zone" + (pf < -17.5f ? "1own" : pf < 17.5f ? "2mid" : "3fin") + "." + (key ?? "other"));
                }
                if (read) Emit(MatchEventType.Interception, controller.Team, controller, Ball.IntendedReceiver, controller.Pos, 0f, WhistleKind.Short, 1);
                else Emit(MatchEventType.Interception, controller.Team, controller, passer, controller.Pos);
            }
            _lastPassKey = null;
        }
    }
}

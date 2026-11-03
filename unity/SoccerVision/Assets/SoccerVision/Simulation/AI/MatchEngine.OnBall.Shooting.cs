using System;
using System.Collections.Generic;
using System.Linq;

namespace SoccerVision.Simulation
{
    /// <summary>In possession - shooting: the expected goals of a spot, the choice of technique and the shot itself.</summary>
    public sealed partial class MatchEngine
    {
        private float _lastShotTime = -10f;

        /// <summary>The team that took the last shot (rebounds: its players follow in, the defenders turn to the goal).</summary>
        private TeamAgent _lastShotTeam;

        private float _lastShotXg;

        /// <summary>How the last shot was struck: 0 foot, 1 volley, 2 cut inside, 3 direct free kick, 4 penalty, 5 header.</summary>
        private int _lastShotKind;

        private ShotTechnique _lastShotTechnique;

        private bool _shotCountedOnTarget;

        private float EstimateShotXG(PlayerAgent p, Vec2 pos, bool header = false, Vec2? keeperAt = null)
        {
            var team = p.Team;
            float xg = ExpectedGoals.FromLocation(pos, team.AttackGoalSign);
            float dist = Vec2.Distance(pos, team.AttackGoal);
            float skill = header ? MathUtil.A(p.Attr.Heading) : (dist > 20f ? MathUtil.A(p.Attr.LongShots) : MathUtil.A(p.Attr.Finishing));
            xg *= 0.65f + 0.7f * skill;
            if (header) xg *= 0.6f;

            // The keeper: how much of the goal he covers from where the shooter stands. On his line he leaves the corners;
            // coming out he closes the angle; standing on the shooter's toes he covers it all - then the shot is the wrong
            // choice (the pass to a free team-mate, going round him or a chip is the right one). A keeper on the ground or
            // beaten leaves it open.
            float cover = KeeperCoverFactor(team, pos, keeperAt);
            // (the winger cutting inside bends it round the keeper to the far post: the keeper covers less of that)
            if (p.CutInUntil > SimTime && !header) cover = MathF.Max(cover, 0.95f);
            xg *= cover;
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

        /// <summary>
        /// 0.45 (the keeper covers the whole goal) .. 1.4 (he is out of it): from the share of the goal mouth, seen from
        /// <paramref name="pos"/>, that the keeper does not cover. Normalised so that a keeper on his line facing a shot from
        /// the penalty spot is about 1 (what the location model already assumes).
        /// </summary>
        private float KeeperCoverFactor(TeamAgent team, Vec2 pos, Vec2? keeperAt = null)
        {
            var gk = team.Opponent.Goalkeeper;
            if (gk == null || gk.SentOff || gk.IsDown) return 1.4f;
            int sign = team.AttackGoalSign;
            Vec2 postA = new Vec2(Pitch.HalfLength * sign, Pitch.GoalHalfWidth), postB = new Vec2(Pitch.HalfLength * sign, -Pitch.GoalHalfWidth);
            float a1 = (postA - pos).Angle, a2 = (postB - pos).Angle;
            float lo = MathF.Min(a1, a2), hi = MathF.Max(a1, a2);
            if (hi - lo > MathF.PI) { float t = lo; lo = hi; hi = t + 2f * MathF.PI; } // (the goal straddles the angle wrap)
            float width = MathF.Max(0.02f, hi - lo);
            Vec2 toK = (keeperAt ?? gk.Pos) - pos;
            float dk = toK.Length;
            // (a keeper behind the shooter, or far off to the side, covers nothing)
            if (dk < 0.3f || Vec2.Dot(toK, (team.AttackGoal - pos)) <= 0f) return 1.4f;
            float ak = toK.Angle;
            while (ak < lo - MathF.PI) ak += 2f * MathF.PI;
            while (ak > lo + MathF.PI) ak -= 2f * MathF.PI;
            // his reach: the spread body close in, the dive further away
            float reach = dk < 4f ? 1.4f + 0.2f * MathUtil.A(gk.Attr.OneOnOne) : 2.0f + 0.4f * MathUtil.A(gk.Attr.Reflexes);
            float h = MathF.Atan2(reach, dk);
            float covered = MathF.Max(0f, MathF.Min(hi, ak + h) - MathF.Max(lo, ak - h));
            float open = MathUtil.Clamp01(1f - covered / width);
            return MathUtil.Clamp(0.45f + 1.6f * open, 0.45f, 1.4f);
        }

        /// <summary>
        /// Where the keeper will be when the carrier gets to <paramref name="target"/>: coming out at him (a keeper does not
        /// stand still while a forward runs at him) - so carrying the ball on is weighed against the keeper who meets him.
        /// </summary>
        private Vec2 KeeperWhenThere(TeamAgent team, Vec2 from, Vec2 target)
        {
            var gk = team.Opponent.Goalkeeper;
            if (gk == null || gk.SentOff) return team.Opponent.OwnGoal;
            float t = Vec2.Distance(from, target) / 6.5f;
            Vec2 toward = target - gk.Pos;
            float d = toward.Length;
            if (d < 0.5f) return gk.Pos;
            return gk.Pos + toward / d * MathF.Min(MathF.Max(0f, d - 1.2f), 6.5f * t);
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

        /// <summary>+1 when the kicker's natural curl bends the ball to the left of its path (right-footers), -1 for left-footers.</summary>
        private static float NaturalCurlSign(PlayerAgent p) => p.Attr.StrongFoot == Foot.Right ? 1f : -1f;

        /// <summary>Shot at goal: aims at a corner away from the keeper, then applies skill-based error.</summary>
        private bool _lastShotOpenGoal;
        private string _lastShotKeeperBucket;

        private void ExecuteShot(PlayerAgent p, bool header, bool penalty, float xg, bool volley = false, bool bicycle = false)
        {
            var team = p.Team;
            int goalSign = team.AttackGoalSign;
            Vec2 from = Ball.Pos;
            float startZ = Ball.Z;
            float dist = Vec2.Distance(from, team.AttackGoal);
            var keeper = team.Opponent.Goalkeeper;

            float skill;
            if (penalty)
            {
                // The crowd on the penalty taker: the home team's takers are lifted, the away team's feel it (and miss more).
                skill = MathUtil.A((p.Attr.Penalty * 2 + p.Attr.Composure) / 3);
                skill = MathUtil.Clamp01(skill * (p.Team == Home ? 1f + 0.06f * Config.HomeAdvantage : 1f - 0.16f * Config.HomeAdvantage));
            }
            else if (header) skill = MathUtil.A(p.Attr.Heading);
            else if (dist > 20f) skill = MathUtil.A(p.Attr.LongShots);
            else skill = MathUtil.A(p.Attr.Finishing);

            // How he strikes it: picked from the situation (distance, angle, where the keeper is, which foot the ball is
            // on) and the player (technique, composure, how good his weaker foot is).
            // (a half-volley is struck just as the ball comes up off the turf; a volley is hit out of the air)
            var tech = bicycle ? ShotTechnique.Bicycle : header ? ShotTechnique.Header : penalty ? ShotTechnique.Penalty : volley ? (startZ < 0.6f ? ShotTechnique.HalfVolley : ShotTechnique.Volley)
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
            bool knuckle = false;
            switch (tech)
            {
                case ShotTechnique.PlacedNear:
                case ShotTechnique.PlacedFar:
                    // Side-foot "pass into the net": less pace, much more control, kept low.
                    power = 15f + 5f * skill + _rng.Range(-1f, 1f);
                    aimZ = _rng.Range(0.15f, 0.7f);
                    accuracy = 0.85f;
                    break;
                case ShotTechnique.Power:
                    // The laces drive: the hard part of the boot straight through the ball - pace over finesse.
                    power = 23f + 6f * skill + 3f * MathUtil.A(p.Attr.PhysicalPower) + _rng.Range(-1.5f, 1.5f);
                    aimZ = _rng.Chance(0.6f) ? _rng.Range(0.2f, 0.9f) : _rng.Range(0.9f, 2.0f);
                    accuracy = 1.18f;
                    break;
                case ShotTechnique.HalfVolley:
                    // Hit the instant it comes up off the grass: low, driven and quick, hard to read for the keeper.
                    power = 21f + 6f * skill + _rng.Range(-1f, 1f);
                    aimZ = _rng.Range(0.2f, 0.9f);
                    accuracy = 1.12f;
                    break;
                case ShotTechnique.Knuckleball:
                    // Struck through the middle of the ball with no spin: it floats and dips unpredictably.
                    power = 24f + 5f * skill + _rng.Range(-1f, 1f);
                    aimZ = _rng.Range(0.8f, 1.9f);
                    aimY = _rng.Range(-2.2f, 2.2f);
                    accuracy = 1.35f;
                    knuckle = true;
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
                case ShotTechnique.Bicycle:
                    // Overhead, the body flat in the air: a wild, spectacular strike - hard to place, harder for the keeper to read.
                    power = 19f + 6f * skill;
                    aimZ = _rng.Range(0.4f, 2f);
                    accuracy = 1.55f;
                    break;
                case ShotTechnique.Backheel:
                    // A flick with the heel, back to goal: soft, but the keeper rarely sees it coming.
                    power = 11f + 4f * skill;
                    aimZ = _rng.Range(0.15f, 0.6f);
                    accuracy = 1.35f;
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
            // An open goal (the keeper beaten or down, nobody in the lane): he just rolls it in - well inside the posts, low,
            // no curl, no showing off. Nine in ten of these go in.
            bool openGoal = !penalty && !header && dist < 26f && (keeper == null || keeper.SentOff || keeper.IsDown
                            || Vec2.DistanceToSegment(keeper.Pos, from, team.AttackGoal, out _) > 2.4f);
            if (openGoal)
                foreach (var o in team.Opponent.Players)
                    if (!o.SentOff && o != keeper && Vec2.DistanceToSegment(o.Pos, from, team.AttackGoal, out _) < 1.2f) { openGoal = false; break; }
            if (openGoal)
            {
                float kY = keeper != null ? keeper.Pos.Y : 0f;
                aimY = MathUtil.Clamp(-MathF.Sign(kY == 0f ? 1f : kY) * 1.4f, -1.8f, 1.8f); // (away from where the keeper is coming back)
                aimZ = 0.35f;
                curlMag = 0f;
                power = MathF.Min(power, 18f + 0.4f * dist);
                if (CollectDiagnostics) Count("shot.openGoal");
            }
            _lastShotOpenGoal = openGoal;
            if (CollectDiagnostics && !penalty && !header)
            {
                // how close the keeper was when the shot was struck (open play, on the ground or volleyed)
                float dk = keeper != null ? Vec2.Distance(keeper.Pos, from) : 99f;
                _lastShotKeeperBucket = dk < 3f ? "a<3" : dk < 6f ? "b3-6" : dk < 10f ? "c6-10" : "d10+";
                if (dk < 3f) _lastShotKeeperBucket += keeper != null && !keeper.IsDown && Vec2.DistanceToSegment(keeper.Pos, from, team.AttackGoal, out _) < 1.6f ? ".front" : ".beaten";
                Count("shot.keeper." + _lastShotKeeperBucket);
            }
            else _lastShotKeeperBucket = null;
            if (CollectDiagnostics && p.FirstTimeUntil > SimTime && !header && !penalty) { Count("shot.firstTime"); if (_lastShotKeeperBucket != null) _lastShotKeeperBucket += ".ft"; }
            p.FirstTimeUntil = -1f;
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
            // Some finishes are aimed into the ground: a header nodded down so it bounces up past the keeper, a volley kept
            // down, a low drive that skips off the turf in front of him. (Bounces are hard for keepers.)
            bool bouncer = !penalty && tech != ShotTechnique.Chip && distToAim > 5f
                           && ((header && startZ > 1.4f && _rng.Chance(0.25f)) || (volley && _rng.Chance(0.35f)) || (tech == ShotTechnique.Power && distToAim > 14f && _rng.Chance(0.18f)));
            if (bouncer)
            {
                float bounceAt = MathF.Max(2f, distToAim - _rng.Range(1.5f, header ? 4.5f : 6.5f));
                vz = KickSolver.VerticalSpeedFor(horizontal, startZ, bounceAt, Pitch.BallRadius, Config, curlSign * curlMag, 0f);
                if (CollectDiagnostics) Count("shot.bouncer");
            }

            float pressure = penalty ? 0f : Pressure(p, out _);
            float composure = MathUtil.A(p.Attr.Composure);
            float stake = penalty ? 0.25f * (p.Team == Away ? 1f + 0.8f * Config.HomeAdvantage : 1f - 0.2f * Config.HomeAdvantage) : 0f; // (the crowd: the away taker feels the pressure more)
            float sigmaA = (0.105f + 0.19f * (1f - skill)) * (1f + 0.75f * pressure * (1.2f - composure) + stake) * p.FatigueError;
            if (_slippery) sigmaA *= 1f + 0.6f * (1f - BallControlFactor(p.Pos));
            if (penalty) sigmaA *= 0.2f;
            // (the techniques for the weaker side already price in which foot is used)
            if (tech == ShotTechnique.Power || tech == ShotTechnique.PlacedNear || tech == ShotTechnique.PlacedFar) sigmaA *= WeakFootFactor(p, dir);
            if (volley) sigmaA *= 1.35f;
            sigmaA *= accuracy;
            if (keeper != null && !penalty && Vec2.Distance(keeper.Pos, from) < 14f) sigmaA *= 1f + 0.5f * (1f - composure); // (cool in front of the keeper)
            if (openGoal) sigmaA *= 0.2f;
            float sigmaZ = (0.6f + 1.8f * (1f - skill)) * (1f + 0.5f * pressure) * p.FatigueError;
            if (openGoal) sigmaZ *= 0.3f;
            if (penalty) sigmaZ *= 0.35f;
            if (tech == ShotTechnique.PlacedNear || tech == ShotTechnique.PlacedFar) sigmaZ *= 0.6f;
            if (tech == ShotTechnique.Chip) sigmaZ *= 0.5f; // a soft lob: errors are in length, not height
            // Technique error that does not shrink with distance (snatched/skied finishes at close range).
            float flight = MathF.Max(0.15f, distToAim / MathF.Max(5f, power));
            float fixedLateral = (penalty ? 0.15f : openGoal ? 0.25f : 1.0f) * (1.3f - skill) * (1f + pressure);
            float fixedHeight = (penalty ? 0.15f : openGoal ? 0.2f : 0.55f + 1.0f * (1f - skill)) * (1f + 0.6f * pressure);
            if (tech == ShotTechnique.Chip) fixedHeight *= 0.3f;
            float angle = dir.Angle + _rng.Gaussian(0f, sigmaA) + _rng.Gaussian(0f, fixedLateral / MathF.Max(3f, distToAim));
            vz += _rng.Gaussian(0f, sigmaZ) + _rng.Gaussian(0f, fixedHeight / flight);
            Vec2 v = Vec2.FromAngle(angle) * power;
            float curl = header ? 0f : (curlMag > 0f ? curlSign * curlMag + _rng.Gaussian(0f, 2.5f) : _rng.Gaussian(0f, tech == ShotTechnique.Chip ? 1f : tech == ShotTechnique.Knuckleball ? 0.4f : 8f));
            if (CollectDiagnostics) Count("shot.tech." + tech);
            _lastShotTechnique = tech;

            KickBall(p, from, v, vz, curl, 0f, header ? KickKind.Header : KickKind.Shot, null, aim, penalty, false, startZ);
            Ball.IsShot = true;
            Ball.ShotKnuckle = knuckle;
            Ball.Shooter = p;
            Ball.ShotSpeed = power;
            _lastShotTime = SimTime;
            _lastShotTeam = team;
            _lastShotXg = xg;
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
                    // He must get a leg or his body to the ball: within reach of where it is struck.
                    if (gap > 1.3f || Vec2.Dot(rel, dir) <= 0.2f * gap) continue;
                    float pBlock = 0.45f * (1f - gap / 1.3f) + 0.12f * MathUtil.A(d.Attr.Tackling) + 0.06f * MathUtil.A(d.Attr.Positioning);
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
            if (tech == ShotTechnique.Knuckleball) team.Stats.Knuckleballs++;
            if (CollectDiagnostics && team.Index == 0 && !header && !penalty && dist > 18f && !cutIn) Count("shot.long.nocut"); // (from range, not the winger's cut-in shot)
            if (cutIn) { team.Stats.CutInShots++; if (CollectDiagnostics) Count("cutin.T" + team.Index + ".N" + p.Number); }
            if (header) team.Stats.Headers++;
            p.CutInUntil = -1f;
            // (a cut inside and a drive or a placed shot is its own kind; a curler, a chip, a knuckleball, a trivela or a backheel keeps its name)
            _lastShotKind = bicycle ? 15 : header ? 5 : penalty ? 4 : volley ? (tech == ShotTechnique.HalfVolley ? 12 : 1) : (cutIn && (tech == ShotTechnique.PlacedFar || tech == ShotTechnique.PlacedNear || tech == ShotTechnique.Power) ? 2 : ShotDetail(tech));
            MomentumShot(p, xg);
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
            // Back to goal right in front of it: the flick with the heel (rare, only for the technical ones).
            Vec2 toGoal = (team.AttackGoal - from).Normalized;
            bool showy = p.Has(PlayerTrait.Showboat);
            if (dist < 9f && Vec2.Dot(p.FacingDir, toGoal) < -0.3f && (technique > 0.62f || showy && technique > 0.5f)
                && _rng.Chance((0.35f * MathF.Max(0f, technique - 0.62f) / 0.38f + 0.04f) * (showy ? 2f : 1f)))
                return ShotTechnique.Backheel;
            if (keeper != null && !keeper.SentOff)
            {
                float off = Pitch.HalfLength - keeper.Pos.X * goalSign; // keeper's distance from his goal line
                float toKeeper = Vec2.Distance(from, keeper.Pos);
                float inLine = Vec2.DistanceToSegment(keeper.Pos, from, team.AttackGoal, out _);
                if (off > 3.5f && inLine < 3.5f && toKeeper > 4f && toKeeper < 18f && dist > 9f && dist < 30f
                    && _rng.Chance((0.1f + 0.45f * technique) * MathUtil.Clamp01((off - 3.5f) / 6f + 0.35f)))
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
                if (p.Has(PlayerTrait.OutsideOfTheFoot)) trivela = 0.45f + 0.25f * technique; // his signature strike
                if (p.Has(PlayerTrait.UsesWeakFoot)) trivela = 0f;                            // he trusts the other foot
                return _rng.Chance(trivela * (0.5f + technique)) ? ShotTechnique.OutsideFoot : ShotTechnique.WeakFoot;
            }
            // The knuckleball: a long, central strike by someone who strikes a ball cleanly (free-kick and long-shot skill).
            if (dist >= 22f && dist <= 33f && MathF.Abs(from.Y) < 11f && p.Attr.LongShots + p.Attr.FreeKicks >= 130
                && _rng.Chance(0.05f + 0.35f * MathUtil.Clamp01((p.Attr.LongShots + p.Attr.FreeKicks - 130f) / 70f)))
                return ShotTechnique.Knuckleball;
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
                case ShotTechnique.HalfVolley: return 12;
                case ShotTechnique.Knuckleball: return 13;
                case ShotTechnique.Power: return 14;
                case ShotTechnique.Bicycle: return 15;
                case ShotTechnique.Backheel: return 16;
                default: return 0;
            }
        }
    }
}

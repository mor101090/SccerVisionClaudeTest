using System;
using System.Collections.Generic;

namespace SoccerVision.Simulation
{
    internal enum ContactKind { None, Feet, Head, Body, Hands }

    /// <summary>Ball contacts: control, interceptions, blocks, headers, aerial duels, saves and tackles.</summary>
    public sealed partial class MatchEngine
    {
        private struct Contact
        {
            public PlayerAgent Player;
            public ContactKind Kind;
            public float Distance;
            public float Weight;
            public float Along;
        }

        private readonly List<Contact> _contacts = new List<Contact>(8);

        private void ResolveTouches()
        {
            if (Ball.Owner != null || Ball.InGoal) return;
            Vec2 a = Ball.PrevPos, b = Ball.Pos;
            float z = Ball.Z;
            _contacts.Clear();
            bool passInFlight = Ball.IntendedReceiver != null || Ball.IsShot;

            foreach (var p in AllPlayers)
            {
                if (p.SentOff || p.TouchCooldown > 0f || p.StunTimer > 0f) continue;
                if (p.Task == PlayerTask.Shield && p.Team.Shielder == p) continue; // letting it run out, on purpose
                if (p == Ball.LastKicker && (Ball.KickerMustWait || SimTime - Ball.LastKickTime < 0.3f)) continue;
                // Law 12.2: an opponent may not prevent the goalkeeper releasing the ball from the hands.
                if (Ball.LastKickKind == KickKind.GoalkeeperDistribution && Ball.LastKicker != null && Ball.LastKicker.Team != p.Team
                    && SimTime - Ball.LastKickTime < 0.4f) continue;
                float d = Vec2.DistanceToSegment(p.Pos, a, b, out _);
                if (d > 2.6f) continue;

                // A player surprised by a new ball path (within his reaction time) can only block with his body.
                bool surprised = _ballChangeExpectedBy != p.Team && Ball.IntendedReceiver != p && SimTime - _ballChangedAt < p.ReactionTime;
                float footReach = surprised ? 0.45f : p.FootReach;
                bool blocker = Ball.IsShot && Ball.Shooter != null && Ball.Shooter.Team != p.Team && !p.IsGoalkeeper;
                if (IsCrossBlock(p))
                {
                    // The full-back closing the crosser gets a leg in the way.
                    surprised = false;
                    footReach = 0.95f;
                }
                if (blocker)
                {
                    // Defenders expect shots: a set defender in the lane gets a sliding block in.
                    surprised = false;
                    footReach = 0.95f;
                }
                ContactKind kind = ContactKind.None;
                bool handsAllowed = p.IsGoalkeeper && Ball.HandlingForbiddenFor != p.Team
                                    && Pitch.InPenaltyArea(p.Pos, p.Team.OwnGoalSign) && Pitch.InPenaltyArea(Ball.Pos, p.Team.OwnGoalSign);
                if (handsAllowed && z <= p.GoalkeeperReach && d <= GoalkeeperHandReach(p)) kind = ContactKind.Hands;
                else if (z <= (blocker || IsCrossBlock(p) ? 1.5f : 1.05f) && d <= footReach) kind = ContactKind.Feet;
                else if (z > 1.05f && z <= p.HeadReach && d <= (surprised ? 0.4f : 0.62f)) kind = ContactKind.Head;
                else if (z <= 1.9f && d <= 0.33f) kind = ContactKind.Body;
                if (kind == ContactKind.None) continue;

                // Team-mates of the passer leave the ball for the intended receiver (or let a shot through).
                bool teammateOfKicker = Ball.LastTouchTeam == p.Team && passInFlight && Ball.IntendedReceiver != p;
                if (teammateOfKicker && (kind != ContactKind.Body || _rng.Chance(0.75f))) continue;

                float weight;
                switch (kind)
                {
                    case ContactKind.Hands:
                        weight = 3f;
                        break;
                    case ContactKind.Head:
                        weight = 0.6f + (p.Attr.Jumping + p.Attr.Heading + p.Attr.PhysicalPower) / 150f + (p.Profile.HeightCm - 180) * 0.012f;
                        break;
                    default:
                        weight = 0.6f + (p.Attr.BallControl + p.Attr.Positioning + p.Attr.PhysicalPower) / 200f;
                        break;
                }
                weight *= 1.3f - d / 2.6f;
                if (Ball.IntendedReceiver == p) weight *= 1.25f;
                float along = Vec2.Dot(p.Pos - a, Ball.Velocity.LengthSq > 0.01f ? Ball.Velocity.Normalized : Vec2.Zero);
                _contacts.Add(new Contact { Player = p, Kind = kind, Distance = d, Weight = weight, Along = along });
            }

            if (_contacts.Count == 0) return;

            int winner = 0;
            bool contested = false;
            if (_contacts.Count > 1)
            {
                var team0 = _contacts[0].Player.Team;
                for (int i = 1; i < _contacts.Count; i++) if (_contacts[i].Player.Team != team0) contested = true;
                var weights = new float[_contacts.Count];
                int first = 0;
                for (int i = 1; i < _contacts.Count; i++) if (_contacts[i].Along < _contacts[first].Along) first = i;
                for (int i = 0; i < _contacts.Count; i++) weights[i] = _contacts[i].Weight * (i == first ? 2.2f : 1f);
                winner = _rng.PickWeighted(weights, weights.Length);
                if (winner < 0) winner = 0;
                for (int i = 0; i < _contacts.Count; i++)
                {
                    if (i == winner) continue;
                    var loser = _contacts[i].Player;
                    loser.TouchCooldown = 0.45f;
                }
            }

            var c = _contacts[winner];
            if (contested && c.Kind == ContactKind.Head)
            {
                // Aerial challenges sometimes end in a push/holding foul.
                foreach (var other in _contacts)
                {
                    if (other.Player == c.Player || other.Player.Team == c.Player.Team) continue;
                    float pFoul = 0.025f + 0.04f * MathUtil.A(other.Player.Attr.Aggression);
                    if (_rng.Chance(pFoul))
                    {
                        CommitFoul(other.Player, c.Player, false, true, c.Player.Pos);
                        return;
                    }
                }
            }
            ApplyTouch(c.Player, c.Kind, contested);
        }

        private float GoalkeeperHandReach(PlayerAgent gk)
        {
            float reach = 1.0f + 0.25f * MathUtil.A(gk.Attr.Reflexes);
            if (gk.DiveTimer > 0f) reach += 1.1f + 0.5f * MathUtil.A(gk.Attr.Reflexes);
            return reach;
        }

        private void ApplyTouch(PlayerAgent p, ContactKind kind, bool contested)
        {
            if (p.SentOff) Violation("sentOffTouch");
            if (p == Ball.LastKicker && Ball.KickerMustWait) Violation("doubleTouch");
            if (IsFlaggedOffside(p))
            {
                OffsideOffence(p);
                return;
            }
            p.Touches++;
            bool opponentTouch = Ball.LastTouchTeam != null && Ball.LastTouchTeam != p.Team;

            if (kind != ContactKind.Hands && IsCrossBlock(p))
            {
                BlockCross(p);
                return;
            }
            switch (kind)
            {
                case ContactKind.Hands:
                    GoalkeeperHandle(p, contested);
                    return;
                case ContactKind.Head:
                    HeaderTouch(p, contested, opponentTouch);
                    return;
                case ContactKind.Body:
                    if (opponentTouch && Ball.Z > 0.9f && (Ball.IsShot || Ball.LastKickKind == KickKind.Cross) && _rng.Chance(0.06f))
                    {
                        Handball(p);
                        return;
                    }
                    if (Ball.IsShot && opponentTouch) BlockShot(p);
                    else Deflect(p, _rng.Range(0.25f, 0.5f), 1.2f);
                    return;
                default:
                    FootTouch(p, contested, opponentTouch);
                    return;
            }
        }

        private void FootTouch(PlayerAgent p, bool contested, bool opponentTouch)
        {
            Vec2 rel = Ball.Velocity - p.Vel;
            float relSpeed = rel.Length;
            float control = MathUtil.A(p.Attr.BallControl);

            // Volley: a ball in the air near goal is hit first time instead of controlled.
            if (!p.IsGoalkeeper && Ball.Z > 0.3f && !(Ball.IsShot && opponentTouch) && TryVolley(p, opponentTouch)) return;

            if (Ball.IsShot && opponentTouch && !p.IsGoalkeeper)
            {
                BlockShot(p);
                return;
            }
            if (Ball.IsShot && opponentTouch && p.IsGoalkeeper)
            {
                // Keeper using feet/legs (outside the area or back-pass): treat as a save attempt without catching.
                SaveAttempt(p, allowCatch: false);
                return;
            }

            float pControl;
            if (opponentTouch)
            {
                pControl = 0.86f - 0.022f * MathF.Max(0f, relSpeed - 6f) * (1.25f - control) + 0.08f * (MathUtil.A(p.Attr.Positioning) - 0.5f);
            }
            else
            {
                pControl = 0.975f - 0.02f * MathF.Max(0f, relSpeed - 7f) * (1.25f - control);
            }
            if (contested) pControl -= 0.2f;
            if (Ball.Z > 0.45f) pControl -= 0.1f * (1.2f - control);
            pControl -= (1f - p.FatigueFactor) * 0.5f;
            if (_slippery) pControl -= (1f - BallControlFactor(Ball.Pos)) * 0.6f;
            pControl = MathUtil.Clamp(pControl, 0.25f, 0.99f);

            if (_rng.Chance(pControl))
            {
                GainControl(p, relSpeed);
            }
            else
            {
                // Heavy touch: the ball bounces off the foot into space.
                if (opponentTouch) ClearOffside(); // a deliberate (if unsuccessful) play by the defending side
                Vec2 dir = Ball.Velocity.LengthSq > 1f ? Ball.Velocity.Normalized.Rotate(_rng.Range(-1.1f, 1.1f)) : p.FacingDir.Rotate(_rng.Range(-1f, 1f));
                float speed = MathUtil.Clamp(relSpeed * _rng.Range(0.2f, 0.45f), 1.5f, 9f);
                SetDeflection(p, dir * speed, _rng.Range(0f, 2f));
                p.TouchCooldown = 0.35f;
            }
        }

        private bool TryVolley(PlayerAgent p, bool opponentTouch)
        {
            var team = p.Team;
            float f = team.Forward(Ball.Pos);
            float dist = Vec2.Distance(Ball.Pos, team.AttackGoal);
            if (f < 30f || dist > 22f || Ball.M.Speed < 3f) return false;
            float finishing = MathUtil.A((p.Attr.Finishing + p.Attr.BallControl) / 2);
            float pTry = (opponentTouch ? 0.35f : 0.5f) + 0.4f * finishing;
            if (!_rng.Chance(pTry)) return false;
            ResolvePendingPass(p);
            Ball.LastToucher = p;
            Ball.LastTouchTeam = team;
            ExecuteShot(p, false, false, EstimateShotXG(p, Ball.Pos) * 0.8f, volley: true);
            return true;
        }

        private void GainControl(PlayerAgent p, float relSpeed)
        {
            if (CollectDiagnostics) ContactCheck(p, Ball.Pos, Ball.Z, "control");
            bool changeOfPossession = Ball.LastTouchTeam != p.Team;
            ResolvePendingPass(p);
            if (changeOfPossession) ClearOffside();
            if (changeOfPossession && Ball.LastToucher != null && Ball.LastToucher.Team != p.Team && Ball.PendingPassFrom == null && Ball.IsShot == false)
            {
                // Loose ball won (no pass pending) - nothing extra to record.
            }
            Ball.Owner = p;
            Ball.InHands = false;
            Ball.ClearFlight();
            Ball.KickerMustWait = false;
            Ball.HandlingForbiddenFor = null;
            Ball.LastToucher = p;
            Ball.LastTouchTeam = p.Team;
            PossessionTeam = p.Team;
            if (changeOfPossession) p.Team.LastAttackSeconds = SimTime;
            SnapshotOffside(p, false);
            _penaltyKickTime = -1f;
            float tempo = p.Team.Tactics.Tempo;
            p.DecisionTimer = MathUtil.Clamp(0.1f + 0.3f * (1f - MathUtil.A(p.Attr.BallControl)) + relSpeed * 0.012f - 0.08f * (tempo - 0.5f), 0.08f, 0.6f);
            p.OneTwoUntil = -1f; // (the return ball has arrived, or the run is over)
            // A team-mate is on a give-and-go run past me: the wall pass is played first time.
            foreach (var r in p.Team.Players)
                if (r.OneTwoWith == p && r.OneTwoUntil > SimTime) { p.DecisionTimer = MathF.Min(p.DecisionTimer, 0.1f); break; }
            p.Task = PlayerTask.OnBall;
            p.HasPendingPlan = false;
            p.HoldTime = 0f;
            p.Target = p.Pos + p.Vel * 0.3f;
            _goalkeeperHoldTimer = 0f;
            AssignBallDuties();
        }

        /// <summary>Gives the ball a new free velocity after touching <paramref name="p"/>.</summary>
        /// <summary>
        /// Diagnostics: how far the player was from the ball when he touched it. A touch from further than a leg (or, for a
        /// keeper, an arm and a dive) can reach would look like the ball moving on its own.
        /// </summary>
        internal void ContactCheck(PlayerAgent p, Vec2 ballPos, float ballZ, string what)
        {
            float d = Vec2.Distance(p.Pos, ballPos);
            float limit = p.IsGoalkeeper ? 2.6f : 1.25f;
            if (d > limit) Count("contact.far." + what + (p.IsGoalkeeper ? ".gk" : "") + (d > limit + 1f ? ".gt1m" : ".lt1m"));
            Count("contact.all");
        }

        private void SetDeflection(PlayerAgent p, Vec2 horizontal, float vz, [System.Runtime.CompilerServices.CallerMemberName] string caller = "")
        {
            if (CollectDiagnostics) ContactCheck(p, Ball.Pos, Ball.Z, "deflect." + caller);
            Ball.Owner = null;
            Ball.InHands = false;
            Ball.M.Vx = horizontal.X;
            Ball.M.Vy = horizontal.Y;
            Ball.M.Vz = vz;
            if (vz > 0f && Ball.M.Z < Pitch.BallRadius + 0.01f) Ball.M.Z = Pitch.BallRadius + 0.01f;
            Ball.M.SideSpin = 0f;
            Ball.M.TopSpin = 0f;
            Ball.LastToucher = p;
            Ball.LastTouchTeam = p.Team;
            Ball.IntendedReceiver = null;
            Ball.KickerMustWait = false;
            Ball.HandlingForbiddenFor = null;
            p.TouchCooldown = MathF.Max(p.TouchCooldown, 0.3f);
            MarkBallChanged();
            RebuildTrajectory();
            _ballChangedAt = SimTime;
            _ballChangeExpectedBy = null;
            AssignBallDuties();
        }

        private void Deflect(PlayerAgent p, float energy, float popUp)
        {
            Vec2 v = Ball.Velocity;
            float speed = v.Length;
            Vec2 dir = speed > 0.5f ? (v / speed).Rotate(_rng.Range(-1.3f, 1.3f)) : Vec2.FromAngle(_rng.Range(0f, MathUtil.TwoPi));
            SetDeflection(p, dir * MathF.Max(1.5f, speed * energy), _rng.Range(0f, popUp * 2f));
        }

        /// <summary>A defender near the crosser, touching the cross in its first moments.</summary>
        private bool IsCrossBlock(PlayerAgent p)
        {
            return Ball.LastKickKind == KickKind.Cross && Ball.LastKicker != null && Ball.LastKicker.Team != p.Team
                   && !p.IsGoalkeeper && SimTime - Ball.LastKickTime < 0.4f && Ball.IntendedReceiver != p;
        }

        /// <summary>Blocked cross: it loops off the defender, usually towards the goal line - often a corner.</summary>
        private void BlockCross(PlayerAgent p)
        {
            var crosser = Ball.LastKicker;
            Vec2 v = Ball.Velocity;
            Vec2 toLine = crosser.Team.DirToWorld(Vec2.UnitX);
            Vec2 dir = (toLine * _rng.Range(0.5f, 1.3f) + (v.LengthSq > 1f ? v.Normalized : Vec2.Zero) * 0.5f + new Vec2(_rng.Range(-0.5f, 0.5f), _rng.Range(-0.5f, 0.5f))).Normalized;
            if (CollectDiagnostics) Count("cross.blocked");
            SetDeflection(p, dir * MathF.Max(4f, v.Length * _rng.Range(0.3f, 0.6f)), _rng.Range(1f, 5f));
        }

        private void BlockShot(PlayerAgent p)
        {
            var shooter = Ball.Shooter;
            if (shooter != null)
            {
                shooter.Team.Stats.ShotsBlocked++;
                Emit(MatchEventType.ShotBlocked, p.Team, p, shooter, Ball.Pos);
            }
            Ball.IsShot = false;
            Vec2 v = Ball.Velocity;
            Vec2 dir = v.LengthSq > 1f ? v.Normalized.Rotate(_rng.Range(-0.9f, 0.9f)) : p.FacingDir;
            SetDeflection(p, dir * MathF.Max(3f, v.Length * _rng.Range(0.2f, 0.55f)), _rng.Range(0f, 4f));
        }

        private void Handball(PlayerAgent p)
        {
            var victim = Ball.LastToucher != null && Ball.LastToucher.Team != p.Team ? Ball.LastToucher : null;
            if (victim == null)
            {
                Deflect(p, 0.3f, 1f);
                return;
            }
            Ball.Stop();
            CommitFoul(p, victim, false, false, p.Pos);
        }

        // ---- Headers ---------------------------------------------------------------------------------

        private void HeaderTouch(PlayerAgent p, bool contested, bool opponentTouch)
        {
            if (CollectDiagnostics && Ball.Z > 1.9f) Count(p.JumpZ > 0.1f ? (p.JumpVz > -0.6f && p.JumpVz < 0.6f ? "header.jump.apex" : "header.jump.airborne") : "header.jump.ground");
            if (Ball.Z > 1.9f) StartJump(p, Ball.Z - 1.75f); // unanticipated: a late spring at least
            if (opponentTouch) ClearOffside();
            var team = p.Team;
            float f = team.Forward(Ball.Pos);
            float distGoal = Vec2.Distance(Ball.Pos, team.AttackGoal);
            float heading = MathUtil.A(p.Attr.Heading);
            Ball.LastToucher = p;
            Ball.LastTouchTeam = team;
            ResolvePendingPass(p);

            if (distGoal < 15f && f > 36f)
            {
                float xg = EstimateShotXG(p, Ball.Pos, header: true);
                // A header shot keeps the ball where it is (no control phase).
                Emit(MatchEventType.Header, team, p, null, Ball.Pos);
                ExecuteShot(p, true, false, xg);
                return;
            }

            Vec2 dir;
            float speed;
            float vz;
            PlayerAgent receiver = null;
            if (f < -12f)
            {
                // Defensive header: away from goal, wide and high - or, under pressure, behind for a corner.
                Vec2 away = team.DirToWorld(Vec2.UnitX).Rotate(_rng.Range(-0.9f, 0.9f));
                bool fromCross = Ball.LastKickKind == KickKind.Cross || Ball.LastKickKind == KickKind.Restart;
                if (fromCross && f < -34f && _rng.Chance(contested ? 0.45f : 0.28f))
                {
                    float side = Ball.Pos.Y >= 0f ? 1f : -1f;
                    away = (team.DirToWorld(new Vec2(-1f, 0f)) + new Vec2(0f, side * _rng.Range(0.4f, 1.2f))).Normalized;
                }
                dir = away;
                speed = 10f + 7f * heading + 3f * MathUtil.A(p.Attr.PhysicalPower);
                vz = _rng.Range(4f, 7.5f);
                Emit(MatchEventType.Clearance, team, p, null, Ball.Pos);
            }
            else
            {
                // Header pass / flick-on to the best placed team-mate ahead.
                PlayerAgent best = null;
                float bestScore = float.NegativeInfinity;
                foreach (var q in team.Players)
                {
                    if (q.SentOff || q == p || q.IsGoalkeeper) continue;
                    float d = Vec2.Distance(q.Pos, p.Pos);
                    if (d < 4f || d > 18f) continue;
                    float score = team.Forward(q.Pos) * 0.08f + NearestOpponentDistance(team, q.Pos) * 0.3f - d * 0.05f;
                    if (score > bestScore) { bestScore = score; best = q; }
                }
                if (best != null)
                {
                    receiver = best;
                    Vec2 to = best.Pos + best.Vel * 0.5f - Ball.Pos;
                    dir = to.Normalized;
                    speed = MathUtil.Clamp(to.Length * 0.75f, 7f, 14f);
                    vz = _rng.Range(1.5f, 3.5f);
                }
                else
                {
                    dir = team.DirToWorld(Vec2.UnitX).Rotate(_rng.Range(-0.6f, 0.6f));
                    speed = 9f + 5f * heading;
                    vz = _rng.Range(2f, 4f);
                }
                Emit(MatchEventType.Header, team, p, receiver, Ball.Pos);
            }
            float sigma = (0.08f + 0.22f * (1f - heading)) * (contested ? 1.3f : 1f);
            dir = dir.Rotate(_rng.Gaussian(0f, sigma));
            KickBall(p, Ball.Pos, dir * speed, vz, 0f, 0f, KickKind.Header, receiver, Ball.Pos + dir * 12f, false, false, Ball.Z);
            Ball.HandlingForbiddenFor = null; // headers back to the keeper may be handled
        }

        // ---- Goalkeeper handling -----------------------------------------------------------------------

        private void GoalkeeperHandle(PlayerAgent gk, bool contested)
        {
            // Only real shots (or a deflection flying at the goal frame) are saves; crosses are claimed or punched.
            bool shot = Ball.IsShot && Ball.Shooter != null && Ball.Shooter.Team != gk.Team;
            bool onFrame = !shot && Ball.LastKickKind != KickKind.Cross && Ball.Velocity.Length > 11f && BallThreatensGoal(gk.Team, out _, out _);
            if (shot || onFrame)
            {
                SaveAttempt(gk, allowCatch: true);
                return;
            }

            // Crosses, through balls, loose balls: catch, punch or (rarely) fumble.
            float crowd = 0f;
            foreach (var o in gk.Team.Opponent.Players)
            {
                if (!o.SentOff && Vec2.Distance(o.Pos, gk.Pos) < 2.5f) crowd += 1f;
            }
            float aerial = MathUtil.A(gk.Attr.AerialAbility);
            float handling = MathUtil.A(gk.Attr.Handling);
            float skill = Ball.Z > 1.3f ? 0.6f * aerial + 0.4f * handling : handling;
            float pCatch = MathUtil.Clamp(0.68f + 0.3f * skill - crowd * 0.09f - (contested ? 0.12f : 0f), 0.35f, 0.98f);
            if (Conditions.IsWet) pCatch *= 0.9f; // a wet ball slips through the gloves
            if (Ball.Z > 1.5f) StartJump(gk, Ball.Z - 1.2f);
            if (_rng.Chance(pCatch))
            {
                GoalkeeperCatch(gk);
                if (Ball.LastKickKind == KickKind.Cross || Ball.Z > 1.3f)
                {
                    gk.Team.Stats.KeeperCatches++;
                    Emit(MatchEventType.GoalkeeperClaim, gk.Team, gk, null, gk.Pos);
                }
                return;
            }
            if (Ball.Z > 1.2f || crowd > 0f)
            {
                // Punch clear (preferred under heavy pressure): high and away from goal.
                Vec2 away = (gk.Team.DirToWorld(Vec2.UnitX) * 1.2f + (gk.Pos - gk.Team.OwnGoal).Normalized).Normalized.Rotate(_rng.Range(-0.6f, 0.6f));
                SetDeflection(gk, away * _rng.Range(13f, 19f), _rng.Range(5f, 8f));
                gk.Team.Stats.KeeperPunches++;
                Emit(MatchEventType.GoalkeeperClaim, gk.Team, gk, null, gk.Pos, 1f);
            }
            else
            {
                Deflect(gk, 0.3f, 0.8f);
            }
            gk.TouchCooldown = 0.5f;
        }

        private void SaveAttempt(PlayerAgent gk, bool allowCatch)
        {
            var shooter = Ball.Shooter;
            float speed = Ball.M.Speed;
            float reflex = MathUtil.A(gk.Attr.Reflexes);
            float handling = MathUtil.A(gk.Attr.Handling);
            bool close = shooter != null && Vec2.Distance(shooter.Pos, gk.Pos) < 12f;
            float positioning = close ? MathUtil.A(gk.Attr.OneOnOne) : MathUtil.A((gk.Attr.Positioning + gk.Attr.OneOnOne) / 2);
            float skill = 0.5f * reflex + 0.2f * handling + 0.3f * positioning;
            float reachNeeded = MathUtil.Clamp(Ball.ShotGoalkeeperDistance / 3.2f, 0f, 1.4f);
            float shotDistance = shooter != null ? Vec2.Distance(shooter.Pos, gk.Team.OwnGoal) : 20f;
            float closeRange = MathUtil.Clamp01((15f - shotDistance) / 10f); // little time to react
            float difficulty = 0.42f * MathUtil.Clamp((speed - 12f) / 20f, 0f, 1.2f) + 0.55f * reachNeeded + (Ball.Z > 1.8f ? 0.12f : 0f) + 0.35f * closeRange;
            if (Ball.LastKickKind == KickKind.Header) difficulty += 0.25f; // close-range headers give little time to react
            float pSave = MathUtil.Clamp(0.98f - difficulty * 0.46f + (skill - 0.5f) * 0.35f, 0.08f, 0.93f);
            if (_penaltyKickTime >= 0f && shooter != null && SimTime - _penaltyKickTime < 1.5f)
            {
                pSave = _penaltyGuessCorrect ? MathUtil.Clamp(0.36f + 0.3f * (skill - 0.5f) - reachNeeded * 0.15f, 0.15f, 0.7f) : 0.05f;
            }

            if (!_rng.Chance(pSave))
            {
                gk.TouchCooldown = 1f; // beaten
                return;
            }

            if (shooter != null && Ball.IsShot)
            {
                if (!_shotCountedOnTarget) shooter.Team.Stats.ShotsOnTarget++;
                _shotCountedOnTarget = true;
                gk.Team.Stats.Saves++;
                gk.Saves++;
            }
            Vec2 savePos = Ball.Pos;
            if (Ball.Z > 1.6f || reachNeeded > 0.5f) StartJump(gk, 0.35f);

            float pCatch = allowCatch ? MathUtil.Clamp(0.25f + 0.6f * handling - (speed - 15f) * 0.025f - difficulty * 0.3f, 0.05f, 0.85f) : 0f;
            if (Conditions.IsWet) pCatch *= 0.85f;
            if (_rng.Chance(pCatch))
            {
                gk.Team.Stats.KeeperCatches++;
                Emit(MatchEventType.Save, gk.Team, gk, shooter, savePos, 0f, WhistleKind.Short, 0);
                GoalkeeperCatch(gk);
                return;
            }
            gk.Team.Stats.KeeperParries++;
            _diagParryAt[gk.Team.Index] = SimTime;
            // Parry: tipped over / round the post for a corner, or pushed away (sometimes into danger).
            Vec2 goal = gk.Team.OwnGoal;
            Vec2 outward = (Ball.Pos - goal);
            Vec2 lateral = new Vec2(0f, outward.Y >= 0f ? 1f : -1f);
            Vec2 forward = (gk.Team.DirToWorld(Vec2.UnitX));
            Ball.IsShot = false;
            if (_rng.Chance(0.7f))
            {
                // Over the bar or round the post: aim outside the frame so it can never trickle in.
                bool overBar = Ball.Z > 1.4f;
                float aimY = overBar ? MathUtil.Clamp(Ball.Pos.Y, -2.5f, 2.5f) : lateral.Y * (Pitch.GoalHalfWidth + _rng.Range(1.2f, 3f));
                Vec2 aim = new Vec2(Pitch.HalfLength * gk.Team.OwnGoalSign, aimY);
                Vec2 behind = (aim - Ball.Pos);
                float dist = MathF.Max(0.5f, behind.Length);
                float spd = MathF.Max(5f, speed * _rng.Range(0.3f, 0.5f));
                float t = dist / spd;
                // Over the bar: enough lift to clear 2.6 m by the line.
                float vz = overBar ? (2.9f - Ball.Z + 0.5f * Config.Gravity * t * t) / t : _rng.Range(0.3f, 1.5f);
                SetDeflection(gk, behind / dist * spd, MathUtil.Clamp(vz, 0f, 12f));
                Emit(MatchEventType.Save, gk.Team, gk, shooter, savePos, 0f, WhistleKind.Short, overBar ? 1 : 2);
            }
            else
            {
                Emit(MatchEventType.Save, gk.Team, gk, shooter, savePos, 0f, WhistleKind.Short, 3);
                // Pushed away to the side, low, out of the danger area - a keeper never palms it back into the middle.
                Vec2 dir = (lateral * _rng.Range(1.0f, 1.6f) + forward * _rng.Range(0.1f, 0.5f)).Normalized;
                SetDeflection(gk, dir * MathF.Max(5f, speed * _rng.Range(0.3f, 0.5f)), _rng.Range(0.2f, 1.6f));
            }
            gk.TouchCooldown = 0.6f;
        }

        private void GoalkeeperCatch(PlayerAgent gk)
        {
            if (!Pitch.InPenaltyArea(gk.Pos, gk.Team.OwnGoalSign)) Violation("handlingOutsideArea");
            ResolvePendingPass(gk);
            ClearOffside();
            Ball.Owner = gk;
            gk.RunUpPhase = 0;
            Ball.InHands = true;
            Ball.ClearFlight();
            Ball.KickerMustWait = false;
            Ball.HandlingForbiddenFor = null;
            Ball.LastToucher = gk;
            Ball.LastTouchTeam = gk.Team;
            Ball.Stop();
            PossessionTeam = gk.Team;
            _goalkeeperHoldTimer = 0f;
            _penaltyKickTime = -1f;
            gk.DecisionTimer = _rng.Range(1.4f, 3.2f) * (1.25f - 0.5f * gk.Team.Tactics.Tempo);
            gk.Task = PlayerTask.GoalkeeperHold;
            gk.HasPendingPlan = false;
            AssignBallDuties();
        }

        // ---- Tackles -----------------------------------------------------------------------------------

        /// <summary>How readily a team goes into challenges (the Tackling instruction).</summary>
        private static float TackleStyleFactor(TeamAgent t) =>
            t.Tactics.Tackling == TacklingStyle.StayOnFeet ? 0.7f : t.Tactics.Tackling == TacklingStyle.GetStuckIn ? 1.35f : 1f;

        /// <summary>How clean a team's challenges are: a gentle team pulls out of the risky ones, an aggressive one goes through.</summary>
        private static float FoulStyleFactor(TeamAgent t) =>
            t.Tactics.Tackling == TacklingStyle.StayOnFeet ? 0.72f : t.Tactics.Tackling == TacklingStyle.GetStuckIn ? 1.2f : 1f;

        /// <summary>How readily a team goes to ground (slide tackles).</summary>
        private static float SlideStyleFactor(TeamAgent t) =>
            t.Tactics.Tackling == TacklingStyle.StayOnFeet ? 0.25f : t.Tactics.Tackling == TacklingStyle.GetStuckIn ? 1.8f : 1f;

        /// <summary>
        /// Defending the ball carrier. Close up (within a stride) a defender jockeys and picks his moment for a standing
        /// tackle - most often when the carrier is exposed: a heavy first touch, his back to goal, or turning. A carrier
        /// getting away from a chasing defender may instead be met with a slide tackle from the side or behind.
        /// </summary>
        private void ResolveTackles(float dt)
        {
            var carrier = Ball.Owner;
            if (carrier == null || Ball.InHands || carrier.SentOff) return;
            var defenders = carrier.Team.Opponent;
            float carrierSpeed = carrier.Vel.Length;
            foreach (var d in defenders.Players)
            {
                if (d.SentOff || d.IsDown || d.TackleCooldown > 0f || d.StunTimer > 0f) continue;
                float dist = Vec2.Distance(d.Pos, carrier.Pos);
                if (dist > 2.8f) continue;
                if (d.IsGoalkeeper && !Pitch.InPenaltyArea(d.Pos, d.Team.OwnGoalSign)) continue;
                float aggression = MathUtil.A(d.Attr.Aggression);
                bool inBox = Pitch.InPenaltyArea(carrier.Pos, d.Team.OwnGoalSign);
                float toGoal = Vec2.Distance(carrier.Pos, d.Team.OwnGoal);
                bool breakaway = IsDenyingObviousGoalScoringOpportunity(carrier, d);

                // A standing tackle or a take-on duel needs the ball within reach of the defender's leg, not just the man.
                float toBall = Vec2.Distance(d.Pos, Ball.Pos);
                if (dist > 1.45f || toBall > 1.1f)
                {
                    if (!d.IsGoalkeeper && dist > 1.5f && dist <= 2.8f && carrierSpeed > 3f && TrySlide(d, carrier, dist, inBox, breakaway, dt)) return;
                    continue;
                }

                // Standing tackle: defenders mostly jockey and wait for the moment the carrier is exposed.
                // (only once the ball itself is within reach, so the moment is short: a higher rate per second)
                float rate = (0.1f + 0.22f * aggression + 0.17f * defenders.Tactics.PressingIntensity) * TackleStyleFactor(defenders);
                if (d.Slot.IsCentralMidfielder) rate *= d == defenders.Anchor ? 1.45f : 1.35f; // the midfield ball-winners
                float exposed = 1f;
                if (carrier.HoldTime < 0.45f) exposed += 0.9f;                                         // still controlling the ball
                Vec2 attackDir = carrier.Team.DirToWorld(Vec2.UnitX);
                if (Vec2.Dot(carrier.FacingDir, attackDir) < -0.2f) exposed += 0.6f;                 // back to goal
                if (carrierSpeed > 1.5f && MathF.Abs(MathUtil.AngleDiff(carrier.Facing, carrier.Vel.Angle)) > 0.9f) exposed += 0.5f; // turning
                rate *= exposed;
                if (d.YellowCards > 0) rate *= 0.5f;
                if (inBox) rate *= toGoal < 13f ? 1.3f : 0.9f;          // last-ditch challenges close to goal
                else if (toGoal < 32f) rate *= 1.6f;                     // engage before the box
                if (breakaway) rate *= 0.3f;
                // Take-on duel: the carrier deliberately runs at a defender standing in his path.
                if (dist < 1.15f && carrierSpeed > 2.5f && carrier.Task == PlayerTask.OnBall && carrier.HoldTime > 0.3f)
                {
                    Vec2 toD = d.Pos - carrier.Pos;
                    if (Vec2.Dot(carrier.Vel.Normalized, toD.Normalized) > 0.6f)
                    {
                        TakeOn(carrier, d);
                        return;
                    }
                }
                if (_rng.Chance(rate * dt))
                {
                    AttemptTackle(d, carrier, exposed - 1f);
                    return;
                }
                // "Smart" tactical foul to stop a counter (design doc: only high-decision players try it).
                if (dist < 1.4f && d.YellowCards == 0 && IsStoppingPromisingAttack(carrier, d) && !breakaway)
                {
                    float smart = MathUtil.A(d.Attr.Decisions) * (0.4f + 0.6f * aggression);
                    // Breaking up counters is the holding midfielder's job above all.
                    float role = d == defenders.Anchor ? 2.5f : d.Slot.IsCentralMidfielder ? 1.6f : 1f;
                    if (smart > 0.35f && _rng.Chance(0.45f * smart * role * dt))
                    {
                        d.Team.Stats.Tackles++;
                        CommitFoul(d, carrier, true, false, carrier.Pos);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Slide tackle: a defender who cannot get in front of a carrier running away (beside or behind him) throws
        /// himself at the ball. Risky - a mistimed slide from behind is a foul and often a card - so cautious and booked
        /// players rarely go to ground, and almost never in their own box.
        /// </summary>
        private bool TrySlide(PlayerAgent d, PlayerAgent carrier, float dist, bool inBox, bool breakaway, float dt)
        {
            var team = d.Team;
            // Only when the carrier is getting away, and the defender is beside or behind him (not squared up in front).
            Vec2 run = carrier.Vel.Normalized;
            Vec2 fromCarrier = (d.Pos - carrier.Pos) / dist;
            if (Vec2.Dot(run, fromCarrier) > 0.35f) return false;
            if (Vec2.Dot(run, team.DirToWorld(-Vec2.UnitX)) < -0.3f) return false; // running away from our goal: no need
            if (d.Vel.Length < 3f) return false;                                    // slides come out of a run
            float aggression = MathUtil.A(d.Attr.Aggression);
            float rate = 0.09f * SlideStyleFactor(team) * (0.35f + 0.65f * aggression) * (0.6f + 0.4f * MathUtil.A(d.Attr.Tackling));
            if (team.Forward(carrier.Pos) < -10f) rate *= 1.5f; // danger: he must be stopped
            if (inBox) rate *= 0.2f;
            if (d.YellowCards > 0) rate *= 0.25f;
            if (breakaway) rate *= 2f;                          // last man: all or nothing
            if (!_rng.Chance(rate * dt)) return false;

            // Aim at where the ball will be when the slide arrives.
            float lunge = 0.42f;
            Vec2 aim = Ball.Pos + carrier.Vel * lunge;
            Vec2 to = aim - d.Pos;
            float len = to.Length;
            if (len < 0.5f) return false;
            d.SlideDir = to / len;
            d.SlideSpeed = MathUtil.Clamp((len + 0.4f) / lunge, 5.5f, 8.5f);
            d.SlideTimer = lunge;
            if (Conditions.IsWet) { d.SlideSpeed *= 1.12f; d.SlideTimer *= 1.15f; } // a wet surface carries the slide further
            d.SlideVictim = carrier;
            d.SlideResolved = false;
            d.TackleCooldown = 3f;
            d.StunTimer = MathF.Max(d.StunTimer, lunge);
            d.Facing = d.SlideDir.Angle;
            team.Stats.Tackles++;
            team.Stats.SlideTackles++;
            _diagSlideAt[team.Index] = SimTime;
            d.Tackles++;
            d.SlideTackles++;
            if (CollectDiagnostics) Count("tackle.slide");
            return true;
        }

        /// <summary>Slides in progress: contact with the ball (won), with the man (foul) or with nothing (missed).</summary>
        private void UpdateSlides()
        {
            foreach (var d in AllPlayers)
            {
                if (d.SlideTimer <= 0f || d.SlideResolved || d.SentOff) continue;
                var victim = d.SlideVictim;
                float toBall = Vec2.Distance(d.Pos + d.SlideDir * 0.6f, Ball.Pos); // the leading foot
                bool ballLow = Ball.Z < 0.6f && !Ball.InHands;
                if (ballLow && toBall < 0.6f && (Ball.Owner == null || Ball.Owner.Team != d.Team))
                {
                    d.SlideResolved = true;
                    ResolveSlideContact(d, Ball.Owner);
                    return;
                }
                if (victim != null && !victim.SentOff && Vec2.Distance(d.Pos, victim.Pos) < 0.55f)
                {
                    // Took the man, not the ball.
                    d.SlideResolved = true;
                    bool fromBehind = MathF.Abs(MathUtil.AngleDiff(victim.Facing, (d.Pos - victim.Pos).Angle)) > 2.1f;
                    if (CollectDiagnostics) Count("tackle.slide.foul");
                    _slideFoul = true;
                    CommitFoul(d, victim, fromBehind, false, victim.Pos);
                    _slideFoul = false;
                    return;
                }
            }
        }

        private void ResolveSlideContact(PlayerAgent d, PlayerAgent carrier)
        {
            var team = d.Team;
            if (carrier == null)
            {
                // A loose ball: swept away.
                SetDeflection(d, d.SlideDir.Rotate(_rng.Range(-0.5f, 0.5f)) * _rng.Range(6f, 11f), _rng.Range(0f, 1.2f));
                return;
            }
            bool fromBehind = MathF.Abs(MathUtil.AngleDiff(carrier.Facing, (d.Pos - carrier.Pos).Angle)) > 2.1f;
            var ca = carrier.Attr;
            var da = d.Attr;
            float attack = (0.45f * ca.Dribbling + 0.25f * ca.Acceleration + 0.2f * ca.BallControl + 0.1f * ca.Composure) * carrier.FatigueFactor * BallControlFactor(carrier.Pos);
            float defend = (0.6f * da.Tackling + 0.2f * da.Decisions + 0.2f * da.Acceleration) * d.FatigueFactor;
            float pFoul = 0.14f + 0.14f * MathUtil.A(da.Aggression) + (fromBehind ? 0.3f : 0.05f) - 0.1f * MathUtil.A(da.Tackling)
                          + (1f - d.Energy) * 0.08f;
            pFoul = MathUtil.Clamp(pFoul * FoulStyleFactor(d.Team), 0.05f, 0.7f);
            float pWin = MathUtil.Clamp(0.62f + (defend - attack) / 100f * 0.9f - (fromBehind ? 0.12f : 0f), 0.2f, 0.9f);
            if (_rng.Chance(pFoul))
            {
                if (CollectDiagnostics) Count("tackle.slide.foul");
                _slideFoul = true;
                CommitFoul(d, carrier, fromBehind, true, carrier.Pos);
                _slideFoul = false;
                return;
            }
            if (_rng.Chance(pWin))
            {
                team.Stats.TacklesWon++;
                team.Stats.SlideTacklesWon++;
                Emit(MatchEventType.Tackle, team, d, carrier, carrier.Pos, 0f, WhistleKind.Short, 1);
                ClearOffside();
                Ball.LastToucher = carrier;
                // The ball is knocked on in the slide's direction (near the touchline usually out of play).
                Vec2 dir = d.SlideDir.Rotate(_rng.Range(-0.7f, 0.7f));
                if (MathF.Abs(carrier.Pos.Y) > Pitch.HalfWidth - 6f && _rng.Chance(0.5f))
                    dir = new Vec2(dir.X * 0.4f, MathF.Sign(carrier.Pos.Y)).Normalized;
                SetDeflection(d, dir * _rng.Range(5f, 10f), _rng.Range(0f, 1f));
                carrier.StunTimer = MathF.Max(carrier.StunTimer, 0.6f);
                MaybeInjure(carrier, 0.015f, 0.003f, "slide");
                return;
            }
            // Skipped over the challenge: the carrier rides it and goes on.
            if (CollectDiagnostics) Count("tackle.slide.beaten");
            Emit(MatchEventType.SlideTackleMissed, team, d, carrier, d.Pos);
            carrier.Vel *= 0.85f;
        }

        /// <summary>Called when a slide ends: the player lies on the ground for a moment before getting up.</summary>
        private void EndSlide(PlayerAgent p)
        {
            p.SlideTimer = 0f;
            p.SlideVictim = null;
            p.GroundTimer = 0.75f + 0.5f * (1f - MathUtil.A(p.Attr.Acceleration));
            p.StunTimer = MathF.Max(p.StunTimer, p.GroundTimer);
        }

        /// <summary>
        /// Body play in a race for a loose ball: two opponents running for it side by side lean into each other
        /// (a fair shoulder-to-shoulder challenge, Law 12, while the ball is within playing distance). The stronger,
        /// heavier and more determined player holds his line and gets there first; the other is knocked off stride.
        /// </summary>
        private void ResolveShoulderDuels()
        {
            if (Ball.Owner != null || Ball.Z > 1.2f) return;
            foreach (var a in Home.Players)
            {
                if (a.SentOff || a.IsDown || a.IsGoalkeeper || a.DuelCooldown > 0f) continue;
                if (a.Task != PlayerTask.Chase && a.Task != PlayerTask.Receive && a.Task != PlayerTask.Shield) continue;
                foreach (var b in Away.Players)
                {
                    if (b.SentOff || b.IsDown || b.IsGoalkeeper || b.DuelCooldown > 0f) continue;
                    if (b.Task != PlayerTask.Chase && b.Task != PlayerTask.Receive && b.Task != PlayerTask.Shield) continue;
                    if (Vec2.DistanceSq(a.Pos, b.Pos) > 1.0f * 1.0f) continue;
                    if (Vec2.Distance(a.Pos, Ball.Pos) > 7f || Vec2.Distance(b.Pos, Ball.Pos) > 7f) continue;
                    // only a real race: both running, roughly the same way
                    if (a.Vel.Length < 2.5f && a.Task != PlayerTask.Shield) continue;
                    if (b.Vel.Length < 2.5f && b.Task != PlayerTask.Shield) continue;
                    ShoulderDuel(a, b);
                    break;
                }
            }
        }

        private static float BodyStrength(PlayerAgent p) =>
            0.5f * MathUtil.A(p.Attr.PhysicalPower) + 0.2f * MathUtil.A(p.Attr.Aggression) + 0.15f * MathUtil.A(p.Attr.Acceleration)
            + MathUtil.Clamp((p.Profile.HeightCm - 165) / 35f, 0f, 1f) * 0.15f + (p.Task == PlayerTask.Shield ? 0.12f : 0f);

        private void ShoulderDuel(PlayerAgent a, PlayerAgent b)
        {
            a.DuelCooldown = b.DuelCooldown = 2f;
            float diff = (BodyStrength(a) - BodyStrength(b)) * a.FatigueFactor / MathF.Max(0.5f, b.FatigueFactor);
            bool aWins = _rng.Chance(MathUtil.Clamp(0.5f + diff * 1.6f, 0.12f, 0.88f));
            var winner = aWins ? a : b;
            var loser = aWins ? b : a;
            if (CollectDiagnostics) Count("duel.shoulder");
            // An over-eager challenge is a push (careless) - rarely.
            if (winner.Task != PlayerTask.Shield && _rng.Chance(0.01f + 0.03f * MathUtil.A(winner.Attr.Aggression)))
            {
                if (CollectDiagnostics) Count("duel.shoulder.foul");
                CommitFoul(winner, loser, false, true, loser.Pos);
                return;
            }
            winner.Team.Stats.ShoulderDuelsWon++;
            Vec2 away = loser.Pos - winner.Pos;
            away = away.LengthSq > 1e-4f ? away.Normalized : loser.Vel.Perp.Normalized;
            loser.Pos = Pitch.ClampToField(loser.Pos + away * 0.35f, 1.5f);
            loser.Vel = loser.Vel * 0.55f + away * 0.8f;
            loser.StunTimer = MathF.Max(loser.StunTimer, 0.3f);
            Emit(MatchEventType.ShoulderDuel, winner.Team, winner, loser, winner.Pos);
            MaybeInjure(loser, 0.004f, 0.0006f, "duel");
        }

        /// <summary>1v1 dribble: beat the man (he is left behind) or lose it (tackle / foul).</summary>
        private void TakeOn(PlayerAgent carrier, PlayerAgent d)
        {
            if (CollectDiagnostics) Count("takeon");
            carrier.Team.Stats.Dribbles++;
            var ca = carrier.Attr;
            var da = d.Attr;
            float attack = (0.5f * ca.Dribbling + 0.2f * ca.BallControl + 0.2f * ca.Acceleration + 0.1f * ca.Composure) * carrier.FatigueFactor * BallControlFactor(carrier.Pos);
            float defend = (0.45f * da.Tackling + 0.3f * da.Positioning + 0.15f * da.Acceleration + 0.1f * da.PhysicalPower) * d.FatigueFactor;
            float pBeat = MathUtil.Clamp(0.38f + (attack - defend) / 100f * 1.1f, 0.1f, 0.75f);
            d.TackleCooldown = 1.2f;
            if (_rng.Chance(pBeat))
            {
                if (CollectDiagnostics) Count("takeon.won");
                carrier.Team.Stats.DribblesWon++;
                d.StunTimer = 0.5f + 0.35f * (1f - MathUtil.A(da.Positioning));
                // Burst past: nudge the carrier round the defender.
                Vec2 side = (carrier.Vel.Normalized.Perp * (Vec2.Cross(carrier.Vel, d.Pos - carrier.Pos) > 0f ? -1f : 1f));
                carrier.Vel += side * 1.5f;
                return;
            }
            AttemptTackle(d, carrier);
        }

        /// <param name="exposed">How exposed the carrier is (0 = shielding the ball .. ~2): well-timed tackles win it more often.</param>
        private void AttemptTackle(PlayerAgent d, PlayerAgent carrier, float exposed = 0f)
        {
            d.Team.Stats.Tackles++;
            d.Team.Stats.StandingTackles++;
            d.Tackles++;
            if (CollectDiagnostics) Count("tackle.standing");
            Vec2 toDefender = d.Pos - carrier.Pos;
            float behindAngle = MathF.Abs(MathUtil.AngleDiff(carrier.Facing, toDefender.Angle));
            bool fromBehind = behindAngle > 2.1f; // defender coming from behind the carrier

            var ca = carrier.Attr;
            var da = d.Attr;
            float attack = (0.45f * ca.Dribbling + 0.3f * ca.BallControl + 0.15f * ca.Acceleration + 0.1f * ca.PhysicalPower) * carrier.FatigueFactor * BallControlFactor(carrier.Pos);
            float defend = (0.55f * da.Tackling + 0.2f * da.Positioning + 0.15f * da.PhysicalPower + 0.1f * da.Aggression) * d.FatigueFactor;
            if (d.IsGoalkeeper) defend = (0.6f * da.OneOnOne + 0.4f * da.Reflexes) * d.FatigueFactor;
            float pWin = MathUtil.Clamp(0.58f + (defend - attack) / 100f * 0.9f - (fromBehind ? 0.15f : 0f) + 0.1f * exposed, 0.12f, 0.88f);
            float pFoul = 0.22f + 0.15f * MathUtil.A(da.Aggression) + (fromBehind ? 0.18f : 0f) - 0.06f * MathUtil.A(da.Tackling)
                          + (1f - d.Energy) * 0.08f;
            if (d == d.Team.Anchor) pFoul += 0.025f; // the holding midfielder's challenges are the most robust
            pFoul *= FoulStyleFactor(d.Team);
            if (d.YellowCards > 0) pFoul *= 0.45f;
            if (IsDenyingObviousGoalScoringOpportunity(carrier, d)) pFoul *= 0.4f; // tries to stay clean
            if (Pitch.InPenaltyArea(carrier.Pos, d.Team.OwnGoalSign)) pFoul *= 0.25f; // careful in the box
            pFoul = MathUtil.Clamp(pFoul, 0.02f, 0.5f);

            if (_rng.Chance(pFoul))
            {
                CommitFoul(d, carrier, fromBehind, true, carrier.Pos);
                return;
            }
            if (_rng.Chance(pWin))
            {
                d.Team.Stats.TacklesWon++;
                Emit(MatchEventType.Tackle, d.Team, d, carrier, carrier.Pos, 0f, WhistleKind.Short, 0);
                ClearOffside();
                if (_rng.Chance(0.5f))
                {
                    Ball.LastToucher = carrier; // the carrier had the last controlled touch
                    GainControl(d, 2f);
                }
                else
                {
                    Vec2 dir = (carrier.Pos - d.Pos).Normalized.Rotate(_rng.Range(-1.2f, 1.2f));
                    // Near the touchline a poke usually ends up out of play.
                    if (MathF.Abs(carrier.Pos.Y) > Pitch.HalfWidth - 4f && _rng.Chance(0.55f))
                        dir = new Vec2(dir.X * 0.5f, MathF.Sign(carrier.Pos.Y)).Normalized;
                    SetDeflection(d, dir * _rng.Range(3f, 7f), _rng.Range(0f, 1.5f));
                }
                carrier.StunTimer = 0.5f;
                d.TackleCooldown = 1f;
                MaybeInjure(carrier, 0.006f, 0.0012f, "tackle");
            }
            else
            {
                d.StunTimer = 0.5f + 0.5f * (1f - MathUtil.A(da.Tackling));
                d.TackleCooldown = 1.5f;
            }
        }
    }
}

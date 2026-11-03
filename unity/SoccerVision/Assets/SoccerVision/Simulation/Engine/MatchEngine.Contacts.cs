using System;
using System.Collections.Generic;

namespace SoccerVision.Simulation
{
    internal enum ContactKind { None, Feet, Head, Body, Hands }

    /// <summary>Ball contacts: control, interceptions, blocks, headers and aerial duels.</summary>
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

        private float _dummyRolledFor = -1f;

        /// <summary>
        /// A clever receiver with a marker on him and a team-mate free further along the line of the pass lets the ball run
        /// between his legs: the marker is fooled, the pass reaches the man behind. Decisions and creativity decide.
        /// </summary>
        private bool TryDummy(PlayerAgent p)
        {
            var team = p.Team;
            Vec2 v = Ball.Velocity;
            float speed = v.Length;
            if (speed < 7f || p.IsGoalkeeper) return false;
            Vec2 dir = v / speed;
            PlayerAgent marker = null;
            float md = 3f;
            foreach (var o in team.Opponent.Players)
            {
                if (o.SentOff || o.IsGoalkeeper) continue;
                float d = Vec2.Distance(o.Pos, p.Pos);
                if (d < md) { md = d; marker = o; }
            }
            if (marker == null) return false;
            PlayerAgent mate = null;
            foreach (var q in team.Players)
            {
                if (q == p || q.SentOff || q.IsGoalkeeper) continue;
                Vec2 rel = q.Pos - p.Pos;
                float along = Vec2.Dot(rel, dir), off = MathF.Abs(Vec2.Cross(dir, rel));
                if (along < 3f || along > 20f || off > 3.5f + 0.15f * along) continue;
                if (NearestOpponentDistance(team, q.Pos) < 2.5f) continue;
                if (team.Forward(q.Pos) > MathF.Max(MathF.Max(SecondLastDefenderForward(team), team.Forward(Ball.Pos)), 0f)) continue; // (offside)
                mate = q;
                break;
            }
            if (mate == null) return false;
            float clever = MathUtil.A((p.Attr.Decisions + p.Attr.Creativity) / 2);
            bool showboat = p.Has(PlayerTrait.Showboat);
            if (showboat) clever = MathF.Min(1f, clever + 0.15f);
            if (clever < 0.45f || !_rng.Chance((0.6f * (clever - 0.45f) / 0.55f + 0.08f) * (showboat ? 1.5f : 1f))) return false;
            p.TouchCooldown = 0.7f;
            marker.StunTimer = MathF.Max(marker.StunTimer, 0.5f);
            Ball.IntendedReceiver = mate;
            Emit(MatchEventType.Skill, team, p, mate, p.Pos, 0f, WhistleKind.Short, 3);
            if (CollectDiagnostics) Count("skill.dummy");
            return true;
        }

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

                // The dummy: the receiver opens his legs and lets the pass run on to a team-mate behind him.
                if (Ball.IntendedReceiver == p && !Ball.IsShot && z < 0.4f && _dummyRolledFor != Ball.LastKickTime)
                {
                    _dummyRolledFor = Ball.LastKickTime;
                    if (TryDummy(p)) continue;
                }
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
                        // (positioning is a defending skill; going for the ball in possession it is reading the game)
                        weight = 0.6f + (p.Attr.BallControl + (p.Team == PossessionTeam ? p.Attr.Decisions : p.Attr.Positioning) + p.Attr.PhysicalPower) / 200f;
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
                    float pFoul = (0.025f + 0.04f * MathUtil.A(other.Player.Attr.Aggression)) * PlayerAgent.Scale(other.Player.Orders.Aggression, 0.6f, 1.4f);
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
            // (and a half-volley: the ball just bouncing up off the turf, Z low but rising)
            if (!p.IsGoalkeeper && (Ball.Z > 0.3f || (Ball.Z > 0.12f && Ball.M.Vz > 1f)) && !(Ball.IsShot && opponentTouch) && TryVolley(p, opponentTouch)) return;

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
            if (p.NutmegChaseUntil > SimTime) { p.Team.Stats.NutmegsWon++; p.NutmegChaseUntil = -1f; if (CollectDiagnostics) Count("skill.nutmegWon"); }
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
            if (changeOfPossession)
            {
                p.Team.LastAttackSeconds = SimTime;
                // (won back inside the counter-press window, near where it was lost)
                if (SimTime - p.Team.LostBallAt < CounterPressWindow(p.Team) && Vec2.Distance(Ball.Pos, p.Team.LostBallSpot) < 25f) p.Team.Stats.CounterPressWins++;
                p.Team.Opponent.LostBallAt = SimTime;
                p.Team.Opponent.LostBallSpot = Ball.Pos;
            }
            SnapshotOffside(p, false);
            _penaltyKickTime = -1f;
            float tempo = TempoNow(p.Team);
            p.DecisionTimer = MathUtil.Clamp(0.1f + 0.3f * (1f - MathUtil.A(p.Attr.BallControl)) + relSpeed * 0.012f - 0.08f * (tempo - 0.5f), 0.08f, 0.6f);
            // First time: a pass into the box at a good angle - a good finisher has already set his body and opened his
            // foot, and he hits it now, before the keeper and the defenders can close him down (no touch to control it).
            if (!changeOfPossession && !p.IsGoalkeeper && Ball.LastPasser != null && Ball.LastPasser != p && Ball.LastPasser.Team == p.Team && SimTime - Ball.LastPassTime < 3f)
            {
                Vec2 goal = p.Team.AttackGoal;
                float dg = Vec2.Distance(Ball.Pos, goal);
                if (dg < 17f && p.Team.Forward(Ball.Pos) > 36f && MathF.Abs(p.Team.Lateral(Ball.Pos)) < 13f)
                {
                    float xg = EstimateShotXG(p, Ball.Pos);
                    float ready = 0.2f + 0.45f * MathUtil.A(p.Attr.Finishing) + 0.15f * MathUtil.A(p.Attr.Composure);
                    if (p.Has(PlayerTrait.Unselfish)) ready *= 0.6f; // (he looks for the pass first)
                    if (xg > 0.09f && _rng.Chance(ready))
                    {
                        p.FirstTimeUntil = SimTime + 0.45f;
                        p.DecisionTimer = 0.04f;
                        if (CollectDiagnostics) Count("shot.firstTime.set");
                    }
                }
            }
            p.OneTwoUntil = -1f; // (the return ball has arrived, or the run is over)
            p.ThirdManUntil = -1f;
            p.RunCheckUntil = -1f;
            // Counter-attack: winning the ball in our own half starts the break.
            if (changeOfPossession && p.Team.Tactics.Special == SpecialTactic.CounterAttack && p.Team.Forward(Ball.Pos) < 12f) { p.Team.CounterUntil = SimTime + 7f; if (CollectDiagnostics) Count("counter.start"); }
            // A team-mate is on a give-and-go run past me: the wall pass is played first time.
            foreach (var r in p.Team.Players)
                if ((r.OneTwoWith == p && r.OneTwoUntil > SimTime) || (r.ThirdManVia == p && r.ThirdManUntil > SimTime)) { p.DecisionTimer = MathF.Min(p.DecisionTimer, 0.12f); break; }
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
                if (CollectDiagnostics && _lastShotOpenGoal) Count("openGoal.blocked");
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
            CommitFoul(p, victim, false, false, p.Pos, noContactNeeded: true); // (a handball is not a contact between players)
        }

        /// <summary>
        /// The last man: no outfield defender is between him and the goal (only the keeper to beat). Such a player never
        /// heads or passes the ball backwards - he goes at goal.
        /// </summary>
        private bool IsLastMan(PlayerAgent p)
        {
            var team = p.Team;
            float f = team.Forward(p.Pos);
            foreach (var o in team.Opponent.Players)
            {
                if (o.SentOff || o.IsGoalkeeper) continue;
                if (team.Forward(o.Pos) > f - 0.5f && MathF.Abs(team.Lateral(o.Pos) - team.Lateral(p.Pos)) < 16f) return false;
            }
            return true;
        }

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

            // The bicycle kick: a ball dropping at chest-to-head height in the box, his back to goal - only a technical
            // player tries it, and rarely. He goes over backwards and lands on his back.
            if (distGoal < 16f && f > 34f && Ball.Z > 1.2f && Ball.Z < 2.2f && !p.IsGoalkeeper)
            {
                float tech = MathUtil.A((p.Attr.BallControl + p.Attr.Finishing + p.Attr.Acceleration + p.Attr.Jumping) / 4);
                Vec2 toGoal = (team.AttackGoal - Ball.Pos).Normalized;
                if (tech > 0.6f && Vec2.Dot(p.FacingDir, toGoal) < 0.1f && _rng.Chance(0.3f * (tech - 0.6f) / 0.4f))
                {
                    float bxg = EstimateShotXG(p, Ball.Pos) * 0.7f;
                    if (CollectDiagnostics) Count("shot.bicycle");
                    ExecuteShot(p, false, false, bxg, bicycle: true);
                    p.GroundTimer = MathF.Max(p.GroundTimer, 1.1f);
                    p.StunTimer = MathF.Max(p.StunTimer, 1.1f);
                    return;
                }
            }
            // The last man with only the keeper to beat never heads it back: at goal when close, otherwise down into his own path.
            if (!(distGoal < 15f && f > 36f) && f > 10f && distGoal < 32f && IsLastMan(p))
            {
                if (distGoal < 20f)
                {
                    Emit(MatchEventType.Header, team, p, null, Ball.Pos);
                    ExecuteShot(p, true, false, EstimateShotXG(p, Ball.Pos, header: true));
                    return;
                }
                Vec2 path = (team.AttackGoal - Ball.Pos).Normalized;
                Emit(MatchEventType.Header, team, p, null, Ball.Pos);
                if (CollectDiagnostics) Count("header.lastman.ownpath");
                KickBall(p, Ball.Pos, path * 5.5f, 0.8f, 0f, 0f, KickKind.Header, null, Ball.Pos + path * 6f, false, false, Ball.Z);
                Ball.HandlingForbiddenFor = null;
                return;
            }
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
    }
}

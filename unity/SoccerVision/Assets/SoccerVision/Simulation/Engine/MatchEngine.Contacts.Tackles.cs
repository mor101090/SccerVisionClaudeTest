using System;
using System.Collections.Generic;
using System.Linq;

namespace SoccerVision.Simulation
{
    /// <summary>Duels for the ball: standing and sliding tackles, shoulder duels, take-ons.</summary>
    public sealed partial class MatchEngine
    {
        /// <summary>How readily a team goes into challenges (the Tackling instruction).</summary>
        private static float TackleStyleFactor(TeamAgent t) =>
            t.Tactics.Tackling == TacklingStyle.StayOnFeet ? 0.7f : t.Tactics.Tackling == TacklingStyle.GetStuckIn ? 1.35f : 1f;

        /// <summary>How readily this player goes into a tackle: his aggression instruction and his role (a destroyer lives for it).</summary>
        private static float PlayerTackleFactor(PlayerAgent d)
        {
            float f = PlayerAgent.Scale(d.Orders.Aggression, 0.6f, 1.4f);
            if (d.Has(PlayerTrait.HardTackler)) f *= 1.3f;
            switch (d.PlayRole)
            {
                case PlayerRole.Destroyer: f *= 1.25f; break;
                case PlayerRole.Stopper: f *= 1.15f; break;
                case PlayerRole.DeepLyingPlaymaker: case PlayerRole.TempoSetter: case PlayerRole.Regista: f *= 0.85f; break;
                case PlayerRole.Trequartista: f *= 0.7f; break;
            }
            return f;
        }

        /// <summary>How clean a team's challenges are: a gentle team pulls out of the risky ones, an aggressive one goes through.</summary>
        private float FoulStyleFactor(TeamAgent t) =>
            (t.Tactics.Tackling == TacklingStyle.StayOnFeet ? 0.72f : t.Tactics.Tackling == TacklingStyle.GetStuckIn ? 1.2f : 1f) * RefereeBias(t, 0.08f, 0.05f);

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
                float rate = (0.1f + 0.22f * aggression + 0.17f * defenders.Eff.PressingIntensity) * TackleStyleFactor(defenders) * PlayerTackleFactor(d);
                if (d.Slot.IsCentralMidfielder) rate *= d == defenders.Anchor ? 1.45f : 1.35f; // the midfield ball-winners
                float exposed = 1f;
                if (carrier.HoldTime < 0.45f) exposed += 0.9f;                                         // still controlling the ball
                Vec2 attackDir = carrier.Team.DirToWorld(Vec2.UnitX);
                if (Vec2.Dot(carrier.FacingDir, attackDir) < -0.2f) exposed += 0.6f;                 // back to goal
                if (carrierSpeed > 1.5f && MathF.Abs(MathUtil.AngleDiff(carrier.Facing, carrier.Vel.Angle)) > 0.9f) exposed += 0.5f; // turning
                rate *= exposed;
                if (d.YellowCards > 0) rate *= 0.5f;
                // In the box a defender jockeys and stays on his feet - a mistimed challenge there is a penalty (a hard man
                // included: he picks his moment outside the box)
                if (inBox) rate *= (toGoal < 13f ? 0.4f : 0.3f) / (d.Has(PlayerTrait.HardTackler) ? 1.3f : 1f);
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
            float rate = 0.09f * (d.Has(PlayerTrait.HardTackler) ? 1.6f : 1f) * SlideStyleFactor(team) * PlayerAgent.Scale(d.Orders.Aggression, 0.35f, 1.6f) * (0.35f + 0.65f * aggression) * (0.6f + 0.4f * MathUtil.A(d.Attr.Tackling));
            if (team.Forward(carrier.Pos) < -10f) rate *= 1.5f; // danger: he must be stopped
            if (inBox) rate *= 0.1f;
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
            // (a creative player's unpredictable moves - a feint, a drop of the shoulder - help him past his man)
            float attack = (0.4f * ca.Dribbling + 0.25f * ca.Acceleration + 0.15f * ca.BallControl + 0.1f * ca.Composure + 0.1f * ca.Creativity) * carrier.FatigueFactor * BallControlFactor(carrier.Pos);
            float defend = (0.6f * da.Tackling + 0.2f * da.Decisions + 0.2f * da.Acceleration) * d.FatigueFactor;
            float pFoul = 0.14f + 0.14f * MathUtil.A(da.Aggression) + (fromBehind ? 0.3f : 0.05f) - 0.1f * MathUtil.A(da.Tackling)
                          + (1f - d.Energy) * 0.08f;
            pFoul = MathUtil.Clamp(pFoul * (d.Has(PlayerTrait.HardTackler) && !Pitch.InPenaltyArea(carrier.Pos, d.Team.OwnGoalSign) ? 1.15f : 1f) * FoulStyleFactor(d.Team) * PlayerAgent.Scale(d.Orders.Aggression, 0.65f, 1.35f), 0.05f, 0.7f);
            pFoul *= BallFirstFoulFactor(d, carrier);
            if (!Pitch.InPenaltyArea(carrier.Pos, d.Team.OwnGoalSign)) pFoul = MathF.Min(0.85f, pFoul * FoulRate); // (see FoulRate)
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

        private float BodyStrength(PlayerAgent p) =>
            0.5f * MathUtil.A(p.Attr.PhysicalPower) + 0.2f * MathUtil.A(p.Attr.Aggression) + 0.15f * MathUtil.A(p.Attr.Acceleration)
            + MathUtil.Clamp((p.Profile.HeightCm - 165) / 35f, 0f, 1f) * 0.15f + (p.Task == PlayerTask.Shield ? 0.12f : 0f)
            + (p.Has(PlayerTrait.BackToGoal) && Ball.Owner == p ? 0.08f : 0f); // the target man knows how to use his body

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
            carrier.Dribbles++;
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
            pFoul *= FoulStyleFactor(d.Team) * PlayerAgent.Scale(d.Orders.Aggression, 0.65f, 1.35f);
            if (d.YellowCards > 0) pFoul *= 0.45f;
            if (IsDenyingObviousGoalScoringOpportunity(carrier, d)) pFoul *= 0.4f; // tries to stay clean
            if (Pitch.InPenaltyArea(carrier.Pos, d.Team.OwnGoalSign)) pFoul *= 0.12f; // careful in the box
            pFoul *= BallFirstFoulFactor(d, carrier);
            pFoul = MathUtil.Clamp(pFoul, 0.02f, 0.5f);
            if (!Pitch.InPenaltyArea(carrier.Pos, d.Team.OwnGoalSign)) pFoul = MathF.Min(0.6f, pFoul * FoulRate); // (see FoulRate)

            if (_rng.Chance(pFoul))
            {
                CommitFoul(d, carrier, fromBehind, true, carrier.Pos);
                return;
            }
            // The man who goes down too easily near the box: the referee may buy it (a free kick, a penalty in the area), or see through
            // it and book him for simulation.
            if (carrier.Has(PlayerTrait.DrawsFouls) && Vec2.Distance(carrier.Pos, d.Team.OwnGoal) < 26f && _rng.Chance(0.07f))
            {
                if (_rng.Chance(0.55f))
                {
                    if (CollectDiagnostics) Count("dive.bought");
                    CommitFoul(d, carrier, false, true, carrier.Pos, true);
                    return;
                }
                if (CollectDiagnostics) Count("dive.booked");
                // The referee sees through it: he stops the play, books the diver (the card is shown with the play stopped) and
                // gives an indirect free kick to the other side (Law 12).
                Whistle(WhistleKind.Short);
                Ball.Owner = null;
                Ball.InHands = false;
                Ball.Stop();
                Ball.ClearFlight();
                ResolvePendingPass(null);
                ClearOffside();
                ApplySanction(new PendingSanction { Player = carrier, Yellow = true, Reason = "simulation" });
                {
                    d.Team.Stats.FreeKicks++;
                    Vec2 simSpot = Pitch.ClampToField(carrier.Pos, -0.3f);
                    BeginRestart(RestartType.IndirectFreeKick, d.Team, simSpot, null);
                    _restart.CountsAsLostTime = true;
                    _restart.WaitForWhistle = true;
                    Emit(MatchEventType.FreeKick, d.Team, _restart.Taker, carrier, simSpot, 0f, WhistleKind.Short, 0);
                }
                return;
            }
            if (_rng.Chance(pWin))
            {
                d.Team.Stats.TacklesWon++;
                AddConfidence(d, 0.08f);
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

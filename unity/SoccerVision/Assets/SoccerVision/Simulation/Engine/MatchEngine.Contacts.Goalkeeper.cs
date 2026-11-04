using System;
using System.Collections.Generic;
using System.Linq;

namespace SoccerVision.Simulation
{
    /// <summary>Ball contacts - the goalkeeper: handling, saves and catches.</summary>
    public sealed partial class MatchEngine
    {
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
            bool dominant = gk.PlayRole == PlayerRole.DominantKeeper || gk.Has(PlayerTrait.BoldKeeper); // he owns his six-yard box: claims through the crowd
            float pCatch = MathUtil.Clamp(0.68f + 0.3f * skill + (dominant ? 0.08f : 0f) - crowd * (dominant ? 0.045f : 0.09f) - (contested ? 0.12f : 0f), 0.35f, 0.98f);
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
            float skill = 0.5f * reflex + 0.2f * handling + 0.3f * positioning + (gk.PlayRole == PlayerRole.ShotStopper ? 0.05f : 0f); // (a shot-stopper lives on his line)
            float reachNeeded = MathUtil.Clamp(Ball.ShotGoalkeeperDistance / 3.2f, 0f, 1.4f);
            float shotDistance = shooter != null ? Vec2.Distance(shooter.Pos, gk.Team.OwnGoal) : 20f;
            // Little time to react from close range - unless he is right on the shooter: then he is a wall, spread in
            // front of the ball, and a shot that is not past his body hits him.
            float shooterGap = shooter != null ? Vec2.Distance(shooter.Pos, gk.Pos) : 20f;
            float smother = MathUtil.Clamp01((shooterGap - 1.2f) / 2.8f);
            float closeRange = MathUtil.Clamp01((15f - shotDistance) / 10f) * smother;
            float difficulty = 0.42f * MathUtil.Clamp((speed - 12f) / 20f, 0f, 1.2f) + 0.55f * reachNeeded + (Ball.Z > 1.8f ? 0.12f : 0f) + 0.35f * closeRange;
            if (Ball.LastKickKind == KickKind.Header) difficulty += 0.25f; // close-range headers give little time to react
            if (Ball.ShotKnuckle) difficulty += 0.22f; // a ball with no spin moves late in the air
            float pSave = MathUtil.Clamp(0.98f - difficulty * 0.44f + (skill - 0.5f) * 0.35f, 0.1f, 0.94f); // (calibrated with the open goals made honest: about 2.8 goals a match)
            if (_penaltyKickTime >= 0f && shooter != null && SimTime - _penaltyKickTime < 1.5f)
            {
                pSave = _penaltyGuessCorrect ? MathUtil.Clamp(0.36f + 0.3f * (skill - 0.5f) - reachNeeded * 0.15f, 0.15f, 0.7f) : 0.05f;
                // (the home keeper has the crowd behind him; the away keeper does not)
                pSave *= gk.Team == Home ? 1f + 0.08f * Config.HomeAdvantage : 1f - 0.06f * Config.HomeAdvantage;
            }

            if (shooter != null && shooterGap < 3.5f && reachNeeded < 0.6f && Ball.IsShot) pSave = MathF.Min(0.95f, pSave + 0.3f * (1f - shooterGap / 3.5f)); // (the block)
            // From range he sees it coming: the further out it was struck, the likelier the save (only one goal in nine comes from
            // outside the box in the top leagues; ours were one in five).
            if (Ball.IsShot && _penaltyKickTime < 0f) pSave = MathF.Min(0.94f, pSave + MathUtil.Clamp((shotDistance - 18f) * 0.012f, 0f, 0.12f));
            if (_lastShotOpenGoal && Ball.IsShot) pSave *= 0.25f; // (a keeper scrambling back to an open goal rarely gets there)
            if (_shotIsFollowUp && Ball.IsShot && _penaltyKickTime < 0f) { pSave *= FollowUpSaveFactor; if (CollectDiagnostics) Count("save.followUp"); }
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
                AddConfidence(gk, 0.15f);
                OnShotWasted(shooter, _lastShotXg);
            }
            Vec2 savePos = Ball.Pos;
            if (Ball.Z > 1.6f || reachNeeded > 0.5f) StartJump(gk, 0.35f + 0.4f * MathUtil.Clamp(Ball.Z - 1.4f, 0f, 1.2f));

            float pCatch = allowCatch ? MathUtil.Clamp(0.25f + 0.6f * handling - (speed - 15f) * 0.025f - difficulty * 0.3f, 0.05f, 0.85f) : 0f;
            if (Conditions.IsWet) pCatch *= 0.85f;
            if (_rng.Chance(pCatch))
            {
                gk.Team.Stats.KeeperCatches++;
                Emit(MatchEventType.Save, gk.Team, gk, shooter, savePos, 0f, WhistleKind.Short, 0);
                if (CollectDiagnostics && _lastShotOpenGoal) Count("openGoal.saved");
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
            // A spill: a hard shot, a slippery ball, unsure hands - he can only beat it down in front of him, into the area, and the
            // man who followed it in has a second chance (in the top leagues one goal in eight in open play is a rebound).
            float pSpill = MathUtil.Clamp(0.15f + 0.012f * (speed - 18f) - 0.15f * (handling - 0.5f) + (Conditions.IsWet ? 0.06f : 0f), 0.06f, 0.34f);
            if (_rng.Chance(pSpill))
            {
                Emit(MatchEventType.Save, gk.Team, gk, shooter, savePos, 0f, WhistleKind.Short, 3);
                Vec2 dirS = (forward + lateral * _rng.Range(-0.6f, 0.6f)).Normalized;
                SetDeflection(gk, dirS * _rng.Range(3f, 7.5f), _rng.Range(0.1f, 0.9f));
                if (CollectDiagnostics) Count("save.spill");
            }
            else if (_rng.Chance(0.7f))
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
                // Pushed away to the side, low, out of the danger area (a parry; the spill above is the other thing).
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
            gk.DecisionTimer = _rng.Range(1.4f, 3.2f) * (1.25f - 0.5f * gk.Team.Eff.Tempo) * (WastesTime(gk.Team) ? 1.4f : 1f);
            gk.Task = PlayerTask.GoalkeeperHold;
            gk.HasPendingPlan = false;
            AssignBallDuties();
        }
    }
}

using System;
using System.Collections.Generic;

namespace SoccerVision.Simulation
{
    /// <summary>How a direct free kick at goal is struck (polish round 6).</summary>
    public enum FreeKickStyle
    {
        /// <summary>Bent over the wall with the inside of the foot, into the corner the curl brings it back to (the classic).</summary>
        Curled,
        /// <summary>Struck hard with the laces, little spin, at goal through or over the wall (from further out).</summary>
        Driven,
        /// <summary>Placed: less pace, more precision - lifted over the end of the wall into the near corner.</summary>
        Placed,
        /// <summary>Low and hard along the ground, under the wall as it jumps.</summary>
        Under,
        /// <summary>No spin: the ball floats and dips late (the best strikers, from long range).</summary>
        Knuckle,
    }

    /// <summary>
    /// Direct free kicks at goal (polish round 6): the kind of strike (curled, driven, placed, under the wall, knuckleball), the wall
    /// that jumps as the ball is struck - each man on his own reaction, so a low shot can go under a jumping wall - and a keeper who
    /// stands near the middle of his goal, a little to the far side of it, where real keepers stand.
    /// </summary>
    public sealed partial class MatchEngine
    {
        /// <summary>Choose among the strikes (false: the curled strike and the knuckleball only, as before).</summary>
        public static bool FreeKickStyles = true;
        /// <summary>Chance of a low shot under the wall (a wall of three or more, 17-27 m): a rare, specialist strike. The chances of the
        /// strikes are approximations (D): StatsBomb records no free-kick technique beyond "Normal".</summary>
        public static float FreeKickUnderChance = 0.12f;
        /// <summary>Chance of a driven strike, from 24 m on (rising with distance and shooting power).</summary>
        public static float FreeKickDrivenChance = 0.35f;
        /// <summary>Chance of a placed strike, inside 24 m (rising with free-kick skill).</summary>
        public static float FreeKickPlacedChance = 0.25f;
        /// <summary>The height a curled free kick is aimed to cross the goal line at (m, drawn evenly between the two; real on target: median
        /// 1.46 m, StatsBomb PL 2015/16).</summary>
        public static float FreeKickAimLow = 0.6f, FreeKickAimHigh = 1.7f;
        /// <summary>
        /// The spread of the strike's height (x the old spread, which made a taker clear the wall almost every time): set with the aim height
        /// so that free kicks are scored as often as real ones (6.2%); blocked 17%, short of the real 34% (StatsBomb PL 2015/16) - a wider
        /// spread sends them over the bar, not into the wall.
        /// </summary>
        public static float FreeKickHeightError = 1.5f;
        /// <summary>How often the taker means to go over the wall into the near corner (else across, to the keeper's side), before he
        /// switches to the side his foot bends the ball to. With the switch below: 77% go to the wall's side (real, not blocked: 75%).</summary>
        public static float FreeKickNearSide = 0.85f;
        /// <summary>A taker whose foot bends the ball away from the near corner switches to the keeper's side this often (else he goes over
        /// the wall all the same).</summary>
        public static float FreeKickFootSwitch = 0.3f;
        /// <summary>A wall player's take-off after the strike (sim seconds): mean and spread of his reaction (approximations, D).</summary>
        public static float WallJumpDelay = 0.05f, WallJumpDelaySpread = 0.05f;
        /// <summary>How high the wall jumps (m, the centre of mass; capped by the Jumping attribute).</summary>
        public static float WallJumpHeight = 0.42f;
        /// <summary>Chance that a man in the wall does not jump (he stays on the ground, against the low shot).</summary>
        public static float WallStaysDownChance = 0.12f;
        /// <summary>The wall's end man stands on the line from the ball to this far outside the near post (m).</summary>
        public static float WallOutsidePost = 0.5f;
        /// <summary>How far the keeper stands from the middle of his goal towards the far post at a free kick with a wall (m; the median of 288 real free kicks with a wall, StatsBomb PL 2015/16 freeze frames).</summary>
        public static float KeeperFarSideOffset = 0.27f;

        private readonly List<(PlayerAgent player, float at, float height)> _wallJumps = new List<(PlayerAgent, float, float)>();
        private readonly HashSet<PlayerAgent> _wallMen = new HashSet<PlayerAgent>();
        private float _wallShotTime = -10f, _wallUnderCountedFor = -20f;
        private Vec2 _wallSpot;
        private bool _wallPassCounted, _wallLineCounted;
        private FreeKickStyle _freeKickStyle;

        private FreeKickStyle ChooseFreeKickStyle(PlayerAgent taker, float dist, int wallCount)
        {
            if (taker.Attr.FreeKicks >= 72 && dist > 23f && dist < 34f && _rng.Chance(0.22f)) return FreeKickStyle.Knuckle;
            if (!FreeKickStyles) return FreeKickStyle.Curled;
            float skill = MathUtil.A(taker.Attr.FreeKicks);
            if (wallCount >= 3 && dist >= 17f && dist <= 27f && _rng.Chance(FreeKickUnderChance * (0.6f + 0.8f * MathUtil.A(taker.Attr.Composure)))) return FreeKickStyle.Under;
            if (dist >= 24f && _rng.Chance(FreeKickDrivenChance * (0.5f + 0.5f * MathUtil.A(taker.Attr.LongShots)) * (0.6f + 0.06f * (dist - 24f)))) return FreeKickStyle.Driven;
            if (dist < 24f && _rng.Chance(FreeKickPlacedChance * (0.6f + 0.8f * skill))) return FreeKickStyle.Placed;
            return FreeKickStyle.Curled;
        }

        /// <summary>The wall jumps as the ball is struck: each man on his own reaction; now and then one stays down.</summary>
        private void ScheduleWallJumps(Restart r)
        {
            _wallJumps.Clear();
            _wallMen.Clear();
            _wallShotTime = SimTime;
            _wallSpot = Ball.Pos;
            _wallPassCounted = false;
            _wallLineCounted = false;
            foreach (var w in r.Wall)
            {
                if (w.SentOff) continue;
                _wallMen.Add(w);
                if (_rng.Chance(WallStaysDownChance)) { if (CollectDiagnostics) Count("fk.wall.staysDown"); continue; }
                float reaction = MathF.Max(0f, _rng.Gaussian(WallJumpDelay, WallJumpDelaySpread) * (1.3f - 0.6f * MathUtil.A(w.Attr.Positioning)));
                _wallJumps.Add((w, SimTime + reaction, WallJumpHeight * _rng.Range(0.85f, 1.1f)));
            }
        }

        private void StepWallJumps()
        {
            for (int i = _wallJumps.Count - 1; i >= 0; i--)
            {
                var j = _wallJumps[i];
                if (SimTime < j.at) continue;
                StartJump(j.player, j.height);
                _wallJumps.RemoveAt(i);
            }
            if (CollectDiagnostics && !_wallPassCounted && _wallMen.Count > 0 && Ball.IsShot && Vec2.Distance(Ball.Pos, _wallSpot) >= Pitch.RestartDistance)
            {
                // where the shot passes the wall: its height, and how far from the nearest man in it
                _wallPassCounted = true;
                float near = float.PositiveInfinity; PlayerAgent man = null;
                foreach (var w in _wallMen) { float d = Vec2.Distance(w.Pos, Ball.Pos); if (d < near) { near = d; man = w; } }
                Count("fk.atWall.z" + (Ball.Z < 1f ? "0-1" : Ball.Z < 1.8f ? "1-1.8" : Ball.Z < 2.4f ? "1.8-2.4" : Ball.Z < 3f ? "2.4-3" : "3+")
                      + (near < 0.7f ? ".onMan" : near < 1.5f ? ".beside" : ".clear"));
            }
            if (CollectDiagnostics && !_wallLineCounted && _wallMen.Count > 0 && Ball.IsShot && MathF.Abs(Ball.Pos.X) > Pitch.HalfLength - 0.3f)
            {
                // where the shot reaches the goal line (a shot saved before it is counted by the keeper)
                _wallLineCounted = true;
                bool inFrame = MathF.Abs(Ball.Pos.Y) < Pitch.GoalHalfWidth && Ball.Z < Pitch.GoalHeight;
                Count(inFrame ? "fk.atLine.z" + (Ball.Z < 0.5f ? "0-0.5" : Ball.Z < 1.46f ? "0.5-1.46" : "1.46+") : Ball.Z >= Pitch.GoalHeight ? "fk.atLine.over" : "fk.atLine.wide");
            }
            if (CollectDiagnostics && !_wallPassCounted && _wallMen.Count > 0 && Ball.IsShot && Vec2.Distance(Ball.Pos, _wallSpot) >= Pitch.RestartDistance)
            {
                // where the shot passes the wall: its height, and how far from the nearest man in it
                _wallPassCounted = true;
                float near = float.PositiveInfinity; PlayerAgent man = null;
                foreach (var w in _wallMen) { float d = Vec2.Distance(w.Pos, Ball.Pos); if (d < near) { near = d; man = w; } }
                Count("fk.atWall.z" + (Ball.Z < 1f ? "0-1" : Ball.Z < 1.8f ? "1-1.8" : Ball.Z < 2.4f ? "1.8-2.4" : Ball.Z < 3f ? "2.4-3" : "3+")
                      + (near < 0.7f ? ".onMan" : near < 1.5f ? ".beside" : ".clear"));
            }
            if (CollectDiagnostics && !_wallLineCounted && _wallMen.Count > 0 && Ball.IsShot && MathF.Abs(Ball.Pos.X) > Pitch.HalfLength - 0.3f && MathF.Abs(Ball.Pos.Y) < Pitch.GoalHalfWidth)
            {
                _wallLineCounted = true;
                Count("fk.atLine.z" + (Ball.Z < 0.5f ? "0-0.5" : Ball.Z < 1.46f ? "0.5-1.46" : "1.46+"));
            }
            if (_wallMen.Count > 0 && SimTime - _wallShotTime > 2f) _wallMen.Clear();
        }

        /// <summary>
        /// A man in the air cannot touch a ball that passes under his feet (a low free kick under a jumping wall, a ball under a leaping
        /// header). True when the ball runs under him.
        /// </summary>
        private bool BallPassesUnder(PlayerAgent p, float ballZ)
        {
            if (p.JumpZ <= 0.05f || ballZ + Pitch.BallRadius >= p.JumpZ) return false;
            if (CollectDiagnostics && _wallMen.Contains(p) && Ball.IsShot && _wallUnderCountedFor != _wallShotTime) { _wallUnderCountedFor = _wallShotTime; Count("fk.wall.under." + _freeKickStyle); }
            return true;
        }

        /// <summary>
        /// The keeper at a free kick in shooting range: with the wall shutting the near half of the goal he stands close to the middle, a little
        /// towards the far post (StatsBomb PL 2015/16 freeze frames, 288 free kicks with a wall: median 0.27 m to the far side, quartiles 0.73 m
        /// to the far side and 0.27 m to the near side) - where he can see the ball past the wall and still reach the corner over it - a step
        /// off his line.
        /// </summary>
        private Vec2 KeeperSpotBehindWall(Restart r, int goalSign)
        {
            float nearSide = MathF.Abs(r.Spot.Y) > 0.5f ? MathF.Sign(r.Spot.Y) : 1f;
            float y = MathF.Abs(r.Spot.Y) < 1.5f ? 0f : -nearSide * KeeperFarSideOffset;
            var spot = new Vec2((Pitch.HalfLength - 0.8f) * goalSign, y);
            // (a free kick from beside the goal: the ball is within 9.15 m of the keeper's step off his line; Law 13 lets him stand on the goal line
            // between the posts, so he stands there instead of being pushed out of the circle past the post)
            if (Vec2.Distance(spot, r.Spot) < Pitch.RestartDistance + 0.3f) spot = new Vec2((Pitch.HalfLength - 0.2f) * goalSign, MathUtil.Clamp(y, -Pitch.GoalHalfWidth + 0.4f, Pitch.GoalHalfWidth - 0.4f));
            return spot;
        }
    }
}

using System;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// Combinations (polish round 4): the one-two played first time or after a touch, the return ball's technique, the third man.
    /// </summary>
    public sealed partial class MatchEngine
    {
        /// <summary>A return pass after the receiver has carried the ball more than this far (m) counts as delayed, else immediate - as in the
        /// data, where a delayed one-two has a carry between the two passes (class D: the threshold of a carry).</summary>
        public static float OneTwoCarryDistance = 2f;

        // Real one-twos (StatsBomb, Premier League 2015/16, all 380 matches - tools/calibration/combinations.py): 24.2 a match, 3.24 of them
        // immediate (no carry between) and 20.95 delayed (the receiver carries it first), the return within 6 s and 5 m or more upfield; the
        // return 89% along the ground, 7% low, 4% high. (Counted a match, like goals and shots.)

        /// <summary>How long (s) the runner keeps his run going when the receiver takes the ball under control first (class B: the data's 6 s).</summary>
        public static float OneTwoDelayedWindow = 6f;
        /// <summary>The clearance of the lane (m) the receiver needs to play the wall pass first time (class D).</summary>
        public static float OneTwoFirstTimeLane = 2.5f;
        /// <summary>How far (m) the runner must be goal-side of the nearest defender for the wall pass to be on first time (class D, set against the real share of immediate one-twos: 13%; 16% here).</summary>
        public static float OneTwoPastHisMan = 5f;
        /// <summary>Below this clearance of the lane (m) the return may be clipped over the man in it (class D).</summary>
        public static float OneTwoChipLane = 1.5f;
        /// <summary>The shortest clipped return (m; class D).</summary>
        public static float OneTwoChipMinDistance = 7f;
        /// <summary>How readily a passer bursts forward for the return (a multiplier of his movement, teamwork, the team's style; class D,
        /// unchanged: at 1 and above nearly every chance of one is taken).</summary>
        public static float OneTwoRunChance = 1.4f;

        /// <summary>The longest (s) the receiver of a delayed one-two looks to carry the ball before he may return it without (class D).</summary>
        public static float OneTwoCarrySeconds = 3f;

        /// <summary>A delayed one-two: he is still carrying it to open the angle (not yet the time, nor the distance).</summary>
        private bool CarryingBeforeTheReturn(PlayerAgent p) => p.OneTwoCarryUntil > SimTime && Vec2.Distance(p.ControlStart, p.Pos) < OneTwoCarryDistance;

        /// <summary>The wall pass is on at once: the lane to the runner's stride is open, so it can be played first time.</summary>
        private bool WallPassOnNow(PlayerAgent receiver, PlayerAgent runner)
        {
            Vec2 toSpot = runner.OneTwoSpot - runner.Pos;
            Vec2 dir = toSpot.LengthSq > 0.25f ? toSpot.Normalized : runner.Team.DirToWorld(Vec2.UnitX);
            Vec2 into = runner.Pos + dir * 3f;
            if (LaneClearance(receiver.Team, receiver.Pos, into) < OneTwoFirstTimeLane) return false;
            // and he is already past his man: goal-side of the nearest defender by a metre (else it is played after a touch or a carry)
            var team = runner.Team;
            PlayerAgent marker = null;
            float best = float.PositiveInfinity;
            foreach (var o in team.Opponent.Players)
            {
                if (o.SentOff || o.IsGoalkeeper) continue;
                float d = Vec2.DistanceSq(o.Pos, runner.Pos);
                if (d < best) { best = d; marker = o; }
            }
            return marker == null || team.Forward(runner.Pos) > team.Forward(marker.Pos) + OneTwoPastHisMan;
        }
    }
}

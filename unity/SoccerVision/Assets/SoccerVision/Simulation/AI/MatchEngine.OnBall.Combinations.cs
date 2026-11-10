using System;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// Combinations (polish round 4): the one-two played first time or after a touch, the return ball's technique, the third man.
    /// </summary>
    public sealed partial class MatchEngine
    {
        /// <summary>A return pass played within this long (s) of taking control counts as first time - an immediate one-two (class D: StatsBomb
        /// has no carry between the two passes; the engine's control and decision take about 0.12 s).</summary>
        public static float OneTwoImmediateWindow = 0.5f;

        // Real one-twos (StatsBomb, Premier League 2015/16, all 380 matches - tools/calibration/combinations.py): 24.2 a match, 3.24 of them
        // immediate (no carry between) and 20.95 delayed (the receiver carries it first), the return within 6 s and 5 m or more upfield; the
        // return 89% along the ground, 7% low, 4% high. The engine plays about half the real number of passes a match (its match is 2.2 times
        // shorter in true time), so the like-for-like targets are about half: some 1.7 immediate and 10.7 delayed.

        /// <summary>How long (s) the runner keeps his run going when the receiver takes the ball under control first (class B: the data's 6 s).</summary>
        public static float OneTwoDelayedWindow = 6f;
        /// <summary>The clearance of the lane (m) the receiver needs to play the wall pass first time (class D).</summary>
        public static float OneTwoFirstTimeLane = 2.5f;
        /// <summary>Below this clearance of the lane (m) the return may be clipped over the man in it (class D).</summary>
        public static float OneTwoChipLane = 1.5f;
        /// <summary>The shortest clipped return (m; class D).</summary>
        public static float OneTwoChipMinDistance = 7f;
        /// <summary>How readily a passer bursts forward for the return (a multiplier of his movement, teamwork, the team's style; class D set
        /// against the real rate above). It was 1.4.</summary>
        public static float OneTwoRunChance = 1.4f;

        /// <summary>The wall pass is on at once: the lane to the runner's stride is open, so it can be played first time.</summary>
        private bool WallPassOnNow(PlayerAgent receiver, PlayerAgent runner)
        {
            Vec2 toSpot = runner.OneTwoSpot - runner.Pos;
            Vec2 dir = toSpot.LengthSq > 0.25f ? toSpot.Normalized : runner.Team.DirToWorld(Vec2.UnitX);
            Vec2 into = runner.Pos + dir * 3f;
            return LaneClearance(receiver.Team, receiver.Pos, into) >= OneTwoFirstTimeLane;
        }
    }
}

using System;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// Opening the legs (polish round 9): the victim of a nutmeg, or a player letting the ball run through his own legs (the dummy), opens
    /// them in time with the ball - from up to a quarter of a second before it reaches him, wide as it goes through, closed again a quarter of a
    /// second after (real time; <see cref="LegsOpenSeconds"/> in all) - a smooth rise and fall, not a switch. The renderers draw the feet
    /// apart by <see cref="PlayerAgent.LegsOpen"/> (0-1; at most about a shoulder width).
    /// </summary>
    public sealed partial class MatchEngine
    {
        /// <summary>The whole movement, open and closed again, in real seconds (the item: about half a second).</summary>
        public static float LegsOpenSeconds = 0.5f;

        /// <summary>The quickest he gets his legs open, in real seconds (a ball that is nearly at his feet already).</summary>
        public static float LegsOpenQuickest = 0.1f;

        /// <summary>The ball will be between his legs at <paramref name="ballThrough"/> (sim time): he opens them for it.</summary>
        private void OpenLegsFor(PlayerAgent p, float ballThrough)
        {
            float half = 0.5f * LegsOpenSeconds * Config.MotionSpeed;
            // (from closed, always: a smooth rise - a quicker one when the ball is nearly there - widest as it goes through)
            p.LegsOpenFrom = p.LegsOpen > 0f ? p.LegsOpenFrom : SimTime;
            p.LegsOpenAt = MathF.Max(p.LegsOpenFrom + LegsOpenQuickest * Config.MotionSpeed, MathF.Min(ballThrough, p.LegsOpenFrom + half));
            p.NutmegVictimUntil = p.LegsOpenAt + half;
        }

        /// <summary>When a ball now moving at <paramref name="speed"/> reaches a point <paramref name="distance"/> away (it slows as it rolls).</summary>
        private float BallReaches(float distance, float speed) => SimTime + MathF.Max(0f, distance) / MathF.Max(1f, 0.9f * speed);

        /// <summary>Each step: how far apart each player's feet are - a smooth rise to the moment the ball goes through, a smooth fall after.</summary>
        private void UpdateLegs()
        {
            float half = 0.5f * LegsOpenSeconds * Config.MotionSpeed;
            foreach (var p in AllPlayers)
            {
                float t = SimTime;
                if (t < p.LegsOpenFrom || t >= p.LegsOpenAt + half) p.LegsOpen = 0f;
                else if (t < p.LegsOpenAt) p.LegsOpen = 0.5f * (1f - MathF.Cos(MathF.PI * (t - p.LegsOpenFrom) / (p.LegsOpenAt - p.LegsOpenFrom)));
                else p.LegsOpen = 0.5f * (1f + MathF.Cos(MathF.PI * (t - p.LegsOpenAt) / half));
            }
        }
    }
}

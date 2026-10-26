using System;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// Field of play geometry (IFAB Law 1, international match size 105 x 68 m).
    /// World coordinates: origin at the centre mark, X along the length (goal lines at +/-52.5),
    /// Y across the width (touchlines at +/-34). Heights (Z) are metres above the grass.
    /// </summary>
    public static class Pitch
    {
        public const float Length = 105f;
        public const float Width = 68f;
        public const float HalfLength = 52.5f;
        public const float HalfWidth = 34f;

        public const float GoalHalfWidth = 3.66f;     // 7.32 m between the inner edges of the posts
        public const float GoalHeight = 2.44f;        // lower edge of the crossbar
        public const float PostRadius = 0.06f;        // posts/crossbar max 12 cm
        public const float GoalDepth = 2.0f;          // net depth (not regulated, typical)

        public const float GoalAreaDepth = 5.5f;
        public const float GoalAreaHalfWidth = GoalHalfWidth + 5.5f;      // 9.16
        public const float PenaltyAreaDepth = 16.5f;
        public const float PenaltyAreaHalfWidth = GoalHalfWidth + 16.5f;  // 20.16
        public const float PenaltySpotDistance = 11f;
        public const float CentreCircleRadius = 9.15f;
        public const float PenaltyArcRadius = 9.15f;
        public const float CornerArcRadius = 1f;
        public const float RestartDistance = 9.15f;   // opponents' distance at kick-offs, free kicks, corners
        public const float ThrowInDistance = 2f;

        public const float BallRadius = 0.11f;        // size 5 ball: 68-70 cm circumference

        /// <summary>X of the goal line for the goal on side <paramref name="goalSign"/> (+1 or -1).</summary>
        public static float GoalLineX(int goalSign) => HalfLength * goalSign;

        public static Vec2 GoalCentre(int goalSign) => new Vec2(HalfLength * goalSign, 0f);

        public static Vec2 PenaltySpot(int goalSign) => new Vec2((HalfLength - PenaltySpotDistance) * goalSign, 0f);

        /// <summary>True if p is inside the penalty area in front of the goal on side goalSign (lines are part of the area).</summary>
        public static bool InPenaltyArea(Vec2 p, int goalSign)
        {
            float depth = HalfLength - p.X * goalSign;
            return depth >= -0.01f && depth <= PenaltyAreaDepth && MathF.Abs(p.Y) <= PenaltyAreaHalfWidth;
        }

        public static bool InGoalArea(Vec2 p, int goalSign)
        {
            float depth = HalfLength - p.X * goalSign;
            return depth >= -0.01f && depth <= GoalAreaDepth && MathF.Abs(p.Y) <= GoalAreaHalfWidth;
        }

        /// <summary>Inside the penalty area or the penalty arc in front of it (used for penalty kick positioning).</summary>
        public static bool InPenaltyAreaOrArc(Vec2 p, int goalSign)
        {
            if (InPenaltyArea(p, goalSign)) return true;
            return Vec2.Distance(p, PenaltySpot(goalSign)) < PenaltyArcRadius;
        }

        public static bool InsideField(Vec2 p, float margin = 0f)
        {
            return MathF.Abs(p.X) <= HalfLength + margin && MathF.Abs(p.Y) <= HalfWidth + margin;
        }

        public static Vec2 ClampToField(Vec2 p, float margin = 0f)
        {
            return new Vec2(MathUtil.Clamp(p.X, -HalfLength - margin, HalfLength + margin),
                            MathUtil.Clamp(p.Y, -HalfWidth - margin, HalfWidth + margin));
        }

        public static Vec2 CornerPosition(int goalSign, int ySign)
        {
            return new Vec2(HalfLength * goalSign, HalfWidth * ySign);
        }

        /// <summary>Angle (radians) subtended by the goal mouth from point p, for the goal at goalSign.</summary>
        public static float GoalAngle(Vec2 p, int goalSign)
        {
            float x = HalfLength - p.X * goalSign;  // distance to goal line
            float y = MathF.Abs(p.Y);
            float num = 2f * GoalHalfWidth * x;
            float den = x * x + y * y - GoalHalfWidth * GoalHalfWidth;
            float a = MathF.Atan2(num, den);
            if (a < 0f) a += MathUtil.Pi;
            return a;
        }
    }
}

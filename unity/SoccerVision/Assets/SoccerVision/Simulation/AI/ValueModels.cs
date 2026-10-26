using System;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// Expected Threat (xT): probability that possession at a location ends in a goal within the next actions.
    /// Grid published by Karun Singh (open_xt_12x8_v1): 12 columns from own goal to opponent goal, 8 rows across.
    /// Used as the value function for passes, carries and positioning.
    /// </summary>
    public static class ExpectedThreat
    {
        private const int Cols = 12;
        private const int Rows = 8;

        private static readonly float[] ColumnsOuter = { 0.00638303f, 0.00779616f, 0.00844854f, 0.00977659f, 0.01126267f, 0.01248344f, 0.01473596f, 0.0174506f, 0.02122129f, 0.02756312f, 0.03485072f, 0.0379259f };
        private static readonly float[] ColumnsWide = { 0.00750072f, 0.00878589f, 0.00942382f, 0.0105949f, 0.01214719f, 0.0138454f, 0.01611813f, 0.01870347f, 0.02401521f, 0.02953272f, 0.04066992f, 0.04647721f };
        private static readonly float[] ColumnsInner = { 0.0088799f, 0.00977745f, 0.01001304f, 0.01110462f, 0.01269174f, 0.01429128f, 0.01685596f, 0.01935132f, 0.0241224f, 0.02855202f, 0.05491138f, 0.06442595f };
        private static readonly float[] ColumnsCentre = { 0.00941056f, 0.01082722f, 0.01016549f, 0.01132376f, 0.01262646f, 0.01484598f, 0.01689528f, 0.0199707f, 0.02385149f, 0.03511326f, 0.10805102f, 0.25745362f };

        private static readonly float[][] Grid = { ColumnsOuter, ColumnsWide, ColumnsInner, ColumnsCentre, ColumnsCentre, ColumnsInner, ColumnsWide, ColumnsOuter };

        /// <summary>xT for a team-local position (forward -52.5..52.5, lateral -34..34), bilinear interpolated.</summary>
        public static float At(float forward, float lateral)
        {
            float cx = (forward + Pitch.HalfLength) / Pitch.Length * Cols - 0.5f;
            float cy = (lateral + Pitch.HalfWidth) / Pitch.Width * Rows - 0.5f;
            cx = MathUtil.Clamp(cx, 0f, Cols - 1);
            cy = MathUtil.Clamp(cy, 0f, Rows - 1);
            int x0 = (int)cx, y0 = (int)cy;
            int x1 = Math.Min(x0 + 1, Cols - 1), y1 = Math.Min(y0 + 1, Rows - 1);
            float fx = cx - x0, fy = cy - y0;
            float a = Grid[y0][x0] + (Grid[y0][x1] - Grid[y0][x0]) * fx;
            float b = Grid[y1][x0] + (Grid[y1][x1] - Grid[y1][x0]) * fx;
            return a + (b - a) * fy;
        }

        public static float At(TeamAgent team, Vec2 world) => At(team.Forward(world), team.Lateral(world));
    }

    /// <summary>
    /// Expected goals from shot location. Logistic model fitted on Wyscout open data (Soccermatics,
    /// D. Sumpter): terms angle, distance, X, C, X^2, C^2, angle*X.
    /// </summary>
    public static class ExpectedGoals
    {
        public static float FromLocation(Vec2 shotPos, int goalSign)
        {
            float x = MathF.Max(0.5f, Pitch.HalfLength - shotPos.X * goalSign); // distance from goal line
            float c = MathF.Abs(shotPos.Y);
            float distance = MathF.Sqrt(x * x + c * c);
            float angle = Pitch.GoalAngle(shotPos, goalSign);
            if (distance > 40f) return 0.005f;
            float sum = 0.5103f + 0.6338f * angle - 0.2798f * distance + 0.1243f * x - 0.0300f * c
                        + 0.0014f * x * x + 0.0041f * c * c - 0.1251f * angle * x;
            float p = MathUtil.Sigmoid(sum);
            return MathUtil.Clamp(p, 0.002f, 0.85f);
        }
    }
}

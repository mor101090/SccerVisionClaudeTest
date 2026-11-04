using System;
using System.Collections.Generic;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// How far the weak and strong squads are from the medium one. The generator's weaker / excellent squads are 12-14 attribute points from
    /// the medium one, which makes the stronger side win 94% of the time - real football's biggest mismatches (a top-two side against a bottom-three
    /// side) end 89% / 8% / 3%, and a clear favourite (60-70% in the betting odds) wins about 68%. So each attribute of a weak or strong squad is
    /// moved only <see cref="StrongSpread"/> / <see cref="WeakSpread"/> of the way from the medium squad towards the generator's squad (the same player in the same slot).
    /// </summary>
    public static class TeamStrengthScaling
    {
        /// <summary>0 = every squad is the medium one, 1 = the generator's weaker / excellent squads as they are. 0.8 matches the real Premier League together with the possession round (see CHANGELOG).</summary>
        public static float StrongSpread = 0.8f;
        /// <summary>The same for the weak squads.</summary>
        public static float WeakSpread = 0.8f;

        public static (List<PlayerProfile> Starters, List<PlayerProfile> Bench) Red(TeamStrength s) => Make(s, GeneratedTeams.Red(), GeneratedTeamLevels.Red(s));
        public static (List<PlayerProfile> Starters, List<PlayerProfile> Bench) Blue(TeamStrength s) => Make(s, GeneratedTeams.Blue(), GeneratedTeamLevels.Blue(s));

        private static (List<PlayerProfile> Starters, List<PlayerProfile> Bench) Make(TeamStrength s,
            (List<PlayerProfile> Starters, List<PlayerProfile> Bench) medium, (List<PlayerProfile> Starters, List<PlayerProfile> Bench) level)
        {
            float spread = s == TeamStrength.Strong ? StrongSpread : WeakSpread;
            if (s == TeamStrength.Medium || spread == 1f) return level;
            for (int i = 0; i < level.Starters.Count; i++) Blend(level.Starters[i].Attributes, medium.Starters[i].Attributes, spread);
            for (int i = 0; i < level.Bench.Count; i++) Blend(level.Bench[i].Attributes, medium.Bench[i].Attributes, spread);
            return level;
        }

        private static int B(int level, int medium, float k) => Math.Max(1, Math.Min(99, (int)Math.Round(medium + (level - medium) * k)));

        private static void Blend(PlayerAttributes a, PlayerAttributes m, float k)
        {
            a.Aggression = B(a.Aggression, m.Aggression, k); a.Teamwork = B(a.Teamwork, m.Teamwork, k); a.Composure = B(a.Composure, m.Composure, k); a.Decisions = B(a.Decisions, m.Decisions, k);
            a.Tackling = B(a.Tackling, m.Tackling, k); a.Marking = B(a.Marking, m.Marking, k); a.Positioning = B(a.Positioning, m.Positioning, k);
            a.Creativity = B(a.Creativity, m.Creativity, k); a.Passing = B(a.Passing, m.Passing, k); a.Crossing = B(a.Crossing, m.Crossing, k);
            a.Finishing = B(a.Finishing, m.Finishing, k); a.LongShots = B(a.LongShots, m.LongShots, k); a.OffTheBall = B(a.OffTheBall, m.OffTheBall, k);
            a.PhysicalPower = B(a.PhysicalPower, m.PhysicalPower, k); a.BallControl = B(a.BallControl, m.BallControl, k); a.Dribbling = B(a.Dribbling, m.Dribbling, k);
            a.Acceleration = B(a.Acceleration, m.Acceleration, k); a.Speed = B(a.Speed, m.Speed, k); a.Jumping = B(a.Jumping, m.Jumping, k); a.Heading = B(a.Heading, m.Heading, k);
            a.Stamina = B(a.Stamina, m.Stamina, k);
            a.FreeKicks = B(a.FreeKicks, m.FreeKicks, k); a.Penalty = B(a.Penalty, m.Penalty, k);
            a.AerialAbility = B(a.AerialAbility, m.AerialAbility, k); a.Handling = B(a.Handling, m.Handling, k); a.Kicking = B(a.Kicking, m.Kicking, k);
            a.OneOnOne = B(a.OneOnOne, m.OneOnOne, k); a.Reflexes = B(a.Reflexes, m.Reflexes, k);
        }
    }
}

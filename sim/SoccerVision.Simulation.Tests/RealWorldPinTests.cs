using System;
using System.Linq;
using System.Threading.Tasks;
using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>
    /// The engine against the real figures it was tuned to (tools/calibration/CLAIMS.md, counted by real_leagues.py: Premier League 2015/16-2024/25 -
    /// goals 2.83, shots 25.4, fouls 21.5 a match; home advantage about +12 points of win share). Medium v medium in all four marking pairings
    /// (both systems in both seats, so neither colour nor seat is the reason), 40 matches each. The ranges are wide on purpose: they catch an
    /// engine that has wandered off (goals 2.0, a marking gap of 25 points), not a change of a tenth of a goal.
    /// </summary>
    public class RealWorldPinTests
    {
        private static readonly Lazy<(MatchEngine[] ZZ, MatchEngine[] ZM, MatchEngine[] MZ, MatchEngine[] MM)> Matches = new Lazy<(MatchEngine[], MatchEngine[], MatchEngine[], MatchEngine[])>(() =>
            (Play(MarkingStyle.Zonal, MarkingStyle.Zonal, 7000), Play(MarkingStyle.Zonal, MarkingStyle.ManToMan, 7100),
             Play(MarkingStyle.ManToMan, MarkingStyle.Zonal, 7200), Play(MarkingStyle.ManToMan, MarkingStyle.ManToMan, 7300)));

        private static MatchEngine[] Play(MarkingStyle home, MarkingStyle away, ulong from)
        {
            var es = new MatchEngine[40];
            Parallel.For(0, es.Length, i =>
            {
                var red = DemoTeams.Red(); var blue = DemoTeams.Blue();
                red.Tactics.Marking = home; blue.Tactics.Marking = away;
                es[i] = new MatchEngine(red, blue, from + (ulong)i, MatchConfig.CreateDefault());
                es[i].RunToEnd();
            });
            return es;
        }

        private static MatchEngine[] All => Matches.Value.ZZ.Concat(Matches.Value.ZM).Concat(Matches.Value.MZ).Concat(Matches.Value.MM).ToArray();

        [Fact]
        public void GoalsShotsAndFoulsAMatchAreInTheRealRange()
        {
            var all = All;
            double goals = all.Average(e => e.Home.Score + e.Away.Score);
            double shots = all.Average(e => e.Home.Stats.Shots + e.Away.Stats.Shots);
            double fouls = all.Average(e => e.Home.Stats.Fouls + e.Away.Stats.Fouls);
            Assert.InRange(goals, 2.4, 3.2);   // real 2.83 (measured here 2.7-2.8; 1.9-2.4 before the goals round)
            Assert.InRange(shots, 22.0, 28.0); // real 25.4 (measured here 24.6)
            Assert.InRange(fouls, 19.0, 24.0); // real 21.5 (measured here 21.2)
        }

        [Fact]
        public void ZonalAndManToManMarkingAreNotWorldsApart()
        {
            var m = Matches.Value;
            // seats pooled: the zonal side's wins against the man-to-man side, whichever colour and seat it had
            double zonalWins = m.ZM.Count(e => e.Home.Score > e.Away.Score) + m.MZ.Count(e => e.Away.Score > e.Home.Score);
            double manWins = m.ZM.Count(e => e.Away.Score > e.Home.Score) + m.MZ.Count(e => e.Home.Score > e.Away.Score);
            double gap = 100.0 * (zonalWins - manWins) / (m.ZM.Length + m.MZ.Length);
            Assert.InRange(gap, -25.0, 25.0); // measured here about +10 (+23 / +26 before the goals round): a system should not decide a match between equal squads
        }

        [Fact]
        public void TheHomeSideHasTheRealSizedEdgeWithTheSeatsAndColoursSwapped()
        {
            var m = Matches.Value;
            // the two same-system pairings: any edge is the seat's (the home side), not the colour's or the system's
            var same = m.ZZ.Concat(m.MM).ToArray();
            double home = 100.0 * same.Count(e => e.Home.Score > e.Away.Score) / same.Length;
            double away = 100.0 * same.Count(e => e.Away.Score > e.Home.Score) / same.Length;
            Assert.InRange(home - away, -5.0, 30.0); // real +12 (43-45% v 28-33%, eight leagues); 80 matches move it by about 7 points on their own
        }
    }
}

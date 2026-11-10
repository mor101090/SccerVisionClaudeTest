using System;
using System.IO;
using System.Linq;
using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>
    /// Ball physics stage 11: the test bench (TESTS 2-14). docs/BALL_TEST_BENCH.md is the bench's own output and must stay so; the bench's rows
    /// meet FIFA's test ranges (class A) and the physics' own laws (energy, symmetry, spin's direction). See docs/BALL_PHYSICS.md.
    /// </summary>
    public class BallTestBenchTests
    {
        private static readonly System.Collections.Generic.List<BallTestBench.Row> Rows = BallTestBench.Run();
        private static BallTestBench.Row Find(string test, string startsWith) => Rows.Single(r => r.Test.StartsWith(test, StringComparison.Ordinal) && r.Case.StartsWith(startsWith, StringComparison.Ordinal));

        private static string RepoRoot()
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "docs", "BALL_PHYSICS.md"))) d = d.Parent;
            Assert.NotNull(d);
            return d.FullName;
        }

        [Fact]
        public void TheDocIsTheBenchsOwnOutput()
        {
            var doc = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "BALL_TEST_BENCH.md")).Replace("\r\n", "\n");
            var bench = BallTestBench.Markdown(Rows);
            Assert.True(doc == bench, "docs/BALL_TEST_BENCH.md is out of date: regenerate it with `SimRunner --bench > docs/BALL_TEST_BENCH.md` (the first difference is at character "
                                      + Enumerable.Range(0, Math.Min(doc.Length, bench.Length)).FirstOrDefault(i => doc[i] != bench[i]) + ")");
        }

        [Fact]
        public void TheBenchCoversTests2To14()
        {
            foreach (int t in Enumerable.Range(2, 13)) Assert.Contains(Rows, r => r.Test.StartsWith(t + " ", StringComparison.Ordinal));
        }

        [Fact]
        public void TheGroundMeetsFifasTestRanges()
        {
            // vertical rebound from 2 m: "excellent" natural 0.60-1.00 m, Quality Pro turf 0.60-0.85 m
            Assert.InRange(Find("13", "natural, dry: drop").BounceHeight, 0.60f, 1.00f);
            Assert.InRange(Find("13", "artificial, dry: drop").BounceHeight, 0.60f, 0.85f);
            // ball roll off the 1 m ramp: 4.0-10.0 m
            Assert.InRange(Find("4", "FIFA ramp roll").Distance, 4f, 10f);
            // angle ball rebound (turf): 45-70% dry, 45-80% wet
            foreach (var r in Rows.Where(r => r.Case.Contains("FIFA angle rebound")))
            {
                float keep = r.SpeedOut / r.SpeedIn;
                Assert.InRange(keep, 0.45f, r.Case.Contains("wet") ? 0.80f : 0.70f);
            }
        }

        [Fact]
        public void NothingIsCreatedAndTheFrameIsSymmetric()
        {
            foreach (var r in Rows.Where(r => !float.IsNaN(r.EnergyLoss))) Assert.True(r.EnergyLoss > -0.5f, $"{r.Test} / {r.Case}: energy grew {-r.EnergyLoss:0.0}%");
            foreach (string t in new[] { "10", "11" })
            {
                var side = Rows.Where(r => r.Test.StartsWith(t, StringComparison.Ordinal) && r.Case.Contains("cm off")).ToList();
                Assert.Equal(5, side.Count);
                // (the post exactly; the bar to within gravity's doing - it pulls the ball the same way on either side of the bar's centre)
                float tol = t == "10" ? 0.01f : 0.3f;
                Assert.InRange(side[4].SpeedOut, side[0].SpeedOut - tol, side[0].SpeedOut + tol);
                Assert.InRange(side[3].SpeedOut, side[1].SpeedOut - tol, side[1].SpeedOut + tol);
                Assert.True(side[2].SpeedOut > side[0].SpeedOut, "a square hit gives back the most of the normal speed's share");
            }
        }

        [Fact]
        public void SpinActsTheWayTheMagnusForceSays()
        {
            float plain = Find("6", "Sp 0.0").Distance;
            Assert.True(Find("6", "Sp 0.2").Distance > plain + 5f, "backspin holds the ball up and carries it further");
            Assert.True(Find("7", "Sp 0.2").Distance < plain - 5f, "topspin brings it down sooner");
            var side = Rows.Where(r => r.Test.StartsWith("8", StringComparison.Ordinal)).Select(r => float.Parse(r.Response.Split(" m off")[0].Split("lands ")[1], System.Globalization.CultureInfo.InvariantCulture)).ToList();
            Assert.Equal(0f, side[0], 2);
            for (int i = 1; i < side.Count; i++) Assert.True(side[i] > side[i - 1], "more sidespin, more curl: " + string.Join(", ", side));
        }

        [Fact]
        public void NoShotPassesThroughThePost()
        {
            foreach (var r in Rows.Where(r => r.Test.StartsWith("14", StringComparison.Ordinal))) Assert.StartsWith("20 of 20 caught", r.Response);
        }
    }
}

using System;
using System.Linq;
using System.Threading.Tasks;
using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>
    /// The marking round after ball physics stage 12: man-to-man keeps a free man behind the markers, hands a man on to midfield, and swaps men when
    /// two forwards cross; a contested header goes wider. Measured in CHANGELOG.md (both marking systems now concede alike).
    /// </summary>
    public class MarkingBalanceTests
    {
        private static MatchEngine Play(string marking, ulong seed)
        {
            var red = DemoTeams.Red(); var blue = DemoTeams.Blue();
            var m = marking == "man" ? MarkingStyle.ManToMan : MarkingStyle.Zonal;
            red.Tactics.Marking = m; blue.Tactics.Marking = m;
            var e = new MatchEngine(red, blue, seed, MatchConfig.CreateDefault()) { CollectDiagnostics = true };
            e.RunToEnd();
            return e;
        }

        [Fact]
        public void ManToManKeepsAFreeManHandsMenOnAndSwapsThem()
        {
            var e = Play("man", 77);
            Assert.True(e.Diagnostics.GetValueOrDefault("mark.freeMan") > 1000, "the free man covers behind the markers");
            Assert.True(e.Diagnostics.GetValueOrDefault("mark.passOn") > 0, "a defender hands a man on who drops into midfield");
            Assert.True(e.Diagnostics.GetValueOrDefault("mark.swap") > 0, "two markers swap men when their forwards cross");
            Assert.Equal(0, e.RecoveredErrors);
        }

        [Fact]
        public void ZonalMarkingIsUntouched()
        {
            var e = Play("zonal", 77);
            Assert.Equal(0, e.Diagnostics.GetValueOrDefault("mark.freeMan"));
            Assert.Equal(0, e.Diagnostics.GetValueOrDefault("mark.passOn"));
            Assert.Equal(0, e.Diagnostics.GetValueOrDefault("mark.swap"));
        }

        [Fact]
        public void TheFreeManIsOneCentreBackAndMarksNobody()
        {
            var red = DemoTeams.Red(); var blue = DemoTeams.Blue();
            red.Tactics.Marking = MarkingStyle.ManToMan;
            var e = new MatchEngine(red, blue, 78, MatchConfig.CreateDefault());
            int checks = 0;
            for (int i = 0; i < 4000 && !e.IsFinished; i++)
            {
                e.Advance(0.25f);
                if (e.Phase != MatchPhase.Live || e.PossessionTeam != e.Away) continue;
                var free = e.Home.Players.Where(p => p.IsFreeMan).ToList();
                if (free.Count == 0) continue;
                Assert.Single(free);
                Assert.Equal(Line.Defence, free[0].Slot.Line);
                Assert.Null(free[0].ManAssignment);
                checks++;
            }
            Assert.True(checks > 100, "checked " + checks + " moments");
        }

        [Fact]
        public void ContestedHeadersAreCounted()
        {
            int contested = 0;
            Parallel.For(0, 4, i => { var e = Play("zonal", 90 + (ulong)i); System.Threading.Interlocked.Add(ref contested, e.Diagnostics.GetValueOrDefault("shot.header.contested")); });
            Assert.True(contested > 0);
        }
    }
}

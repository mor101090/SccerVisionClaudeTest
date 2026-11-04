using System.Linq;
using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>
    /// The crowd reacts to the result at the final whistle (not 30 seconds later, when the walk-off is over): the whistle is marked
    /// (Detail 1) exactly once per match - a whistle after ninety minutes that is followed by extra time or penalties is not the end,
    /// and after a shoot-out the result comes with the FullTime event ("pens 4-3").
    /// </summary>
    public class FinalWhistleTests
    {
        private static MatchEngine Played(ulong seed, Competition c)
        {
            var cfg = MatchConfig.CreateDefault();
            cfg.Competition = c;
            var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), seed, cfg);
            e.RunToEnd();
            return e;
        }

        [Fact]
        public void TheCrowdIsToldTheResultExactlyOncePerMatch()
        {
            foreach (var c in new[] { Competition.League, Competition.Cup, Competition.LeagueCup })
                for (ulong seed = 1; seed <= 6; seed++)
                {
                    var e = Played(seed, c);
                    var marked = e.EventLog.Count(x => x.Type == MatchEventType.Whistle && x.Whistle == WhistleKind.FullTime && x.Detail == 1);
                    var pens = e.EventLog.Count(x => x.Type == MatchEventType.FullTime && (x.Info ?? "").StartsWith("pens"));
                    Assert.True(marked + pens == 1, $"{c} seed {seed}: {marked} marked whistles and {pens} shoot-out results");
                    if (marked == 1)
                    {
                        // the marked whistle comes before the end of the match, with the walk-off in between
                        var whistle = e.EventLog.First(x => x.Type == MatchEventType.Whistle && x.Detail == 1);
                        var end = e.EventLog.Last(x => x.Type == MatchEventType.FullTime);
                        Assert.True(whistle.ClockSeconds <= end.ClockSeconds);
                        Assert.True(e.EventLog.IndexOf(whistle) < e.EventLog.IndexOf(end));
                    }
                }
        }
    }
}

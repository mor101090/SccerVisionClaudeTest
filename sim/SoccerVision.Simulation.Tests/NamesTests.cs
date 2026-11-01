using System.Collections.Generic;
using System.Linq;
using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>The demo squads' names come from the Israeli name database, anew for every match.</summary>
    public class NamesTests
    {
        private static IEnumerable<PlayerProfile> Squad(TeamSetup t) => t.Starters.Concat(t.Bench);

        [Fact]
        public void NamesComeFromTheDatabasesWithFourteenForeignersAndNoSurnameRepeats()
        {
            foreach (var arr in new[] { IsraeliNames.IsraelFirst, IsraeliNames.IsraelLast, IsraeliNames.SpanishFirst, IsraeliNames.SpanishLast, IsraeliNames.AmericanFirst, IsraeliNames.AmericanLast })
                Assert.Equal(arr.Length, arr.Distinct().Count()); // (duplicates merged)
            foreach (ulong seed in new ulong[] { 1001, 7, 123456789 })
            {
                var home = DemoTeams.Red(seed);
                var away = DemoTeams.Blue(seed);
                var all = Squad(home).Concat(Squad(away)).ToList();
                Assert.Equal(all.Count, all.Select(p => p.LastName).Distinct().Count());
                Assert.Equal(IsraeliNames.ForeignPlayers, all.Count(p => p.Nationality != "Israel"));
                Assert.Equal(IsraeliNames.ForeignPlayers / 2, all.Count(p => p.Nationality == "Spain"));
                Assert.Equal(IsraeliNames.ForeignPlayers / 2, all.Count(p => p.Nationality == "USA"));
                foreach (var p in all)
                {
                    var (fs, ls) = p.Nationality == "Spain" ? (IsraeliNames.SpanishFirst, IsraeliNames.SpanishLast)
                                 : p.Nationality == "USA" ? (IsraeliNames.AmericanFirst, IsraeliNames.AmericanLast) : (IsraeliNames.IsraelFirst, IsraeliNames.IsraelLast);
                    Assert.Contains(p.FirstName, fs);
                    Assert.Contains(p.LastName, ls);
                }
                Assert.False(string.IsNullOrEmpty(home.Manager));
                Assert.False(string.IsNullOrEmpty(away.Manager));
                // the starting elevens are mostly Israeli
                Assert.True(home.Starters.Count(p => p.Nationality == "Israel") >= 4);
            }
        }

        [Fact]
        public void EveryMatchNamesThePlayersAnew()
        {
            var a = Squad(DemoTeams.Red(11)).Select(p => p.FullName).ToList();
            var b = Squad(DemoTeams.Red(12)).Select(p => p.FullName).ToList();
            Assert.NotEqual(a, b);
            // the same seed gives the same names (a replay stays the same)
            Assert.Equal(a, Squad(DemoTeams.Red(11)).Select(p => p.FullName).ToList());
        }
    }
}

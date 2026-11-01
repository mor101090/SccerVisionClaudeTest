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
        public void NamesComeFromTheDatabaseAndNoSurnameRepeatsInAMatch()
        {
            Assert.Equal(IsraeliNames.First.Length, IsraeliNames.First.Distinct().Count()); // (duplicates merged)
            Assert.Equal(IsraeliNames.Last.Length, IsraeliNames.Last.Distinct().Count());
            foreach (ulong seed in new ulong[] { 1001, 7, 123456789 })
            {
                var home = DemoTeams.Red(seed);
                var away = DemoTeams.Blue(seed);
                var all = Squad(home).Concat(Squad(away)).ToList();
                Assert.All(all, p => Assert.Contains(p.FirstName, IsraeliNames.First));
                Assert.All(all, p => Assert.Contains(p.LastName, IsraeliNames.Last));
                Assert.Equal(all.Count, all.Select(p => p.LastName).Distinct().Count());
                Assert.False(string.IsNullOrEmpty(home.Manager));
                Assert.False(string.IsNullOrEmpty(away.Manager));
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

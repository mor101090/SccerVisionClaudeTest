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
                var groups = new[]
                {
                    (IsraeliNames.IsraelFirst, IsraeliNames.IsraelLast), (IsraeliNames.ArabFirst, IsraeliNames.ArabLast),
                    (IsraeliNames.RussianFirst, IsraeliNames.RussianLast), (IsraeliNames.EthiopianFirst, IsraeliNames.EthiopianLast),
                };
                foreach (var p in all)
                {
                    if (p.Nationality == "Spain") { Assert.Contains(p.FirstName, IsraeliNames.SpanishFirst); Assert.Contains(p.LastName, IsraeliNames.SpanishLast); }
                    else if (p.Nationality == "USA") { Assert.Contains(p.FirstName, IsraeliNames.AmericanFirst); Assert.Contains(p.LastName, IsraeliNames.AmericanLast); }
                    else Assert.Contains(groups, g => g.Item1.Contains(p.FirstName) && g.Item2.Contains(p.LastName)); // (both names from the same list)
                }
                Assert.False(string.IsNullOrEmpty(home.Manager));
                Assert.False(string.IsNullOrEmpty(away.Manager));
                // the starting elevens are mostly Israeli
                Assert.True(home.Starters.Count(p => p.Nationality == "Israel") >= 4);
            }
        }

        [Fact]
        public void TheListsAreSeparateAndHoldNoHeadings()
        {
            var all = new[] { IsraeliNames.IsraelFirst, IsraeliNames.IsraelLast, IsraeliNames.ArabFirst, IsraeliNames.ArabLast, IsraeliNames.RussianFirst,
                IsraeliNames.RussianLast, IsraeliNames.EthiopianFirst, IsraeliNames.EthiopianLast, IsraeliNames.SpanishFirst, IsraeliNames.SpanishLast,
                IsraeliNames.AmericanFirst, IsraeliNames.AmericanLast };
            foreach (var a in all) { Assert.NotEmpty(a); Assert.DoesNotContain(a, n => n.Contains("name") || n.Contains("database") || n.EndsWith(":")); }
            // a Muhammad never has a Jewish surname, nor an Avi an Arab one
            Assert.DoesNotContain("Muhammad", IsraeliNames.IsraelFirst);
            Assert.DoesNotContain("Cohen", IsraeliNames.ArabLast);
            Assert.Contains("Cohen", IsraeliNames.IsraelLast);
            var kinds = new HashSet<string>();
            for (ulong seed = 1; seed <= 40; seed++)
                foreach (var p in Squad(DemoTeams.Red(seed)).Where(p => p.Nationality == "Israel"))
                    kinds.Add(IsraeliNames.ArabFirst.Contains(p.FirstName) ? "arab" : IsraeliNames.RussianFirst.Contains(p.FirstName) ? "russian" : IsraeliNames.EthiopianFirst.Contains(p.FirstName) ? "ethiopian" : "jewish");
            Assert.True(kinds.Count == 4, "all four communities appear: " + string.Join(",", kinds));
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

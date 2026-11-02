using System.Collections.Generic;
using System.Linq;
using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>How players look: decided once, by where they come from, never touching the match.</summary>
    public class AppearanceTests
    {
        private static IEnumerable<PlayerProfile> Players(int matches)
        {
            for (ulong seed = 1; seed <= (ulong)matches; seed++)
            {
                ulong names = seed * 2654435761UL + 17UL;
                foreach (var t in new[] { DemoTeams.Red(names), DemoTeams.Blue(names) })
                    foreach (var p in t.Starters.Concat(t.Bench)) yield return p;
            }
        }

        [Fact]
        public void EveryoneHasAnOriginAndALookThatFitsIt()
        {
            var all = Players(30).ToList();
            Assert.All(all, p => { Assert.False(string.IsNullOrEmpty(p.Origin)); Assert.InRange(p.Appearance.Skin, 1, 6); Assert.True(p.Appearance.FaceSeed > 0); });
            // Ethiopian Israelis look East African, Spanish players Southern European, and so on - with the skin tones that belong
            Assert.All(all.Where(p => p.Origin == "ethiopian"), p => { Assert.Equal("east-african", p.Appearance.Look); Assert.InRange(p.Appearance.Skin, 5, 6); });
            Assert.All(all.Where(p => p.Origin == "russian"), p => Assert.Contains(p.Appearance.Look, new[] { "eastern-european", "european" }));
            Assert.All(all.Where(p => p.Origin == "arab"), p => Assert.Contains(p.Appearance.Look, new[] { "middle-eastern", "mediterranean" }));
            Assert.All(all.Where(p => p.Origin == "spanish"), p => Assert.Contains(p.Appearance.Look, new[] { "mediterranean", "european", "latin-american" }));
            Assert.Contains(all, p => p.Origin == "american" && p.Appearance.Look == "west-african");
        }

        [Fact]
        public void TheSpreadHasSeveralHairstylesBeardsAndBuilds()
        {
            var all = Players(30).ToList();
            Assert.True(all.Select(p => p.Appearance.Hair).Distinct().Count() >= 6);
            Assert.True(all.Select(p => p.Appearance.Facial).Distinct().Count() >= 4);
            Assert.Equal(3, all.Select(p => p.Appearance.Build).Distinct().Count());
            Assert.InRange(all.Count(p => p.Appearance.Facial != "none") / (double)all.Count, 0.2, 0.5);
        }

        [Fact]
        public void TheSameSeedGivesTheSameFacesAndAnotherSeedOthers()
        {
            string Sig(ulong n) => string.Join("|", DemoTeams.Red(n).Starters.Select(p => p.Appearance.Look + p.Appearance.Hair + p.Appearance.Facial + p.Appearance.FaceSeed));
            Assert.Equal(Sig(5), Sig(5));
            Assert.NotEqual(Sig(5), Sig(6));
        }

        [Fact]
        public void TheMatchDoesNotDependOnTheFaces()
        {
            // another face for the same player: the same match
            var a = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 3, MatchConfig.CreateDefault());
            var b = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 3, MatchConfig.CreateDefault());
            foreach (var p in b.Home.Players) p.Profile.Appearance = new PlayerAppearance { Look = "east-asian", Skin = 3, Hair = "bald", Facial = "full-beard" };
            a.RunToEnd(); b.RunToEnd();
            Assert.Equal(a.Home.Score, b.Home.Score);
            Assert.Equal(a.EventLog.Count, b.EventLog.Count);
        }
    }
}

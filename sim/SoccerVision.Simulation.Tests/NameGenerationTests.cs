using System;
using System.Collections.Generic;
using System.Linq;
using SoccerVision.Simulation;
using Xunit;
using Xunit.Abstractions;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>
    /// The player-name generator built on the Onomaverse names datasets (CC BY 4.0): the countries, the separate first-name and surname
    /// lists, the weighting by frequency, accents kept, no repeated full name, the same seed the same names - and the existing demo
    /// squads (IsraeliNames) untouched.
    /// </summary>
    public class NameGenerationTests
    {
        private readonly ITestOutputHelper _out;
        public NameGenerationTests(ITestOutputHelper output) { _out = output; }

        private static readonly string[] Wanted = { "ES", "DE", "FR", "IT", "GB", "BR", "AR", "PT", "NL", "HR" };

        [Fact]
        public void TheRequiredNationalitiesAreThereWithTheirOwnNameLists()
        {
            foreach (var code in Wanted)
            {
                var c = NameBank.Get(code);
                Assert.True(c.FirstNames.Count >= 30, code + " first names " + c.FirstNames.Count);
                Assert.True(c.Surnames.Count >= 25, code + " surnames " + c.Surnames.Count);
                Assert.NotSame(c.FirstNames, c.Surnames); // (two lists, never one)
                Assert.Same(c, NameBank.Get(c.Nationality)); // (by code or by nationality)
                Assert.Same(c, NameBank.Get(code.ToLowerInvariant()));
            }
            Assert.Equal("England", NameBank.Get("GB").Nationality);
            Assert.True(NameBank.Countries.Count >= 30, "other nationalities too: " + NameBank.Countries.Count);
            Assert.False(NameBank.TryGet("XX", out _));
        }

        [Fact]
        public void TheFirstNamesAreMaleAndTheSurnamesAreNotGivenNames()
        {
            // the women's names of the dataset are not in the lists of the men
            var female = new Dictionary<string, string[]>
            {
                ["ES"] = new[] { "Maria", "Carmen", "Ana", "Nuria" }, ["IT"] = new[] { "Maria", "Anna", "Francesca" }, ["BR"] = new[] { "Maria", "Ana" },
                ["HR"] = new[] { "Ivana", "Marija", "Martina" }, ["PT"] = new[] { "Maria", "Ana" },
            };
            static string Plain(string s) => new string(s.Normalize(System.Text.NormalizationForm.FormD).Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
            foreach (var kv in female)
                foreach (var n in kv.Value) Assert.DoesNotContain(NameBank.Get(kv.Key).FirstNames.Names, x => Plain(x) == n); // (María neither)
            // the common men's names are
            Assert.Contains("José", NameBank.Get("ES").FirstNames.Names);
            Assert.Contains("Giuseppe", NameBank.Get("IT").FirstNames.Names);
            Assert.Contains("Ivan", NameBank.Get("HR").FirstNames.Names);
            Assert.Contains("Andrea", NameBank.Get("IT").FirstNames.Names); // (recorded for both sexes, mostly men's in Italy)
        }

        [Fact]
        public void AccentsAndSpecialCharactersAreKept()
        {
            string All(string code, bool first) => string.Join(" ", (first ? NameBank.Get(code).FirstNames : NameBank.Get(code).Surnames).Names);
            Assert.Contains("João", All("BR", true));
            Assert.Contains("Gonçalves", All("BR", false));
            Assert.Contains("Müller", All("DE", false));
            Assert.Contains("Jürgen", All("DE", true));
            Assert.Contains("Sébastien", All("FR", true));
            Assert.Contains("Muñoz", All("ES", false));
            // the accents the dataset lacks, put back where Wikipedia confirms them (tools/names/restore_accents.py)
            Assert.Contains("García", NameBank.Get("ES").Surnames.Names);
            Assert.Contains("López", NameBank.Get("ES").Surnames.Names);
            Assert.DoesNotContain("Garcia", NameBank.Get("ES").Surnames.Names);
            Assert.Contains("José", NameBank.Get("BR").FirstNames.Names);
            Assert.Contains("François", NameBank.Get("FR").FirstNames.Names);
            // Irish and Scottish spellings
            Assert.Contains("O'Brien", NameBank.Get("IE").Surnames.Names);
            Assert.Contains("McDonald", NameBank.Get("GB").Surnames.Names);
            Assert.DoesNotContain("Obrien", NameBank.Get("IE").Surnames.Names);
            Assert.Contains("Kovačević", All("HR", false));
            Assert.Contains("Željko", All("HR", true));
            // and they come out of the generator as they went in
            var g = new PlayerNameGenerator(5);
            var seen = new HashSet<string>();
            for (int i = 0; i < 400; i++) { var n = g.Next("DE"); seen.Add(n.First + " " + n.Last); }
            Assert.Contains(seen, s => s.Any(ch => ch > 127));
        }

        [Fact]
        public void TheTablesHoldOnlyCleanNames()
        {
            foreach (var c in NameBank.Countries)
                foreach (var list in new[] { c.FirstNames, c.Surnames })
                {
                    Assert.Equal(list.Count, list.Names.Distinct().Count()); // (no name twice in a list)
                    foreach (var n in list.Names)
                    {
                        Assert.False(string.IsNullOrWhiteSpace(n));
                        Assert.Equal(n.Trim(), n);
                        Assert.True(n.All(ch => char.IsLetter(ch) || ch == ' ' || ch == '\'' || ch == '-' || ch == '.' || ch == '’'), c.Code + ": '" + n + "'");
                        Assert.True(n.Any(char.IsLetter));
                    }
                }
        }

        [Fact]
        public void ImmigrantNamesAreFilteredFromTheLocalLists()
        {
            foreach (var code in new[] { "ES", "DE", "FR", "IT", "GB", "NL", "PT", "BR", "AR" })
            {
                var c = NameBank.Get(code);
                foreach (var n in new[] { "Mohamed", "Ahmed", "Ali", "Mehmet", "Hassan" }) Assert.DoesNotContain(n, c.FirstNames.Names);
                foreach (var n in new[] { "Singh", "Ali", "Yilmaz", "Nguyen", "Khan", "Mohamed", "Hassan", "Diallo" }) Assert.DoesNotContain(n, c.Surnames.Names);
            }
            // but a name shared by neighbours stays (Antonio in Spain, Jan in the Netherlands)
            Assert.Contains("Antonio", NameBank.Get("ES").FirstNames.Names);
            Assert.Contains("Jan", NameBank.Get("NL").FirstNames.Names);
            // and a man's surname is a man's: no Kowalska in Poland, no Ivanova in Russia
            Assert.DoesNotContain(NameBank.Get("PL").Surnames.Names, n => n.EndsWith("ska"));
            Assert.DoesNotContain(NameBank.Get("RU").Surnames.Names, n => n.EndsWith("ova"));
        }

        [Fact]
        public void AFirstNameIsNeverAlsoTheSurname()
        {
            var g = new PlayerNameGenerator(3);
            for (int i = 0; i < 3000; i++) { var n = g.Next("BR"); Assert.NotEqual(n.First, n.Last, StringComparer.OrdinalIgnoreCase); }
        }

        [Fact]
        public void CommonNamesComeUpMoreOftenThanRareOnes()
        {
            // the weights follow the frequency: the most common Spanish surname (García) is about as likely as its weight says
            var es = NameBank.Get("ES");
            var rng = new Rng(77);
            var counts = new int[es.Surnames.Count];
            const int draws = 60000;
            for (int i = 0; i < draws; i++) counts[es.Surnames.PickIndex(rng)]++;
            double expectedTop = (double)es.Surnames.WeightOf(0) / es.Surnames.TotalWeight * draws;
            _out.WriteLine($"top surname {es.Surnames[0]}: {counts[0]} draws, expected {expectedTop:0}; rarest {es.Surnames[es.Surnames.Count - 1]}: {counts[counts.Length - 1]}");
            Assert.InRange(counts[0], expectedTop * 0.85, expectedTop * 1.15);
            Assert.True(counts[0] > 5 * counts.Skip(es.Surnames.Count / 2).Average(), "the common name beats the rare ones several times over");
            Assert.True(es.Surnames.WeightOf(0) > es.Surnames.WeightOf(es.Surnames.Count - 1));
            // not an even pick: the top ten surnames take far more than 10/N of the draws
            Assert.True(counts.Take(10).Sum() > draws * 10.0 / es.Surnames.Count * 3);
            // and the rarer ones are still reached: every name that should come up ten times or more does
            for (int i = 0; i < counts.Length; i++)
                if ((double)es.Surnames.WeightOf(i) / es.Surnames.TotalWeight * draws >= 10) Assert.True(counts[i] > 0, es.Surnames[i]);
        }

        [Fact]
        public void ANameListWithoutWeightsPicksEvenly()
        {
            var list = WeightedNameList.Parse("Aaa;Bbb;Ccc;Ddd");
            var rng = new Rng(3);
            var counts = new int[4];
            for (int i = 0; i < 8000; i++) counts[list.PickIndex(rng)]++;
            foreach (var c in counts) Assert.InRange(c, 1700, 2300);
        }

        [Fact]
        public void NoFullNameIsGivenTwiceInAPlayerDatabase()
        {
            var g = new PlayerNameGenerator(2026);
            var all = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var mix = NationMix.League("ES");
            for (int i = 0; i < 6000; i++)
            {
                var n = g.Next(mix);
                Assert.True(all.Add(n.First + " " + n.Last), "duplicate: " + n.First + " " + n.Last);
            }
            // even a country with few names runs through its combinations without a repeat
            var small = new PlayerNameGenerator(1);
            BankWith("ZZ", "Zedland", "Al;Bo;Cy", "Xa;Xb;Xc", out string code);
            var seen = new HashSet<string>();
            for (int i = 0; i < 9; i++) { var n = small.Next(code); Assert.True(seen.Add(n.First + " " + n.Last)); }
            Assert.Throws<InvalidOperationException>(() => small.Next(code)); // (all nine taken)
        }

        [Fact]
        public void NamesAlreadyInTheDatabaseAreNeverGivenAgain()
        {
            var g = new PlayerNameGenerator(9);
            var c = NameBank.Get("NL");
            // reserve every combination of the 5 most common first names with all surnames except one
            var keep = c.Surnames[0];
            for (int f = 0; f < 5; f++)
                for (int l = 1; l < c.Surnames.Count; l++) g.Reserve(c.FirstNames[f], c.Surnames[l]);
            for (int i = 0; i < 300; i++)
            {
                var n = g.Next("NL");
                Assert.False(Array.IndexOf(c.FirstNames.Names.Take(5).ToArray(), n.First) >= 0 && n.Last != keep);
            }
        }

        [Fact]
        public void TheSameSeedMakesTheSameNames()
        {
            List<string> Run(ulong seed)
            {
                var g = new PlayerNameGenerator(seed);
                var mix = NationMix.League("DE");
                return Enumerable.Range(0, 80).Select(_ => { var n = g.Next(mix); return n.First + " " + n.Last + " (" + n.Nationality + ")"; }).ToList();
            }
            Assert.Equal(Run(31), Run(31));
            Assert.NotEqual(Run(31), Run(32));
        }

        [Fact]
        public void NamesFitTheirCountry()
        {
            // a Spanish player never has a German surname, a German one never a Brazilian first name: the lists are the country's own
            var g = new PlayerNameGenerator(12);
            foreach (var code in Wanted)
            {
                var c = NameBank.Get(code);
                for (int i = 0; i < 60; i++)
                {
                    var n = g.Next(code);
                    Assert.Contains(n.First, c.FirstNames.Names);
                    Assert.Contains(n.Last, c.Surnames.Names);
                }
            }
            // a mixed league: mostly the home country, some foreigners, each with a name of his own country
            var mixed = new PlayerNameGenerator(4);
            var nations = new Dictionary<string, int>();
            for (int i = 0; i < 1200; i++)
            {
                var n = mixed.Next(NationMix.League("IT"));
                nations[n.Nationality] = nations.GetValueOrDefault(n.Nationality) + 1;
                Assert.Contains(n.First, NameBank.Get(n.Nationality).FirstNames.Names);
            }
            _out.WriteLine(string.Join(", ", nations.OrderByDescending(kv => kv.Value).Select(kv => kv.Key + " " + kv.Value)));
            Assert.True(nations["Italy"] > 400 && nations.Count >= 8);
        }

        [Fact]
        public void ACountryIsAddedWithoutTouchingTheCode()
        {
            NameBank.Register("QQ", "Qland", "Quin|10;Quill|5", "Quarrel|7;Quist|3;Quade|3", "test country");
            var c = NameBank.Get("Qland");
            Assert.Equal("QQ", c.Code);
            Assert.Equal(2, c.FirstNames.Count);
            var n = new PlayerNameGenerator(1).Next("qq");
            Assert.Contains(n.First, new[] { "Quin", "Quill" });
        }

        [Fact]
        public void ASquadIsNamedWithoutChangingTheRestOfThePlayers()
        {
            var team = DemoTeams.Red(500);
            var before = team.Starters.Concat(team.Bench).Select(p => (p.Id, p.ShirtNumber, p.Age, p.HeightCm, p.Attributes.Speed, p.Origin, p.Position)).ToList();
            var g = new PlayerNameGenerator(8);
            g.NameSquad(team, NationMix.League("GB"));
            var after = team.Starters.Concat(team.Bench).Select(p => (p.Id, p.ShirtNumber, p.Age, p.HeightCm, p.Attributes.Speed, p.Origin, p.Position)).ToList();
            Assert.Equal(before, after);
            Assert.All(team.Starters.Concat(team.Bench), p => { Assert.False(string.IsNullOrEmpty(p.FirstName)); Assert.False(string.IsNullOrEmpty(p.Nationality)); Assert.True(g.IsTaken(p.FirstName, p.LastName)); });
            Assert.Equal(21, g.Count); // 20 players and the manager
            Assert.False(string.IsNullOrEmpty(team.Manager));
            // two teams of one match with one generator: all 42 names differ
            var a = DemoTeams.Red(501); var b = DemoTeams.Blue(501);
            var gen = new PlayerNameGenerator(501);
            gen.NameSquad(a, NationMix.League("ES")); gen.NameSquad(b, NationMix.League("ES"));
            var full = a.Starters.Concat(a.Bench).Concat(b.Starters).Concat(b.Bench).Select(p => p.FullName).ToList();
            Assert.Equal(full.Count, full.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        [Fact]
        public void TheDemoSquadsAndTheirNamesAreAsTheyWere()
        {
            // the Israeli name database still names the demo squads, with the same seed the same players
            var a = DemoTeams.Red(1001);
            var names = string.Join("; ", a.Starters.Select(p => p.FullName + "/" + p.Nationality));
            _out.WriteLine(names);
            Assert.Equal(ExpectedRed1001, names);
        }

        [Fact]
        public void TheAttributionIsInTheCodeAndInTheNotices()
        {
            Assert.Equal("Names data from Onomaverse (https://onomaverse.com/datasets), licensed CC BY 4.0.", NameAttribution.Notice);
            Assert.Contains("CC BY 4.0", NameAttribution.License);
            Assert.Contains("onomaverse.com/datasets", NameAttribution.Citation);
            Assert.Equal("v2026.10", OnomaverseNameData.Release);
            // a country with few names says so
            Assert.Contains(NameBank.Countries, c => c.Note.StartsWith("Thin"));
            // the croatian surnames are said to be a supplement, not Onomaverse data
            Assert.Contains("supplementary", NameBank.Get("HR").Note);
            string dir = AppContext.BaseDirectory;
            string root = null;
            for (var d = new System.IO.DirectoryInfo(dir); d != null; d = d.Parent) if (System.IO.File.Exists(System.IO.Path.Combine(d.FullName, "THIRD_PARTY_NOTICES.md"))) { root = d.FullName; break; }
            Assert.NotNull(root);
            string notices = System.IO.File.ReadAllText(System.IO.Path.Combine(root, "THIRD_PARTY_NOTICES.md"));
            Assert.Contains(NameAttribution.Notice, notices);
            Assert.Contains("creativecommons.org/licenses/by/4.0", notices);
        }

        private static void BankWith(string code, string nation, string first, string last, out string key)
        {
            NameBank.Register(code, nation, first, last);
            key = code;
        }

        private const string ExpectedRed1001 = "Doriel Cohen/Israel; Gideon Tadin/Israel; Nazir Abdul/Israel; Diego Benitez/Spain; Afework Mengistu/Israel; Fermin Castillo/Spain; Emilio Alba/Spain; Amit Itzhaki/Israel; Ofir Tavori/Israel; Dotan Shemesh/Israel; Salah Mahdi/Israel";
    }
}

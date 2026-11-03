using System;
using System.Collections.Generic;
using System.Globalization;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// Where the player names come from, and what the licence asks for. Keep this text (or the same words) wherever the names are shown
    /// or shipped - in the credits of the game, on the web page.
    /// </summary>
    public static class NameAttribution
    {
        public const string Notice = "Names data from Onomaverse (https://onomaverse.com/datasets), licensed CC BY 4.0.";
        public const string License = "Creative Commons Attribution 4.0 International (CC BY 4.0), https://creativecommons.org/licenses/by/4.0/";
        public const string Citation = "The Onomaverse Team. Onomaverse Names Datasets (v2026.10). https://onomaverse.com/datasets. Licensed CC BY 4.0.";
        public const string Source = "https://github.com/onomaverse/datasets";
        public const string Changes = "Changed: only male given names and the most common names of each country were kept; each given name's weight is its share of the "
            + "country's records (for a name recorded for both sexes, times the share of its bearers that are male); names not written in the Latin script were left out.";
    }

    /// <summary>The raw table of one country, as the generated data file holds it: "name|weight;name|weight".</summary>
    public sealed class OnomaverseCountry
    {
        public readonly string Code, Nationality, FirstNames, Surnames, Note;
        public OnomaverseCountry(string code, string nationality, string firstNames, string surnames, string note = "")
        { Code = code; Nationality = nationality; FirstNames = firstNames; Surnames = surnames; Note = note; }
    }

    /// <summary>
    /// A list of names with their weights, for picking one at random in proportion to how common it is: the cumulative weights and a
    /// binary search (the first name of a country's list is as likely as its weight says, not 1 in N).
    /// </summary>
    public sealed class WeightedNameList
    {
        private readonly string[] _names;
        private readonly uint[] _weights;
        private readonly ulong[] _cumulative;
        public readonly ulong TotalWeight;

        public WeightedNameList(IList<KeyValuePair<string, uint>> entries)
        {
            _names = new string[entries.Count];
            _weights = new uint[entries.Count];
            _cumulative = new ulong[entries.Count];
            ulong sum = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                _names[i] = entries[i].Key;
                _weights[i] = Math.Max(1u, entries[i].Value);
                sum += _weights[i];
                _cumulative[i] = sum;
            }
            TotalWeight = sum;
        }

        /// <summary>Parses "name|weight;name|weight". A name with no weight counts 1 (a list without frequency data picks evenly).</summary>
        public static WeightedNameList Parse(string table)
        {
            var list = new List<KeyValuePair<string, uint>>();
            foreach (var item in (table ?? "").Split(';'))
            {
                if (item.Length == 0) continue;
                int bar = item.LastIndexOf('|');
                uint w = 1;
                string name = item;
                if (bar > 0 && uint.TryParse(item.Substring(bar + 1), NumberStyles.None, CultureInfo.InvariantCulture, out uint parsed)) { name = item.Substring(0, bar); w = parsed; }
                list.Add(new KeyValuePair<string, uint>(name, w));
            }
            return new WeightedNameList(list);
        }

        public int Count => _names.Length;
        public string this[int i] => _names[i];
        public uint WeightOf(int i) => _weights[i];
        public IReadOnlyList<string> Names => _names;

        /// <summary>Index of a random name, in proportion to the weights.</summary>
        public int PickIndex(Rng rng)
        {
            if (_names.Length == 0) throw new InvalidOperationException("The name list is empty.");
            ulong r = ((ulong)rng.NextUInt() << 16 ^ rng.NextUInt()) % TotalWeight; // (more than 32 bits, so the lighter names are reached too)
            int lo = 0, hi = _cumulative.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (r < _cumulative[mid]) hi = mid; else lo = mid + 1;
            }
            return lo;
        }

        public string Pick(Rng rng) => _names[PickIndex(rng)];
    }

    /// <summary>One country's names: its first names (male) and its surnames, in two lists that never mix.</summary>
    public sealed class CountryNames
    {
        public readonly string Code, Nationality, Note;
        public readonly WeightedNameList FirstNames, Surnames;

        public CountryNames(string code, string nationality, WeightedNameList firstNames, WeightedNameList surnames, string note = "")
        { Code = code; Nationality = nationality; FirstNames = firstNames; Surnames = surnames; Note = note ?? ""; }

        /// <summary>How many different full names the country can make.</summary>
        public long Combinations => (long)FirstNames.Count * Surnames.Count;
    }

    /// <summary>
    /// The name tables by country. Look a country up by its ISO code ("ES"), or by the nationality as the game writes it ("Spain");
    /// the codes and nationalities are case-insensitive. The tables of the Onomaverse dataset are loaded the first time they are
    /// asked for. A country is added with <see cref="Register"/> (from any list of names, with or without weights) or, for more of
    /// the dataset, by adding its line to tools/names/build_onomaverse_names.py and running it.
    /// </summary>
    public static class NameBank
    {
        private static readonly object Lock = new object();
        private static readonly Dictionary<string, CountryNames> ByKey = new Dictionary<string, CountryNames>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<CountryNames> All = new List<CountryNames>();
        private static bool _loaded;

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            lock (Lock)
            {
                if (_loaded) return;
                foreach (var c in OnomaverseNameData.Countries)
                    Add(new CountryNames(c.Code, c.Nationality, WeightedNameList.Parse(c.FirstNames), WeightedNameList.Parse(c.Surnames), c.Note));
                _loaded = true;
            }
        }

        private static void Add(CountryNames c)
        {
            if (c.FirstNames.Count == 0 || c.Surnames.Count == 0) throw new ArgumentException("A country needs at least one first name and one surname: " + c.Code);
            if (ByKey.TryGetValue(c.Code, out var old)) All.Remove(old);
            All.Add(c);
            ByKey[c.Code] = c;
            ByKey[c.Nationality] = c;
        }

        /// <summary>Adds (or replaces) a country. Tables are "name|weight;name|weight", or just "name;name" for an even pick.</summary>
        public static void Register(string code, string nationality, string firstNames, string surnames, string note = "")
        {
            EnsureLoaded();
            lock (Lock) Add(new CountryNames(code, nationality, WeightedNameList.Parse(firstNames), WeightedNameList.Parse(surnames), note));
        }

        public static IReadOnlyList<CountryNames> Countries { get { EnsureLoaded(); return All; } }

        public static bool TryGet(string codeOrNationality, out CountryNames country)
        {
            EnsureLoaded();
            country = null;
            return !string.IsNullOrEmpty(codeOrNationality) && ByKey.TryGetValue(codeOrNationality.Trim(), out country);
        }

        public static CountryNames Get(string codeOrNationality)
        {
            if (!TryGet(codeOrNationality, out var c)) throw new KeyNotFoundException("No names for the country '" + codeOrNationality + "'.");
            return c;
        }
    }
}

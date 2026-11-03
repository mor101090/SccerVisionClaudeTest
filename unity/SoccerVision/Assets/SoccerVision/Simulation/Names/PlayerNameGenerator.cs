using System;
using System.Collections.Generic;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// Makes the names of male football players: a first name and a surname of the same country, each picked in proportion to how
    /// common it is there, and never a full name that the generator has already given out (or been told about with
    /// <see cref="Reserve"/>). Use one generator for one player database; the same seed gives the same names, on every runtime.
    /// The names are written exactly as the dataset has them - accents and special characters are kept.
    /// </summary>
    public sealed class PlayerNameGenerator
    {
        private readonly Rng _rng;
        private readonly HashSet<string> _used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public PlayerNameGenerator(ulong seed) { _rng = new Rng(seed, 0x4E414D4553UL); }

        /// <summary>How many full names have been given out (or reserved).</summary>
        public int Count => _used.Count;

        private static string Key(string first, string last) => first + " " + last;
        private static bool Same(string first, string last) => string.Equals(first, last, StringComparison.OrdinalIgnoreCase);

        /// <summary>Marks a name as taken - for the players that already exist, so a new one never repeats theirs.</summary>
        public bool Reserve(string firstName, string lastName) => _used.Add(Key(firstName, lastName));

        public bool IsTaken(string firstName, string lastName) => _used.Contains(Key(firstName, lastName));

        /// <summary>A new full name for a player of this country (ISO code or nationality, e.g. "BR" or "Brazil").</summary>
        public (string First, string Last) Next(string country)
        {
            var c = NameBank.Get(country);
            // random picks first; a country with few names may repeat itself, so after a while walk through the combinations in order
            for (int tries = 0; tries < 60; tries++)
            {
                string f = c.FirstNames.Pick(_rng), l = c.Surnames.Pick(_rng);
                if (Same(f, l)) continue; // (no Bruno Bruno)
                if (_used.Add(Key(f, l))) return (f, l);
            }
            int fi = _rng.Range(0, c.FirstNames.Count), li = _rng.Range(0, c.Surnames.Count);
            for (long k = 0; k < c.Combinations; k++)
            {
                string f = c.FirstNames[(int)((fi + k) % c.FirstNames.Count)], l = c.Surnames[(int)((li + k / c.FirstNames.Count) % c.Surnames.Count)];
                if (!Same(f, l) && _used.Add(Key(f, l))) return (f, l);
            }
            throw new InvalidOperationException("All " + c.Combinations + " full names of " + c.Nationality + " are taken.");
        }

        /// <summary>A name for a player of a country picked from a mix of nationalities (the weights need not add up to 1).</summary>
        public (string First, string Last, string Nationality) Next(NationMix mix)
        {
            var c = NameBank.Get(mix.Pick(_rng));
            var n = Next(c.Code);
            return (n.First, n.Last, c.Nationality);
        }

        /// <summary>
        /// Names a squad: starters, bench and the manager of <paramref name="team"/>, each from a country of the mix. Only the names and
        /// the nationality are set - the player's ability, looks (<see cref="PlayerProfile.Origin"/>) and everything else stay as they are.
        /// Use one generator for both teams of a match (or the whole league) so no full name appears twice.
        /// </summary>
        public void NameSquad(TeamSetup team, NationMix mix)
        {
            foreach (var p in team.Starters) Name(p, mix);
            foreach (var p in team.Bench) Name(p, mix);
            var m = Next(mix);
            team.Manager = Key(m.First, m.Last);
        }

        private void Name(PlayerProfile p, NationMix mix)
        {
            var n = Next(mix);
            p.FirstName = n.First;
            p.LastName = n.Last;
            p.Nationality = n.Nationality;
        }
    }

    /// <summary>A weighted choice of nationalities: "ES" 3, "BR" 2, "FR" 1 - a Spanish player is three times as likely as a French one.</summary>
    public sealed class NationMix
    {
        private readonly List<string> _countries = new List<string>();
        private readonly List<double> _weights = new List<double>();
        private double _total;

        public NationMix Add(string country, double weight)
        {
            NameBank.Get(country); // (unknown countries fail here, not in the middle of a match)
            if (weight <= 0) throw new ArgumentOutOfRangeException(nameof(weight));
            _countries.Add(country); _weights.Add(weight); _total += weight;
            return this;
        }

        public string Pick(Rng rng)
        {
            if (_countries.Count == 0) throw new InvalidOperationException("The nationality mix is empty.");
            double r = rng.NextFloat() * _total;
            for (int i = 0; i < _countries.Count; i++) { r -= _weights[i]; if (r < 0) return _countries[i]; }
            return _countries[_countries.Count - 1];
        }

        public static NationMix Of(string country) => new NationMix().Add(country, 1);

        /// <summary>A European league's squads: mostly the home country, the rest from the big football nations.</summary>
        public static NationMix League(string home) => new NationMix().Add(home, 6).Add("ES", 1).Add("FR", 1).Add("BR", 1.2).Add("AR", 0.8).Add("PT", 0.7)
            .Add("DE", 0.7).Add("IT", 0.6).Add("NL", 0.5).Add("HR", 0.3).Add("GB", 0.6);

        /// <summary>Nations in the way of a world cup: every country the game has names for, evenly.</summary>
        public static NationMix World()
        {
            var m = new NationMix();
            foreach (var c in NameBank.Countries) m.Add(c.Code, 1);
            return m;
        }
    }
}

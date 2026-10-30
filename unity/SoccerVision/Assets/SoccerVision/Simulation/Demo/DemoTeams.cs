using System;
using System.Collections.Generic;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// Fictional demo squads (red vs blue, both 4-4-2). The players are made by the team generator (data/teams, see
    /// GeneratedTeams.cs): some are stronger, faster, taller, left- or right-footed, and the engine plays them that way.
    /// <see cref="Create"/> still makes a squad of seeded, role-appropriate players.
    /// </summary>
    public static class DemoTeams
    {
        private static readonly string[] FirstNames =
        {
            "Adrian", "Bruno", "Cael", "Dario", "Emil", "Felix", "Goran", "Hugo", "Ivo", "Jonas", "Kai", "Leon",
            "Marek", "Nilo", "Oren", "Pavel", "Quinn", "Rafa", "Soren", "Tomas", "Uri", "Viktor", "Wes", "Yanis",
            "Zeke", "Amir", "Basil", "Cyrus", "Dov", "Elio", "Fabio", "Gil", "Haris", "Idan", "Joel", "Kobi",
        };

        private static readonly string[] LastNames =
        {
            "Arvel", "Brakko", "Castell", "Dravin", "Eskand", "Falcor", "Grenit", "Holvar", "Istrom", "Jarvio",
            "Kestrel", "Lunden", "Morvak", "Nerith", "Ostrand", "Pellar", "Quorin", "Rastov", "Selvane", "Tarvik",
            "Ulmar", "Varkas", "Wendor", "Yarrow", "Zalvo", "Aldane", "Borin", "Corvel", "Dunmar", "Elric",
            "Fendar", "Galvin", "Harrow", "Ilvane", "Jestin", "Korval", "Lorcan", "Mirren", "Novak", "Orsini",
        };

        private static TeamSetup FromGenerated(string name, string shortName, KitColors kit, (List<PlayerProfile> Starters, List<PlayerProfile> Bench) squad)
        {
            var t = new TeamSetup { Name = name, ShortName = shortName, Kit = kit, Formation = Formation.F442(), Tactics = new TeamTactics() };
            t.Starters.AddRange(squad.Starters);
            t.Bench.AddRange(squad.Bench);
            return t;
        }

        /// <summary>Red Rovers defend zonally and tackle normally.</summary>
        public static TeamSetup Red(ulong seed = 1001)
        {
            var t = FromGenerated("Red Rovers", "RED", new KitColors
            {
                Primary = "#D32F2F",
                Secondary = "#1A1A1A", // the edge of the player marker (black, as in the design)
                Number = "#FFFFFF",
                GoalkeeperPrimary = "#F9D71C",
                GoalkeeperNumber = "#1A1A1A",
            }, GeneratedTeams.Red());
            t.Manager = "Daniel Harper";
            t.Tactics.Marking = MarkingStyle.Zonal;
            t.Tactics.Tackling = TacklingStyle.Normal;
            return t;
        }

        /// <summary>Blue Falcons mark man-to-man and get stuck in (more slide tackles).</summary>
        public static TeamSetup Blue(ulong seed = 2002)
        {
            var t = FromGenerated("Blue Falcons", "BLU", new KitColors
            {
                Primary = "#1E5BC6",
                Secondary = "#1A1A1A", // the edge of the player marker (black, as in the design)
                Number = "#FFFFFF",
                GoalkeeperPrimary = "#2E9E4F",
                GoalkeeperNumber = "#FFFFFF",
            }, GeneratedTeams.Blue());
            t.Manager = "Marco Lindqvist";
            t.Tactics.Marking = MarkingStyle.ManToMan;
            t.Tactics.Tackling = TacklingStyle.GetStuckIn;
            return t;
        }

        private static readonly int[] StarterNumbers = { 1, 2, 4, 5, 3, 7, 8, 6, 11, 9, 10 };

        /// <param name="namePool">0 or 1: each demo team draws surnames from its own half of the list (no duplicates across teams).</param>
        public static TeamSetup Create(string name, string shortName, ulong seed, int namePool, KitColors kit)
        {
            var rng = new Rng(seed, 77);
            var setup = new TeamSetup
            {
                Name = name,
                ShortName = shortName,
                Kit = kit,
                Formation = Formation.F442(),
                Tactics = new TeamTactics(),
            };
            var usedNames = new HashSet<string>();
            int id = (int)(seed % 1000) * 100;
            for (int i = 0; i < 11; i++)
            {
                var slot = setup.Formation.Slots[i];
                setup.Starters.Add(CreatePlayer(rng, ++id, StarterNumbers[i], slot, usedNames, namePool));
            }
            // Bench: GK, two defenders, two midfielders, two forwards.
            var bench = new[] { 0, 2, 4, 6, 8, 9, 10 };
            for (int i = 0; i < bench.Length; i++)
            {
                var slot = setup.Formation.Slots[bench[i]];
                var p = CreatePlayer(rng, ++id, 12 + i, slot, usedNames, namePool);
                setup.Bench.Add(p);
            }
            return setup;
        }

        private static PlayerProfile CreatePlayer(Rng rng, int id, int number, FormationSlot slot, HashSet<string> usedNames, int namePool)
        {
            string first, last;
            int guard = 0;
            int half = LastNames.Length / 2;
            do
            {
                first = FirstNames[rng.Range(0, FirstNames.Length)];
                last = LastNames[(namePool & 1) * half + rng.Range(0, half)];
            } while (!usedNames.Add(last) && guard++ < 50);

            var a = new PlayerAttributes();
            int R(float mean, float sd = 7f) => MathUtil.Clamp((int)MathF.Round(rng.Gaussian(mean, sd)), 15, 95);

            PositionGroup group;
            switch (slot.Line)
            {
                case Line.Goalkeeper: group = PositionGroup.Goalkeeper; break;
                case Line.Defence: group = PositionGroup.Defender; break;
                case Line.Midfield: group = PositionGroup.Midfielder; break;
                default: group = PositionGroup.Forward; break;
            }

            // Baseline for everyone.
            a.Aggression = R(55, 12); a.Teamwork = R(62); a.Composure = R(60); a.Decisions = R(60);
            a.Tackling = R(45); a.Marking = R(45); a.Positioning = R(55);
            a.Creativity = R(52); a.Passing = R(58); a.Crossing = R(50);
            a.Finishing = R(45); a.LongShots = R(48); a.OffTheBall = R(52);
            a.PhysicalPower = R(62); a.BallControl = R(60); a.Dribbling = R(55); a.Acceleration = R(64); a.Speed = R(64);
            a.Jumping = R(60); a.Heading = R(52); a.Stamina = R(68); a.FreeKicks = R(45); a.Penalty = R(52);
            a.AerialAbility = R(18, 5); a.Handling = R(18, 5); a.Kicking = R(25, 6); a.OneOnOne = R(18, 5); a.Reflexes = R(20, 5);
            int height = (int)MathF.Round(rng.Gaussian(178f, 6.5f));

            if (slot.Line == Line.Goalkeeper)
            {
                a.AerialAbility = R(70); a.Handling = R(70); a.Kicking = R(66); a.OneOnOne = R(68); a.Reflexes = R(72);
                a.Positioning = R(68); a.Decisions = R(64); a.Jumping = R(68); a.Speed = R(48); a.Acceleration = R(55);
                a.Passing = R(48); a.Finishing = R(15, 4); a.Dribbling = R(30); a.Stamina = R(60);
                height = (int)MathF.Round(rng.Gaussian(189f, 4f));
            }
            else if (slot.IsCentreBack)
            {
                a.Tackling = R(70); a.Marking = R(70); a.Positioning = R(68); a.Heading = R(70); a.Jumping = R(68);
                a.PhysicalPower = R(72); a.Speed = R(60); a.Acceleration = R(58); a.Passing = R(56); a.Dribbling = R(45);
                a.Finishing = R(35); a.Aggression = R(62, 10);
                height = (int)MathF.Round(rng.Gaussian(190f, 4.5f));
            }
            else if (slot.IsFullBack)
            {
                a.Tackling = R(64); a.Marking = R(62); a.Positioning = R(62); a.Speed = R(72); a.Acceleration = R(72);
                a.Crossing = R(62); a.Stamina = R(76); a.Dribbling = R(58); a.Passing = R(58);
            }
            else if (slot.IsCentralMidfielder)
            {
                a.Passing = R(70); a.Creativity = R(64); a.Decisions = R(66); a.Composure = R(64); a.Stamina = R(76);
                a.Tackling = R(58); a.Marking = R(56); a.LongShots = R(60); a.BallControl = R(68); a.Teamwork = R(68);
                a.FreeKicks = R(58); a.Finishing = R(52);
            }
            else if (slot.IsWideMidfielder)
            {
                a.Speed = R(74); a.Acceleration = R(74); a.Dribbling = R(70); a.Crossing = R(68); a.BallControl = R(68);
                a.Creativity = R(62); a.Finishing = R(56); a.LongShots = R(56); a.OffTheBall = R(62); a.Stamina = R(72);
                a.FreeKicks = R(55);
            }
            else
            {
                a.Finishing = R(70); a.OffTheBall = R(68); a.Composure = R(64); a.Heading = R(64); a.Jumping = R(64);
                a.Speed = R(70); a.Acceleration = R(70); a.Dribbling = R(64); a.BallControl = R(66); a.LongShots = R(58);
                a.Penalty = R(66); a.PhysicalPower = R(66); a.Tackling = R(35); a.Marking = R(32);
                height = (int)MathF.Round(rng.Gaussian(183f, 7f)); // a mix of target men and small quick forwards
            }

            // Foot preference: left-sided players are more often left-footed.
            bool leftSide = slot.BaseLateral > 10f;
            float pLeft = leftSide ? 0.55f : 0.18f;
            float r = rng.NextFloat();
            if (r < 0.05f)
            {
                a.LeftFoot = 3; a.RightFoot = 3;
            }
            else if (rng.Chance(pLeft))
            {
                a.LeftFoot = 3; a.RightFoot = rng.Chance(0.3f) ? 2 : 1;
            }
            else
            {
                a.RightFoot = 3; a.LeftFoot = rng.Chance(0.3f) ? 2 : 1;
            }
            float fit = rng.NextFloat();
            a.Fitness = fit < 0.15f ? FitnessLevel.Low : (fit < 0.75f ? FitnessLevel.Normal : FitnessLevel.High);

            return new PlayerProfile
            {
                Id = id,
                FirstName = first,
                LastName = last,
                ShirtNumber = number,
                NaturalPosition = group,
                HeightCm = MathUtil.Clamp(height, 165, 200),
                Attributes = a,
            };
        }
    }
}

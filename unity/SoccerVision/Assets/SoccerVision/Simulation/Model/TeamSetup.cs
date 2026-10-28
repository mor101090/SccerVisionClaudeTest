using System;
using System.Collections.Generic;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// Team-wide sliders from the Tactics System section of the design document. All values are 0..1,
    /// 0.5 is the balanced default. The live tactics panel will edit these during a match.
    /// </summary>
    [Serializable]
    /// <summary>
    /// How the team marks without the ball. Zonal: each player defends an area and takes whoever enters it, handing him
    /// over to the next zone. Man-to-man: each defender and midfielder is given one opponent and follows him.
    /// </summary>
    public enum MarkingStyle { Zonal, ManToMan }

    /// <summary>How readily players go to ground: stay on their feet, normal, or get stuck in (slide tackles).</summary>
    public enum TacklingStyle { StayOnFeet, Normal, GetStuckIn }

    public sealed class TeamTactics
    {
        /// <summary>0 = defensive, 1 = ultra attacking.</summary>
        public float Mentality = 0.5f;
        /// <summary>0 = deep block, 1 = high line / high press.</summary>
        public float DefensiveLine = 0.5f;
        /// <summary>0 = narrow, 1 = wide.</summary>
        public float Width = 0.5f;
        /// <summary>0 = slow build-up, 1 = fast and direct.</summary>
        public float Tempo = 0.5f;
        /// <summary>0 = passive, 1 = aggressive.</summary>
        public float PressingIntensity = 0.5f;
        public MarkingStyle Marking = MarkingStyle.Zonal;
        public TacklingStyle Tackling = TacklingStyle.Normal;

        public TeamTactics Clone() => (TeamTactics)MemberwiseClone();

        public void Sanitize()
        {
            Mentality = MathUtil.Clamp01(Mentality);
            DefensiveLine = MathUtil.Clamp01(DefensiveLine);
            Width = MathUtil.Clamp01(Width);
            Tempo = MathUtil.Clamp01(Tempo);
            PressingIntensity = MathUtil.Clamp01(PressingIntensity);
        }
    }

    [Serializable]
    public sealed class KitColors
    {
        public string Primary = "#D32F2F";
        public string Secondary = "#FFFFFF";
        public string Number = "#FFFFFF";
        public string GoalkeeperPrimary = "#FBC02D";
        public string GoalkeeperNumber = "#000000";
        /// <summary>Goalkeeper's trim (empty: the team's).</summary>
        public string GoalkeeperSecondary = "";
    }

    /// <summary>Everything the engine needs to field a team.</summary>
    [Serializable]
    public sealed class TeamSetup
    {
        public string Name = "Team";
        public string ShortName = "TEA";
        public KitColors Kit = new KitColors();
        public Formation Formation = Formation.F442();
        public TeamTactics Tactics = new TeamTactics();
        /// <summary>Exactly 11 players; index i plays formation slot i (index 0 is the goalkeeper).</summary>
        public List<PlayerProfile> Starters = new List<PlayerProfile>();
        public List<PlayerProfile> Bench = new List<PlayerProfile>();

        public void Validate()
        {
            if (Formation == null) throw new ArgumentException(Name + ": formation missing");
            if (Starters == null || Starters.Count != 11) throw new ArgumentException(Name + ": exactly 11 starters required");
            var numbers = new HashSet<int>();
            foreach (var p in Starters)
            {
                if (p == null) throw new ArgumentException(Name + ": null starter");
                if (!numbers.Add(p.ShirtNumber)) throw new ArgumentException(Name + ": duplicate shirt number " + p.ShirtNumber);
            }
            Tactics?.Sanitize();
        }
    }
}

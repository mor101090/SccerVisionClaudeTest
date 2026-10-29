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

    /// <summary>How the team moves the ball: short passing combinations, a mix, or balls in behind and long.</summary>
    public enum PassingStyle { Short, Mixed, Direct }

    /// <summary>How the team defends: balanced, a high press (wins it high, costs stamina), an offside trap (the whole line steps up together to catch the forwards), or parking the bus (a bunker, time wasting).</summary>
    public enum DefenceStyle { Balanced, HighPress, OffsideTrap, ParkTheBus }

    /// <summary>How the team attacks: mixed, crosses into the box, runs in behind, or shots from distance (20-30 m).</summary>
    public enum AttackStyle { Mixed, CrossesIntoBox, RunsInBehind, LongShots }

    /// <summary>One extra tactic: keep the ball, waste time, counter-attacks, or relentless pressure on the box.</summary>
    public enum SpecialTactic { None, Possession, TimeWasting, CounterAttack, Siege }

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
        public PassingStyle Passing = PassingStyle.Mixed;
        public DefenceStyle Defence = DefenceStyle.Balanced;
        public AttackStyle Attack = AttackStyle.Mixed;
        public SpecialTactic Special = SpecialTactic.None;

        public TeamTactics Clone() => (TeamTactics)MemberwiseClone();

        /// <summary>
        /// The tactics as the players play them: the chosen defence, attack and special tactic set (or limit) the
        /// sliders - a high press means a high line and hard pressing, parking the bus a deep line, narrow and passive, and
        /// so on. The sliders stay what the manager set.
        /// </summary>
        public TeamTactics Effective()
        {
            var e = Clone();
            switch (Defence)
            {
                case DefenceStyle.HighPress: e.PressingIntensity = Math.Max(e.PressingIntensity, 0.88f); e.DefensiveLine = Math.Max(e.DefensiveLine, 0.8f); break;
                case DefenceStyle.OffsideTrap: e.DefensiveLine = Math.Max(e.DefensiveLine, 0.72f); e.PressingIntensity = Math.Max(e.PressingIntensity, 0.5f); break;
                case DefenceStyle.ParkTheBus: e.DefensiveLine = Math.Min(e.DefensiveLine, 0.03f); e.Width = Math.Min(e.Width, 0.3f); e.Mentality = Math.Min(e.Mentality, 0.12f); e.PressingIntensity = Math.Min(e.PressingIntensity, 0.2f); e.Tempo = Math.Min(e.Tempo, 0.3f); break;
            }
            switch (Attack)
            {
                case AttackStyle.CrossesIntoBox: e.Width = Math.Max(e.Width, 0.82f); break;
                case AttackStyle.RunsInBehind: e.Passing = PassingStyle.Direct; e.Tempo = Math.Max(e.Tempo, 0.62f); break;
                case AttackStyle.LongShots: e.Mentality = Math.Max(e.Mentality, 0.6f); break;
            }
            switch (Special)
            {
                case SpecialTactic.Possession: e.Passing = PassingStyle.Short; e.Tempo = Math.Min(e.Tempo, 0.35f); e.PressingIntensity = Math.Max(e.PressingIntensity, 0.55f); break;
                case SpecialTactic.TimeWasting: e.Tempo = Math.Min(e.Tempo, 0.1f); e.Mentality = Math.Min(e.Mentality, 0.25f); e.DefensiveLine = Math.Min(e.DefensiveLine, 0.35f); e.PressingIntensity = Math.Min(e.PressingIntensity, 0.3f); break;
                case SpecialTactic.CounterAttack: e.DefensiveLine = Math.Min(e.DefensiveLine, 0.35f); e.PressingIntensity = Math.Min(e.PressingIntensity, 0.45f); e.Tempo = Math.Max(e.Tempo, 0.8f); break;
                case SpecialTactic.Siege: e.Mentality = 0.95f; e.DefensiveLine = Math.Max(e.DefensiveLine, 0.92f); e.Width = Math.Max(e.Width, 0.72f); e.Tempo = Math.Max(e.Tempo, 0.7f); e.PressingIntensity = Math.Max(e.PressingIntensity, 0.9f); break;
            }
            e.Sanitize();
            return e;
        }

        /// <summary>How fast the team's legs tire: a high press and relentless pressure cost stamina, a bunker and time wasting save it.</summary>
        public float EnergyDrain
        {
            get
            {
                float f = 1f;
                if (Defence == DefenceStyle.HighPress) f *= 1.4f;
                else if (Defence == DefenceStyle.ParkTheBus) f *= 0.82f;
                if (Special == SpecialTactic.Siege) f *= 1.25f;
                else if (Special == SpecialTactic.TimeWasting) f *= 0.88f;
                else if (Special == SpecialTactic.Possession) f *= 0.94f;
                return f;
            }
        }
        /// <summary>In words, for the commentary: "high line, wide, short passing, fast tempo, aggressive".</summary>
        public string Describe()
        {
            string line = DefensiveLine < 0.3f ? "deep line" : DefensiveLine > 0.7f ? "high line" : "middle line";
            string width = Width < 0.3f ? "narrow" : Width > 0.7f ? "wide" : "normal width";
            string pass = Passing == PassingStyle.Short ? "short passing" : Passing == PassingStyle.Direct ? "balls in behind" : "mixed passing";
            string tempo = Tempo < 0.35f ? "slow tempo" : Tempo > 0.65f ? "fast tempo" : "normal tempo";
            string style = Tackling == TacklingStyle.GetStuckIn ? "aggressive" : Tackling == TacklingStyle.StayOnFeet ? "gentle" : "balanced";
            string mind = Mentality > 0.65f ? "all-out attack, " : Mentality < 0.35f ? "defensive, " : "";
            string extra = "";
            if (Defence != DefenceStyle.Balanced) extra += ", " + (Defence == DefenceStyle.HighPress ? "high press" : Defence == DefenceStyle.OffsideTrap ? "offside trap" : "parking the bus");
            if (Attack != AttackStyle.Mixed) extra += ", " + (Attack == AttackStyle.CrossesIntoBox ? "crosses into the box" : Attack == AttackStyle.RunsInBehind ? "runs in behind" : "long shots");
            if (Special != SpecialTactic.None) extra += ", " + (Special == SpecialTactic.Possession ? "keeping the ball" : Special == SpecialTactic.TimeWasting ? "wasting time" : Special == SpecialTactic.CounterAttack ? "counter-attacks" : "relentless pressure on the box");
            return mind + line + ", " + width + ", " + pass + ", " + tempo + ", " + style + extra;
        }

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

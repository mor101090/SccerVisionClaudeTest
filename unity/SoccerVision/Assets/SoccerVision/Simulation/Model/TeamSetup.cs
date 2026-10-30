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

    /// <summary>
    /// How the team defends: balanced, a high press (wins it high, costs stamina), an offside trap (the whole line steps up
    /// together to catch the forwards), parking the bus (a bunker, time wasting), a high backline (the whole team compact
    /// round halfway, the line steps up on every back pass and the keeper sweeps behind it), or inverted full-backs (in
    /// possession the full-backs come inside into midfield beside the holding man - a 3-2 base that stops counters through
    /// the middle, but leaves the flanks open).
    /// </summary>
    public enum DefenceStyle { Balanced, HighPress, OffsideTrap, ParkTheBus, HighBackline, InvertedFullBacks }

    /// <summary>How the team attacks: mixed, crosses into the box, runs in behind, or shots from distance (20-30 m).</summary>
    public enum AttackStyle { Mixed, CrossesIntoBox, RunsInBehind, LongShots }

    /// <summary>One extra tactic: keep the ball, waste time, counter-attacks, or relentless pressure on the box.</summary>
    public enum SpecialTactic { None, Possession, TimeWasting, CounterAttack, Siege }

    /// <summary>Where one player stands at a corner (see <see cref="TeamTactics.CornerSetupAttack"/>).</summary>
    [Serializable]
    public struct CornerSpot
    {
        public int Number;
        public float Forward, Lateral;
        public static readonly CornerSpot[] None = new CornerSpot[0];
    }

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
        /// <summary>The formation to play (a name from <see cref="SoccerVision.Simulation.Formation.Names"/>); empty: the team's own.</summary>
        public string FormationName = "";
        /// <summary>Role and individual instructions per formation slot (slot 0 the keeper); the player in the slot plays them, a substitute too.</summary>
        public PlayerOrders[] Orders = NewOrders();
        /// <summary>
        /// Set-piece takers, a priority list of shirt numbers each: the first one who is on the pitch and fit takes it (a
        /// substituted, sent-off or injured player is skipped); an empty list picks the best one on the pitch. Corners and
        /// free kicks at goal have a list for each side of the pitch (the team's right and left).
        /// </summary>
        public int[] CornerTakersRight = Empty, CornerTakersLeft = Empty, FreeKickTakersRight = Empty, FreeKickTakersLeft = Empty, PenaltyTakers = Empty;
        public static readonly int[] Empty = new int[0];
        /// <summary>Corner routine per side: 0 auto (varied), 1 near post, 2 far post, 3 penalty spot, 4 pulled back to the edge, 5 short.</summary>
        public int CornerRoutineRight, CornerRoutineLeft;
        /// <summary>Who stands where for a corner (shirt numbers for the six zones; 0 = whoever fits) - see <see cref="CornerZones"/>.</summary>
        public int[] CornerZonesRight = new int[6], CornerZonesLeft = new int[6];
        public const int CornerZoneCount = 6;
        /// <summary>
        /// The manager's own corner set-up, attacking and defending: where each player stands (shirt number and a spot in the
        /// team's own frame - forward, lateral - for a corner taken on the side where lateral is positive; mirrored for the
        /// other side). Players not listed (or substitutes) are placed automatically. Empty: all automatic.
        /// </summary>
        public CornerSpot[] CornerSetupAttack = CornerSpot.None, CornerSetupDefence = CornerSpot.None;

        public static PlayerOrders[] NewOrders()
        {
            var o = new PlayerOrders[11];
            for (int i = 0; i < o.Length; i++) o[i] = new PlayerOrders();
            return o;
        }

        /// <summary>The orders for a slot (never null).</summary>
        public PlayerOrders OrdersFor(int slot) => Orders != null && slot >= 0 && slot < Orders.Length && Orders[slot] != null ? Orders[slot] : Default;
        private static readonly PlayerOrders Default = new PlayerOrders();

        public TeamTactics Clone()
        {
            var c = (TeamTactics)MemberwiseClone();
            c.Orders = NewOrders();
            for (int i = 0; i < 11; i++) c.Orders[i] = OrdersFor(i).Clone();
            c.CornerTakersRight = (int[])CornerTakersRight.Clone(); c.CornerTakersLeft = (int[])CornerTakersLeft.Clone();
            c.FreeKickTakersRight = (int[])FreeKickTakersRight.Clone(); c.FreeKickTakersLeft = (int[])FreeKickTakersLeft.Clone();
            c.PenaltyTakers = (int[])PenaltyTakers.Clone();
            c.CornerZonesRight = (int[])CornerZonesRight.Clone(); c.CornerZonesLeft = (int[])CornerZonesLeft.Clone();
            c.CornerSetupAttack = (CornerSpot[])CornerSetupAttack.Clone(); c.CornerSetupDefence = (CornerSpot[])CornerSetupDefence.Clone();
            return c;
        }

        /// <summary>
        /// The tactics as the players play them: the chosen defence, attack and special tactic set (or limit) the
        /// sliders - a high press means a high line and hard pressing, parking the bus a deep line, narrow and passive, and
        /// so on. The sliders stay what the manager set.
        /// </summary>
        public TeamTactics Effective()
        {
            var e = Clone();
            // The styles SHIFT the sliders, they do not pin them: whatever the manager sets, moving the line or width slider
            // still moves the team (a high press just plays higher than the same slider without it).
            void Shift(ref float v, float d) => v = MathUtil.Clamp01(v + d);
            switch (Defence)
            {
                case DefenceStyle.HighPress: Shift(ref e.PressingIntensity, 0.3f); Shift(ref e.DefensiveLine, 0.22f); break;
                case DefenceStyle.OffsideTrap: Shift(ref e.DefensiveLine, 0.18f); Shift(ref e.PressingIntensity, 0.1f); break;
                case DefenceStyle.ParkTheBus: Shift(ref e.DefensiveLine, -0.35f); Shift(ref e.Width, -0.25f); Shift(ref e.Mentality, -0.3f); Shift(ref e.PressingIntensity, -0.3f); Shift(ref e.Tempo, -0.2f); break;
                case DefenceStyle.HighBackline: Shift(ref e.DefensiveLine, 0.34f); Shift(ref e.PressingIntensity, 0.25f); break;
                case DefenceStyle.InvertedFullBacks: if (e.Passing == PassingStyle.Mixed) e.Passing = PassingStyle.Short; break;
            }
            switch (Attack)
            {
                case AttackStyle.CrossesIntoBox: Shift(ref e.Width, 0.25f); break;
                case AttackStyle.RunsInBehind: if (e.Passing == PassingStyle.Mixed) e.Passing = PassingStyle.Direct; Shift(ref e.Tempo, 0.15f); break;
                case AttackStyle.LongShots: Shift(ref e.Mentality, 0.12f); break;
            }
            switch (Special)
            {
                case SpecialTactic.Possession: if (e.Passing == PassingStyle.Mixed) e.Passing = PassingStyle.Short; Shift(ref e.Tempo, -0.1f); Shift(ref e.PressingIntensity, 0.1f); break;
                case SpecialTactic.TimeWasting: Shift(ref e.Tempo, -0.3f); Shift(ref e.Mentality, -0.2f); Shift(ref e.DefensiveLine, -0.15f); Shift(ref e.PressingIntensity, -0.15f); break;
                case SpecialTactic.CounterAttack: Shift(ref e.DefensiveLine, -0.15f); Shift(ref e.PressingIntensity, -0.1f); Shift(ref e.Tempo, 0.25f); break;
                case SpecialTactic.Siege: Shift(ref e.Mentality, 0.4f); Shift(ref e.DefensiveLine, 0.3f); Shift(ref e.Width, 0.2f); Shift(ref e.Tempo, 0.2f); Shift(ref e.PressingIntensity, 0.3f); break;
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
                else if (Defence == DefenceStyle.HighBackline) f *= 1.12f;
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
            if (Defence != DefenceStyle.Balanced) extra += ", " + (Defence == DefenceStyle.HighPress ? "high press" : Defence == DefenceStyle.OffsideTrap ? "offside trap" : Defence == DefenceStyle.ParkTheBus ? "parking the bus" : Defence == DefenceStyle.HighBackline ? "high backline" : "inverted full-backs");
            if (Attack != AttackStyle.Mixed) extra += ", " + (Attack == AttackStyle.CrossesIntoBox ? "crosses into the box" : Attack == AttackStyle.RunsInBehind ? "runs in behind" : "long shots");
            if (Special != SpecialTactic.None) extra += ", " + (Special == SpecialTactic.Possession ? "keeping the ball" : Special == SpecialTactic.TimeWasting ? "wasting time" : Special == SpecialTactic.CounterAttack ? "counter-attacks" : "relentless pressure on the box");
            return (string.IsNullOrEmpty(FormationName) ? "" : FormationName + ", ") + mind + line + ", " + width + ", " + pass + ", " + tempo + ", " + style + extra;
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

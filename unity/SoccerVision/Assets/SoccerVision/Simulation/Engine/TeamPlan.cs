using System;
using System.Collections.Generic;
using System.Globalization;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// A manager's plan for his team as plain text (what the browser page sends): the formation, who plays in which
    /// position, each position's role and individual instructions, the set-piece takers and the team instructions.
    /// "formation=4-3-3;slots=1,2,5,...;orders=0.1.1.1,...;takers=7,10,9;line=0.5;width=0.5;passing=1;tempo=0.5;
    /// aggression=0.5;defence=0;attack=0;special=0". Every field is optional.
    /// Before kick-off it picks the starting eleven from the whole squad; during the match it reshapes the team.
    /// </summary>
    public sealed class TeamPlan
    {
        public string Formation = "";
        /// <summary>Shirt number per formation slot (slot 0 the keeper); null keeps the players where they are.</summary>
        public int[] Slots;
        /// <summary>Per slot: role and the three individual instructions; null keeps them.</summary>
        public PlayerOrders[] Orders;
        public int[] CornerTakersRight, CornerTakersLeft, FreeKickTakersRight, FreeKickTakersLeft, PenaltyTakers;
        public int CornerRoutineRight = -1, CornerRoutineLeft = -1;
        public int[] CornerZonesRight, CornerZonesLeft;
        public bool HasTeamInstructions;
        public float Line = 0.5f, Width = 0.5f, Tempo = 0.5f, Aggression = 0.5f;
        public int Passing = 1, Defence, Attack, Special;

        private static float F(string v) => float.Parse(v, NumberStyles.Float, CultureInfo.InvariantCulture);
        private static int I(string v) => int.Parse(v, NumberStyles.Integer, CultureInfo.InvariantCulture);
        /// <summary>A list "7.9.10" of shirt numbers (zeros dropped unless <paramref name="keepZeros"/>), at most <paramref name="max"/>.</summary>
        private static int[] List(string v, bool keepZeros = false, int max = 11)
        {
            var l = new List<int>();
            foreach (var x in v.Split('.')) { if (x.Length == 0) continue; int n = I(x); if ((n > 0 || keepZeros) && l.Count < max) l.Add(n); }
            return l.ToArray();
        }

        public static TeamPlan Parse(string text)
        {
            var plan = new TeamPlan();
            if (string.IsNullOrEmpty(text)) return plan;
            foreach (var part in text.Split(';'))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                string k = part.Substring(0, eq).Trim(), v = part.Substring(eq + 1).Trim();
                if (v.Length == 0) continue;
                switch (k)
                {
                    case "formation": plan.Formation = v; break;
                    case "slots":
                        var s = v.Split(',');
                        if (s.Length == 11) { plan.Slots = new int[11]; for (int i = 0; i < 11; i++) plan.Slots[i] = I(s[i]); }
                        break;
                    case "orders":
                        var o = v.Split(',');
                        if (o.Length == 11)
                        {
                            plan.Orders = new PlayerOrders[11];
                            for (int i = 0; i < 11; i++)
                            {
                                var x = o[i].Split('.');
                                var po = new PlayerOrders();
                                if (x.Length > 0) po.Role = (PlayerRole)Math.Max(0, Math.Min((int)PlayerRole.FalseNine, I(x[0])));
                                if (x.Length > 1) po.LongShots = (Amount)Math.Max(0, Math.Min(2, I(x[1])));
                                if (x.Length > 2) po.Dribbling = (Amount)Math.Max(0, Math.Min(2, I(x[2])));
                                if (x.Length > 3) po.Aggression = (Amount)Math.Max(0, Math.Min(2, I(x[3])));
                                plan.Orders[i] = po;
                            }
                        }
                        break;
                    case "takers": // (one taker each: corners, free kicks, penalties)
                        var t = v.Split(',');
                        if (t.Length > 0) plan.CornerTakersRight = plan.CornerTakersLeft = List(t[0]);
                        if (t.Length > 1) plan.FreeKickTakersRight = plan.FreeKickTakersLeft = List(t[1]);
                        if (t.Length > 2) plan.PenaltyTakers = List(t[2]);
                        break;
                    case "ctr": plan.CornerTakersRight = List(v); break;
                    case "ctl": plan.CornerTakersLeft = List(v); break;
                    case "fkr": plan.FreeKickTakersRight = List(v); break;
                    case "fkl": plan.FreeKickTakersLeft = List(v); break;
                    case "pen": plan.PenaltyTakers = List(v); break;
                    case "crr": plan.CornerRoutineRight = Math.Max(0, Math.Min(5, I(v))); break;
                    case "crl": plan.CornerRoutineLeft = Math.Max(0, Math.Min(5, I(v))); break;
                    case "czr": { var z = List(v, true, 6); plan.CornerZonesRight = new int[6]; z.CopyTo(plan.CornerZonesRight, 0); break; }
                    case "czl": { var z = List(v, true, 6); plan.CornerZonesLeft = new int[6]; z.CopyTo(plan.CornerZonesLeft, 0); break; }
                    case "line": plan.Line = F(v); plan.HasTeamInstructions = true; break;
                    case "width": plan.Width = F(v); plan.HasTeamInstructions = true; break;
                    case "tempo": plan.Tempo = F(v); plan.HasTeamInstructions = true; break;
                    case "aggression": plan.Aggression = F(v); plan.HasTeamInstructions = true; break;
                    case "passing": plan.Passing = I(v); plan.HasTeamInstructions = true; break;
                    case "defence": plan.Defence = I(v); plan.HasTeamInstructions = true; break;
                    case "attack": plan.Attack = I(v); plan.HasTeamInstructions = true; break;
                    case "special": plan.Special = I(v); plan.HasTeamInstructions = true; break;
                }
            }
            return plan;
        }

        /// <summary>The team tactics with this plan applied on top of <paramref name="from"/>.</summary>
        public TeamTactics ToTactics(TeamTactics from)
        {
            var t = from.Clone();
            if (HasTeamInstructions)
            {
                t.Defence = Defence == 1 ? DefenceStyle.HighPress : Defence == 2 ? DefenceStyle.OffsideTrap : Defence == 3 ? DefenceStyle.ParkTheBus : Defence == 4 ? DefenceStyle.HighBackline : Defence == 5 ? DefenceStyle.InvertedFullBacks : DefenceStyle.Balanced;
                t.Attack = Attack == 1 ? AttackStyle.CrossesIntoBox : Attack == 2 ? AttackStyle.RunsInBehind : Attack == 3 ? AttackStyle.LongShots : AttackStyle.Mixed;
                t.Special = Special == 1 ? SpecialTactic.Possession : Special == 2 ? SpecialTactic.TimeWasting : Special == 3 ? SpecialTactic.CounterAttack : Special == 4 ? SpecialTactic.Siege : SpecialTactic.None;
                t.DefensiveLine = Line;
                t.Width = Width;
                t.Passing = Passing == 0 ? PassingStyle.Short : Passing == 2 ? PassingStyle.Direct : PassingStyle.Mixed;
                t.Tempo = Tempo;
                t.PressingIntensity = 0.5f + 0.6f * (Aggression - 0.5f);
                t.Tackling = Aggression < 0.34f ? TacklingStyle.StayOnFeet : Aggression > 0.66f ? TacklingStyle.GetStuckIn : TacklingStyle.Normal;
            }
            if (!string.IsNullOrEmpty(Formation)) t.FormationName = Formation;
            if (Orders != null) for (int i = 0; i < 11; i++) t.Orders[i] = Orders[i].Clone();
            if (CornerTakersRight != null) t.CornerTakersRight = CornerTakersRight;
            if (CornerTakersLeft != null) t.CornerTakersLeft = CornerTakersLeft;
            if (FreeKickTakersRight != null) t.FreeKickTakersRight = FreeKickTakersRight;
            if (FreeKickTakersLeft != null) t.FreeKickTakersLeft = FreeKickTakersLeft;
            if (PenaltyTakers != null) t.PenaltyTakers = PenaltyTakers;
            if (CornerRoutineRight >= 0) t.CornerRoutineRight = CornerRoutineRight;
            if (CornerRoutineLeft >= 0) t.CornerRoutineLeft = CornerRoutineLeft;
            if (CornerZonesRight != null) t.CornerZonesRight = CornerZonesRight;
            if (CornerZonesLeft != null) t.CornerZonesLeft = CornerZonesLeft;
            t.Sanitize();
            return t;
        }

        /// <summary>
        /// Before kick-off: the formation, the starting eleven picked from the whole squad (the rest go on the bench) and
        /// the tactics. An eleven that is not eleven different players of the squad is ignored.
        /// </summary>
        public void ApplyBeforeKickOff(TeamSetup setup)
        {
            if (!string.IsNullOrEmpty(Formation) && Formation != setup.Formation.Name)
            {
                var next = SoccerVision.Simulation.Formation.ByName(Formation);
                if (Slots == null)
                {
                    // nobody placed by hand: each starter moves to the nearest position of the new shape
                    var map = SoccerVision.Simulation.Formation.Remap(setup.Formation, next);
                    var moved = new PlayerProfile[11];
                    for (int i = 0; i < 11; i++) moved[map[i]] = setup.Starters[i];
                    setup.Starters.Clear();
                    setup.Starters.AddRange(moved);
                }
                setup.Formation = next;
            }
            if (Slots != null)
            {
                var pool = new List<PlayerProfile>(setup.Starters);
                pool.AddRange(setup.Bench);
                var eleven = new List<PlayerProfile>();
                foreach (int n in Slots)
                {
                    var p = pool.Find(q => q.ShirtNumber == n);
                    if (p == null || eleven.Contains(p)) { eleven = null; break; }
                    eleven.Add(p);
                }
                if (eleven != null)
                {
                    setup.Starters.Clear();
                    setup.Starters.AddRange(eleven);
                    setup.Bench.Clear();
                    foreach (var p in pool) if (!eleven.Contains(p)) setup.Bench.Add(p);
                }
            }
            setup.Tactics = ToTactics(setup.Tactics);
            setup.Tactics.FormationName = setup.Formation.Name;
        }

        /// <summary>During the match: the new tactics (and formation), then the players placed in their positions.</summary>
        public void Submit(MatchEngine engine, int team)
        {
            var current = engine.TeamOf(team).Tactics;
            engine.Submit(new ChangeTacticsCommand(team, ToTactics(current)));
            if (Slots != null) engine.Submit(new LineupCommand(team, Slots));
        }
    }
}

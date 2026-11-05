using System;
using System.Collections.Generic;
using System.Linq;

namespace SoccerVision.Simulation
{
    /// <summary>Marking: zonal and man-to-man.</summary>
    public sealed partial class MatchEngine
    {
        /// <summary>(experiment) How far a man-marker stands from his man (1 = as calibrated).</summary>
        public static float ManMarkGapScale = 1f;
        /// <summary>(experiment) How strongly a zonal defender steps to the man in his zone (1 = as calibrated).</summary>
        public static float ZonalPull = 1f;

        private readonly List<PlayerAgent> _markPool = new List<PlayerAgent>(11);

        private readonly List<PlayerAgent> _threats = new List<PlayerAgent>(11);

        /// <summary>Zone radius in zonal marking: an opponent inside it is this player's responsibility.</summary>
        private static float ZoneRadius(PlayerAgent p) => (p.Slot.Line == Line.Defence ? 9f : 10.5f) * (p.Has(PlayerTrait.HoldsPosition) ? 0.85f : 1f) * ZoneRadiusScale;

        /// <summary>(experiment) The size of a zonal defender's area (1 = as calibrated): a smaller one leaves more men between two zones.</summary>
        public static float ZoneRadiusScale = 1f;

        /// <summary>One marker per attacker (no two defenders on the same man), by the team's marking system.</summary>
        private void AssignMarking(TeamAgent team, bool inPossession, float ballF)
        {
            foreach (var p in team.Players) p.PrevMarkTarget = p.MarkTarget;
            foreach (var p in team.Players) p.MarkTarget = null;
            if (inPossession)
            {
                // A new defensive phase gets new man-to-man assignments.
                foreach (var p in team.Players) p.ManAssignment = null;
                return;
            }
            if (team.Tactics.Marking == MarkingStyle.ManToMan) AssignManToMan(team, ballF);
            else AssignZonal(team, ballF);
            foreach (var p in team.Players)
                if (p.PrevMarkTarget != null && p.MarkTarget != null && p.PrevMarkTarget != p.MarkTarget) team.Stats.MarkingSwitches++;
        }

        /// <summary>
        /// Zonal marking: every defender and midfielder owns an area around his place in the block (which shifts with the
        /// ball). He takes the most dangerous opponent inside it and lets him go when he leaves - the next zone picks him up.
        /// </summary>
        private void AssignZonal(TeamAgent team, float ballF)
        {
            if (ballF > 12f) return; // ball deep in their half: hold the shape, no marking
            _threats.Clear();
            foreach (var o in team.Opponent.Players)
            {
                if (o.SentOff || o.IsGoalkeeper || o == Ball.Owner) continue;
                if (team.Forward(o.Pos) < 5f) _threats.Add(o);
            }
            Vec2 goal = team.OwnGoal;
            _threats.Sort((a, b) => Vec2.DistanceSq(a.Pos, goal).CompareTo(Vec2.DistanceSq(b.Pos, goal)));
            _markPool.Clear();
            foreach (var p in team.Players)
            {
                if (p.SentOff || p.IsGoalkeeper || p.IsDown || p == team.Presser || p == team.SecondPresser) continue;
                if (p.Slot.Line == Line.Attack) continue;
                _markPool.Add(p);
            }
            foreach (var a in _threats)
            {
                PlayerAgent best = null;
                float bestD = float.PositiveInfinity;
                foreach (var d in _markPool)
                {
                    // whose zone is he in?
                    float dist = Vec2.Distance(d.ShapeTarget, a.Pos);
                    if (dist < ZoneRadius(d) && dist < bestD) { bestD = dist; best = d; }
                }
                if (best == null) continue; // in between zones and nobody's: the block's shape covers him
                best.MarkTarget = a;
                _markPool.Remove(best);
                if (_markPool.Count == 0) break;
            }
        }

        /// <summary>
        /// Man-to-man marking: when the ball is lost, the four defenders take the four most advanced opponents and the
        /// midfielders the next ones (nearest first); each keeps his man for the whole defensive phase and follows him out
        /// of position. The strikers do not mark (they stay up and press the centre-backs).
        /// </summary>
        private void AssignManToMan(TeamAgent team, float ballF)
        {
            bool needBuild = true;
            foreach (var p in team.Players) if (p.ManAssignment != null) { needBuild = false; break; }
            var opponents = new List<PlayerAgent>();
            foreach (var o in team.Opponent.Players) if (!o.SentOff && !o.IsGoalkeeper) opponents.Add(o);
            // most advanced (closest to our goal) first
            opponents.Sort((a, b) => team.Forward(a.Pos).CompareTo(team.Forward(b.Pos)));
            if (needBuild)
            {
                var taken = new HashSet<PlayerAgent>();
                foreach (var line in new[] { Line.Defence, Line.Midfield })
                {
                    var markers = new List<PlayerAgent>();
                    foreach (var p in team.Players) if (!p.SentOff && !p.IsGoalkeeper && p.Slot.Line == line) markers.Add(p);
                    var group = new List<PlayerAgent>();
                    foreach (var o in opponents) { if (taken.Contains(o)) continue; group.Add(o); if (group.Count == markers.Count) break; }
                    // greedy nearest pairs inside the group
                    while (markers.Count > 0 && group.Count > 0)
                    {
                        PlayerAgent bm = null, bo = null;
                        float bd = float.PositiveInfinity;
                        foreach (var m in markers)
                            foreach (var o in group)
                            {
                                float d = Vec2.Distance(m.Pos, o.Pos) + 0.5f * MathF.Abs(team.Lateral(m.ShapeTarget) - team.Lateral(o.Pos));
                                if (d < bd) { bd = d; bm = m; bo = o; }
                            }
                        bm.ManAssignment = bo;
                        taken.Add(bo);
                        markers.Remove(bm);
                        group.Remove(bo);
                    }
                }
            }
            foreach (var p in team.Players)
            {
                if (p.SentOff || p.IsGoalkeeper || p.Slot.Line == Line.Attack) continue;
                var man = p.ManAssignment;
                if (man == null || man.SentOff)
                {
                    // his man was sent off (or none was left): pick up the nearest opponent nobody marks
                    p.ManAssignment = null;
                    float bd = 25f;
                    foreach (var o in opponents)
                    {
                        bool marked = false;
                        foreach (var q in team.Players) if (q.ManAssignment == o) { marked = true; break; }
                        if (marked) continue;
                        float d = Vec2.Distance(p.Pos, o.Pos);
                        if (d < bd) { bd = d; p.ManAssignment = o; }
                    }
                    man = p.ManAssignment;
                }
                // the ball deep in their half: hold the shape (the assignment is kept for when they come forward)
                if (ballF > 15f || man == null || p.IsDown || p == team.Presser || p == team.SecondPresser || man == Ball.Owner) continue;
                p.MarkTarget = man;
            }
        }
    }
}

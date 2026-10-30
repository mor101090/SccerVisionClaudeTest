using System;
using System.Collections.Generic;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// Substitutions by the manager, under IFAB Law 3 as the top leagues play it: up to nine named substitutes, five
    /// changes, made in at most three opportunities during play (changes at half-time do not use one); a player who has
    /// been substituted takes no further part, and a sent-off player cannot be replaced. A change waits for a stoppage:
    /// the player coming off walks to the halfway line by his bench and the substitute comes on there, while the
    /// restart waits.
    /// </summary>
    public sealed partial class MatchEngine
    {
        /// <summary>A change asked for and not made yet.</summary>
        private sealed class PendingSubstitution
        {
            public PlayerAgent Out;
            public PlayerAgent In;
        }

        private readonly List<PendingSubstitution> _pendingSubs = new List<PendingSubstitution>();

        /// <summary>Changes the team still may make (five, less those made and those waiting).</summary>
        public int SubstitutionsLeft(TeamAgent team)
        {
            int waiting = 0;
            foreach (var s in _pendingSubs) if (s.Out.Team == team) waiting++;
            return Config.MaxSubstitutions - team.SubstitutionsUsed - waiting;
        }

        /// <summary>True if the team may still use a substitution opportunity: at half-time, during an opportunity already open, or with one left.</summary>
        private bool WindowAvailable(TeamAgent team) =>
            Phase == MatchPhase.HalfTimeBreak || SecondHalfKickOff || (_restart != null && team.WindowRestart == _restart) || team.WindowsUsed < Config.MaxSubstitutionWindows;

        /// <summary>The kick-off of the second half is still being set up: changes now count as made at half-time (Law 3).</summary>
        private bool SecondHalfKickOff => _restart != null && _restart.Type == RestartType.KickOff && Clock.Period == MatchPeriod.SecondHalf && Clock.Seconds <= Config.RegulationHalfClockSeconds + 1f;

        /// <summary>
        /// Asks for a change: the player with shirt <paramref name="outNumber"/> off, the bench player <paramref name="inNumber"/>
        /// on. Returns null when it is accepted (made at the next stoppage, at once during the half-time break), otherwise why not.
        /// </summary>
        internal string RequestSubstitution(int teamIndex, int outNumber, int inNumber)
        {
            var team = TeamOf(teamIndex);
            PlayerAgent off = null, on = null;
            foreach (var p in team.Players) if (p.Number == outNumber && !p.InjuredOff) off = p;
            foreach (var b in team.Bench) if (b.Number == inNumber) on = b;
            string why = null;
            if (IsFinished) why = "the match is over";
            else if (off == null) why = "#" + outNumber + " is not on the pitch";
            else if (off.SentOff) why = off.Profile.ShortName + " has been sent off and cannot be replaced";
            else if (on == null) why = "#" + inNumber + " is not on the bench (a player who has come off cannot return)";
            else if (SubstitutionsLeft(team) <= 0) why = "all " + Config.MaxSubstitutions + " substitutions are used";
            else if (!WindowAvailable(team)) why = "all " + Config.MaxSubstitutionWindows + " substitution opportunities are used";
            else
                foreach (var s in _pendingSubs)
                    if (s.Out == off || s.In == on) why = "that change is already waiting";
            var e = Emit(MatchEventType.SubstitutionRequest, team, off, null, Ball.Pos, why == null ? 0f : 1f);
            e.Info = outNumber + "|" + inNumber;
            if (why != null)
            {
                e.Text = "Substitution refused: " + why;
                return why;
            }
            e.Text = team.Name + " get ready to make a change: " + on.Label + " for " + off.Label;
            if (Phase == MatchPhase.HalfTimeBreak)
            {
                MakeChange(off, on, false); // in the dressing room: no opportunity used, nobody has to walk off
                return null;
            }
            _pendingSubs.Add(new PendingSubstitution { Out = off, In = on });
            return null;
        }

        /// <summary>
        /// During a stoppage: the waiting changes start (they use one opportunity together), the player coming off walks to
        /// the halfway line and the change is made there. Returns true while a change is still being made (the restart waits).
        /// </summary>
        private bool StepSubstitutions(float dt)
        {
            if (_pendingSubs.Count == 0) return false;
            bool stoppage = _restart != null && Phase == MatchPhase.Stoppage;
            if (!stoppage) return false;
            for (int i = _pendingSubs.Count - 1; i >= 0; i--)
            {
                var s = _pendingSubs[i];
                var p = s.Out;
                if (p.SentOff || p.Injury == InjuryLevel.Serious) { _pendingSubs.RemoveAt(i); continue; } // (sent off meanwhile, or the injury change takes over)
                var team = p.Team;
                if (team.WindowRestart != _restart && !SecondHalfKickOff)
                {
                    team.WindowRestart = _restart;
                    team.WindowsUsed++;
                }
                if (_restart.Taker == p) _restart.Taker = null;
                // Law 3: he leaves by the nearest point on the boundary line (the substitute comes on at the halfway line)
                p.Target = NearestExit(p.Pos);
                p.Task = PlayerTask.Leave;
                p.FaceBall = false;
                p.StunTimer = 0f;
                Vec2 to = p.Target - p.Pos;
                float d = to.Length, v = 3.4f; // a brisk walk off
                if (d > 0.05f) { p.Pos += to / d * MathF.Min(d, v * dt); p.Vel = to / d * v; p.Facing = (to / d).Angle; }
                if (d < 0.3f)
                {
                    MakeChange(p, s.In, true);
                    _pendingSubs.RemoveAt(i);
                }
            }
            return true;
        }

        /// <summary>The nearest point just outside the touchlines or goal lines.</summary>
        private static Vec2 NearestExit(Vec2 pos)
        {
            float toSide = Pitch.HalfWidth - MathF.Abs(pos.Y), toEnd = Pitch.HalfLength - MathF.Abs(pos.X);
            return toSide <= toEnd
                ? new Vec2(pos.X, MathF.Sign(pos.Y == 0f ? 1f : pos.Y) * (Pitch.HalfWidth + 0.4f))
                : new Vec2(MathF.Sign(pos.X == 0f ? 1f : pos.X) * (Pitch.HalfLength + 0.4f), MathUtil.Clamp(pos.Y, -Pitch.HalfWidth + 1f, Pitch.HalfWidth - 1f));
        }

        /// <summary>True for a player walking off for a waiting change (the stoppage loop leaves him to StepSubstitutions).</summary>
        private bool IsComingOff(PlayerAgent p)
        {
            if (_restart == null || Phase != MatchPhase.Stoppage) return false;
            foreach (var s in _pendingSubs) if (s.Out == p) return true;
            return false;
        }

        /// <summary>The change itself: the substitute takes the shirt slot (and the role and instructions of the position).</summary>
        private void MakeChange(PlayerAgent p, PlayerAgent sub, bool walkOn)
        {
            var team = p.Team;
            team.Bench.Remove(sub);
            team.SubbedOff.Add(p.Profile);
            team.SubstitutionsUsed++;
            team.Stats.Substitutions++;
            DampMomentum(0.85f);
            string outLabel = p.Label;
            int outNumber = p.Number;
            CloseRecord(p);
            p.Substitute(sub.Profile);
            OpenRecord(p);
            p.Pos = p.PrevPos = walkOn ? BenchSpot(team) : p.ShapeTarget;
            p.Vel = Vec2.Zero;
            p.Target = p.ShapeTarget;
            p.Task = PlayerTask.Shape;
            if (walkOn) _substituteOnUntil = SimTime + 2.5f * Config.MotionSpeed;
            var e = Emit(MatchEventType.Substitution, team, p, null, p.Pos);
            e.Info = p.Number + "|" + p.Profile.ShortName + "|" + outNumber;
            e.Text = Commentary.Substitution(team, p, outLabel);
            if (CollectDiagnostics) Count("substitution.manager");
        }

        /// <summary>Puts the players in the slots asked for (shirt number per slot); ignored unless it is a reshuffle of the players on the pitch.</summary>
        internal void ApplyLineup(int teamIndex, int[] numberBySlot)
        {
            var team = TeamOf(teamIndex);
            if (numberBySlot == null || numberBySlot.Length != 11) return;
            var bySlot = new PlayerAgent[11];
            foreach (var p in team.Players)
            {
                int slot = Array.IndexOf(numberBySlot, p.Number);
                if (slot < 0 || bySlot[slot] != null) return;
                bySlot[slot] = p;
            }
            // the keeper's slot is the keeper's: a reshuffle never puts an outfield player in goal during play
            if (bySlot[0] != null && bySlot[0].SlotIndex != 0) return;
            foreach (var p in team.Players) p.SlotIndex = Array.IndexOf(numberBySlot, p.Number);
        }
    }

    /// <summary>The manager asks for a change (made at the next stoppage, under the substitution rules).</summary>
    public sealed class SubstituteCommand : IMatchCommand
    {
        public int Team, OutNumber, InNumber;

        public SubstituteCommand(int team, int outNumber, int inNumber)
        {
            Team = team;
            OutNumber = outNumber;
            InNumber = inNumber;
        }

        public void Apply(MatchEngine engine) => engine.RequestSubstitution(Team, OutNumber, InNumber);
    }

    /// <summary>The manager moves his players between positions (shirt number per formation slot).</summary>
    public sealed class LineupCommand : IMatchCommand
    {
        public int Team;
        public int[] NumberBySlot;

        public LineupCommand(int team, int[] numberBySlot)
        {
            Team = team;
            NumberBySlot = numberBySlot;
        }

        public void Apply(MatchEngine engine) => engine.ApplyLineup(Team, NumberBySlot);
    }
}

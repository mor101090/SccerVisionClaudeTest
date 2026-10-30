using System;
using System.Collections.Generic;
using System.Linq;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// The computer's manager in a live match against a human: every few minutes it reads the game and the human's
    /// tactics and answers them, through the same tactics commands the human uses.
    ///  - Against a high line it plays balls in behind; against a deep block it pushes up, stretches the pitch and keeps the
    ///    ball; against a narrow team it goes wide; against an aggressive one it moves the ball quickly.
    ///  - Losing after the hour it attacks (higher, faster, more aggressive late on); winning late it sits deep and slows
    ///    the game down.
    ///  - It makes its changes like a real manager: two around the hour, two more after 70 minutes, the last after 80 - the
    ///    tired and the knocked go off first for a fresh player of the same kind (three opportunities, five changes).
    /// </summary>
    public sealed class ComputerManager
    {
        private readonly MatchEngine _engine;
        private readonly int _team;
        private float _nextCheck;
        private string _last;
        private float _nextSubCheck = 56f * 60f;
        private readonly HashSet<int> _askedOut = new HashSet<int>(), _askedIn = new HashSet<int>();

        public ComputerManager(MatchEngine engine, int team)
        {
            _engine = engine;
            _team = team;
            _nextCheck = 6f * 60f;
            _last = engine.TeamOf(team).Tactics.Describe();
        }

        /// <summary>Call after each step of the match (cheap: it decides every few match minutes).</summary>
        public void Update()
        {
            var clock = _engine.Clock;
            if (clock.Period != MatchPeriod.FirstHalf && clock.Period != MatchPeriod.SecondHalf) return;
            ConsiderSubstitutions(clock);
            if (clock.Seconds < _nextCheck) return;
            _nextCheck = clock.Seconds + 4f * 60f;

            var me = _engine.TeamOf(_team);
            var human = _engine.TeamOf(1 - _team).Tactics;
            var t = new TeamTactics { Marking = me.Tactics.Marking, FormationName = me.Tactics.FormationName,
                                      CornerTakersRight = me.Tactics.CornerTakersRight, CornerTakersLeft = me.Tactics.CornerTakersLeft,
                                      FreeKickTakersRight = me.Tactics.FreeKickTakersRight, FreeKickTakersLeft = me.Tactics.FreeKickTakersLeft, PenaltyTakers = me.Tactics.PenaltyTakers,
                                      CornerRoutineRight = me.Tactics.CornerRoutineRight, CornerRoutineLeft = me.Tactics.CornerRoutineLeft,
                                      CornerZonesRight = me.Tactics.CornerZonesRight, CornerZonesLeft = me.Tactics.CornerZonesLeft };
            for (int i = 0; i < 11; i++) t.Orders[i] = me.Tactics.OrdersFor(i).Clone(); // (the roles and instructions stay)
            float minute = clock.Seconds / 60f;
            int diff = me.Score - _engine.TeamOf(1 - _team).Score;

            // Answer the human's set-up.
            if (human.DefensiveLine > 0.65f) { t.Passing = PassingStyle.Direct; t.DefensiveLine = 0.4f; }
            else if (human.DefensiveLine < 0.35f) { t.Passing = PassingStyle.Short; t.DefensiveLine = 0.65f; t.Width = 0.75f; }
            if (human.Width < 0.35f) t.Width = 0.85f;
            else if (human.Width > 0.7f) t.Width = 0.4f; // stay compact against a wide team
            if (human.Tackling == TacklingStyle.GetStuckIn) t.Tempo = 0.75f;
            if (human.Passing == PassingStyle.Direct) t.DefensiveLine = MathF.Min(t.DefensiveLine, 0.35f); // no space behind to run into
            if (human.Passing == PassingStyle.Short) { t.PressingIntensity = 0.7f; t.Tackling = TacklingStyle.Normal; t.Defence = DefenceStyle.HighPress; }

            // Answer the human's chosen styles.
            if (human.Defence == DefenceStyle.OffsideTrap) { t.Attack = AttackStyle.LongShots; t.Passing = PassingStyle.Short; }       // no balls in behind into a trap
            else if (human.Defence == DefenceStyle.ParkTheBus) { t.Attack = AttackStyle.CrossesIntoBox; t.Special = SpecialTactic.Possession; } // work it wide against a bunker
            else if (human.Defence == DefenceStyle.HighPress) { t.Attack = AttackStyle.RunsInBehind; }
            else if (human.Defence == DefenceStyle.HighBackline) { t.Attack = AttackStyle.RunsInBehind; t.Passing = PassingStyle.Direct; } // balls over the top, races with the keeper
            else if (human.Defence == DefenceStyle.InvertedFullBacks) { t.Width = 0.85f; t.Special = SpecialTactic.CounterAttack; }  // the flanks are left open: break into them
            if (human.Special == SpecialTactic.CounterAttack) { t.Special = SpecialTactic.Possession; t.DefensiveLine = Math.Min(t.DefensiveLine, 0.5f); } // do not leave space for the break
            else if (human.Special == SpecialTactic.TimeWasting && diff <= 0) t.Special = SpecialTactic.Siege;

            // The score and the time.
            if (diff < 0 && minute >= 55f)
            {
                t.Mentality = minute >= 75f ? 0.9f : 0.75f;
                t.DefensiveLine = MathF.Max(t.DefensiveLine, 0.7f);
                t.Tempo = 0.85f;
                t.PressingIntensity = 0.8f;
                if (minute >= 75f) { t.Tackling = TacklingStyle.GetStuckIn; t.Special = SpecialTactic.Siege; t.Attack = AttackStyle.CrossesIntoBox; }
            }
            else if (diff > 0 && minute >= 70f)
            {
                t.Mentality = 0.25f;
                t.DefensiveLine = 0.25f;
                t.Width = 0.4f;
                t.Tempo = 0.25f;
                t.Passing = PassingStyle.Short;
                t.Defence = DefenceStyle.ParkTheBus;
                t.Special = SpecialTactic.TimeWasting;
            }

            string now = t.Describe();
            if (now == _last) return;
            _last = now;
            _engine.Submit(new ChangeTacticsCommand(_team, t));
        }

        private void ConsiderSubstitutions(MatchClock clock)
        {
            if (clock.Period != MatchPeriod.SecondHalf || clock.Seconds < _nextSubCheck) return;
            _nextSubCheck = clock.Seconds + 60f;
            var me = _engine.TeamOf(_team);
            int left = _engine.SubstitutionsLeft(me);
            if (left <= 0 || me.WindowsUsed >= _engine.Config.MaxSubstitutionWindows) return;
            float minute = clock.Seconds / 60f;
            int want = minute >= 82f ? 5 : minute >= 72f ? 4 : minute >= 60f ? 2 : 0;
            int done = _engine.Config.MaxSubstitutions - left;
            int now = Math.Min(want - done, 2);
            if (now <= 0) return;
            // the tired and the knocked first; nobody fresh goes off
            var tired = me.Players.Where(p => !p.SentOff && !p.IsGoalkeeper && !_askedOut.Contains(p.Number) && (p.Energy < 0.72f || p.Injury == InjuryLevel.Light))
                                  .OrderBy(p => p.Energy - (p.Injury == InjuryLevel.Light ? 1f : 0f)).ToList();
            foreach (var p in tired)
            {
                if (now <= 0) break;
                var group = p.Slot.Line == Line.Defence ? PositionGroup.Defender : p.Slot.Line == Line.Midfield ? PositionGroup.Midfielder : PositionGroup.Forward;
                var sub = me.Bench.Where(b => !_askedIn.Contains(b.Number) && b.Profile.NaturalPosition != PositionGroup.Goalkeeper)
                                  .OrderByDescending(b => (b.Profile.NaturalPosition == group ? 1000 : 0) + b.Profile.Attributes.Mental + b.Profile.Attributes.Athletic).FirstOrDefault();
                if (sub == null) break;
                _askedOut.Add(p.Number);
                _askedIn.Add(sub.Number);
                _engine.Submit(new SubstituteCommand(_team, p.Number, sub.Number));
                now--;
            }
        }
    }
}

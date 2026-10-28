using System;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// The computer's manager in a live match against a human: every few minutes it reads the game and the human's
    /// tactics and answers them, through the same tactics commands the human uses.
    ///  - Against a high line it plays balls in behind; against a deep block it pushes up, stretches the pitch and keeps the
    ///    ball; against a narrow team it goes wide; against an aggressive one it moves the ball quickly.
    ///  - Losing after the hour it attacks (higher, faster, more aggressive late on); winning late it sits deep and slows
    ///    the game down.
    /// </summary>
    public sealed class ComputerManager
    {
        private readonly MatchEngine _engine;
        private readonly int _team;
        private float _nextCheck;
        private string _last;

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
            if (clock.Seconds < _nextCheck) return;
            _nextCheck = clock.Seconds + 4f * 60f;

            var me = _engine.TeamOf(_team);
            var human = _engine.TeamOf(1 - _team).Tactics;
            var t = new TeamTactics { Marking = me.Tactics.Marking };
            float minute = clock.Seconds / 60f;
            int diff = me.Score - _engine.TeamOf(1 - _team).Score;

            // Answer the human's set-up.
            if (human.DefensiveLine > 0.65f) { t.Passing = PassingStyle.Direct; t.DefensiveLine = 0.4f; }
            else if (human.DefensiveLine < 0.35f) { t.Passing = PassingStyle.Short; t.DefensiveLine = 0.65f; t.Width = 0.75f; }
            if (human.Width < 0.35f) t.Width = 0.85f;
            else if (human.Width > 0.7f) t.Width = 0.4f; // stay compact against a wide team
            if (human.Tackling == TacklingStyle.GetStuckIn) t.Tempo = 0.75f;
            if (human.Passing == PassingStyle.Direct) t.DefensiveLine = MathF.Min(t.DefensiveLine, 0.35f); // no space behind to run into
            if (human.Passing == PassingStyle.Short) { t.PressingIntensity = 0.7f; t.Tackling = TacklingStyle.Normal; }

            // The score and the time.
            if (diff < 0 && minute >= 55f)
            {
                t.Mentality = minute >= 75f ? 0.9f : 0.75f;
                t.DefensiveLine = MathF.Max(t.DefensiveLine, 0.7f);
                t.Tempo = 0.85f;
                t.PressingIntensity = 0.8f;
                if (minute >= 75f) t.Tackling = TacklingStyle.GetStuckIn;
            }
            else if (diff > 0 && minute >= 70f)
            {
                t.Mentality = 0.25f;
                t.DefensiveLine = 0.25f;
                t.Width = 0.4f;
                t.Tempo = 0.25f;
                t.Passing = PassingStyle.Short;
            }

            string now = t.Describe();
            if (now == _last) return;
            _last = now;
            _engine.Submit(new ChangeTacticsCommand(_team, t));
        }
    }
}

using System;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// Home advantage and momentum.
    ///
    /// Home advantage (the home team scores about a third of a goal more than the away team in the top leagues): the home
    /// players play a little more confidently (crowd, familiarity), the referee gives the home team a slightly kinder
    /// whistle (fewer fouls given against them, fewer cautions - the well-documented crowd effect on referees), and the
    /// away team tires a little faster (travel).
    ///
    /// Momentum is the psychological swing of a match: goals are what players say moves it most, and a goal that comes
    /// soon after another one, an equaliser or a late goal moves it most (research: goals cluster - a goal in the last
    /// five minutes lifts the chance of another in the next five from about 15% to 18% - and perceived momentum is higher
    /// after a team's own goal, especially a late one). It is one number from -1 (all the away team's) to +1 (all the home
    /// team's). The team with the momentum is confident: it plays a little better and a little higher, faster and braver.
    /// The team under pressure is rattled: a little worse, and it calms the game down - slower tempo, safer football. It
    /// fades with time (about 8 match minutes), and every stoppage (fouls, cards, injuries, substitutions) breaks the
    /// rhythm and takes some of it away.
    /// </summary>
    public sealed partial class MatchEngine
    {
        /// <summary>Momentum from the home team's side: -1 (the away team's) .. +1 (the home team's).</summary>
        public float Momentum { get; internal set; }

        private int _momentumStep;
        private readonly float[] _lastGoalClock = { -1e9f, -1e9f };
        private float _lastMomentumNote = -1e9f;
        private int _momentumNoteSide;

        /// <summary>The team's momentum, -1..1 (positive: it has the upper hand).</summary>
        public float MomentumOf(TeamAgent team) => team == Home ? Momentum : -Momentum;

        private void ApplyHomeAdvantage()
        {
            float ha = Config.HomeAdvantage;
            Home.HomeSkill = 1f + 0.02f * ha;
            Away.HomeSkill = 1f - 0.008f * ha;
            RefreshMomentumEffects(true);
        }

        /// <summary>Pushes momentum towards <paramref name="favoured"/>.</summary>
        internal void AddMomentum(TeamAgent favoured, float amount)
        {
            if (Config.MomentumStrength <= 0f) return;
            Momentum = MathUtil.Clamp(Momentum + (favoured == Home ? amount : -amount) * Config.MomentumStrength, -1f, 1f);
            RefreshMomentumEffects(false);
        }

        /// <summary>A stoppage breaks the rhythm: part of the momentum goes.</summary>
        internal void DampMomentum(float keep)
        {
            if (Momentum == 0f) return;
            Momentum *= keep;
            if (MathF.Abs(Momentum) < 0.005f) Momentum = 0f;
            RefreshMomentumEffects(false);
        }

        /// <summary>It fades with time (a time constant of about eight match minutes).</summary>
        internal void DecayMomentum(float clockDt)
        {
            if (Momentum == 0f) return;
            Momentum *= MathF.Exp(-clockDt / (Config.MomentumMinutes * 60f));
            if (MathF.Abs(Momentum) < 0.005f) Momentum = 0f;
            if (StepCount % 25 == 0) RefreshMomentumEffects(false);
        }

        /// <summary>Recomputes what the teams play with, when the momentum has moved a step (or on demand).</summary>
        private void RefreshMomentumEffects(bool force)
        {
            int step = (int)MathF.Round(Momentum * 6f);
            if (!force && step == _momentumStep) return;
            _momentumStep = step;
            float m = step / 6f;
            foreach (var t in Teams)
            {
                float mt = t == Home ? m : -m;
                t.ConfidenceFactor = t.HomeSkill * (1f + 0.035f * mt);
                t.MomentumLevel = mt;
                t.RefreshEff();
            }
            MaybeNoteMomentum();
        }

        /// <summary>A line of commentary when one team clearly has the upper hand (at most every four match minutes).</summary>
        private void MaybeNoteMomentum()
        {
            int side = Momentum > 0.55f ? 1 : Momentum < -0.55f ? -1 : 0;
            if (side == 0) { if (MathF.Abs(Momentum) < 0.25f) _momentumNoteSide = 0; return; }
            if (side == _momentumNoteSide || Clock.Seconds - _lastMomentumNote < 240f) return;
            _momentumNoteSide = side;
            _lastMomentumNote = Clock.Seconds;
            var on = side > 0 ? Home : Away;
            var e = Emit(MatchEventType.MomentumShift, on, null, null, Ball.Pos, side * Momentum);
            e.Text = on.Name + " have the momentum - " + on.Opponent.Name + " are rattled and slow the game down";
        }

        internal void MomentumGoal(TeamAgent scorer)
        {
            var other = scorer.Opponent;
            float minute = Clock.Seconds / 60f;
            float gain = 0.42f;
            // a goal soon after the same team's last one is a double blow
            if (Clock.Seconds - _lastGoalClock[scorer.Index] < 8f * 60f) gain += 0.22f;
            // an equaliser (the score is level again) lifts the team and flattens the other
            if (scorer.Score == other.Score) gain *= 1.25f;
            // late goals count for more
            if (minute >= 75f) gain *= 1.25f;
            // a goal soon after conceding: the answer lands hard
            if (Clock.Seconds - _lastGoalClock[other.Index] < 4f * 60f) gain *= 1.15f;
            _lastGoalClock[scorer.Index] = Clock.Seconds;
            AddMomentum(scorer, gain);
        }

        private void MomentumShot(PlayerAgent shooter, float xg) => AddMomentum(shooter.Team, 0.012f + 0.07f * xg);
    }
}

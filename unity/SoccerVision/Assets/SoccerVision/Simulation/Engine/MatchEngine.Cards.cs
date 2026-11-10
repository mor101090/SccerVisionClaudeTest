using System;

namespace SoccerVision.Simulation
{
    /// <summary>How the referee and the player come together for a card (polish round 3).</summary>
    public enum CardApproach
    {
        /// <summary>The player is near: the referee walks or jogs over to him.</summary>
        Approach,
        /// <summary>The player is far: the referee stops and calls him over; the player walks to him.</summary>
        Summon,
        /// <summary>The player turns his back and walks away; the referee does not chase - he waits, then calls him back.</summary>
        WalkAway,
    }

    /// <summary>
    /// The card as a sequence (polish round 3), one state the whole match shares: the referee never chases a player - he jogs to one who is
    /// near, calls over one who is far, and waits for one who walks away; the card counts (the event, the indicator, the statistics) from
    /// the moment he holds it up; after a red the player reacts and walks off, and play waits for him to be off the pitch. Nothing restarts
    /// while any of it is going on (<see cref="CardSequenceActive"/>).
    /// </summary>
    public sealed partial class MatchEngine
    {
        /// <summary>The referee's pace on his way to a player with a card (m/s; class D: a jog - walking is 1.5, jogging 3.9 in the engine).</summary>
        public static float CardApproachSpeed = 3.6f;
        /// <summary>Beyond this distance (m) the referee calls the player over instead of going to him (class D).</summary>
        public static float CardSummonDistance = 12f;
        /// <summary>How often a player walks away from the referee, scaled by his temper (aggression, lack of composure; class D).</summary>
        public static float CardWalkAwayChance = 0.45f;
        /// <summary>How long (s, at match pace) a player walks away before the referee calls him back (class D).</summary>
        public static float CardWalkAwaySeconds = 1.6f;
        /// <summary>How long (s, at match pace) a sent-off player stands and reacts before he walks (class D).</summary>
        public static float CardReactionSeconds = 1.8f;
        /// <summary>The longest (s, at match pace) play waits for a sent-off player to leave the pitch (class D).</summary>
        public static float SentOffLeaveWaitMax = 18f;
        /// <summary>The longest (s, at match pace) the referee waits for a player before he shows the card from where he stands (class D).</summary>
        public static float CardWaitMax = 7f;

        private CardApproach _cardHow;
        private float _cardWalkAwayUntil;
        private PlayerAgent _sendOffPending, _leavingAfterRed;
        private float _sendOffAt, _leavingSince;

        /// <summary>A card is being shown, or followed up (the red after a second yellow, a sent-off player still on the pitch), or another waits its
        /// turn: nothing restarts.</summary>
        internal bool CardSequenceActive => Referee.PendingCard != 0 || Referee.CardShown != 0 || _secondYellowRed != null || _sendOffPending != null || LeavingAfterRed
                                           || _cardQueue.Count > 0;

        /// <summary>Cards decided while another is being shown (two players booked for one incident): each waits its turn - none is lost.</summary>
        private readonly System.Collections.Generic.List<(int Card, PlayerAgent Player)> _cardQueue = new System.Collections.Generic.List<(int, PlayerAgent)>();

        /// <summary>The next card in the queue, once the referee has put the last one away.</summary>
        private void NextQueuedCard()
        {
            if (_cardQueue.Count == 0 || Referee.PendingCard != 0 || Referee.CardShown != 0) return;
            var (card, p) = _cardQueue[0];
            _cardQueue.RemoveAt(0);
            if (p == null || p.SentOff) return;
            if (CollectDiagnostics) Count("card.queued");
            ShowCard(card, p);
        }

        /// <summary>How the current card is being brought about (for the renderers' and the tests' benefit).</summary>
        public CardApproach CurrentCardApproach => _cardHow;

        private bool LeavingAfterRed
        {
            get
            {
                var p = _leavingAfterRed;
                if (p == null) return false;
                if (MathF.Abs(p.Pos.Y) > Pitch.HalfWidth + 0.5f || SimTime - _leavingSince > SentOffLeaveWaitMax * Config.MotionSpeed)
                {
                    if (CollectDiagnostics) Count(MathF.Abs(p.Pos.Y) > Pitch.HalfWidth + 0.5f ? "card.red.leftPitch" : "card.red.leaveTimeout");
                    _leavingAfterRed = null;
                    return false;
                }
                return true;
            }
        }

        /// <summary>The referee decides on a card for a player away from him: how they come together.</summary>
        private void BeginCard(int card, PlayerAgent p)
        {
            Referee.PendingCard = card;
            Referee.CardFor = p;
            Referee.CardWalk = 0f;
            float d = Vec2.Distance(Referee.Pos, p.Pos);
            float temper = MathUtil.A(p.Attr.Aggression) * (1.2f - MathUtil.A(p.Attr.Composure));
            if (_rng.Chance(MathUtil.Clamp(CardWalkAwayChance * temper * (card == 2 ? 1.4f : 1f), 0f, 0.6f)))
            {
                _cardHow = CardApproach.WalkAway;
                _cardWalkAwayUntil = SimTime + CardWalkAwaySeconds * Config.MotionSpeed;
                Gesture(p, GestureKind.Protest, CardWalkAwaySeconds * Config.MotionSpeed, (Referee.Pos - p.Pos).Angle);
            }
            else _cardHow = d > CardSummonDistance ? CardApproach.Summon : CardApproach.Approach;
            if (CollectDiagnostics) Count("card.how." + _cardHow);
        }

        /// <summary>He holds the card up: from now it counts - the event the indicator and the commentary follow, the statistics, the send-off.</summary>
        private void ShowCardNow(int card, PlayerAgent p)
        {
            if (CollectDiagnostics) Count(p == null ? "card.shownNoPlayer" : Vec2.Distance(Referee.Pos, p.Pos) <= CardDistance + 0.2f ? "card.shownBeside" : "card.shownFar");
            Referee.PendingCard = 0;
            Referee.CardFor = p;
            Referee.CardShown = card;
            Referee.CardTimer = (card == 2 ? 3f : 2.2f) * Config.MotionSpeed;
            if (p == null) return;
            if (card == 1)
            {
                p.Team.Stats.YellowCards++;
                Emit(MatchEventType.YellowCard, p.Team, p, null, p.Pos, p.YellowCards);
                MaybeProtest(p, 0.5f);
                if (p.YellowCards >= 2)
                {
                    // Second caution: the yellow card first, then the red one.
                    if (CollectDiagnostics) Count("card.secondyellow");
                    _secondYellowRed = p;
                    _secondYellowRedAt = SimTime + SecondCardDelay;
                }
                return;
            }
            p.Team.Stats.RedCards++;
            AddMomentum(p.Team.Opponent, 0.3f);
            DampMomentum(0.93f);
            Emit(MatchEventType.RedCard, p.Team, p, null, p.Pos, p.YellowCards >= 2 ? 1f : 0f);
            // He takes it in (hands on his head, or an arm out at the referee), then walks.
            float temper = MathUtil.A(p.Attr.Aggression) * (1.2f - MathUtil.A(p.Attr.Composure));
            Gesture(p, _rng.Chance(MathUtil.Clamp(0.3f + temper, 0f, 0.9f)) ? GestureKind.Protest : GestureKind.Disappointed, CardReactionSeconds * Config.MotionSpeed, (Referee.Pos - p.Pos).Angle);
            _sendOffPending = p;
            _sendOffAt = SimTime + CardReactionSeconds * Config.MotionSpeed;
        }

        /// <summary>Each step: the send-off after the player's reaction.</summary>
        private void StepCardSequence()
        {
            NextQueuedCard();
            if (_sendOffPending != null && SimTime >= _sendOffAt)
            {
                var p = _sendOffPending;
                _sendOffPending = null;
                if (!p.SentOff)
                {
                    SendOff(p);
                    _leavingAfterRed = p;
                    _leavingSince = SimTime;
                }
            }
        }

        /// <summary>
        /// The referee during a card: on his way to a near player at a jog; standing (calling him over, or waiting while he walks away) for one
        /// who is far; then standing in front of him while he holds it up. Never a sprint.
        /// </summary>
        private bool CardRefereeTarget(float dt, out Vec2 target, out float speed)
        {
            target = Referee.Pos;
            speed = CardApproachSpeed;
            var pl = Referee.CardFor;
            if (Referee.PendingCard == 0 && (Referee.CardShown == 0 || pl == null || pl.SentOff)) return false;
            if (Referee.PendingCard == 0) return true; // (holding it up: he stays where he showed it)
            Referee.CardWalk += dt;
            if (_cardHow == CardApproach.WalkAway && SimTime >= _cardWalkAwayUntil)
            {
                _cardHow = CardApproach.Summon; // (he calls him back)
                if (CollectDiagnostics) Count("card.walkAway.calledBack");
            }
            // (called over and still not here halfway through his patience: he goes to him himself - at a jog)
            if (_cardHow == CardApproach.Summon && Referee.CardWalk > 0.5f * CardWaitMax * Config.MotionSpeed)
            {
                _cardHow = CardApproach.Approach;
                if (CollectDiagnostics) Count("card.summon.refereeWent");
            }
            if (pl != null && _cardHow == CardApproach.Approach)
            {
                Vec2 away = Referee.Pos - pl.Pos;
                float ad = away.Length;
                target = pl.Pos + (ad > 0.1f ? away / ad : new Vec2(0f, 1f)) * 1.6f;
            }
            // (counted after his first second: he is slowing down from wherever play had him running)
            if (CollectDiagnostics && Referee.CardWalk > 1f && Referee.Vel.Length > CardApproachSpeed + 0.3f) Count("card.refereeFast");
            // (his patience is up and they are still apart: he goes to him himself - he does not hold it up from afar; only at twice
            // that wait - a player who cannot be reached - is it shown from where he stands)
            float apart = pl == null ? 0f : Vec2.Distance(Referee.Pos, pl.Pos);
            bool late = Referee.CardWalk > CardWaitMax * Config.MotionSpeed;
            if (late && pl != null && apart > CardDistance && Referee.CardWalk <= 2f * CardWaitMax * Config.MotionSpeed)
            {
                if (_cardHow != CardApproach.Approach && CollectDiagnostics) Count("card.late.refereeWent");
                _cardHow = CardApproach.Approach;
                Vec2 away = Referee.Pos - pl.Pos;
                target = pl.Pos + (apart > 0.1f ? away / apart : new Vec2(0f, 1f)) * 1.6f;
            }
            else if (pl == null || apart <= CardDistance || late)
            {
                if (CollectDiagnostics) for (int k = 0; k <= (int)(Referee.CardWalk * 10f); k++) Count("card.walkTenths");
                ShowCardNow(Referee.PendingCard, pl);
            }
            return true; // (the restart waits on CardSequenceActive; the players meanwhile take their places, the carded one comes over)
        }

        /// <summary>
        /// The carded player during the sequence: he walks away (his back to the referee), walks over when he is called, stands when the
        /// referee comes to him, and stands while the card is held up and while he takes a red in.
        /// </summary>
        private bool CardPlayerTarget(PlayerAgent p, out Vec2 target, out float urgency)
        {
            target = p.Pos;
            urgency = 0f;
            if (p.SentOff) return false;
            bool carded = (Referee.PendingCard != 0 || Referee.CardShown != 0) && p == Referee.CardFor;
            if (!carded && p != _sendOffPending) return false;
            if (Referee.PendingCard != 0 && _cardHow == CardApproach.WalkAway)
            {
                Vec2 away = p.Pos - Referee.Pos;
                target = Pitch.ClampToField(p.Pos + (away.LengthSq > 0.01f ? away.Normalized : new Vec2(1f, 0f)) * 3f, -1f);
                urgency = 0.12f;
            }
            else if (Referee.PendingCard != 0 && _cardHow == CardApproach.Summon)
            {
                Vec2 toMe = p.Pos - Referee.Pos;
                target = Referee.Pos + (toMe.LengthSq > 0.01f ? toMe.Normalized : new Vec2(1f, 0f)) * 1.5f;
                urgency = 0.22f;
            }
            return true;
        }
    }
}

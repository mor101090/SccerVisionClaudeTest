using SoccerVision.Simulation;
using UnityEngine;

namespace SoccerVision.Runtime
{
    /// <summary>
    /// Player as described in the design document: a circle with a border, the shirt number in the centre and
    /// two small legs showing the facing direction. Grows while jumping (shadow stays on the grass), and shows
    /// "#10 - Name" under the circle while in possession.
    /// </summary>
    public sealed class PlayerView : MonoBehaviour
    {
        public const float Diameter = 1.8f;

        public PlayerAgent Agent { get; private set; }

        private Transform _body, _lift;
        private SpriteRenderer _fill, _ring, _shadow, _legL, _legR, _card;
        private TextMesh _number, _label;
        private float _stepPhase;
        private Vector2 _lastPos;
        private float _fade = 1f;

        public void Init(PlayerAgent agent, Color kit, Color ringColor, Color numberColor)
        {
            Agent = agent;
            _shadow = SpriteFactory.AddSprite(transform, "Shadow", SpriteFactory.Shadow, new Color(0f, 0f, 0f, 0.5f), 10, Diameter * 1.25f);
            // Everything except the shadow rides on _lift: when the player jumps he rises on screen while the
            // shadow stays on the grass, so the height is easy to read.
            _lift = new GameObject("Lift").transform;
            _lift.SetParent(transform, false);
            var legs = new Color(0.1f, 0.1f, 0.1f, 1f);
            _legL = SpriteFactory.AddSprite(_lift, "LegL", SpriteFactory.Circle, legs, 11, Diameter * 0.32f);
            _legR = SpriteFactory.AddSprite(_lift, "LegR", SpriteFactory.Circle, legs, 11, Diameter * 0.32f);
            _body = new GameObject("Body").transform;
            _body.SetParent(_lift, false);
            _fill = SpriteFactory.AddSprite(_body, "Fill", SpriteFactory.Circle, kit, 12, 1f);
            _ring = SpriteFactory.AddSprite(_body, "Ring", SpriteFactory.Ring, ringColor, 13, 1f);
            // TextMesh world height ~= fontSize * characterSize * 0.1 (relative to the 1-unit body here).
            _number = SpriteFactory.AddText(_body, "Number", agent.Number.ToString(), numberColor, 14, 0.085f);
            _card = SpriteFactory.AddSprite(_body, "Card", SpriteFactory.Square, new Color(1f, 0.85f, 0.1f, 1f), 15, 0.22f);
            _card.transform.localPosition = new Vector3(0.38f, 0.38f, 0f);
            _card.transform.localScale = new Vector3(0.16f, 0.22f, 1f);
            _card.enabled = false;
            _label = SpriteFactory.AddText(transform, "Label", agent.Profile.Label.Replace(" ", " - "), Color.white, 30, 0.15f, 56);
            _label.transform.localPosition = new Vector3(0f, -1.55f, 0f);
            _label.gameObject.SetActive(false);
            _lastPos = new Vector2(agent.Pos.X, agent.Pos.Y);
            UpdateBodyScale(0f);
        }

        public void SetKit(Color kit, Color ringColor, Color numberColor)
        {
            _fill.color = kit;
            _ring.color = ringColor;
            _number.color = numberColor;
        }

        public void Render(float alpha, bool hasBall, float dt)
        {
            var a = Agent;
            float x = Mathf.Lerp(a.PrevPos.X, a.Pos.X, alpha);
            float y = Mathf.Lerp(a.PrevPos.Y, a.Pos.Y, alpha);
            float facing = Mathf.LerpAngle(a.PrevFacing * Mathf.Rad2Deg, a.Facing * Mathf.Rad2Deg, alpha) * Mathf.Deg2Rad;
            float jump = Mathf.Lerp(a.PrevJumpZ, a.JumpZ, alpha);
            transform.localPosition = new Vector3(x, y, 0f);

            // Legs: step animation proportional to running speed.
            var pos = new Vector2(x, y);
            float speed = dt > 0f ? (pos - _lastPos).magnitude / dt : 0f;
            _lastPos = pos;
            _stepPhase += Mathf.Clamp(speed, 0f, 40f) * dt * 1.6f;
            var fwd = new Vector2(Mathf.Cos(facing), Mathf.Sin(facing));
            var side = new Vector2(-fwd.y, fwd.x);
            float stride = speed > 0.4f ? Mathf.Sin(_stepPhase) * 0.18f * Diameter : 0f;
            float r = Diameter * 0.5f;
            Vector2 l = fwd * (r * 0.62f + stride) + side * (r * 0.42f);
            Vector2 rr = fwd * (r * 0.62f - stride) - side * (r * 0.42f);
            _legL.transform.localPosition = new Vector3(l.x, l.y, 0f);
            _legR.transform.localPosition = new Vector3(rr.x, rr.y, 0f);

            UpdateBodyScale(jump);
            _card.enabled = a.YellowCards > 0 && !a.SentOff;
            if (_label.gameObject.activeSelf != hasBall) _label.gameObject.SetActive(hasBall);

            // Sent-off players walk off and fade away.
            float target = a.SentOff && Mathf.Abs(y) > Pitch.HalfWidth + 1.5f ? 0f : 1f;
            _fade = Mathf.MoveTowards(_fade, target, dt * 1.5f);
            SetAlpha(_fade);
        }

        private void UpdateBodyScale(float jump)
        {
            // Design doc: the circle grows at the peak of a jump, the shadow stays on the ground and fades.
            _lift.localPosition = new Vector3(0f, Mathf.Clamp(jump, 0f, 1.5f) * 1.8f, 0f);
            float s = Diameter * (1f + Mathf.Clamp(jump, 0f, 1.2f) * 0.3f);
            _body.localScale = new Vector3(s, s, 1f);
            float shadow = Diameter * 1.25f * (1f - Mathf.Clamp01(jump) * 0.25f);
            _shadow.transform.localScale = new Vector3(shadow, shadow, 1f);
            var c = _shadow.color;
            c.a = 0.5f * (1f - Mathf.Clamp01(jump) * 0.4f) * _fade;
            _shadow.color = c;
        }

        private void SetAlpha(float a)
        {
            if (Mathf.Approximately(_fill.color.a, a)) return;
            void Set(SpriteRenderer sr) { var c = sr.color; c.a = a; sr.color = c; }
            Set(_fill);
            Set(_ring);
            Set(_legL);
            Set(_legR);
            var n = _number.color; n.a = a; _number.color = n;
            gameObject.SetActive(a > 0.01f || !Agent.SentOff);
        }
    }

    /// <summary>
    /// Ball with the design document's height illusion: it grows towards its peak and shrinks as it falls;
    /// its shadow stays on the grass, smaller and lighter the higher the ball. Fast shots leave a short trail.
    /// </summary>
    public sealed class BallView : MonoBehaviour
    {
        public const float Diameter = 0.75f;
        private SpriteRenderer _ball, _outline, _shadow;
        private TrailRenderer _trail;
        private Vector2 _last;

        public void Init()
        {
            _shadow = SpriteFactory.AddSprite(transform, "Shadow", SpriteFactory.Shadow, new Color(0f, 0f, 0f, 0.6f), 40, Diameter * 1.2f);
            var body = new GameObject("Ball").transform;
            body.SetParent(transform, false);
            _ball = SpriteFactory.AddSprite(body, "Fill", SpriteFactory.Circle, Color.white, 50, 1f);
            _outline = SpriteFactory.AddSprite(body, "Outline", SpriteFactory.Ring, new Color(0.1f, 0.1f, 0.1f, 1f), 51, 1f);
            _trail = body.gameObject.AddComponent<TrailRenderer>();
            _trail.time = 0.18f;
            _trail.startWidth = Diameter * 0.8f;
            _trail.endWidth = 0f;
            _trail.minVertexDistance = 0.2f;
            var shader = Shader.Find("Sprites/Default");
            if (shader != null) _trail.material = new Material(shader);
            _trail.startColor = new Color(1f, 1f, 1f, 0.55f);
            _trail.endColor = new Color(1f, 1f, 1f, 0f);
            _trail.sortingOrder = 45;
            _trail.emitting = false;
        }

        public void Render(BallState ball, float alpha, float dt)
        {
            float x = Mathf.Lerp(ball.PrevPos.X, ball.Pos.X, alpha);
            float y = Mathf.Lerp(ball.PrevPos.Y, ball.Pos.Y, alpha);
            float z = Mathf.Lerp(ball.PrevZ, ball.Z, alpha);
            float h = Mathf.Max(0f, z - Pitch.BallRadius);
            transform.localPosition = new Vector3(x, y, 0f);
            var body = _ball.transform.parent;
            float s = Diameter * (1f + Mathf.Clamp(h, 0f, 20f) * 0.07f);
            body.localScale = new Vector3(s, s, 1f);
            // The ball rises on screen with its height; the shadow stays on the grass beneath it.
            body.localPosition = new Vector3(0f, Mathf.Clamp(h, 0f, 20f) * 0.55f, 0f);
            float sh = Diameter * 1.2f * Mathf.Clamp(1f - h * 0.05f, 0.45f, 1f);
            _shadow.transform.localScale = new Vector3(sh, sh, 1f);
            var c = _shadow.color;
            c.a = Mathf.Clamp(0.6f - h * 0.04f, 0.18f, 0.6f);
            _shadow.color = c;
            // In the air the ball renders above the players, on the ground below their bodies.
            int order = h > 1.2f ? 60 : 50;
            _ball.sortingOrder = order;
            _outline.sortingOrder = order + 1;

            var p = new Vector2(x, y);
            float speed = dt > 0f ? (p - _last).magnitude / dt : 0f;
            _last = p;
            _trail.emitting = speed > 40f && ball.IsFree; // ~18 m/s of football at the default playback speed
        }

        public void ResetTrail()
        {
            if (_trail != null) _trail.Clear();
        }
    }

    /// <summary>Referee (centre) and assistant referees (touchlines, flag raised for offside / ball out).</summary>
    public sealed class OfficialView : MonoBehaviour
    {
        private Official _o;
        private SpriteRenderer _flag;

        public void Init(Official official, bool assistant)
        {
            _o = official;
            SpriteFactory.AddSprite(transform, "Shadow", SpriteFactory.Shadow, new Color(0f, 0f, 0f, 0.3f), 10, 1.9f);
            SpriteFactory.AddSprite(transform, "Fill", SpriteFactory.Circle, new Color(0.08f, 0.08f, 0.08f, 1f), 12, 1.45f);
            SpriteFactory.AddSprite(transform, "Ring", SpriteFactory.Ring, assistant ? new Color(1f, 0.85f, 0.1f) : new Color(1f, 0.92f, 0.2f), 13, 1.45f);
            var letter = SpriteFactory.AddText(transform, "Letter", assistant ? "L" : "R", assistant ? new Color(1f, 0.85f, 0.1f) : new Color(1f, 0.92f, 0.2f), 14, 0.09f);
            letter.fontStyle = FontStyle.Bold;
            if (assistant)
            {
                _flag = SpriteFactory.AddSprite(transform, "Flag", SpriteFactory.Flag, new Color(1f, 0.85f, 0.1f), 14, 1.1f);
                _flag.transform.localPosition = new Vector3(0.2f, 0.9f, 0f);
                _flag.enabled = false;
            }
        }

        public void Render(float alpha, float time)
        {
            float x = Mathf.Lerp(_o.PrevPos.X, _o.Pos.X, alpha);
            float y = Mathf.Lerp(_o.PrevPos.Y, _o.Pos.Y, alpha);
            transform.localPosition = new Vector3(x, y, 0f);
            if (_flag != null)
            {
                _flag.enabled = _o.FlagRaised;
                if (_o.FlagRaised) _flag.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 12f) * 12f);
            }
        }
    }

    /// <summary>Corner flag with a gentle flutter.</summary>
    public sealed class CornerFlagView : MonoBehaviour
    {
        private Transform _cloth;
        private float _phase;

        public void Init(float phase)
        {
            _phase = phase;
            SpriteFactory.AddSprite(transform, "Base", SpriteFactory.Circle, new Color(0.95f, 0.95f, 0.95f), 20, 0.35f);
            var pole = SpriteFactory.AddSprite(transform, "Pole", SpriteFactory.Square, new Color(0.9f, 0.9f, 0.9f), 21, 1f);
            pole.transform.localScale = new Vector3(0.12f, 1.6f, 1f);
            pole.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            var cloth = SpriteFactory.AddSprite(transform, "Cloth", SpriteFactory.Flag, new Color(1f, 0.78f, 0.1f), 22, 0.9f);
            _cloth = cloth.transform;
            _cloth.localPosition = new Vector3(0.05f, 1.35f, 0f);
        }

        private void Update()
        {
            float t = Time.time * 3.2f + _phase;
            _cloth.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t) * 9f + Mathf.Sin(t * 2.3f) * 4f);
            _cloth.localScale = new Vector3(0.9f * (0.9f + 0.1f * Mathf.Sin(t * 1.7f)), 0.9f, 1f);
        }
    }
}

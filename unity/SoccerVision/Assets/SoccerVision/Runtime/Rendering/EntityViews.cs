using SoccerVision.Simulation;
using UnityEngine;

namespace SoccerVision.Runtime
{
    /// <summary>
    /// Player as described in the design document: a circle with a border and the shirt number in the centre.
    /// Rises while jumping (its shadow stays on the grass, shrinking with height), shows the player's name, centred on a
    /// dark plate, under the circle while in possession, and a chequered square above him when he is caught offside.
    /// After a slide tackle the body lies stretched along the slide (with a streak of churned-up grass while sliding).
    /// A player injured but playing on shows a white square with a red cross; a substitute takes over the view of the
    /// player he replaces (number and name are refreshed).
    /// </summary>
    public sealed class PlayerView : MonoBehaviour
    {
        public const float Diameter = 1.8f;

        public PlayerAgent Agent { get; private set; }

        private Transform _body, _lift;
        private SpriteRenderer _fill, _ring, _shadow, _card, _streak;
        private Transform _feet;
        private readonly SpriteRenderer[] _boots = new SpriteRenderer[2];
        private readonly SpriteRenderer[] _extraShadows = new SpriteRenderer[3];
        private readonly Vector3[] _lights = new Vector3[4];
        private GameObject _injury;
        private PlayerProfile _shownProfile;
        private GameObject _offsideMark;
        private TextMesh _number, _label;
        private SpriteRenderer _labelPlate;
        private float _fade = 1f;

        public void Init(PlayerAgent agent, Color kit, Color ringColor, Color numberColor)
        {
            Agent = agent;
            _shadow = SpriteFactory.AddSprite(transform, "Shadow", SpriteFactory.Shadow, new Color(0f, 0f, 0f, 0.5f), 10, Diameter * 1.25f);
            // at night three more, one from each floodlight tower
            for (int i = 0; i < 3; i++) _extraShadows[i] = SpriteFactory.AddSprite(transform, "Shadow" + (i + 2), SpriteFactory.Shadow, new Color(0f, 0f, 0f, 0.16f), 10, Diameter * 1.25f);
            // Everything except the shadow rides on _lift: when the player jumps he rises on screen while the
            // shadow stays on the grass, so the height is easy to read.
            _lift = new GameObject("Lift").transform;
            _lift.SetParent(transform, false);
            _body = new GameObject("Body").transform;
            _body.SetParent(_lift, false);
            _fill = SpriteFactory.AddSprite(_body, "Fill", SpriteFactory.Circle, kit, 12, 1f);
            // the design's player marker: shirt colour, a thin edge in the team's trim colour (black by default), the number
            _ring = SpriteFactory.AddSprite(_body, "Ring", SpriteFactory.ThinRing, ringColor, 13, 1f);
            // two small boots peeking out under the circle, pointing where he faces (static, as in the design)
            _feet = new GameObject("Feet").transform;
            _feet.SetParent(_body, false);
            for (int side = -1; side <= 1; side += 2)
            {
                var boot = SpriteFactory.AddSprite(_feet, "Boot", SpriteFactory.Circle, new Color(0.91f, 0.925f, 0.937f), 11, 1f);
                boot.transform.localPosition = new Vector3(0.4f, 0.16f * side, 0f); // close under the front edge, a little back, close together
                boot.transform.localScale = new Vector3(0.3f, 0.21f, 1f);
                _boots[side < 0 ? 0 : 1] = boot;
            }
            _streak = SpriteFactory.AddSprite(transform, "SlideStreak", SpriteFactory.Square, new Color(0.88f, 0.94f, 0.8f, 0.3f), 11, 1f);
            _streak.enabled = false;
            // TextMesh world height ~= fontSize * characterSize * 0.1 (relative to the 1-unit body here).
            _number = SpriteFactory.AddText(_body, "Number", agent.Number.ToString(), numberColor, 14, 0.085f);
            _card = SpriteFactory.AddSprite(_body, "Card", SpriteFactory.Square, new Color(1f, 0.85f, 0.1f, 1f), 15, 0.22f);
            _card.transform.localPosition = new Vector3(0.38f, 0.38f, 0f);
            _card.transform.localScale = new Vector3(0.16f, 0.22f, 1f);
            _card.enabled = false;
            // Injured but playing on: a small white square with a red cross, opposite the card.
            _injury = new GameObject("Injury");
            _injury.transform.SetParent(_body, false);
            _injury.transform.localPosition = new Vector3(-0.4f, 0.4f, 0f);
            SpriteFactory.AddSprite(_injury.transform, "Square", SpriteFactory.Square, Color.white, 15, 0.3f);
            var bar = SpriteFactory.AddSprite(_injury.transform, "CrossV", SpriteFactory.Square, new Color(0.88f, 0.15f, 0.17f), 16, 1f);
            bar.transform.localScale = new Vector3(0.055f, 0.2f, 1f);
            bar = SpriteFactory.AddSprite(_injury.transform, "CrossH", SpriteFactory.Square, new Color(0.88f, 0.15f, 0.17f), 16, 1f);
            bar.transform.localScale = new Vector3(0.2f, 0.055f, 1f);
            _injury.SetActive(false);
            _shownProfile = agent.Profile;
            _label = SpriteFactory.AddText(transform, "Label", agent.Profile.ShortName, Color.white, 30, 0.15f, 56);
            _label.transform.localPosition = new Vector3(0f, -1.6f, 0f);
            _label.gameObject.SetActive(false);
            // Plate sized to the text once it is laid out (see FitLabelPlate).
            _labelPlate = SpriteFactory.AddSprite(transform, "LabelPlate", SpriteFactory.Square, new Color(0f, 0f, 0f, 0.6f), 29, 1f);
            _labelPlate.transform.localPosition = new Vector3(0f, -1.6f, 0f);
            _labelPlate.enabled = false;
            // Offside marker: a small chequered square above the player who was caught (no text, no pole).
            _offsideMark = new GameObject("OffsideMark");
            _offsideMark.transform.SetParent(transform, false);
            _offsideMark.transform.localPosition = new Vector3(0f, 2.9f, 0f);
            const float cell = 0.65f;
            for (int cx = 0; cx < 2; cx++)
                for (int cy = 0; cy < 2; cy++)
                {
                    var sq = SpriteFactory.AddSprite(_offsideMark.transform, "Cell", SpriteFactory.Square, (cx + cy) % 2 == 1 ? new Color(0.9f, 0.22f, 0.21f) : new Color(1f, 0.82f, 0.1f), 31, cell);
                    sq.transform.localPosition = new Vector3((cx - 0.5f) * cell, (cy - 0.5f) * cell, 0f);
                }
            ShowOffside(false);
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
            float jump = Mathf.Lerp(a.PrevJumpZ, a.JumpZ, alpha);
            transform.localPosition = new Vector3(x, y, 0f);

            ShowOffside(a.OffsideMarkTimer > 0f);
            UpdateBodyScale(jump);
            UpdateDown(a.SlideTimer > 0f, a.IsDown && !a.SentOff, a.Facing);
            UpdateKick(a.KickTimer, a.KickLeft);
            _card.enabled = a.YellowCards > 0 && !a.SentOff;
            bool hurt = a.Injury != InjuryLevel.None && !a.SentOff;
            if (_injury.activeSelf != hurt) _injury.SetActive(hurt);
            if (_shownProfile != a.Profile)
            {
                // A substitute has taken over this shirt slot.
                _shownProfile = a.Profile;
                _number.text = a.Number.ToString();
                _label.text = a.Profile.ShortName;
                _fade = 1f;
            }
            if (_label.gameObject.activeSelf != hasBall)
            {
                _label.gameObject.SetActive(hasBall);
                _labelPlate.enabled = hasBall;
            }
            if (hasBall) FitLabelPlate();

            // Sent-off players walk off and fade away.
            float target = a.SentOff && Mathf.Abs(y) > Pitch.HalfWidth + 1.5f ? 0f : 1f;
            _fade = Mathf.MoveTowards(_fade, target, dt * 1.5f);
            SetAlpha(_fade);
        }

        /// <summary>The plate is centred on the label's anchor (the glyphs are centred there too) and fits the text width.</summary>
        private void FitLabelPlate()
        {
            var b = _label.GetComponent<MeshRenderer>().bounds;
            if (b.size.x < 1e-3f) return; // mesh not built yet; retried next frame
            _labelPlate.transform.localScale = new Vector3(b.size.x + 0.6f, 1.0f, 1f);
        }

        private void UpdateBodyScale(float jump)
        {
            // The body rises on screen with the leap (0.2-0.75 m), the design doc's slight growth at the peak; the
            // shadow stays on the grass and shrinks and fades in proportion to the height, so the height reads at a glance.
            float h = Mathf.Clamp(jump, 0f, 1f);
            _lift.localPosition = new Vector3(0f, h * 2.2f, 0f);
            float s = Diameter * (1f + h * 0.1f);
            _body.localScale = new Vector3(s, s, 1f);
            float shadow = Diameter * 1.25f * (1f - h * 0.6f);
            // shadows fall away from the light: the sun, a grey sky or the four floodlights
            int n = Atmosphere.Shadows(new Vector2(transform.localPosition.x, transform.localPosition.y), _lights);
            for (int i = 0; i < 4; i++)
            {
                var sr = i == 0 ? _shadow : _extraShadows[i - 1];
                bool on = i < n;
                if (sr.enabled != on) sr.enabled = on;
                if (!on) continue;
                var l = _lights[i];
                sr.transform.localPosition = new Vector3(l.x, l.y, 0f);
                bool stretched = n > 1 || Atmosphere.Look.sky == Weather.Sunny;
                sr.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(l.y, l.x) * Mathf.Rad2Deg);
                sr.transform.localScale = new Vector3(shadow * (stretched ? 1.28f : 1f), shadow * (stretched ? 0.86f : 1f), 1f);
                var c = sr.color;
                c.a = Mathf.Min(0.8f, 0.5f * l.z) * (1f - h * 0.6f) * _fade;
                sr.color = c;
            }
        }

        /// <summary>On the ground: the kit circle becomes a body stretched along the facing direction (the number stays upright).</summary>
        private void UpdateDown(bool sliding, bool down, float facing)
        {
            var rot = down ? Quaternion.Euler(0f, 0f, facing * Mathf.Rad2Deg) : Quaternion.identity;
            var scale = down ? new Vector3(1.22f, 0.93f, 1f) : Vector3.one;
            _fill.transform.localRotation = rot;
            _ring.transform.localRotation = rot;
            _fill.transform.localScale = scale;
            _ring.transform.localScale = scale;
            _feet.gameObject.SetActive(!down);
            if (!down) _feet.localRotation = Quaternion.Euler(0f, 0f, facing * Mathf.Rad2Deg);
            _streak.enabled = sliding;
            if (sliding)
            {
                const float len = 2.0f;
                Vector2 dir = new Vector2(Mathf.Cos(facing), Mathf.Sin(facing));
                _streak.transform.localPosition = new Vector3(-dir.x * len * 0.5f, -dir.y * len * 0.5f, 0f);
                _streak.transform.localRotation = Quaternion.Euler(0f, 0f, facing * Mathf.Rad2Deg);
                _streak.transform.localScale = new Vector3(len, Diameter * 0.55f, 1f);
            }
        }

        /// <summary>
        /// The swing of a kick or a pass: the kicking foot goes back (backswing), forward through the ball, then settles.
        /// The legs never run on the spot. _boots[0] is the right foot, _boots[1] the left.
        /// </summary>
        private void UpdateKick(float timer, bool left)
        {
            float offset = 0f;
            if (timer > 0f)
            {
                float t = 1f - timer / PlayerAgent.KickSwing;
                offset = t < 0.4f ? Mathf.Lerp(0f, -0.55f, t / 0.4f) : t < 0.6f ? Mathf.Lerp(-0.55f, 0.55f, (t - 0.4f) / 0.2f) : Mathf.Lerp(0.55f, 0f, (t - 0.6f) / 0.4f);
            }
            for (int i = 0; i < 2; i++)
            {
                var p = _boots[i].transform.localPosition;
                bool kicking = (i == 1) == left;
                p.x = 0.4f + (kicking ? offset * 0.5f : 0f);
                _boots[i].transform.localPosition = p;
            }
        }

        private void ShowOffside(bool on)
        {
            if (_offsideMark.activeSelf != on) _offsideMark.SetActive(on);
        }

        private void SetAlpha(float a)
        {
            if (Mathf.Approximately(_fill.color.a, a)) return;
            void Set(SpriteRenderer sr) { var c = sr.color; c.a = a; sr.color = c; }
            Set(_fill);
            Set(_ring);
            Set(_boots[0]);
            Set(_boots[1]);
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
        private SpriteRenderer _ball, _shadow;
        private Ball3DTexture _look;
        private readonly Vector3[] _lights = new Vector3[4];
        private TrailRenderer _trail;
        private Vector2 _last;

        public void Init()
        {
            _shadow = SpriteFactory.AddSprite(transform, "Shadow", SpriteFactory.Shadow, new Color(0f, 0f, 0f, 0.6f), 40, Diameter * 1.2f);
            var body = new GameObject("Ball").transform;
            body.SetParent(transform, false);
            // A 3D-looking ball (black pentagons, white hexagons, lit from the top left) that rotates as it rolls.
            _look = new Ball3DTexture(48);
            var go = new GameObject("Fill");
            go.transform.SetParent(body, false);
            _ball = go.AddComponent<SpriteRenderer>();
            _ball.sprite = _look.Sprite;
            _ball.sortingOrder = 50;
            // the snow ball's glow: a soft orange halo (lit only in the snow)
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

        public void Render(BallState ball, float alpha, float dt, bool inPlay = true)
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
            _ball.sortingOrder = h > 1.2f ? 60 : 50;
            // Held by the keeper or carried overhead for a throw-in: the ball moves with the hands and does not roll.
            // No spin while held (keeper's hands, carried overhead for a throw-in) or while play is stopped (the ball waits to be taken).
            bool held = !inPlay || (ball.Owner != null && (ball.InHands || ball.Owner.IsGoalkeeper || z > 0.4f));
            _look.Spin(new Vector2(x, y), h, dt, held);
            _look.Winter = Atmosphere.Look.sky == Weather.Snow;
            _look.Redraw();
            Atmosphere.Shadows(new Vector2(x, y), _lights);
            _shadow.transform.localPosition = new Vector3(_lights[0].x, _lights[0].y, 0f) * (Atmosphere.Look.night ? 0f : 1f);

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
        private Transform _flag;
        private SpriteRenderer _card;
        private Transform _cardArm;
        private Transform _feet;

        public void Init(Official official, bool assistant)
        {
            _o = official;
            // Same size as the players.
            float d = PlayerView.Diameter;
            SpriteFactory.AddSprite(transform, "Shadow", SpriteFactory.Shadow, new Color(0f, 0f, 0f, 0.3f), 10, d * 1.25f);
            _fill = SpriteFactory.AddSprite(transform, "Fill", SpriteFactory.Circle, new Color(0.08f, 0.08f, 0.08f, 1f), 12, d);
            _trim = assistant ? new Color(1f, 0.85f, 0.1f) : new Color(1f, 0.92f, 0.2f);
            _ringSr = SpriteFactory.AddSprite(transform, "Ring", SpriteFactory.ThinRing, _trim, 13, d);
            // two small boots, as the players have: the referee watches the ball, the assistants face the pitch
            _feet = new GameObject("Feet").transform;
            _feet.SetParent(transform, false);
            for (int side = -1; side <= 1; side += 2)
            {
                var boot = SpriteFactory.AddSprite(_feet, "Boot", SpriteFactory.Circle, new Color(0.91f, 0.925f, 0.937f), 11, d * 0.3f);
                boot.transform.localPosition = new Vector3(d * 0.4f, d * 0.16f * side, 0f); // (as a player's)
                boot.transform.localScale = new Vector3(d * 0.3f, d * 0.21f, 1f);
            }
            var letter = SpriteFactory.AddText(transform, "Letter", assistant ? "L" : "R", _trim, 14, 0.112f);
            _letter = letter;
            letter.fontStyle = FontStyle.Bold;
            if (assistant)
            {
                // Raised only when he signals (as before): black handle and the checked orange / neon-yellow flag.
                _flag = new GameObject("Flag").transform;
                _flag.SetParent(transform, false);
                _flag.localPosition = new Vector3(0.3f, 0.5f, 0f);
                var handle = SpriteFactory.AddSprite(_flag, "Handle", SpriteFactory.Square, new Color(0.07f, 0.07f, 0.07f), 15, 1f);
                handle.transform.localScale = new Vector3(0.15f, 2.1f, 1f);
                handle.transform.localPosition = new Vector3(0f, 1.05f, 0f);
                var cloth = SpriteFactory.AddSprite(_flag, "Cloth", SpriteFactory.CheckerFlag, Color.white, 16, 1f);
                cloth.transform.localScale = new Vector3(1.6f, 1.45f, 1f);
                cloth.transform.localPosition = new Vector3(0f, 1.55f, 0f);
                _flag.gameObject.SetActive(false);
            }
            else
            {
                // The card the referee holds up.
                _cardArm = SpriteFactory.AddSprite(transform, "Arm", SpriteFactory.Square, new Color(0.07f, 0.07f, 0.07f), 15, 1f).transform;
                _cardArm.localScale = new Vector3(0.14f, 2.1f, 1f);
                _cardArm.localPosition = new Vector3(0.3f, 1.5f, 0f);
                _card = SpriteFactory.AddSprite(transform, "Card", SpriteFactory.Square, Color.yellow, 16, 1f);
                _card.transform.localScale = new Vector3(1.05f, 1.45f, 1f);
                _card.transform.localPosition = new Vector3(0.45f, 2.9f, 0f);
                _card.enabled = false;
                _cardArm.gameObject.SetActive(false);
            }
        }

        private SpriteRenderer _fill, _ringSr;
        private TextMesh _letter;
        private Color _trim;

        /// <summary>
        /// Shirt, trim and letter from the developer settings (empty shirt: black with yellow trim; trim and letter not
        /// picked follow the shirt in black or white).
        /// </summary>
        public void SetColour(string shirt, string trim, string letter)
        {
            Color mark = _trim;
            if (!string.IsNullOrEmpty(shirt) && ColorUtility.TryParseHtmlString(shirt, out var c))
            {
                _fill.color = c;
                mark = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f > 0.63f ? new Color(0.07f, 0.07f, 0.07f) : Color.white;
            }
            else _fill.color = new Color(0.08f, 0.08f, 0.08f, 1f);
            _ringSr.color = !string.IsNullOrEmpty(trim) && ColorUtility.TryParseHtmlString(trim, out var t) ? t : mark;
            _letter.color = !string.IsNullOrEmpty(letter) && ColorUtility.TryParseHtmlString(letter, out var l) ? l : mark;
        }

        public void Render(float alpha, float time)
        {
            float x = Mathf.Lerp(_o.PrevPos.X, _o.Pos.X, alpha);
            float y = Mathf.Lerp(_o.PrevPos.Y, _o.Pos.Y, alpha);
            transform.localPosition = new Vector3(x, y, 0f);
            _feet.localRotation = Quaternion.Euler(0f, 0f, _o.Facing * Mathf.Rad2Deg);
            if (_flag != null)
            {
                if (_flag.gameObject.activeSelf != _o.FlagRaised) _flag.gameObject.SetActive(_o.FlagRaised);
                // A calm sway, no flapping.
                if (_o.FlagRaised) _flag.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 0.9f) * 2.5f);
            }
            if (_card != null)
            {
                bool show = _o.CardShown != 0;
                _card.enabled = show;
                if (_cardArm.gameObject.activeSelf != show) _cardArm.gameObject.SetActive(show);
                if (show) _card.color = _o.CardShown == 2 ? new Color(0.88f, 0.15f, 0.17f) : new Color(1f, 0.82f, 0.1f);
            }
        }
    }

    /// <summary>
    /// Corner flag seen from above (top view, like the rest of the pitch): the white top of the pole and the yellow cloth
    /// seen edge-on, hanging out away from the pitch and swaying slowly in the breeze.
    /// </summary>
    public sealed class CornerFlagView : MonoBehaviour
    {
        private Transform _cloth;
        private Transform _shadow;
        private float _phase;

        public void Init(float phase)
        {
            _phase = phase;
            _shadow = SpriteFactory.AddSprite(transform, "Shadow", SpriteFactory.Cloth, new Color(0f, 0f, 0f, 0.22f), 20, 1f).transform;
            _shadow.localPosition = new Vector3(0.35f, -0.35f, 0f);
            _cloth = SpriteFactory.AddSprite(transform, "Cloth", SpriteFactory.Cloth, new Color(1f, 0.82f, 0.12f), 21, 1f).transform;
            SpriteFactory.AddSprite(transform, "Pole", SpriteFactory.Circle, new Color(0.96f, 0.96f, 0.95f), 22, 0.4f);
        }

        private void Update()
        {
            float t = Time.time + _phase;
            // in a strong wind the cloth streams out downwind, stretched and rippling fast; in still air it hangs limp
            bool windy = Atmosphere.Windy;
            float baseAngle = Atmosphere.WindAngle * Mathf.Rad2Deg; // the wind, or at least a breeze
            float angle = baseAngle + (Mathf.Sin(t * (windy ? 1.3f : 0.45f)) * (windy ? 0.07f : 0.16f) + Mathf.Sin(t * 0.17f) * 0.1f) * Mathf.Rad2Deg;
            var rot = Quaternion.Euler(0f, 0f, angle);
            var scale = new Vector3((windy ? 1.4f : 1.05f) * (0.97f + 0.03f * Mathf.Sin(t * (windy ? 4.2f : 1.7f))), 0.56f, 1f);
            _cloth.localRotation = _shadow.localRotation = rot;
            _cloth.localScale = _shadow.localScale = scale;
        }
    }
}

using SoccerVision.Simulation;
using UnityEngine;

namespace SoccerVision.Runtime
{
    /// <summary>
    /// A goal's net seen from above, drawn as a mesh of strings that deform: the ball pushes the netting out where it
    /// lands and the net ripples after a hard shot (NetHit). The posts and crossbar flash when the ball rattles them.
    /// Same model as drawNet() in web/src/replay/drawPitch.ts.
    /// </summary>
    public sealed class GoalNetView : MonoBehaviour
    {
        private const float Mesh = 0.3f;
        private int _side;
        private LineRenderer[] _strings;   // constant lateral y, from the goal line to the back
        private LineRenderer[] _rows;      // constant depth
        private float[] _ys, _ds;
        private SpriteRenderer _flash;
        private float _flashTime = -10f;
        private float _rippleTime = -10f, _rippleAmp;
        private Vector2 _rippleAt;

        public void Init(int side)
        {
            _side = side;
            var mat = new Material(Shader.Find("Sprites/Default"));
            var color = new Color(0.92f, 0.92f, 0.92f, 0.65f);
            int ny = Mathf.RoundToInt(2f * Pitch.GoalHalfWidth / Mesh) + 1;
            int nd = Mathf.RoundToInt(Pitch.GoalDepth / Mesh) + 1;
            _ys = new float[ny];
            _ds = new float[nd];
            for (int i = 0; i < ny; i++) _ys[i] = -Pitch.GoalHalfWidth + i * (2f * Pitch.GoalHalfWidth / (ny - 1));
            for (int i = 0; i < nd; i++) _ds[i] = i * (Pitch.GoalDepth / (nd - 1));
            _strings = new LineRenderer[ny];
            _rows = new LineRenderer[nd];
            for (int i = 0; i < ny; i++) _strings[i] = MakeLine("String", mat, color, nd, i == 0 || i == ny - 1 ? 0.09f : 0.035f);
            for (int i = 0; i < nd; i++) _rows[i] = MakeLine("Row", mat, color, ny, i == nd - 1 ? 0.09f : 0.035f);
            _flash = SpriteFactory.AddSprite(transform, "WoodworkFlash", SpriteFactory.Shadow, new Color(1f, 0.95f, 0.6f, 0f), 46, 1.6f);
            Deform(Vector2.zero, 0f);
        }

        private LineRenderer MakeLine(string name, Material mat, Color color, int points, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.sharedMaterial = mat;
            lr.startColor = lr.endColor = color;
            lr.startWidth = lr.endWidth = width;
            lr.positionCount = points;
            lr.sortingOrder = 45; // the roof netting is above a ball lying in the goal
            lr.numCapVertices = 0;
            return lr;
        }

        /// <summary>Net colour (developer settings).</summary>
        public void SetColour(Color c)
        {
            c.a = 0.65f;
            foreach (var lr in _strings) lr.startColor = lr.endColor = c;
            foreach (var lr in _rows) lr.startColor = lr.endColor = c;
        }

        /// <summary>A shot struck the net (speed m/s, impact on the ground plane).</summary>
        public void OnNetHit(Vector2 at, float speed)
        {
            // Keep the stronger ripple if one is still running (a soft second touch must not cancel it).
            float remaining = _rippleAmp * Mathf.Exp(-3f * (Time.time - _rippleTime));
            if (Mathf.Min(0.9f, speed * 0.045f) < remaining) return;
            _rippleTime = Time.time;
            _rippleAmp = Mathf.Min(0.9f, speed * 0.045f);
            _rippleAt = at;
        }

        public void OnWoodwork(Vector2 at)
        {
            _flashTime = Time.time;
            _flash.transform.localPosition = new Vector3(at.x, at.y, 0f);
        }

        public void Render(BallState ball)
        {
            // Ball inside this goal: the netting is pressed out around it.
            float pressAmp = 0f;
            Vector2 pressAt = Vector2.zero;
            if (ball.Pos.X * _side > Pitch.HalfLength && Mathf.Abs(ball.Pos.Y) < Pitch.GoalHalfWidth + 0.5f)
            {
                float depth = ball.Pos.X * _side - Pitch.HalfLength;
                pressAmp = Mathf.Clamp01(depth / Pitch.GoalDepth) * 0.35f;
                pressAt = new Vector2(ball.Pos.X, ball.Pos.Y);
            }
            float t = Time.time - _rippleTime;
            float ripple = t < 2f ? _rippleAmp * Mathf.Exp(-3f * t) * Mathf.Cos(12f * t) : 0f;
            if (ripple * ripple > pressAmp * pressAmp || pressAmp == 0f) { Deform(_rippleAt, ripple); }
            else Deform(pressAt, pressAmp);

            float ft = Time.time - _flashTime;
            var c = _flash.color;
            c.a = ft < 0.6f ? 0.85f * (1f - ft / 0.6f) : 0f;
            _flash.color = c;
            float s = 1.2f + ft * 1.5f;
            _flash.transform.localScale = new Vector3(s, s, 1f);
        }

        /// <summary>Gaussian bulge of the given amplitude (m) centred on a point; pushes strings outward (away from the pitch).</summary>
        private void Deform(Vector2 at, float amp)
        {
            float gl = Pitch.HalfLength * _side;
            float atD = at.x * _side - Pitch.HalfLength;
            const float sigma2 = 2f * 0.85f * 0.85f;
            Vector3 P(float y, float d)
            {
                float w = amp == 0f ? 0f : Mathf.Exp(-((y - at.y) * (y - at.y) + (d - atD) * (d - atD)) / sigma2);
                float edge = Mathf.Clamp01(d / Pitch.GoalDepth); // fixed at the goal line, free at the back
                float push = amp * w * (0.35f + 0.65f * edge);
                float spread = (y - at.y) * amp * w * 0.25f;
                return new Vector3(gl + (d + push) * _side, y + spread, 0f);
            }
            for (int i = 0; i < _strings.Length; i++)
                for (int k = 0; k < _ds.Length; k++) _strings[i].SetPosition(k, P(_ys[i], _ds[k]));
            for (int k = 0; k < _rows.Length; k++)
                for (int i = 0; i < _ys.Length; i++) _rows[k].SetPosition(i, P(_ys[i], _ds[k]));
        }
    }
}

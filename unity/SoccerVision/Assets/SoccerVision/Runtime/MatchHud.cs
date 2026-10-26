using SoccerVision.Simulation;
using UnityEngine;

namespace SoccerVision.Runtime
{
    /// <summary>
    /// Built-in HUD for standalone/editor play (the React shell can hide it and draw its own): scoreboard
    /// top-left, live commentary box below the field (colour coded, scrollable), hover tooltip with skills,
    /// half-time / full-time panels with statistics and basic playback controls.
    /// </summary>
    public sealed class MatchHud : MonoBehaviour
    {
        public const float PanelFraction = 0.22f;
        public MatchController Controller;

        private GUIStyle _score, _small, _comment, _box, _title, _button;
        private Vector2 _scroll;
        private int _lastCount;
        private float _scale = 1f;
        private Texture2D _white;

        private static readonly Color GoalColor = new Color(0.35f, 0.95f, 0.45f);
        private static readonly Color FoulColor = new Color(1f, 0.42f, 0.38f);
        private static readonly Color CardColor = new Color(1f, 0.86f, 0.25f);
        private static readonly Color ChanceColor = new Color(1f, 0.7f, 0.35f);
        private static readonly Color InfoColor = new Color(0.6f, 0.82f, 1f);
        private static readonly Color PanelColor = new Color(0.07f, 0.09f, 0.12f, 0.96f);

        private void EnsureStyles()
        {
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.6f, 2f);
            if (_score != null && Mathf.Approximately(scale, _scale)) return;
            _scale = scale;
            _white = Texture2D.whiteTexture;
            _score = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(30 * scale), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _score.normal.textColor = Color.white;
            _small = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(17 * scale), alignment = TextAnchor.MiddleLeft, richText = true };
            _small.normal.textColor = new Color(0.85f, 0.88f, 0.92f);
            _comment = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(18 * scale), richText = true, wordWrap = true };
            _comment.normal.textColor = Color.white;
            _title = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(26 * scale), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
            _title.normal.textColor = Color.white;
            _box = new GUIStyle(GUI.skin.box);
            _button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(16 * scale) };
        }

        private void OnGUI()
        {
            if (Controller == null || Controller.Engine == null || !Controller.showHud) return;
            EnsureStyles();
            var m = Controller.Engine;
            DrawScoreboard(m);
            DrawPanel(m);
            DrawTooltip(m);
            if (m.Phase == MatchPhase.HalfTimeBreak) DrawStatsOverlay(m, "HALF TIME", "Second half in " + FormatSeconds(m.HalftimeRemaining));
            else if (m.IsFinished) DrawStatsOverlay(m, "FULL TIME", null);
        }

        private void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = old;
        }

        private void DrawScoreboard(MatchEngine m)
        {
            float s = _scale;
            float x = 14f * s, y = 12f * s, h = 46f * s;
            float teamW = 92f * s, scoreW = 96f * s, minuteW = 84f * s;
            Fill(new Rect(x, y, teamW * 2 + scoreW + minuteW, h), new Color(0f, 0f, 0f, 0.78f));
            var home = SpriteFactory.Hex(m.Home.Setup.Kit.Primary, Color.red);
            var away = SpriteFactory.Hex(m.Away.Setup.Kit.Primary, Color.blue);
            Fill(new Rect(x, y, 8f * s, h), home);
            Fill(new Rect(x + teamW * 2 + scoreW - 8f * s, y, 8f * s, h), away);
            GUI.Label(new Rect(x, y, teamW, h), m.Home.ShortName, _score);
            GUI.Label(new Rect(x + teamW, y, scoreW, h), m.Home.Score + " - " + m.Away.Score, _score);
            GUI.Label(new Rect(x + teamW + scoreW, y, teamW, h), m.Away.ShortName, _score);
            Fill(new Rect(x + teamW * 2 + scoreW, y, minuteW, h), new Color(0.85f, 0.85f, 0.85f, 0.95f));
            var minuteStyle = new GUIStyle(_score);
            minuteStyle.normal.textColor = Color.black;
            minuteStyle.fontSize = Mathf.RoundToInt(24 * s);
            GUI.Label(new Rect(x + teamW * 2 + scoreW, y, minuteW, h), m.Clock.MinuteText, minuteStyle);
            if (m.Clock.AddedMinutes > 0 && m.Clock.InAddedTime && m.Phase != MatchPhase.HalfTimeBreak && !m.IsFinished)
            {
                Fill(new Rect(x + teamW * 2 + scoreW, y + h, minuteW, 26f * s), new Color(0.1f, 0.6f, 0.2f, 0.95f));
                var add = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                GUI.Label(new Rect(x + teamW * 2 + scoreW, y + h, minuteW, 26f * s), "+" + m.Clock.AddedMinutes, add);
            }
        }

        private void DrawPanel(MatchEngine m)
        {
            float s = _scale;
            float top = Screen.height * (1f - PanelFraction);
            var panel = new Rect(0f, top, Screen.width, Screen.height - top);
            Fill(panel, PanelColor);

            // Live commentary box (left).
            float pad = 10f * s;
            var area = new Rect(pad, top + pad, Screen.width * 0.66f, panel.height - pad * 2f);
            Fill(area, new Color(1f, 1f, 1f, 0.04f));
            var lines = Controller.Commentary;
            float lineH = 24f * s;
            float contentH = Mathf.Max(area.height, lines.Count * lineH);
            if (lines.Count != _lastCount)
            {
                _lastCount = lines.Count;
                _scroll.y = float.MaxValue; // auto-scroll to the newest line
            }
            _scroll = GUI.BeginScrollView(area, _scroll, new Rect(0f, 0f, area.width - 20f * s, contentH));
            for (int i = 0; i < lines.Count; i++)
            {
                var l = lines[i];
                string color = ColorUtility.ToHtmlStringRGB(ToneColor(l.Tone));
                GUI.Label(new Rect(6f * s, i * lineH, area.width - 30f * s, lineH), "<color=#9aa4b2>" + l.Minute + "</color>  <color=#" + color + ">" + l.Text + "</color>", _comment);
            }
            GUI.EndScrollView();

            // Quick stats and controls (right).
            float rx = area.xMax + pad;
            float rw = Screen.width - rx - pad;
            var hs = m.Home.Stats;
            var aw = m.Away.Stats;
            float poss = hs.PossessionSeconds + aw.PossessionSeconds;
            int hp = poss > 0f ? Mathf.RoundToInt(hs.PossessionSeconds / poss * 100f) : 50;
            float y = top + pad;
            float rowH = 22f * s;
            GUI.Label(new Rect(rx, y, rw, rowH), "<b>" + m.Home.ShortName + "</b>  possession  <b>" + m.Away.ShortName + "</b>", _small); y += rowH;
            GUI.Label(new Rect(rx, y, rw, rowH), hp + "%   ·   " + (100 - hp) + "%", _small); y += rowH;
            GUI.Label(new Rect(rx, y, rw, rowH), "Shots " + hs.Shots + " (" + hs.ShotsOnTarget + ")  ·  " + aw.Shots + " (" + aw.ShotsOnTarget + ")", _small); y += rowH;
            GUI.Label(new Rect(rx, y, rw, rowH), "Corners " + hs.Corners + " · " + aw.Corners + "   Fouls " + hs.Fouls + " · " + aw.Fouls, _small); y += rowH * 1.3f;

            float bw = (rw - pad * 3f) / 4f;
            float bh = 30f * s;
            if (GUI.Button(new Rect(rx, y, bw, bh), Controller.Paused ? "Play" : "Pause", _button)) Controller.Paused = !Controller.Paused;
            if (GUI.Button(new Rect(rx + (bw + pad), y, bw, bh), "x" + Controller.playbackSpeed.ToString("0"), _button))
            {
                Controller.playbackSpeed = Controller.playbackSpeed >= 8f ? 1f : Controller.playbackSpeed * 2f;
            }
            if (GUI.Button(new Rect(rx + (bw + pad) * 2f, y, bw, bh), "New match", _button)) Controller.StartMatch(Random.Range(1, int.MaxValue));
            if (GUI.Button(new Rect(rx + (bw + pad) * 3f, y, bw, bh), "Hide HUD", _button)) { Controller.showHud = false; Controller.FitCamera(); }
            // Match pace slider (50-100%, steps of 5).
            y += bh + pad;
            GUI.Label(new Rect(rx, y, bw * 1.3f, bh), "Pace " + Controller.pacePercent + "%", _small);
            float pace = GUI.HorizontalSlider(new Rect(rx + bw * 1.4f, y + bh * 0.35f, rw - bw * 1.4f, bh), Controller.pacePercent, 50f, 100f);
            Controller.pacePercent = Mathf.RoundToInt(pace / 5f) * 5;
        }

        private void DrawTooltip(MatchEngine m)
        {
            var mouse = Event.current.mousePosition;
            if (mouse.y > Screen.height * (1f - PanelFraction)) return;
            var p = Controller.PlayerAtScreen(mouse);
            if (p == null) return;
            var a = p.Attr;
            float s = _scale;
            string text = "<b>#" + p.Number + " " + p.Profile.FullName + "</b>  (" + Formation.RoleAbbreviation(p.Slot.Role) + ", " + p.Team.ShortName + ")\n" +
                          (p.IsGoalkeeper
                              ? "Reflexes " + a.Reflexes + "   Handling " + a.Handling + "   Aerial " + a.AerialAbility
                              : "Passing " + a.Passing + "   Speed " + a.Speed + "   Stamina " + a.Stamina + "\nFinishing " + a.Finishing + "   Tackling " + a.Tackling + "   Dribbling " + a.Dribbling) +
                          "\nFatigue " + Mathf.RoundToInt((1f - p.Energy) * 100f) + "%" + (p.YellowCards > 0 ? "   <color=#ffd84a>booked</color>" : "");
            var size = new Vector2(330f * s, (p.IsGoalkeeper ? 78f : 100f) * s);
            var rect = new Rect(Mathf.Min(mouse.x + 18f, Screen.width - size.x - 4f), Mathf.Max(4f, mouse.y - size.y - 8f), size.x, size.y);
            Fill(rect, new Color(0f, 0f, 0f, 0.85f));
            GUI.Label(new Rect(rect.x + 8f, rect.y + 4f, rect.width - 16f, rect.height - 8f), text, _comment);
        }

        private void DrawStatsOverlay(MatchEngine m, string title, string subtitle)
        {
            float s = _scale;
            float w = 520f * s, h = 430f * s;
            float top = Screen.height * (1f - PanelFraction);
            var r = new Rect((Screen.width - w) * 0.5f, Mathf.Max(20f, (top - h) * 0.5f), w, h);
            Fill(r, new Color(0.03f, 0.05f, 0.08f, 0.92f));
            float y = r.y + 12f * s;
            GUI.Label(new Rect(r.x, y, w, 34f * s), title, _title); y += 36f * s;
            GUI.Label(new Rect(r.x, y, w, 34f * s), m.Home.Name + "  " + m.Home.Score + " - " + m.Away.Score + "  " + m.Away.Name, _title); y += 40f * s;
            if (subtitle != null) { GUI.Label(new Rect(r.x, y, w, 24f * s), subtitle, new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter }); y += 28f * s; }

            var hs = m.Home.Stats;
            var aw = m.Away.Stats;
            float poss = hs.PossessionSeconds + aw.PossessionSeconds;
            int hp = poss > 0f ? Mathf.RoundToInt(hs.PossessionSeconds / poss * 100f) : 50;
            void Row(string label, string a, string b)
            {
                var c = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };
                GUI.Label(new Rect(r.x, y, w * 0.3f, 24f * s), a, c);
                GUI.Label(new Rect(r.x + w * 0.3f, y, w * 0.4f, 24f * s), label, c);
                GUI.Label(new Rect(r.x + w * 0.7f, y, w * 0.3f, 24f * s), b, c);
                y += 24f * s;
            }
            Row("Possession", hp + "%", (100 - hp) + "%");
            Row("Shots (on target)", hs.Shots + " (" + hs.ShotsOnTarget + ")", aw.Shots + " (" + aw.ShotsOnTarget + ")");
            Row("Expected goals", hs.ExpectedGoals.ToString("0.00"), aw.ExpectedGoals.ToString("0.00"));
            Row("Passes completed", hs.PassesCompleted + "/" + hs.Passes, aw.PassesCompleted + "/" + aw.Passes);
            Row("Corners", hs.Corners.ToString(), aw.Corners.ToString());
            Row("Fouls", hs.Fouls.ToString(), aw.Fouls.ToString());
            Row("Yellow / red cards", hs.YellowCards + " / " + hs.RedCards, aw.YellowCards + " / " + aw.RedCards);
            Row("Offsides", hs.Offsides.ToString(), aw.Offsides.ToString());
            Row("Saves", hs.Saves.ToString(), aw.Saves.ToString());
            if (m.IsFinished)
            {
                if (GUI.Button(new Rect(r.x + w * 0.3f, r.yMax - 44f * s, w * 0.4f, 34f * s), "New match", _button)) Controller.StartMatch(Random.Range(1, int.MaxValue));
            }
        }

        private static Color ToneColor(CommentaryTone tone)
        {
            switch (tone)
            {
                case CommentaryTone.Goal: return GoalColor;
                case CommentaryTone.Foul: return FoulColor;
                case CommentaryTone.Card: return CardColor;
                case CommentaryTone.Chance: return ChanceColor;
                case CommentaryTone.Info: return InfoColor;
                default: return new Color(0.92f, 0.94f, 0.96f);
            }
        }

        private static string FormatSeconds(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }
    }
}

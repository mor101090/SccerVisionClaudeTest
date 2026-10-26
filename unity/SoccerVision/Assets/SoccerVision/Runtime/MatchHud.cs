using SoccerVision.Simulation;
using UnityEngine;

namespace SoccerVision.Runtime
{
    /// <summary>
    /// Built-in HUD for standalone/editor play (the React shell can hide it and draw its own): broadcast scoreboard
    /// top-left, play/pause and settings top-right, a commentary bar under the pitch in the colour of the team on the
    /// ball, the live commentary box below (colour coded, scrollable), hover tooltip with skills, half-time /
    /// full-time panels with statistics and basic playback controls.
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
            DrawTopControls();
            DrawCommentaryBar(m);
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

        // Broadcast scoreboard (design: broadcast_scoreboard.ai, bottom variant).
        private static readonly Color ClockBg = new Color32(45, 50, 75, 255);
        private static readonly Color TeamBg = new Color32(37, 38, 51, 255);
        private static readonly Color ScoreBg = new Color32(123, 139, 176, 255);
        private static readonly Color RedMark = new Color32(237, 28, 36, 255);
        private Texture2D _logo, _iconPlay, _iconPause, _iconGear;
        private bool _settingsOpen;

        private void DrawScoreboard(MatchEngine m)
        {
            float s = _scale;
            float x = 22f * s, y = 18f * s, h = 44f * s;
            float clockW = 104f * s, teamW = 112f * s, scoreW = 82f * s, underline = 5f * s;
            var home = SpriteFactory.Hex(m.Home.Setup.Kit.Primary, Color.red);
            var away = SpriteFactory.Hex(m.Away.Setup.Kit.Primary, Color.blue);
            var text = new GUIStyle(_score) { fontStyle = FontStyle.Normal, fontSize = Mathf.RoundToInt(27 * s) };
            string clock = m.Phase == MatchPhase.HalfTimeBreak ? "HT" : m.IsFinished ? "FT" : m.Clock.ClockText;
            Fill(new Rect(x, y, clockW, h), ClockBg);
            GUI.Label(new Rect(x, y, clockW, h), clock, text);
            float tx = x + clockW;
            DrawTeamBlock(new Rect(tx, y, teamW, h), m.Home.ShortName, home, m.Home.Stats.RedCards, underline, text);
            Fill(new Rect(tx + teamW, y, scoreW, h), ScoreBg);
            GUI.Label(new Rect(tx + teamW, y, scoreW, h), m.Home.Score + "-" + m.Away.Score, text);
            DrawTeamBlock(new Rect(tx + teamW + scoreW, y, teamW, h), m.Away.ShortName, away, m.Away.Stats.RedCards, underline, text);
            if (m.Clock.AddedMinutes > 0 && m.Clock.InAddedTime && m.Phase != MatchPhase.HalfTimeBreak && !m.IsFinished)
            {
                var add = new GUIStyle(text) { fontSize = Mathf.RoundToInt(20 * s) };
                add.normal.textColor = new Color(0.62f, 0.89f, 0.69f);
                Fill(new Rect(tx + teamW * 2 + scoreW, y, 52f * s, h), ClockBg);
                GUI.Label(new Rect(tx + teamW * 2 + scoreW, y, 52f * s, h), "+" + m.Clock.AddedMinutes, add);
            }
            // Competition strip with the SoccerVision logo.
            float subH = 30f * s, subW = clockW + teamW + 60f * s;
            Fill(new Rect(x, y + h, subW, subH), ScoreBg);
            if (_logo == null) _logo = Resources.Load<Texture2D>("soccervision-logo");
            float logoH = subH * 0.72f, logoW = _logo != null ? logoH * _logo.width / (float)_logo.height : 0f;
            if (_logo != null)
            {
                Fill(new Rect(x + 6f * s, y + h + (subH - logoH) * 0.5f, logoW + 4f * s, logoH), Color.white);
                GUI.DrawTexture(new Rect(x + 8f * s, y + h + (subH - logoH) * 0.5f, logoW, logoH), _logo, ScaleMode.ScaleToFit);
            }
            var comp = new GUIStyle(_small) { fontSize = Mathf.RoundToInt(19 * s) };
            comp.normal.textColor = Color.white;
            GUI.Label(new Rect(x + logoW + 20f * s, y + h, subW - logoW - 20f * s, subH), "Ligue match", comp);
        }

        private void DrawTeamBlock(Rect r, string name, Color teamColor, int reds, float underline, GUIStyle text)
        {
            Fill(r, TeamBg);
            Fill(new Rect(r.x, r.yMax - underline, r.width, underline), teamColor);
            GUI.Label(new Rect(r.x, r.y, r.width, r.height - underline), name, text);
            // One small red mark above the team name per red card.
            float mark = 9f * _scale;
            for (int i = 0; i < reds; i++) Fill(new Rect(r.x + 10f * _scale + i * (mark + 4f * _scale), r.y - mark - 3f * _scale, mark, mark), RedMark);
        }

        /// <summary>Play / pause and settings, top right (settings is empty for now).</summary>
        private void DrawTopControls()
        {
            float s = _scale;
            if (_iconPlay == null) MakeIcons();
            float size = 38f * s, y = 20f * s, right = Screen.width - 22f * s;
            var gearRect = new Rect(right - size, y, size, size);
            var playRect = new Rect(gearRect.x - 12f * s - size * 2f, y, size * 2f, size);
            var old = GUI.color;
            // the action you can take is drawn bright: pause bars while playing, the triangle while paused
            GUI.color = new Color(1f, 1f, 1f, Controller.Paused ? 1f : 0.38f);
            GUI.DrawTexture(new Rect(playRect.x, playRect.y, size, size), _iconPlay);
            GUI.color = new Color(1f, 1f, 1f, Controller.Paused ? 0.38f : 1f);
            GUI.DrawTexture(new Rect(playRect.x + size, playRect.y, size, size), _iconPause);
            GUI.color = Color.white;
            GUI.DrawTexture(gearRect, _iconGear);
            GUI.color = old;
            if (GUI.Button(playRect, GUIContent.none, GUIStyle.none)) Controller.Paused = !Controller.Paused;
            if (GUI.Button(gearRect, GUIContent.none, GUIStyle.none)) _settingsOpen = !_settingsOpen;
            if (_settingsOpen)
            {
                var panel = new Rect(right - 280f * s, y + size + 10f * s, 280f * s, 86f * s);
                Fill(panel, new Color(0.08f, 0.1f, 0.12f, 0.94f));
                GUI.Label(new Rect(panel.x + 12f * s, panel.y + 8f * s, panel.width, 26f * s), "<b>SETTINGS</b>", _small);
                GUI.Label(new Rect(panel.x + 12f * s, panel.y + 40f * s, panel.width, 26f * s), "No settings yet.", _small);
                if (GUI.Button(new Rect(panel.xMax - 40f * s, panel.y + 8f * s, 30f * s, 26f * s), "X", _button)) _settingsOpen = false;
            }
        }

        private void MakeIcons()
        {
            const int n = 64;
            Texture2D Make(System.Func<float, float, bool> inside)
            {
                var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        // 4x supersampling for smooth edges; y up in texture space
                        int hits = 0;
                        for (int k = 0; k < 4; k++) if (inside((x + 0.25f + 0.5f * (k & 1)) / n, (y + 0.25f + 0.5f * (k >> 1)) / n)) hits++;
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 4));
                    }
                t.SetPixels32(px);
                t.Apply();
                return t;
            }
            _iconPlay = Make((u, v) => u > 0.2f && u < 0.85f && Mathf.Abs(v - 0.5f) < (0.85f - u) * 0.62f);
            _iconPause = Make((u, v) => v > 0.12f && v < 0.88f && ((u > 0.2f && u < 0.4f) || (u > 0.6f && u < 0.8f)));
            _iconGear = Make((u, v) =>
            {
                float dx = u - 0.5f, dy = v - 0.5f, r = Mathf.Sqrt(dx * dx + dy * dy), a = Mathf.Atan2(dy, dx);
                float teeth = Mathf.Cos(a * 8f) > 0.35f ? 0.46f : 0.36f;
                return r < teeth && r > 0.15f;
            });
        }

        /// <summary>
        /// Commentary bar under the pitch: the latest line on the colour of the team on the ball; for 6 s after a goal it
        /// flashes between both teams' colours, for 4 s after a card it turns yellow or red.
        /// </summary>
        private void DrawCommentaryBar(MatchEngine m)
        {
            float s = _scale;
            float top = Screen.height * (1f - PanelFraction);
            float w = Screen.width * 0.62f, h = 34f * s;
            var r = new Rect((Screen.width - w) * 0.5f, top - h - 10f * s, w, h);
            var home = SpriteFactory.Hex(m.Home.Setup.Kit.Primary, Color.red);
            var away = SpriteFactory.Hex(m.Away.Setup.Kit.Primary, Color.blue);
            Color bg = m.PossessionTeam == m.Home ? home : m.PossessionTeam == m.Away ? away : new Color32(60, 69, 82, 255);
            var lines = Controller.Commentary;
            string text = lines.Count > 0 ? lines[lines.Count - 1].Text : "Kick-off";
            float age = m.RealTimeElapsed - Controller.HighlightAt;
            if (Controller.HighlightType == MatchEventType.Goal && age < 6f)
            {
                bg = Mathf.FloorToInt(age / 0.35f) % 2 == 0 ? home : away;
                text = Controller.HighlightText;
            }
            else if ((Controller.HighlightType == MatchEventType.YellowCard || Controller.HighlightType == MatchEventType.RedCard) && age < 4f)
            {
                bg = Controller.HighlightType == MatchEventType.RedCard ? new Color32(213, 0, 0, 255) : new Color32(255, 210, 26, 255);
                text = Controller.HighlightText;
            }
            Fill(r, bg);
            var st = new GUIStyle(_comment) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Clip };
            st.normal.textColor = bg.r * 0.299f + bg.g * 0.587f + bg.b * 0.114f > 0.63f ? new Color(0.07f, 0.07f, 0.07f) : Color.white;
            GUI.Label(r, text, st);
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

            // Play / pause sits top right of the pitch.
            float bw = (rw - pad * 2f) / 3f;
            float bh = 30f * s;
            if (GUI.Button(new Rect(rx, y, bw, bh), "x" + Controller.playbackSpeed.ToString("0"), _button))
            {
                Controller.playbackSpeed = Controller.playbackSpeed >= 8f ? 1f : Controller.playbackSpeed * 2f;
            }
            if (GUI.Button(new Rect(rx + (bw + pad), y, bw, bh), "New match", _button)) Controller.StartMatch(Random.Range(1, int.MaxValue));
            if (GUI.Button(new Rect(rx + (bw + pad) * 2f, y, bw, bh), "Hide HUD", _button)) { Controller.showHud = false; Controller.FitCamera(); }
        }

        private void DrawTooltip(MatchEngine m)
        {
            var mouse = Event.current.mousePosition;
            if (mouse.y > Screen.height * (1f - PanelFraction)) return;
            var p = Controller.PlayerAtScreen(mouse);
            if (p == null) return;
            var a = p.Attr;
            float s = _scale;
            string text = "<b>#" + p.Number + " " + p.Profile.FullName + "</b>  (" + Formation.RoleAbbreviation(p.Slot.Role) + ", " + p.Team.ShortName + ")  " + p.Profile.HeightCm + " cm, " + (a.LeftFoot >= 3 && a.RightFoot >= 3 ? "two-footed" : a.StrongFoot == Foot.Left ? "left-footed" : "right-footed") + "\n" +
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

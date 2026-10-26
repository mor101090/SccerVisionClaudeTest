using System.Collections.Generic;
using SoccerVision.Simulation;
using UnityEngine;

namespace SoccerVision.Runtime
{
    /// <summary>
    /// Runs one match: owns the simulation, feeds it real time, renders every entity with interpolation, plays
    /// audio, keeps the commentary log and talks to the React shell. Everything is created from code so an
    /// empty scene is enough (see MatchBootstrap).
    /// </summary>
    public sealed class MatchController : MonoBehaviour
    {
        [Header("Match")]
        public long seed = 2026;
        public bool randomSeed = true;
        [Tooltip("Real seconds per half (45 clock minutes). Added time comes on top.")]
        public float halfRealSeconds = 600f;
        [Tooltip("Real seconds of the half-time break.")]
        public float halftimeRealSeconds = 120f;
        [Tooltip("Football seconds simulated per real second (motion speed).")]
        public float motionSpeed = 2.25f;

        [Header("Presentation")]
        public bool showHud = true;
        public bool startPaused;
        [Tooltip("Debug fast-forward multiplier (1 = the real 10 minute halves).")]
        public float playbackSpeed = 1f;
        public Texture2D fieldOverride;

        public MatchEngine Engine { get; private set; }
        public bool Paused { get; set; }
        public Camera Camera => _camera;
        public readonly List<CommentLine> Commentary = new List<CommentLine>();
        /// <summary>Last goal or card (for the commentary bar), stamped with the match's real time.</summary>
        public MatchEventType HighlightType { get; private set; }
        public string HighlightText { get; private set; }
        public float HighlightAt { get; private set; } = -100f;

        private Camera _camera;
        private Transform _world;
        private readonly List<PlayerView> _players = new List<PlayerView>();
        private BallView _ball;
        private readonly List<OfficialView> _officials = new List<OfficialView>();
        private MatchAudio _audio;
        private readonly System.Collections.Generic.List<GoalNetView> _nets = new System.Collections.Generic.List<GoalNetView>();
        private WebBridge _bridge;
        private MatchHud _hud;
        private readonly List<MatchEvent> _events = new List<MatchEvent>();
        private float _stateTimer;
        private double _lastRealtime;
        private Vector2Int _lastScreen;
        private bool _lastHud;

        public struct CommentLine
        {
            public string Minute;
            public string Text;
            public CommentaryTone Tone;
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            SetupCamera();
            _audio = gameObject.AddComponent<MatchAudio>();
            _audio.Init();
            var bridgeGo = GameObject.Find(WebBridge.GameObjectName);
            if (bridgeGo == null) bridgeGo = new GameObject(WebBridge.GameObjectName);
            _bridge = bridgeGo.GetComponent<WebBridge>();
            if (_bridge == null) _bridge = bridgeGo.AddComponent<WebBridge>();
            _bridge.CommandReceived += HandleCommand;
            _hud = gameObject.AddComponent<MatchHud>();
            _hud.Controller = this;
        }

        private void Start()
        {
            StartMatch(randomSeed ? (long)Random.Range(1, int.MaxValue) : seed);
            Paused = startPaused;
        }

        private void OnDestroy()
        {
            if (_bridge != null) _bridge.CommandReceived -= HandleCommand;
        }

        // ---- Match lifecycle -----------------------------------------------------------------------------------

        public void StartMatch(long matchSeed)
        {
            seed = matchSeed;
            var config = MatchConfig.CreateDefault();
            config.HalfRealSeconds = Mathf.Max(10f, halfRealSeconds);
            config.HalftimeRealSeconds = Mathf.Max(0f, halftimeRealSeconds);
            config.MotionSpeed = Mathf.Clamp(motionSpeed, 0.5f, 6f);
            Engine = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), (ulong)matchSeed, config);
            Commentary.Clear();
            HighlightAt = -100f;
            BuildWorld();
            _lastRealtime = Time.realtimeSinceStartupAsDouble;
            _bridge.Emit("MatchReady", BuildReady());
            AddComment(Engine.Clock.MinuteText, Engine.Home.Name + " v " + Engine.Away.Name + " - " + Engine.Home.Formation.Name + " v " + Engine.Away.Formation.Name, CommentaryTone.Info);
        }

        private void BuildWorld()
        {
            if (_world != null) Destroy(_world.gameObject);
            _players.Clear();
            _officials.Clear();
            _world = new GameObject("Pitch").transform;
            _world.SetParent(transform, false);

            var pitch = new GameObject("Field").AddComponent<PitchRenderer>();
            pitch.transform.SetParent(_world, false);
            pitch.fieldOverride = fieldOverride;
            pitch.Build();

            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    var flag = new GameObject("CornerFlag").AddComponent<CornerFlagView>();
                    flag.transform.SetParent(_world, false);
                    flag.transform.localPosition = new Vector3(Pitch.HalfLength * sx, Pitch.HalfWidth * sy, 0f);
                    flag.Init(sx * 1.3f + sy * 0.7f);
                }
            }

            _nets.Clear();
            for (int side = -1; side <= 1; side += 2)
            {
                var net = new GameObject("GoalNet").AddComponent<GoalNetView>();
                net.transform.SetParent(_world, false);
                net.Init(side);
                _nets.Add(net);
            }

            foreach (var p in Engine.AllPlayers)
            {
                var kit = p.Team.Setup.Kit;
                var view = new GameObject("Player " + p.Team.ShortName + " " + p.Number).AddComponent<PlayerView>();
                view.transform.SetParent(_world, false);
                Color fill = SpriteFactory.Hex(p.IsGoalkeeper ? kit.GoalkeeperPrimary : kit.Primary, Color.gray);
                Color ring = SpriteFactory.Hex(kit.Secondary, Color.white);
                Color number = SpriteFactory.Hex(p.IsGoalkeeper ? kit.GoalkeeperNumber : kit.Number, Color.white);
                view.Init(p, fill, ring, number);
                _players.Add(view);
            }

            _ball = new GameObject("Ball").AddComponent<BallView>();
            _ball.transform.SetParent(_world, false);
            _ball.Init();

            for (int i = 0; i < Engine.Officials.Length; i++)
            {
                var o = new GameObject(i == 0 ? "Referee" : "Assistant " + i).AddComponent<OfficialView>();
                o.transform.SetParent(_world, false);
                o.Init(Engine.Officials[i], i > 0);
                _officials.Add(o);
            }
        }

        // ---- Frame -------------------------------------------------------------------------------------------

        private void Update()
        {
            if (Engine == null) return;
            double now = Time.realtimeSinceStartupAsDouble;
            float realDt = Mathf.Clamp((float)(now - _lastRealtime), 0f, 1f);
            _lastRealtime = now;

            HandleKeyboard();

            if (!Paused && !Engine.IsFinished)
            {
                Engine.Advance(realDt * Mathf.Clamp(playbackSpeed, 0.1f, 16f));
            }

            _events.Clear();
            Engine.DrainEvents(_events);
            foreach (var e in _events)
            {
                _audio.OnEvent(e, Engine.Home.Score, Engine.Away.Score);
                if ((e.Type == MatchEventType.NetHit || e.Type == MatchEventType.HitWoodwork) && _nets.Count == 2)
                {
                    var net = _nets[e.X < 0f ? 0 : 1];
                    if (e.Type == MatchEventType.NetHit) net.OnNetHit(new Vector2(e.X, e.Y), e.Value);
                    else net.OnWoodwork(new Vector2(e.X, e.Y));
                }
                if (e.Type == MatchEventType.Goal || e.Type == MatchEventType.YellowCard || e.Type == MatchEventType.RedCard)
                {
                    HighlightType = e.Type;
                    HighlightText = e.Text;
                    HighlightAt = Engine.RealTimeElapsed;
                }
                if (SoccerVision.Simulation.Commentary.IsCommentary(e.Type) && !string.IsNullOrEmpty(e.Text))
                {
                    AddComment(e.Minute, e.Text, SoccerVision.Simulation.Commentary.ToneOf(e.Type));
                }
                if (e.Type == MatchEventType.Goal || e.Type == MatchEventType.KickOff) _ball.ResetTrail();
                _bridge.Emit("MatchEvent", MatchEventDto.From(e, Engine));
            }

            Render(realDt);

            _stateTimer -= realDt;
            if (_stateTimer <= 0f)
            {
                _stateTimer = 0.25f;
                _bridge.Emit("MatchState", BuildState());
            }

            if (_lastScreen.x != Screen.width || _lastScreen.y != Screen.height || _lastHud != showHud) FitCamera();
        }

        private void Render(float dt)
        {
            float alpha = Engine.InterpolationAlpha;
            var owner = Engine.Ball.Owner;
            bool showLabel = Engine.Phase == MatchPhase.Live;
            for (int i = 0; i < _players.Count; i++)
            {
                var v = _players[i];
                v.Render(alpha, showLabel && owner == v.Agent, dt);
            }
            bool hideBall = Engine.Phase == MatchPhase.HalfTimeBreak;
            if (_ball.gameObject.activeSelf == hideBall) _ball.gameObject.SetActive(!hideBall);
            _ball.Render(Engine.Ball, alpha, dt);
            foreach (var o in _officials) o.Render(alpha, Time.time);
            foreach (var n in _nets) n.Render(Engine.Ball);
        }

        private void HandleKeyboard()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Space)) Paused = !Paused;
            if (Input.GetKeyDown(KeyCode.Alpha1)) playbackSpeed = 1f;
            if (Input.GetKeyDown(KeyCode.Alpha2)) playbackSpeed = 2f;
            if (Input.GetKeyDown(KeyCode.Alpha3)) playbackSpeed = 4f;
            if (Input.GetKeyDown(KeyCode.Alpha4)) playbackSpeed = 8f;
            if (Input.GetKeyDown(KeyCode.H)) showHud = !showHud;
            if (Input.GetKeyDown(KeyCode.R)) StartMatch(Random.Range(1, int.MaxValue));
#endif
        }

        private void AddComment(string minute, string text, CommentaryTone tone)
        {
            Commentary.Add(new CommentLine { Minute = minute, Text = text, Tone = tone });
            if (Commentary.Count > 400) Commentary.RemoveRange(0, Commentary.Count - 400);
        }

        // ---- Camera --------------------------------------------------------------------------------------------

        private void SetupCamera()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                _camera = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.1f, 0.24f, 0.12f);
            _camera.transform.position = new Vector3(0f, 0f, -20f);
            _camera.transform.rotation = Quaternion.identity;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 100f;
            FitCamera();
        }

        /// <summary>Fits the pitch (with margins) into the screen, leaving the bottom strip for the HUD panel.</summary>
        public void FitCamera()
        {
            if (_camera == null) return;
            _lastScreen = new Vector2Int(Screen.width, Screen.height);
            _lastHud = showHud;
            float hudFraction = showHud ? MatchHud.PanelFraction : 0f;
            _camera.rect = new Rect(0f, hudFraction, 1f, 1f - hudFraction);
            float viewAspect = Screen.width / Mathf.Max(1f, Screen.height * (1f - hudFraction));
            const float worldW = Pitch.Length + 2f * PitchRenderer.Margin - 2f;
            const float worldH = PitchRenderer.Top - PitchRenderer.Bottom - 2f;
            _camera.orthographicSize = Mathf.Max(worldH * 0.5f, worldW * 0.5f / viewAspect);
            var cp = _camera.transform.position;
            _camera.transform.position = new Vector3(0f, (PitchRenderer.Top + PitchRenderer.Bottom) * 0.5f, cp.z);
        }

        // ---- Bridge ---------------------------------------------------------------------------------------------

        public void HandleCommand(BridgeCommand cmd)
        {
            switch (cmd.type)
            {
                case "pause": Paused = true; break;
                case "resume": Paused = false; break;
                case "togglePause": Paused = !Paused; break;
                case "setSpeed": playbackSpeed = Mathf.Clamp(cmd.value, 0.1f, 16f); break;
                case "setHud": showHud = cmd.flag; FitCamera(); break;
                case "restart": StartMatch(cmd.seed > 0 ? cmd.seed : Random.Range(1, int.MaxValue)); break;
                case "setTactics":
                    if (cmd.tactics != null && (cmd.team == 0 || cmd.team == 1)) Engine.Submit(new ChangeTacticsCommand(cmd.team, cmd.tactics.ToModel()));
                    break;
                case "requestState":
                    _bridge.Emit("MatchReady", BuildReady());
                    _bridge.Emit("MatchState", BuildState());
                    break;
            }
        }

        private MatchReadyDto BuildReady()
        {
            TeamInfoDto Team(TeamAgent t)
            {
                var players = new PlayerDto[t.Players.Count];
                for (int i = 0; i < players.Length; i++)
                {
                    var p = t.Players[i];
                    players[i] = new PlayerDto
                    {
                        index = p.Index,
                        number = p.Number,
                        name = p.Profile.FullName,
                        role = Formation.RoleAbbreviation(p.Slot.Role),
                        goalkeeper = p.IsGoalkeeper,
                        passing = p.Attr.Passing,
                        speed = p.Attr.Speed,
                        stamina = p.Attr.Stamina,
                        finishing = p.Attr.Finishing,
                        tackling = p.Attr.Tackling,
                    };
                }
                return new TeamInfoDto
                {
                    name = t.Name,
                    shortName = t.ShortName,
                    color = t.Setup.Kit.Primary,
                    goalkeeperColor = t.Setup.Kit.GoalkeeperPrimary,
                    formation = t.Formation.Name,
                    players = players,
                };
            }
            return new MatchReadyDto
            {
                seed = seed,
                halfRealSeconds = Engine.Config.HalfRealSeconds,
                halftimeRealSeconds = Engine.Config.HalftimeRealSeconds,
                home = Team(Engine.Home),
                away = Team(Engine.Away),
            };
        }

        private MatchStateDto BuildState()
        {
            var e = Engine;
            float poss = e.Home.Stats.PossessionSeconds + e.Away.Stats.PossessionSeconds;
            float homeShare = poss > 0f ? e.Home.Stats.PossessionSeconds / poss * 100f : 50f;
            return new MatchStateDto
            {
                phase = e.Phase.ToString(),
                period = e.Clock.Period.ToString(),
                minute = e.Clock.MinuteText,
                clock = e.Clock.ClockText,
                homeScore = e.Home.Score,
                awayScore = e.Away.Score,
                addedMinutes = e.Clock.AddedMinutes,
                halftimeRemaining = e.HalftimeRemaining,
                realElapsed = e.RealTimeElapsed,
                paused = Paused,
                speed = playbackSpeed,
                finished = e.IsFinished,
                homeStats = TeamStatsDto.From(e.Home.Stats, homeShare),
                awayStats = TeamStatsDto.From(e.Away.Stats, 100f - homeShare),
                ballTeam = e.PossessionTeam == e.Home ? 0 : e.PossessionTeam == e.Away ? 1 : -1,
            };
        }

        /// <summary>Player under a screen position (IMGUI coordinates), for the hover tooltip.</summary>
        public PlayerAgent PlayerAtScreen(Vector2 guiPosition)
        {
            if (_camera == null || Engine == null) return null;
            var world = _camera.ScreenToWorldPoint(new Vector3(guiPosition.x, Screen.height - guiPosition.y, -_camera.transform.position.z));
            PlayerAgent best = null;
            float bestD = PlayerView.Diameter * 0.65f;
            foreach (var p in Engine.AllPlayers)
            {
                if (p.SentOff) continue;
                float d = Vector2.Distance(new Vector2(world.x, world.y), new Vector2(p.Pos.X, p.Pos.Y));
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }
    }
}

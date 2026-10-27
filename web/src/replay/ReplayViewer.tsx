import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { MatchSound } from "../audio/matchAudio";
import { barState, Commentary, CommentaryBar, Scoreboard, StatsTable, TopControls, type CommentLine } from "../components/MatchHud";
import { drawEntities, drawPitch, makeView, resetBall, WORLD_H, WORLD_W } from "./drawPitch";
import { frameTime, minuteText, sample, type Replay } from "./replay";

/**
 * Dev tool: plays a replay recorded by the C# engine (`npm run replay`) on a canvas, in real time
 * (10 minute halves, 2 minute break) or faster. Unity remains the real match renderer.
 */
export function ReplayViewer() {
  const [replay, setReplay] = useState<Replay | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetch("/replays/demo.json")
      .then((r) => (r.ok && (r.headers.get("content-type") ?? "").includes("json") ? r.json() : Promise.reject(new Error("missing"))))
      .then((data: Replay) => setReplay(data))
      .catch(() => setError("No replay at public/replays/demo.json yet."));
  }, []);

  const [busy, setBusy] = useState(false);
  const newMatch = useCallback(() => {
    setBusy(true);
    setError(null);
    fetch(`/api/new-replay?seed=${Math.floor(Math.random() * 2147483646) + 1}`)
      .then((r) => (r.ok ? r.json() : Promise.reject(new Error("failed"))))
      .then((data: Replay) => setReplay(data))
      .catch(() => setError("Could not simulate a new match (needs `npm run dev` and the .NET 8 SDK)."))
      .finally(() => setBusy(false));
  }, []);

  const onFile = useCallback((file: File) => {
    file
      .text()
      .then((txt) => {
        setReplay(JSON.parse(txt) as Replay);
        setError(null);
      })
      .catch(() => setError("Could not read that file."));
  }, []);

  if (!replay)
    return (
      <div className="placeholder">
        <h2>Engine replay</h2>
        <p>{error ?? "Loading..."}</p>
        <p>
          Generate one with <code>npm run replay</code> (needs the .NET 8 SDK), or open a replay file:
        </p>
        <p>
          <button onClick={newMatch} disabled={busy}>
            {busy ? "Simulating..." : "New match"}
          </button>
        </p>
        <input type="file" accept=".json,application/json" onChange={(e) => e.target.files?.[0] && onFile(e.target.files[0])} />
      </div>
    );
  return (
    <>
      <div className="controls" style={{ marginBottom: 12 }}>
        <button onClick={newMatch} disabled={busy}>
          {busy ? "Simulating..." : "New match"}
        </button>
        {error && <span className="hint">{error}</span>}
      </div>
      <Player key={replay.seed} replay={replay} />
    </>
  );
}

function Player({ replay }: { replay: Replay }) {
  const canvas = useRef<HTMLCanvasElement>(null);
  const wrap = useRef<HTMLDivElement>(null);
  const [time, setTime] = useState(frameTime(replay.frames[0]));
  const [playing, setPlaying] = useState(true);
  const [speed, setSpeed] = useState(1);
  const timeRef = useRef(time);
  const sound = useRef<MatchSound | null>(null);
  if (!sound.current) sound.current = new MatchSound();
  const [soundOn, setSoundOn] = useState(false);
  const lastEvent = useRef(0);
  const end = frameTime(replay.frames[replay.frames.length - 1]);
  const secondHalfStart = useMemo(() => replay.events.find((e) => e.type === "SecondHalfStart")?.t ?? Infinity, [replay]);

  useEffect(() => {
    timeRef.current = time;
  }, [time]);
  useEffect(() => () => sound.current?.disable(), []);
  useEffect(() => resetBall(), [replay]);

  // Animation loop: advance replay time by real time x speed and redraw.
  useEffect(() => {
    let raf = 0;
    let last = performance.now();
    const loop = (now: number) => {
      const dt = Math.min(0.25, (now - last) / 1000);
      last = now;
      if (playing) {
        const prev = timeRef.current;
        const t = Math.min(end, prev + dt * speed);
        timeRef.current = t;
        setTime(t);
        // Sound: events that happened since the last frame (muted when fast-forwarding past 4x).
        const evs = replay.events;
        let i = lastEvent.current;
        if (i > 0 && evs[i - 1].t > prev) i = 0; // seeked backwards
        while (i < evs.length && evs[i].t <= t) {
          if (speed <= 4 && evs[i].t > prev - 0.001) {
            const ev = evs[i];
            const now = sample(replay, ev.t);
            sound.current?.play({ type: ev.type, whistle: ev.whistle, detail: ev.detail, value: ev.v, team: ev.team, homeScore: now.homeScore, awayScore: now.awayScore });
          }
          i++;
        }
        lastEvent.current = i;
        sound.current?.tick(dt);
      }
      const c = canvas.current;
      const w = wrap.current;
      if (c && w) {
        const dpr = window.devicePixelRatio || 1;
        const cssW = w.clientWidth;
        const cssH = (cssW * WORLD_H) / WORLD_W;
        if (c.width !== Math.round(cssW * dpr)) {
          c.width = Math.round(cssW * dpr);
          c.height = Math.round(cssH * dpr);
          c.style.height = cssH + "px";
        }
        const ctx = c.getContext("2d");
        if (ctx) {
          const view = makeView(c.width, c.height);
          drawPitch(ctx, view);
          drawEntities(ctx, view, replay, sample(replay, timeRef.current), now / 1000, playing ? dt * speed : 0);
        }
      }
      raf = requestAnimationFrame(loop);
    };
    raf = requestAnimationFrame(loop);
    return () => cancelAnimationFrame(raf);
  }, [replay, playing, speed, end]);

  const snap = sample(replay, time);
  const secondHalf = time >= secondHalfStart;
  const minute = snap.halftime ? "HT" : time >= end ? "FT" : minuteText(snap.clock, secondHalf);
  const lines: CommentLine[] = useMemo(() => {
    const out: CommentLine[] = [];
    replay.events.forEach((e, i) => {
      if (e.t <= time && e.type !== "Whistle" && e.type !== "Pass" && e.text) out.push({ id: i, minute: e.minute, text: e.text, tone: e.tone });
    });
    return out.slice(-200);
  }, [replay, time]);

  // Commentary bar: latest line, team on the ball, goal flash, card colours.
  const possession = useRef(-1);
  if (snap.ball.owner >= 0) possession.current = snap.ball.owner < 11 ? 0 : 1;
  let latest: string | null = null;
  let goal: { text: string; age: number; colors: [string, string] } | null = null;
  let card: { text: string; red: boolean; age: number } | null = null;
  let homeReds = 0;
  let awayReds = 0;
  for (const e of replay.events) {
    if (e.t > time) break;
    if (!e.text || e.type === "Whistle" || e.type === "NetHit") continue;
    latest = e.text;
    if (e.type === "Goal") {
      const kit = (e.team === 1 ? replay.away : replay.home).kit;
      goal = { text: e.text, age: time - e.t, colors: [kit.primary, kit.secondary] };
    }
    if (e.type === "YellowCard" || e.type === "RedCard") card = { text: e.text, red: e.type === "RedCard", age: time - e.t };
    if (e.type === "RedCard") {
      if (e.team === 0) homeReds++;
      else awayReds++;
    }
  }
  const bar = barState({
    latest,
    possessionColor: possession.current >= 0 ? [replay.home, replay.away][possession.current].kit.primary : null,
    goal,
    card,
  });
  const clockSecs = Math.floor(snap.clock);
  const clock = snap.halftime ? "HT" : time >= end ? "FT" : `${String(Math.floor(clockSecs / 60)).padStart(2, "0")}:${String(clockSecs % 60).padStart(2, "0")}`;

  const hs = replay.home as unknown as { stats?: Record<string, number> };
  const as = replay.away as unknown as { stats?: Record<string, number> };

  return (
    <div className="match">
      <div className="pitch-wrap" ref={wrap}>
        <canvas ref={canvas} className="replay-canvas" />
        <Scoreboard
          s={{
            homeShort: replay.home.shortName,
            awayShort: replay.away.shortName,
            homeColor: replay.home.kit.primary,
            awayColor: replay.away.kit.primary,
            homeScore: snap.homeScore,
            awayScore: snap.awayScore,
            minute,
            clock,
            addedMinutes: 0,
            homeReds,
            awayReds,
          }}
        />
        <TopControls playing={playing} onToggle={() => setPlaying((p) => !p)} />
        <CommentaryBar bar={bar} />
        {snap.halftime && (
          <div className="banner">
            Half time - second half in {Math.max(0, Math.ceil(secondHalfStart - time))}s
            <button onClick={() => setTime(Math.min(end, secondHalfStart + 0.01))}>Skip break</button>
          </div>
        )}
      </div>
      <div className="below">
        <Commentary lines={lines} />
        <div className="side">
          <div className="controls">
            <button
              onClick={() => {
                if (soundOn) sound.current?.disable();
                else sound.current?.enable();
                setSoundOn(!soundOn);
              }}
              aria-pressed={soundOn}
            >
              {soundOn ? "Sound on" : "Sound off"}
            </button>
            <button onClick={() => setSpeed((s) => (s >= 16 ? 1 : s * 2))}>x{speed}</button>
            <input
              type="range"
              min={0}
              max={end}
              step={0.5}
              value={time}
              onChange={(e) => setTime(Number(e.target.value))}
              aria-label="Seek"
            />
          </div>
          {time >= end && hs.stats && as.stats && (
            <StatsTable
              title="Full time"
              rows={[
                {
                  label: "Possession",
                  home: `${Math.round((100 * hs.stats.PossessionSeconds) / Math.max(1, hs.stats.PossessionSeconds + as.stats.PossessionSeconds))}%`,
                  away: `${Math.round((100 * as.stats.PossessionSeconds) / Math.max(1, hs.stats.PossessionSeconds + as.stats.PossessionSeconds))}%`,
                },
                { label: "Shots (on target)", home: `${hs.stats.Shots} (${hs.stats.ShotsOnTarget})`, away: `${as.stats.Shots} (${as.stats.ShotsOnTarget})` },
                { label: "Tackles (won)", home: `${hs.stats.Tackles} (${hs.stats.TacklesWon})`, away: `${as.stats.Tackles} (${as.stats.TacklesWon})` },
                { label: "Dribbles (won)", home: `${hs.stats.Dribbles} (${hs.stats.DribblesWon})`, away: `${as.stats.Dribbles} (${as.stats.DribblesWon})` },
                { label: "xG", home: hs.stats.ExpectedGoals.toFixed(2), away: as.stats.ExpectedGoals.toFixed(2) },
                { label: "Corners", home: hs.stats.Corners, away: as.stats.Corners },
                { label: "Fouls", home: hs.stats.Fouls, away: as.stats.Fouls },
                { label: "Passes", home: `${hs.stats.PassesCompleted}/${hs.stats.Passes}`, away: `${as.stats.PassesCompleted}/${as.stats.Passes}` },
              ]}
            />
          )}
          <p className="hint">
            Seed {replay.seed} - real-time playback: {replay.halfRealSeconds / 60} min halves, {replay.halftimeRealSeconds / 60} min break.
          </p>
        </div>
      </div>
    </div>
  );
}

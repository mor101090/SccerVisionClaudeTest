import { useCallback, useEffect, useMemo, useState } from "react";
import { Unity, useUnityContext } from "react-unity-webgl";
import {
  BRIDGE_METHOD,
  BRIDGE_OBJECT,
  isCommentary,
  parsePayload,
  type BridgeCommand,
  type MatchEvent,
  type MatchReady,
  type MatchState,
} from "../bridge/protocol";
import { Commentary, Scoreboard, StatsTable, type CommentLine } from "../components/MatchHud";

const BUILD = "/unity/SoccerVision/Build/SoccerVision";

/** Hosts the Unity WebGL match if a build exists in public/unity, otherwise explains how to make one. */
export function UnityMatch() {
  const [available, setAvailable] = useState<boolean | null>(null);
  useEffect(() => {
    // Vite answers unknown paths with index.html, so check the content type too.
    fetch(BUILD + ".loader.js", { method: "HEAD" })
      .then((r) => setAvailable(r.ok && (r.headers.get("content-type") ?? "").includes("javascript")))
      .catch(() => setAvailable(false));
  }, []);

  if (available === null) return <div className="placeholder">Looking for the Unity build...</div>;
  if (!available)
    return (
      <div className="placeholder">
        <h2>Unity build not found</h2>
        <p>
          Open <code>unity/SoccerVision</code> in Unity 6.3 LTS and run <b>SoccerVision &gt; Build WebGL for React</b>. The build is
          written to <code>web/public/unity/SoccerVision</code> and appears here on reload.
        </p>
        <p>
          Meanwhile, the <b>Engine replay</b> tab plays a match recorded by the same C# simulation.
        </p>
      </div>
    );
  return <UnityPlayer />;
}

function UnityPlayer() {
  const { unityProvider, isLoaded, loadingProgression, sendMessage, addEventListener, removeEventListener } = useUnityContext({
    loaderUrl: BUILD + ".loader.js",
    dataUrl: BUILD + ".data",
    frameworkUrl: BUILD + ".framework.js",
    codeUrl: BUILD + ".wasm",
  });
  const [ready, setReady] = useState<MatchReady | null>(null);
  const [state, setState] = useState<MatchState | null>(null);
  const [lines, setLines] = useState<CommentLine[]>([]);
  const [pace, setPace] = useState(100);

  const send = useCallback((cmd: BridgeCommand) => sendMessage(BRIDGE_OBJECT, BRIDGE_METHOD, JSON.stringify(cmd)), [sendMessage]);

  useEffect(() => {
    let nextId = 1;
    const onReady = (payload: unknown) => {
      const r = parsePayload<MatchReady>(payload);
      if (!r) return;
      setReady(r);
      setLines([]);
      send({ type: "setHud", flag: false }); // React draws the HUD
      return undefined;
    };
    const onState = (payload: unknown) => {
      const s = parsePayload<MatchState>(payload);
      if (s) setState(s);
      return undefined;
    };
    const onEvent = (payload: unknown) => {
      const e = parsePayload<MatchEvent>(payload);
      if (!e || !isCommentary(e)) return undefined;
      setLines((prev) => [...prev.slice(-300), { id: nextId++, minute: e.minute, text: e.text, tone: e.tone }]);
      return undefined;
    };
    addEventListener("MatchReady", onReady);
    addEventListener("MatchState", onState);
    addEventListener("MatchEvent", onEvent);
    return () => {
      removeEventListener("MatchReady", onReady);
      removeEventListener("MatchState", onState);
      removeEventListener("MatchEvent", onEvent);
    };
  }, [addEventListener, removeEventListener, send]);

  useEffect(() => {
    if (isLoaded) send({ type: "requestState" });
  }, [isLoaded, send]);

  const score = useMemo(
    () =>
      ready && state
        ? {
            homeShort: ready.home.shortName,
            awayShort: ready.away.shortName,
            homeColor: ready.home.color,
            awayColor: ready.away.color,
            homeScore: state.homeScore,
            awayScore: state.awayScore,
            minute: state.minute,
            addedMinutes: state.addedMinutes,
          }
        : null,
    [ready, state],
  );

  return (
    <div className="match">
      <div className="pitch-wrap">
        {!isLoaded && <div className="loading">Loading match {Math.round(loadingProgression * 100)}%</div>}
        <Unity unityProvider={unityProvider} className="unity-canvas" style={{ visibility: isLoaded ? "visible" : "hidden" }} />
        {score && <Scoreboard s={score} />}
        {state?.phase === "HalfTimeBreak" && <div className="banner">Half time - second half in {Math.ceil(state.halftimeRemaining)}s</div>}
        {state?.finished && <div className="banner">Full time</div>}
      </div>
      <div className="below">
        <Commentary lines={lines} />
        <div className="side">
          <div className="controls">
            <button onClick={() => send({ type: "togglePause" })}>{state?.paused ? "Play" : "Pause"}</button>
            <button onClick={() => send({ type: "setSpeed", value: state && state.speed < 8 ? state.speed * 2 : 1 })}>x{state?.speed ?? 1}</button>
            <button onClick={() => send({ type: "restart" })}>New match</button>
            <label className="pace">
              Pace {pace}%
              <input
                type="range"
                min={50}
                max={100}
                step={5}
                value={pace}
                onChange={(e) => {
                  const value = Number(e.target.value);
                  setPace(value);
                  send({ type: "setPace", value });
                }}
                aria-label="Match pace"
              />
            </label>
          </div>
          {state && (
            <StatsTable
              rows={[
                { label: "Possession", home: Math.round(state.homeStats.possession) + "%", away: Math.round(state.awayStats.possession) + "%" },
                { label: "Shots (on target)", home: `${state.homeStats.shots} (${state.homeStats.shotsOnTarget})`, away: `${state.awayStats.shots} (${state.awayStats.shotsOnTarget})` },
                { label: "Corners", home: state.homeStats.corners, away: state.awayStats.corners },
                { label: "Fouls", home: state.homeStats.fouls, away: state.awayStats.fouls },
                { label: "Cards", home: `${state.homeStats.yellowCards}/${state.homeStats.redCards}`, away: `${state.awayStats.yellowCards}/${state.awayStats.redCards}` },
              ]}
            />
          )}
        </div>
      </div>
    </div>
  );
}

import { useEffect, useRef, useState } from "react";
import type React from "react";
import type { Tone } from "../bridge/protocol";

export interface CommentLine {
  id: number;
  minute: string;
  text: string;
  tone: Tone;
}

import logo from "../assets/soccervision-logo.png";

export interface ScoreInfo {
  homeShort: string;
  awayShort: string;
  homeColor: string;
  awayColor: string;
  homeScore: number;
  awayScore: number;
  minute: string;
  /** Broadcast clock "mm:ss" (or HT / FT). */
  clock: string;
  addedMinutes: number;
  homeReds: number;
  awayReds: number;
  competition?: string;
  /** "Day · Rain" etc. */
  weather?: string;
  /** Wind arrow on screen (degrees) and its speed, when it blows. */
  windArrow?: number;
  windText?: string;
}

/** Broadcast scoreboard (design: broadcast_scoreboard.ai, bottom variant): clock, teams with their colour, score, red cards. */
export function Scoreboard({ s }: { s: ScoreInfo }) {
  const reds = (n: number) => (
    <span className="sb-reds" aria-label={n ? `${n} red card${n > 1 ? "s" : ""}` : undefined}>
      {Array.from({ length: n }, (_, i) => (
        <i key={i} />
      ))}
    </span>
  );
  return (
    <div className="sb" role="group" aria-label="Scoreboard">
      <div className="sb-row">
        <span className="sb-clock">{s.clock}</span>
        <span className="sb-team" style={{ ["--team" as string]: s.homeColor }}>
          {reds(s.homeReds)}
          {s.homeShort}
        </span>
        <span className="sb-score">
          {s.homeScore}-{s.awayScore}
        </span>
        <span className="sb-team" style={{ ["--team" as string]: s.awayColor }}>
          {reds(s.awayReds)}
          {s.awayShort}
        </span>
        {s.addedMinutes > 0 && s.minute.includes("+") && <span className="sb-add">+{s.addedMinutes}</span>}
      </div>
      <div className="sb-sub">
        <img src={logo} alt="SoccerVision" />
        <span>{s.competition ?? "Ligue match"}</span>
      </div>
      {s.weather && (
        <div>
          <span className="sb-wx">
            <span>{s.weather}</span>
            {s.windArrow !== undefined && (
              <>
                <svg viewBox="0 0 16 16" aria-hidden="true" style={{ transform: `rotate(${s.windArrow}deg)` }}>
                  <path d="M1.5 8h11M9 4.5L12.8 8 9 11.5" fill="none" stroke="#fff" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
                <span>{s.windText}</span>
              </>
            )}
          </span>
        </div>
      )}
    </div>
  );
}

/** Play / pause and the (developer) settings, top right of the pitch. */
export function TopControls({ playing, onToggle, settings }: { playing: boolean; onToggle: () => void; settings?: React.ReactNode }) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <div className="topctl">
        <button type="button" className="icon play" aria-label={playing ? "Pause" : "Play"} title="Play / pause" onClick={onToggle}>
          <svg viewBox="0 0 48 24" aria-hidden="true">
            {/* the action you can take is drawn bright */}
            <path className={playing ? "dim" : ""} d="M2 2 L20 12 L2 22 Z" fill="currentColor" />
            <g className={playing ? "" : "dim"}>
              <rect x="26" y="2" width="7" height="20" fill="currentColor" />
              <rect x="38" y="2" width="7" height="20" fill="currentColor" />
            </g>
          </svg>
        </button>
        <button type="button" className="icon gear" aria-label="Settings" aria-expanded={open} title="Settings" onClick={() => setOpen((o) => !o)}>
          <svg viewBox="0 0 24 24" aria-hidden="true" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <circle cx="12" cy="12" r="3" />
            <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z" />
          </svg>
        </button>
      </div>
      {open && (
        <div className="settings" role="dialog" aria-label="Settings">
          <div className="settings-head">
            <span>
              Settings<span className="dev">DEV</span>
            </span>
            <button type="button" aria-label="Close settings" onClick={() => setOpen(false)}>
              ✕
            </button>
          </div>
          {settings ?? <p>No settings yet.</p>}
        </div>
      )}
    </>
  );
}

export interface BarState {
  text: string;
  background: string;
}

/**
 * What the commentary bar under the pitch shows: the latest line on the colour of the team on the ball; for 6 s after
 * a goal it flashes between the two colours of the scoring team, for 4 s after a card it turns yellow or red.
 */
export function barState(o: {
  latest: string | null;
  possessionColor: string | null;
  /** A goal flashes in the two colours of the team that scored it (e.g. blue and white for the blue team). */
  goal: { text: string; age: number; colors: [string, string] } | null;
  card: { text: string; red: boolean; age: number } | null;
}): BarState {
  if (o.goal && o.goal.age < 6) return { text: o.goal.text, background: o.goal.colors[Math.floor(o.goal.age / 0.35) % 2] };
  if (o.card && o.card.age < 4) return { text: o.card.text, background: o.card.red ? "#d50000" : "#ffd21a" };
  return { text: o.latest ?? "Kick-off", background: o.possessionColor ?? "#3c4552" };
}

function readableOn(hex: string): string {
  const n = parseInt(hex.slice(1), 16);
  const lum = ((n >> 16) & 255) * 0.299 + ((n >> 8) & 255) * 0.587 + (n & 255) * 0.114;
  return lum > 160 ? "#111" : "#fff";
}

export function CommentaryBar({ bar }: { bar: BarState }) {
  return (
    <div className="cbar" style={{ background: bar.background, color: readableOn(bar.background) }} aria-live="polite">
      <span>{bar.text}</span>
    </div>
  );
}

/** Live commentary box below the field: auto-scrolls, colour coded (goals green, fouls red, cards yellow). */
export function Commentary({ lines }: { lines: CommentLine[] }) {
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const el = ref.current;
    if (el) el.scrollTop = el.scrollHeight;
  }, [lines.length]);
  return (
    <div className="commentary" ref={ref} aria-live="polite">
      {lines.map((l) => (
        <div key={l.id} className={"line tone-" + l.tone.toLowerCase()}>
          <span className="min">{l.minute}</span>
          {l.text}
        </div>
      ))}
    </div>
  );
}

export interface StatRow {
  label: string;
  home: string | number;
  away: string | number;
}

export function StatsTable({ rows, title }: { rows: StatRow[]; title?: string }) {
  return (
    <div className="stats">
      {title && <h3>{title}</h3>}
      <table>
        <tbody>
          {rows.map((r) => (
            <tr key={r.label}>
              <td className="num">{r.home}</td>
              <td className="label">{r.label}</td>
              <td className="num">{r.away}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

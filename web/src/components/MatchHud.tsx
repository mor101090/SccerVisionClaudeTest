import { useEffect, useRef } from "react";
import type { Tone } from "../bridge/protocol";

export interface CommentLine {
  id: number;
  minute: string;
  text: string;
  tone: Tone;
}

export interface ScoreInfo {
  homeShort: string;
  awayShort: string;
  homeColor: string;
  awayColor: string;
  homeScore: number;
  awayScore: number;
  minute: string;
  addedMinutes: number;
}

/** Top-left scoreboard (design doc: team names, score, match minute). */
export function Scoreboard({ s }: { s: ScoreInfo }) {
  return (
    <div className="scoreboard" aria-label="Scoreboard">
      <span className="team" style={{ borderLeftColor: s.homeColor }}>{s.homeShort}</span>
      <span className="score">
        {s.homeScore} - {s.awayScore}
      </span>
      <span className="team away" style={{ borderRightColor: s.awayColor }}>{s.awayShort}</span>
      <span className="minute">{s.minute}</span>
      {s.addedMinutes > 0 && s.minute.includes("+") && <span className="added">+{s.addedMinutes}</span>}
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

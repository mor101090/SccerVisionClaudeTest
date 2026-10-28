import type { DevSettings, GrassPattern, Sky, TeamColours, Upkeep } from "../settings/devSettings";
import { DEFAULT_SETTINGS, textOn } from "../settings/devSettings";

interface Props {
  value: DevSettings;
  onChange: (s: DevSettings) => void;
  /** The teams' own kits (shown when no colour is picked). */
  kits: [Required<TeamColours>, Required<TeamColours>];
  names: [string, string];
  note?: string | null;
}

function Seg<T extends string | number | boolean>({ options, value, onPick }: { options: [T, string][]; value: T; onPick: (v: T) => void }) {
  return (
    <div className="seg">
      {options.map(([v, label]) => (
        <button key={String(v)} type="button" aria-pressed={v === value} onClick={() => onPick(v)}>
          {label}
        </button>
      ))}
    </div>
  );
}

/** Developer settings: pace, pitch, weather, day/night, nets and team colours. */
export function DevSettingsPanel({ value: s, onChange, kits, names, note }: Props) {
  const set = (patch: Partial<DevSettings>) => onChange({ ...s, ...patch });
  const colour = (side: 0 | 1, key: keyof TeamColours, v: string) => {
    const cur: TeamColours = { ...((side ? s.away : s.home) ?? { primary: kits[side].primary, secondary: kits[side].secondary }) };
    cur[key] = v;
    if (key === "primary") delete cur.number; // the numbers follow a new shirt colour until they are picked again
    set(side ? { away: cur } : { home: cur });
  };
  const shown = (side: 0 | 1): Required<TeamColours> => {
    const c = side ? s.away : s.home;
    const k = kits[side];
    if (!c) return k;
    return { primary: c.primary, secondary: c.secondary, number: c.number ?? textOn(c.primary), gk: c.gk ?? k.gk };
  };
  return (
    <div className="dev-settings">
      <div className="srow">
        <label htmlFor="sv-pace">Match pace</label>
        <div className="colors">
          <input id="sv-pace" type="range" min={50} max={100} step={5} value={s.pace} onChange={(e) => set({ pace: Number(e.target.value) })} />
          <span className="num">{s.pace}%</span>
        </div>
      </div>
      <h3>Pitch</h3>
      <div className="srow">
        <span className="l">Grass</span>
        <Seg<GrassPattern> options={[["along", "Stripes along"], ["across", "Stripes across"], ["circles", "Circles"]]} value={s.grass} onPick={(grass) => set({ grass })} />
      </div>
      <div className="srow">
        <span className="l">Upkeep</span>
        <Seg<Upkeep> options={[[2, "Perfect"], [1, "Average"], [0, "Neglected"]]} value={s.upkeep} onPick={(upkeep) => set({ upkeep })} />
      </div>
      <div className="srow">
        <label htmlFor="sv-net">Goal nets</label>
        <div className="colors">
          <input id="sv-net" type="color" value={s.net} onChange={(e) => set({ net: e.target.value })} />
        </div>
      </div>
      <h3>Weather</h3>
      <div className="srow">
        <span className="l">Sky</span>
        <Seg<Sky> options={[["Sunny", "Sunny"], ["Cloudy", "Cloudy"], ["Rain", "Rain"], ["Snow", "Snow"]]} value={s.sky} onPick={(sky) => set({ sky })} />
      </div>
      <div className="srow">
        <span className="l">Wind</span>
        <Seg<boolean> options={[[false, "Calm"], [true, "Strong"]]} value={s.windy} onPick={(windy) => set({ windy })} />
      </div>
      <div className="srow">
        <span className="l">Kick-off</span>
        <Seg<boolean> options={[[false, "Day"], [true, "Night"]]} value={s.night} onPick={(night) => set({ night })} />
      </div>
      <h3>Team colours</h3>
      {([0, 1] as const).map((side) => {
        const c = shown(side);
        return (
          <div className="srow" key={side}>
            <span className="l">{names[side]}</span>
            <div className="colors">
              <input type="color" aria-label={`${names[side]} shirt colour`} value={c.primary} onChange={(e) => colour(side, "primary", e.target.value)} />
              <input type="color" aria-label={`${names[side]} trim colour`} value={c.secondary} onChange={(e) => colour(side, "secondary", e.target.value)} />
              <input type="color" aria-label={`${names[side]} number colour`} value={c.number} onChange={(e) => colour(side, "number", e.target.value)} />
              <input type="color" aria-label={`${names[side]} goalkeeper colour`} value={c.gk} onChange={(e) => colour(side, "gk", e.target.value)} />
            </div>
          </div>
        );
      })}
      <div className="srow">
        <span className="l" />
        <div className="colors">
          <small>shirt · trim · numbers · goalkeeper</small>
        </div>
      </div>
      <div className="srow">
        <label htmlFor="sv-refs">Referees</label>
        <div className="colors">
          <input id="sv-refs" type="color" value={s.refs ?? "#141414"} onChange={(e) => set({ refs: e.target.value })} />
        </div>
      </div>
      {note && <p className="note">{note}</p>}
      <button type="button" className="reset" onClick={() => onChange({ ...DEFAULT_SETTINGS })}>
        Reset to defaults
      </button>
    </div>
  );
}

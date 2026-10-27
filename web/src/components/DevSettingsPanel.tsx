import type { DevSettings, GrassPattern, Sky, TeamColours, Upkeep } from "../settings/devSettings";
import { DEFAULT_SETTINGS } from "../settings/devSettings";

interface Props {
  value: DevSettings;
  onChange: (s: DevSettings) => void;
  /** The teams' own kits (shown when no colour is picked). */
  kits: [TeamColours, TeamColours];
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
    const cur = (side ? s.away : s.home) ?? kits[side];
    set(side ? { away: { ...cur, [key]: v } } : { home: { ...cur, [key]: v } });
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
        const c = (side ? s.away : s.home) ?? kits[side];
        return (
          <div className="srow" key={side}>
            <span className="l">{names[side]}</span>
            <div className="colors">
              <input type="color" aria-label={`${names[side]} shirt colour`} value={c.primary} onChange={(e) => colour(side, "primary", e.target.value)} />
              <input type="color" aria-label={`${names[side]} trim colour`} value={c.secondary} onChange={(e) => colour(side, "secondary", e.target.value)} />
              <small>shirt · trim</small>
            </div>
          </div>
        );
      })}
      {note && <p className="note">{note}</p>}
      <button type="button" className="reset" onClick={() => onChange({ ...DEFAULT_SETTINGS })}>
        Reset to defaults
      </button>
    </div>
  );
}

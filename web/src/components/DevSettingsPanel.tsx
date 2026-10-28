import type { DevSettings, GrassPattern, RefColours, Sky, Stadium, TeamColours, Upkeep } from "../settings/devSettings";
import { DEFAULT_SETTINGS, refColours, textOn } from "../settings/devSettings";

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
    if (key === "gk") delete cur.gkNumber;
    set(side ? { away: cur } : { home: cur });
  };
  const shown = (side: 0 | 1): Required<TeamColours> => {
    const c = side ? s.away : s.home;
    const k = kits[side];
    if (!c) return k;
    const gk = c.gk ?? k.gk;
    return {
      primary: c.primary,
      secondary: c.secondary,
      number: c.number ?? textOn(c.primary),
      gk,
      gkTrim: c.gkTrim ?? c.secondary,
      gkNumber: c.gkNumber ?? (c.gk ? textOn(c.gk) : k.gkNumber),
    };
  };
  const refs = refColours(s.refs);
  const refColour = (key: keyof RefColours, v: string) => {
    const r: RefColours = { ...(s.refs ?? {}) };
    r[key] = v;
    if (key === "shirt") {
      delete r.trim;
      delete r.letter;
    }
    set({ refs: r });
  };
  const picker = (label: string, value: string, onPick: (v: string) => void) => <input type="color" aria-label={label} title={label} value={value} onChange={(e) => onPick(e.target.value)} />;
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
        <span className="l">Snow lines</span>
        <Seg<"yellow" | "orange"> options={[["yellow", "Yellow"], ["orange", "Orange"]]} value={s.snowLines} onPick={(snowLines) => set({ snowLines })} />
      </div>
      <div className="srow">
        <span className="l">Wind</span>
        <Seg<boolean> options={[[false, "Calm"], [true, "Strong"]]} value={s.windy} onPick={(windy) => set({ windy })} />
      </div>
      <div className="srow">
        <span className="l">Stadium</span>
        <Seg<Stadium> options={[["open", "Open"], ["stand", "Main stand"], ["bowl", "Bowl"]]} value={s.stadium} onPick={(stadium) => set({ stadium })} />
      </div>
      <div className="srow">
        <span className="l">Kick-off</span>
        <Seg<boolean> options={[[false, "Day"], [true, "Night"]]} value={s.night} onPick={(night) => set({ night })} />
      </div>
      <h3>Team colours</h3>
      <div className="srow">
        <span className="l" />
        <div className="colors">
          <small>shirt · trim · numbers</small>
        </div>
      </div>
      {([0, 1] as const).map((side) => {
        const c = shown(side);
        const n = names[side];
        return (
          <div key={side}>
            <div className="srow">
              <span className="l">{n}</span>
              <div className="colors">
                {picker(`${n} shirt`, c.primary, (v) => colour(side, "primary", v))}
                {picker(`${n} trim`, c.secondary, (v) => colour(side, "secondary", v))}
                {picker(`${n} numbers`, c.number, (v) => colour(side, "number", v))}
              </div>
            </div>
            <div className="srow">
              <span className="l">{n} keeper</span>
              <div className="colors">
                {picker(`${n} goalkeeper shirt`, c.gk, (v) => colour(side, "gk", v))}
                {picker(`${n} goalkeeper trim`, c.gkTrim, (v) => colour(side, "gkTrim", v))}
                {picker(`${n} goalkeeper number`, c.gkNumber, (v) => colour(side, "gkNumber", v))}
              </div>
            </div>
          </div>
        );
      })}
      <div className="srow">
        <span className="l">Referees</span>
        <div className="colors">
          {picker("Referees' shirt", refs.shirt, (v) => refColour("shirt", v))}
          {picker("Referees' trim", refs.trim, (v) => refColour("trim", v))}
          {picker("Referees' letter", refs.letter, (v) => refColour("letter", v))}
        </div>
      </div>
      {note && <p className="note">{note}</p>}
      <button type="button" className="reset" onClick={() => onChange({ ...DEFAULT_SETTINGS })}>
        Reset to defaults
      </button>
    </div>
  );
}

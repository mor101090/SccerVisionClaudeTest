import type { GeneratorPlayer } from "./generator";
import { weakFootStars } from "./weak-foot";

/**
 * A manager's club as LiveEngine.UseHomeTeam reads it (sim/SoccerVision.Wasm/Program.cs). The attribute mapping is the
 * one tools/teams/build_teams.py uses for the engine's own squads, so a generated player plays the same way here.
 */
const ATTRS: [string, string, string][] = [
  ["Aggression", "mental", "aggression"],
  ["Teamwork", "mental", "teamwork"],
  ["Composure", "mental", "composure"],
  ["Decisions", "mental", "decisions"],
  ["Tackling", "defense", "tackling"],
  ["Marking", "defense", "marking"],
  ["Positioning", "defense", "positioning"],
  ["Creativity", "playmaking", "creative"],
  ["Passing", "playmaking", "passing"],
  ["Crossing", "playmaking", "crossing"],
  ["Finishing", "attack", "finishing"],
  ["LongShots", "attack", "long_shots"],
  ["OffTheBall", "attack", "off_the_ball"],
  ["PhysicalPower", "athletic", "physical_power"],
  ["BallControl", "athletic", "ball_control"],
  ["Dribbling", "athletic", "dribbling"],
  ["Acceleration", "athletic", "acceleration"],
  ["Speed", "athletic", "speed"],
  ["Jumping", "athletic", "jumping"],
  ["Heading", "athletic", "heading"],
  ["FreeKicks", "set_pieces", "free_kicks"],
  ["Penalty", "set_pieces", "penalty"],
  ["AerialAbility", "goalkeeping", "aerial_ability"],
  ["Handling", "goalkeeping", "handling"],
  ["Kicking", "goalkeeping", "kicking"],
  ["OneOnOne", "goalkeeping", "one_on_one"],
  ["Reflexes", "goalkeeping", "reflexes"],
];
const KEEPER_OUTFIELD: Record<string, number> = {
  Tackling: 20,
  Marking: 20,
  Creativity: 35,
  Crossing: 25,
  Finishing: 12,
  LongShots: 15,
  OffTheBall: 20,
};
const OUTFIELD_KEEPING: Record<string, number> = {
  AerialAbility: 15,
  Handling: 15,
  Kicking: 22,
  OneOnOne: 15,
  Reflexes: 18,
};

function attr(p: GeneratorPlayer, name: string, grp: string, key: string): number {
  const v = p.stats[grp]?.[key];
  if (v !== undefined) return Math.trunc(v);
  if (p.position === "goalkeeper") {
    const gk = p.stats.goalkeeping ?? {};
    if (name === "Passing") return Math.trunc((gk.kicking ?? 50) * 0.85);
    if (name === "Positioning")
      return Math.trunc(((gk.one_on_one ?? 50) + (gk.reflexes ?? 50)) / 2);
    return KEEPER_OUTFIELD[name] ?? 30;
  }
  return OUTFIELD_KEEPING[name] ?? 30;
}

/** The 27 engine attributes in PlayerAttributes order (as GeneratedTeams.P takes them). */
export function engineAttributes(p: GeneratorPlayer): number[] {
  return ATTRS.map(([n, grp, k]) => attr(p, n, grp, k));
}

export function engineAttributeMap(p: GeneratorPlayer): Record<string, number> {
  return Object.fromEntries(ATTRS.map(([n, grp, k]) => [n, attr(p, n, grp, k)]));
}

/** [left, right] in stars, as build_teams.foot_ratings: the strong foot 5, the other its weak-foot stars (5 and 5: two-footed). */
export function footRatings(p: GeneratorPlayer): [number, number] {
  const weak = weakFootStars(p);
  return p.preferred_foot.dominant_foot === "Left" ? [5, weak] : [weak, 5];
}

const GROUP: Record<GeneratorPlayer["position"], string> = {
  goalkeeper: "Goalkeeper",
  defender: "Defender",
  fullback: "Defender",
  midfielder: "Midfielder",
  winger: "Midfielder",
  striker: "Forward",
};

/** Team-sheet label (GK, CB, RB/LB, DM/CM/AM, RW/LW, ST), as build_teams.pos_label. */
export function positionLabel(p: GeneratorPlayer): string {
  const left = p.preferred_foot.dominant_foot === "Left";
  const s = (grp: string, k: string) => p.stats[grp]?.[k] ?? 30;
  switch (p.position) {
    case "goalkeeper":
      return "GK";
    case "defender":
      return "CB";
    case "fullback":
      return left ? "LB" : "RB";
    case "winger":
      return left ? "LW" : "RW";
    case "striker":
      return "ST";
    default: {
      const defend =
        (s("defense", "tackling") + s("defense", "marking") + s("defense", "positioning")) / 3;
      const create =
        (s("playmaking", "creative") + s("playmaking", "passing") + s("playmaking", "crossing")) /
        3;
      const attack =
        (s("attack", "finishing") + s("attack", "off_the_ball") + s("attack", "long_shots")) / 3;
      if (defend > create + 4 && defend > attack) return "DM";
      if (attack + 4 > create && attack > defend + 8) return "AM";
      return "CM";
    }
  }
}

const FITNESS = { Low: "Low", Moderate: "Normal", Peak: "High" } as const;
const clean = (s: string) => s.replace(/[|\n\r]/g, " ").trim();

export type EngineKit = {
  primary: string;
  secondary: string;
  number: string;
  gkPrimary: string;
  gkNumber: string;
  gkSecondary: string;
};

/**
 * The UseHomeTeam text: a header line, then the 11 starters in slot order and the bench.
 * `wideMidfield` slots (5 RM and 8 LM) play as Midfielder whatever the generator position, as build_teams does.
 */
export function toEngineTeam(input: {
  name: string;
  shortName: string;
  manager: string;
  kit: EngineKit;
  seed: number;
  starters: GeneratorPlayer[];
  bench: GeneratorPlayer[];
}): string {
  const k = input.kit;
  const head = [
    input.name,
    input.shortName,
    input.manager,
    k.primary,
    k.secondary,
    k.number,
    k.gkPrimary,
    k.gkNumber,
    k.gkSecondary,
    String(input.seed),
  ]
    .map((v) => clean(v))
    .join("|");
  const line = (p: GeneratorPlayer, slot: number | null) => {
    const [first, ...rest] = clean(p.name).split(" ");
    const [left, right] = footRatings(p);
    const group = slot !== null && slot >= 5 && slot <= 8 ? "Midfielder" : GROUP[p.position];
    return [
      first ?? "",
      rest.join(" "),
      p.shirt_number,
      group,
      positionLabel(p),
      p.height,
      p.stamina,
      FITNESS[p.fitness],
      left,
      right,
      p.age,
      clean(p.nationality),
      engineAttributes(p).join(","),
    ].join("|");
  };
  return [
    head,
    ...input.starters.map((p, i) => line(p, i)),
    ...input.bench.map((p) => line(p, null)),
  ].join("\n");
}

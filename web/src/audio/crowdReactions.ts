// What the home crowd does for each match event - kept apart from the audio so it can be checked without a browser
// (see crowdReactions.check.ts). The user's team is always the home team; the visitors have a small section of fans.

export type CrowdSample = "fan_angry" | "after_goal" | "booing" | "clapping";

export interface SoundEvent {
  type: string;
  whistle?: string | null;
  detail?: number;
  /** Event value: ball speed (m/s) for NetHit and HitWoodwork. */
  value?: number;
  /** 0 = home, 1 = away, -1 = none (as in the engine's events). For a Save it is the keeper's side, for a block the blocker's. */
  team?: number;
  homeScore?: number;
  awayScore?: number;
  /** Match minute (for the crowd's sense of time-wasting). */
  minute?: number;
  /** The event's extra text (full time: "pens 4-3" after a shoot-out). */
  info?: string;
}

export interface SampleOptions {
  wide?: boolean;
  pan?: number;
  echo?: number;
  maxSeconds?: number;
  far?: boolean;
  offset?: number;
  fadeIn?: number;
  fadeOut?: number;
}

export interface Reaction {
  /** The recording to play; none: only the swell / hush of the steady crowd. */
  sample?: CrowdSample;
  gain: number;
  opts: SampleOptions;
  /** A swell of the steady crowd (anticipation), and a hush (a goal against). */
  swell?: number;
  hush?: number;
  /** A note for the log / the check ("away goal: the visitors' corner"). */
  why: string;
}

/**
 * How loud each recording is, as measured (average level of the part that plays, 0..1 full scale). The applause was recorded very
 * quietly (peak 0.12, about a thirteenth of the others), so it had to be lifted by LOUDNESS_FIX or it could not be heard at all.
 */
export const MEASURED_LEVEL: Record<CrowdSample, number> = { fan_angry: 0.2, after_goal: 0.25, booing: 0.2, clapping: 0.016 };
export const LOUDNESS_FIX: Partial<Record<CrowdSample, number>> = { clapping: 10 };

/** What the crowd has seen lately (a shot that has not been answered yet). */
export interface CrowdMemory {
  homeShotAt: number;
  homeShotAnswered: boolean;
  awayShotAt: number;
  awayShotAnswered: boolean;
}

export const newCrowdMemory = (): CrowdMemory => ({ homeShotAt: -100, homeShotAnswered: true, awayShotAt: -100, awayShotAnswered: true });

const ROAR = { wide: true, offset: 2.05, fadeIn: 0.15, fadeOut: 3 } as const; // (the recording's roar starts 2.05 s in)

const angry = (gain: number, why: string): Reaction => ({ sample: "fan_angry", gain, opts: { wide: true, maxSeconds: 7, fadeIn: 0.5, fadeOut: 2.5 }, why });
const booing = (gain: number, why: string): Reaction => ({ sample: "booing", gain, opts: { wide: true, offset: 0.6, maxSeconds: 8, fadeIn: 0.6, fadeOut: 2.5 }, why }); // (the boos start 0.6 s in)
const clapping = (gain: number, why: string): Reaction => ({ sample: "clapping", gain, opts: { wide: true, fadeIn: 0.3, fadeOut: 1.5 }, why });
const cheer = (gain: number, seconds: number, why: string): Reaction => ({ sample: "after_goal", gain, opts: { ...ROAR, maxSeconds: seconds }, why });

/**
 * The reactions to one event (an empty list: the crowd does nothing). `now` is the audio clock (s), used to tell a corner that
 * follows a shot from any other. The memory is updated.
 */
export function crowdReactions(e: SoundEvent, mem: CrowdMemory, now: number): Reaction[] {
  const home = e.team === 0;
  const away = e.team === 1;
  const out: Reaction[] = [];
  switch (e.type) {
    case "Shot":
      if (home) { mem.homeShotAt = now; mem.homeShotAnswered = false; out.push({ gain: 0, opts: {}, swell: 0.1, why: "a home shot: the crowd rises" }); }
      else if (away) { mem.awayShotAt = now; mem.awayShotAnswered = false; out.push({ gain: 0, opts: {}, swell: 0.08, why: "a shot by the visitors: the crowd holds its breath" }); }
      break;
    case "Save":
      // e.team is the keeper's side: the home keeper saves a shot by the visitors - the crowd cheers him; a home shot saved - applause for the effort
      if (home) { mem.awayShotAnswered = true; out.push(clapping(0.85, "our keeper saves it")); }
      else if (away) { mem.homeShotAnswered = true; out.push(clapping(0.6, "a home shot saved: applause for it")); }
      break;
    case "ShotOffTarget":
      if (home) { mem.homeShotAnswered = true; out.push(clapping(0.45, "a home shot goes wide")); }
      else if (away) { mem.awayShotAnswered = true; out.push(clapping(0.45, "relief: the visitors' shot goes wide")); }
      break;
    case "ShotBlocked":
      // e.team is the blocker's side
      if (home) { mem.awayShotAnswered = true; out.push(clapping(0.7, "our defender blocks it")); }
      else if (away) { mem.homeShotAnswered = true; out.push(clapping(0.4, "a home shot is blocked")); }
      break;
    case "HitWoodwork":
      // e.team is the shooter's side
      if (home) { mem.homeShotAnswered = true; out.push(clapping(0.6, "a home shot off the woodwork")); }
      else if (away) { mem.awayShotAnswered = true; out.push(clapping(0.85, "relief: the visitors hit the woodwork")); }
      break;
    case "Corner":
      // a shot turned round the post or deflected behind: applause for the effort (home attack) or for the save (visitors' attack)
      if (home && !mem.homeShotAnswered && now - mem.homeShotAt < 5) { mem.homeShotAnswered = true; out.push(clapping(0.5, "a home shot turned for a corner")); }
      else if (away && !mem.awayShotAnswered && now - mem.awayShotAt < 5) { mem.awayShotAnswered = true; out.push(clapping(0.75, "our keeper turns it round the post")); }
      break;
    case "Goal":
      if (home) out.push({ ...cheer(1, 8, "a home goal: the whole ground goes up"), swell: 0.3 });
      else if (away) {
        // the visitors' fans go wild - in their one small corner of the ground, hard to one side - and the rest of the stadium boos
        out.push({ sample: "after_goal", gain: 0.55, opts: { pan: 0.95, far: true, offset: 2.05, maxSeconds: 6, fadeIn: 0.25, fadeOut: 2 }, why: "a goal for the visitors: their corner celebrates" });
        out.push({ ...booing(0.9, "a goal for the visitors: the rest of the ground boos"), hush: 1 });
      }
      break;
    case "Foul":
      // e.team is the offender's side
      if (home) out.push(angry(0.75, "a free kick against us: furious with the referee"));
      else if (away) out.push(booing(0.55, "a foul on our player: the crowd jeers the visitors"));
      break;
    case "YellowCard":
      if (home) out.push(angry(0.85, "a card for our player: furious with the referee"));
      else if (away) out.push(clapping(0.7, "a card for the visitors: the crowd approves"));
      break;
    case "RedCard":
      if (home) { out.push(angry(0.9, "a red card for our player")); out.push(booing(0.8, "a red card for our player")); }
      else if (away) { out.push(cheer(0.65, 4.5, "a red card for the visitors: the crowd roars")); out.push(booing(0.5, "...and jeers him off")); }
      break;
    case "PenaltyAwarded":
      // e.team is the side given the penalty
      if (away) { out.push(angry(1, "a penalty against us")); out.push(booing(0.7, "...and the taker is booed")); }
      else if (home) out.push(cheer(0.7, 5, "a penalty for us"));
      break;
    case "Whistle":
      // the final whistle (the engine marks it with detail 1; a whistle at the end of ninety minutes that is followed by extra time is not)
      if (e.whistle === "FullTime" && e.detail === 1) out.push(...result(e.homeScore ?? 0, e.awayScore ?? 0));
      break;
    case "FullTime": {
      // only a shoot-out ends this way: the result of the whole match is in the text
      const pens = /pens (\d+)-(\d+)/.exec(e.info ?? "");
      if (pens) out.push(...result(+pens[1], +pens[2]));
      break;
    }
    case "ShootoutKick": {
      const scored = (e.value ?? 0) > 0.5;
      if (home === scored) out.push(cheer(scored ? 0.6 : 0.5, 4, "a shoot-out kick: " + (scored ? "scored by us" : "missed by the visitors")));
      else if (!scored) out.push(angry(0.6, "a shoot-out kick missed by us"));
      else out.push(booing(0.5, "a shoot-out kick scored by the visitors"));
      break;
    }
    case "Substitution":
      // a late change by the visitors when they are ahead: wasting time - booed
      if (away && (e.awayScore ?? 0) > (e.homeScore ?? 0) && (e.minute ?? 0) >= 70) out.push(booing(0.6, "time wasting"));
      break;
    case "GoalkeeperHoldViolation":
      if (away) out.push(booing(0.6, "the visitors' keeper holds it too long"));
      break;
  }
  return out;
}

/** The home fans at the end: a win is cheered, a defeat booed, a draw gets polite applause. */
function result(h: number, a: number): Reaction[] {
  if (h > a) return [cheer(0.9, 9, "full time: a home win")];
  if (h < a) return [booing(0.95, "full time: a home defeat")];
  return [clapping(0.7, "full time: a draw")];
}

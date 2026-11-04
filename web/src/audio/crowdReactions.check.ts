// Checks what the crowd does (no browser needed):  node --experimental-strip-types src/audio/crowdReactions.check.ts
import { crowdReactions, LOUDNESS_FIX, MEASURED_LEVEL, newCrowdMemory, type Reaction, type SoundEvent } from "./crowdReactions.ts";

let failed = 0;
function check(name: string, ok: boolean, detail = "") {
  if (!ok) failed++;
  console.log((ok ? "ok   " : "FAIL ") + name + (detail ? "  - " + detail : ""));
}
const SAMPLE_LEVEL = 0.42; // (as in matchAudio.ts)
/** How loud a reaction really is at the speaker, as a fraction of full scale (one copy; the "wide" ones play three). */
const loudness = (r: Reaction) => (r.sample ? r.gain * SAMPLE_LEVEL * (LOUDNESS_FIX[r.sample] ?? 1) * MEASURED_LEVEL[r.sample] : 0);
const run = (e: SoundEvent, mem = newCrowdMemory(), now = 100) => crowdReactions(e, mem, now);
const samples = (rs: Reaction[]) => rs.filter((r) => r.sample).map((r) => r.sample);

// 1. every recorded reaction is audible - the applause was recorded so quietly that it could not be heard
const AUDIBLE = 0.012;
const every: SoundEvent[] = [
  { type: "Save", team: 0 }, { type: "Save", team: 1 }, { type: "ShotOffTarget", team: 0 }, { type: "ShotOffTarget", team: 1 }, { type: "ShotBlocked", team: 0 },
  { type: "ShotBlocked", team: 1 }, { type: "HitWoodwork", team: 0 }, { type: "HitWoodwork", team: 1 }, { type: "YellowCard", team: 1 }, { type: "Foul", team: 0 }, { type: "Foul", team: 1 },
  { type: "YellowCard", team: 0 }, { type: "RedCard", team: 0 }, { type: "RedCard", team: 1 }, { type: "PenaltyAwarded", team: 0 }, { type: "PenaltyAwarded", team: 1 },
  { type: "Goal", team: 0 }, { type: "Goal", team: 1 },
  { type: "Whistle", whistle: "FullTime", detail: 1, homeScore: 2, awayScore: 1 }, { type: "Whistle", whistle: "FullTime", detail: 1, homeScore: 0, awayScore: 1 }, { type: "Whistle", whistle: "FullTime", detail: 1, homeScore: 1, awayScore: 1 },
];
for (const e of every) for (const r of run(e)) if (r.sample) check(`audible: ${e.type} team ${e.team}: ${r.why}`, loudness(r) >= AUDIBLE, `${loudness(r).toFixed(3)} (${r.sample})`);

// 2. an opponent's goal: the visitors' fans celebrate loudly but only from one side; the rest of the stadium boos
{
  const rs = run({ type: "Goal", team: 1 });
  const yes = rs.find((r) => r.sample === "after_goal"), boo = rs.find((r) => r.sample === "booing");
  check("away goal: their corner celebrates", !!yes && yes.gain >= 0.4, yes ? `gain ${yes.gain}` : "none");
  check("away goal: ...from one side only (not a wide stereo sound)", !!yes && !yes.opts.wide && Math.abs(yes.opts.pan ?? 0) >= 0.9);
  check("away goal: the rest of the ground boos, wide and loud", !!boo && !!boo.opts.wide && boo.gain >= 0.8);
  check("away goal: the booing is louder than the visitors' celebration", !!yes && !!boo && loudness(boo) > loudness(yes));
  check("away goal: the crowd goes quiet (hush)", rs.some((r) => (r.hush ?? 0) > 0));
  const home = run({ type: "Goal", team: 0 });
  check("home goal: the whole ground roars (wide)", home.some((r) => r.sample === "after_goal" && r.opts.wide && r.gain >= 0.9));
}

// 3. the opponent fouls, is carded; the keeper turns a shot round the post; the opponent hits the post
check("foul by the visitors: the crowd jeers", samples(run({ type: "Foul", team: 1 })).includes("booing"));
check("foul by our side: the crowd is angry with the referee", samples(run({ type: "Foul", team: 0 })).includes("fan_angry"));
check("card for the visitors: the crowd approves", samples(run({ type: "YellowCard", team: 1 })).includes("clapping"));
check("card for our player: the crowd is angry", samples(run({ type: "YellowCard", team: 0 })).includes("fan_angry"));
check("red card for the visitors: the crowd roars", samples(run({ type: "RedCard", team: 1 })).includes("after_goal"));
{
  const mem = newCrowdMemory();
  run({ type: "Shot", team: 1 }, mem, 50);
  check("a shot by the visitors: the crowd swells", run({ type: "Shot", team: 1 }, newCrowdMemory(), 50).some((r) => (r.swell ?? 0) > 0));
  check("our keeper saves it: applause", samples(run({ type: "Save", team: 0 }, mem, 51)).includes("clapping"));
  const mem2 = newCrowdMemory();
  run({ type: "Shot", team: 1 }, mem2, 50);
  check("the shot is turned round the post for a corner: applause", samples(run({ type: "Corner", team: 1 }, mem2, 52)).includes("clapping"));
  check("...but a corner that follows nothing gets none", samples(run({ type: "Corner", team: 1 }, newCrowdMemory(), 52)).length === 0);
}
check("the visitors hit the post: relief and applause", samples(run({ type: "HitWoodwork", team: 1 })).includes("clapping"));
check("a home shot off the woodwork: applause", samples(run({ type: "HitWoodwork", team: 0 })).includes("clapping"));
check("a shot by the visitors goes wide: relief", samples(run({ type: "ShotOffTarget", team: 1 })).includes("clapping"));
check("our defender blocks a shot: applause", samples(run({ type: "ShotBlocked", team: 0 })).includes("clapping"));

// 4. the final whistle: a win is cheered, a defeat booed, a draw gets applause - at the whistle, not after the walk-off
const fin = (h: number, a: number): SoundEvent => ({ type: "Whistle", whistle: "FullTime", detail: 1, homeScore: h, awayScore: a });
check("final whistle, a home win: the crowd cheers", samples(run(fin(2, 0))).join() === "after_goal");
check("final whistle, a home defeat: the crowd boos", samples(run(fin(0, 1))).join() === "booing");
check("final whistle, a draw: applause", samples(run(fin(1, 1))).join() === "clapping");
check("a whistle that is not the final one (extra time follows) gets no reaction", run({ type: "Whistle", whistle: "FullTime", detail: 0, homeScore: 1, awayScore: 1 }).length === 0);
check("half time gets no reaction", run({ type: "Whistle", whistle: "HalfTime", detail: 0 }).length === 0);
check("the FullTime event itself does not react a second time", run({ type: "FullTime", homeScore: 2, awayScore: 0 }).length === 0);
check("...except after a shoot-out (the text holds the result)", samples(run({ type: "FullTime", info: "pens 4-5", homeScore: 1, awayScore: 1 })).join() === "booing");

console.log(failed ? `\n${failed} FAILED` : "\nall checks passed");
process.exit(failed ? 1 : 0);

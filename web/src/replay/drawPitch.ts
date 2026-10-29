import { Ball3D } from "./ball3d";
import { type DevSettings, refColours } from "../settings/devSettings";
import { PitchLook } from "./conditions";
import { playerAt } from "./replay";
import type { Replay, Snapshot } from "./replay";

// Field of play (IFAB Law 1) in metres; world origin at the centre mark.
const L = 105;
const W = 68;
const HL = L / 2;
const HW = W / 2;
export const MARGIN = 5;
/** World shown: the pitch plus a wider band on top (scoreboard, technical areas) and below (commentary bar). */
const TOP = 46;
const BOTTOM = -41;
export const WORLD_W = L + MARGIN * 2;
export const WORLD_H = TOP - BOTTOM;

export interface View {
  scale: number; // px per metre
  ox: number;
  oy: number;
}

export function makeView(width: number, height: number): View {
  const scale = Math.min(width / WORLD_W, height / WORLD_H);
  return { scale, ox: width / 2, oy: (height - WORLD_H * scale) / 2 + TOP * scale };
}

const px = (v: View, x: number) => v.ox + x * v.scale;
const py = (v: View, y: number) => v.oy - y * v.scale;

/** Weather, pitch and light (one per page: the textures are built once per match). */
export const look = new PitchLook();

/**
 * Everything that does not move - turf, mud, puddles, the painted lines and the light - is drawn once into an image and
 * only copied each frame; in the snow the picture (turf, the snow as trodden so far, the lines) is put together twice a
 * second.
 */
let bg: { key: string; canvas: HTMLCanvasElement; base: HTMLCanvasElement | null; lines: HTMLCanvasElement | null; composedAt: number } | null = null;

export function drawPitch(ctx: CanvasRenderingContext2D, v: View, settings: DevSettings) {
  const w = ctx.canvas.width;
  const h = ctx.canvas.height;
  const snow = settings.sky === "Snow";
  const key = `${w}x${h}|${v.scale}|${settings.grass}|${settings.sky}|${settings.night}|${settings.snowLines}|${settings.stadium}|${look.groundKey}`;
  const make = () => {
    const c = document.createElement("canvas");
    c.width = w;
    c.height = h;
    return c;
  };
  if (!bg || bg.key !== key) {
    const canvas = make();
    const g = canvas.getContext("2d");
    if (!g) return;
    look.grass(g, v, settings, TOP, BOTTOM);
    look.ground(g, v);
    if (snow) {
      look.bakeLight(g, v, settings);
      const lines = make();
      const lg = lines.getContext("2d");
      if (lg) paintLines(lg, v, settings);
      bg = { key, canvas: make(), base: canvas, lines, composedAt: -1 };
    } else {
      paintLines(g, v, settings);
      look.bakeLight(g, v, settings);
      bg = { key, canvas, base: null, lines: null, composedAt: -1 };
    }
  }
  if (bg.base && bg.lines) {
    const now = performance.now();
    if (bg.composedAt < 0 || (look.snowChanged && Math.abs(now - bg.composedAt) > 500)) {
      bg.composedAt = now;
      look.snowChanged = false;
      const g = bg.canvas.getContext("2d");
      if (g) {
        g.drawImage(bg.base, 0, 0);
        look.snowLayer(g, v);
        g.drawImage(bg.lines, 0, 0);
      }
    }
  }
  ctx.drawImage(bg.canvas, 0, 0);
}

function paintLines(ctx: CanvasRenderingContext2D, v: View, settings: DevSettings) {
  const s = v.scale;
  const paint = look.linePaint(settings);
  ctx.strokeStyle = paint;
  ctx.lineWidth = Math.max(1, 0.12 * s);
  const snow = settings.sky === "Snow";
  if (snow) {
    // the coloured lines on the snow, with a faint edge
    ctx.save();
    ctx.shadowColor = "rgba(10,30,10,0.35)";
    ctx.shadowBlur = Math.max(1, 0.15 * s);
    ctx.lineWidth = Math.max(1.4, 0.16 * s);
  }
  // Technical areas (coaches) above the top touchline, either side of the halfway line.
  ctx.strokeRect(px(v, -19), py(v, 39.6), 17.5 * s, 3.6 * s);
  ctx.strokeRect(px(v, 1.5), py(v, 39.6), 17.5 * s, 3.6 * s);
  const line = (x1: number, y1: number, x2: number, y2: number) => {
    ctx.beginPath();
    ctx.moveTo(px(v, x1), py(v, y1));
    ctx.lineTo(px(v, x2), py(v, y2));
    ctx.stroke();
  };
  const arc = (x: number, y: number, r: number, a0: number, a1: number) => {
    ctx.beginPath();
    ctx.arc(px(v, x), py(v, y), r * s, -a1, -a0);
    ctx.stroke();
  };
  ctx.strokeRect(px(v, -HL), py(v, HW), L * s, W * s);
  line(0, -HW, 0, HW);
  arc(0, 0, 9.15, 0, Math.PI * 2);
  ctx.fillStyle = paint;
  ctx.beginPath();
  ctx.arc(px(v, 0), py(v, 0), Math.max(1.5, 0.2 * s), 0, Math.PI * 2);
  ctx.fill();
  for (const side of [-1, 1]) {
    const gl = HL * side;
    const pa = (HL - 16.5) * side;
    const ga = (HL - 5.5) * side;
    line(gl, -20.16, pa, -20.16);
    line(gl, 20.16, pa, 20.16);
    line(pa, -20.16, pa, 20.16);
    line(gl, -9.16, ga, -9.16);
    line(gl, 9.16, ga, 9.16);
    line(ga, -9.16, ga, 9.16);
    const spot = (HL - 11) * side;
    ctx.beginPath();
    ctx.arc(px(v, spot), py(v, 0), Math.max(1.5, 0.15 * s), 0, Math.PI * 2);
    ctx.fill();
    const half = Math.acos(5.5 / 9.15);
    const centre = side > 0 ? Math.PI : 0;
    arc(spot, 0, 9.15, centre - half, centre + half);
    for (const c of [-1, 1]) {
      const start = side > 0 ? (c > 0 ? Math.PI : Math.PI / 2) : c > 0 ? (3 * Math.PI) / 2 : 0;
      arc(gl, HW * c, 1, start, start + Math.PI / 2);
    }
    // (the goal frame is drawn over the nets by goalFrame, always white)
  }
  if (snow) ctx.restore();
}

/**
 * A goal's net from above as a mesh of strings: the ball presses the netting out where it lies, a hard shot makes
 * it bulge and ripple (NetHit), and the frame flashes when the ball rattles the woodwork. Same model as Unity's GoalNetView.
 */
function drawNet(ctx: CanvasRenderingContext2D, v: View, side: number, ball: { x: number; y: number }, ripple: { x: number; y: number; amp: number } | null, flash: { x: number; y: number; age: number } | null, colour: string) {
  const HALF_GOAL = 3.66;
  const DEPTH = 2;
  const gl = HL * side;
  let at = { x: 0, y: 0 };
  let amp = 0;
  if (ball.x * side > HL && Math.abs(ball.y) < HALF_GOAL + 0.5) {
    amp = Math.min(1, (ball.x * side - HL) / DEPTH) * 0.35;
    at = ball;
  }
  if (ripple && (Math.abs(ripple.amp) > amp || amp === 0)) {
    amp = ripple.amp;
    at = ripple;
  }
  const atD = at.x * side - HL;
  const P = (y: number, d: number): [number, number] => {
    const w = amp === 0 ? 0 : Math.exp(-((y - at.y) ** 2 + (d - atD) ** 2) / (2 * 0.85 * 0.85));
    const push = amp * w * (0.35 + 0.65 * Math.min(1, d / DEPTH));
    return [px(v, gl + (d + push) * side), py(v, y + (y - at.y) * amp * w * 0.25)];
  };
  ctx.save();
  const n = parseInt(colour.slice(1), 16);
  ctx.strokeStyle = `rgba(${n >> 16},${(n >> 8) & 255},${n & 255},0.62)`;
  const ny = 25;
  const nd = 8;
  const ys = Array.from({ length: ny }, (_, i) => -HALF_GOAL + (i * 2 * HALF_GOAL) / (ny - 1));
  const ds = Array.from({ length: nd }, (_, i) => (i * DEPTH) / (nd - 1));
  ys.forEach((y, i) => {
    ctx.lineWidth = i === 0 || i === ny - 1 ? Math.max(1.5, 0.18 * v.scale) : 1;
    ctx.beginPath();
    ds.forEach((d, k) => (k ? ctx.lineTo(...P(y, d)) : ctx.moveTo(...P(y, d))));
    ctx.stroke();
  });
  ds.forEach((d, k) => {
    ctx.lineWidth = k === nd - 1 ? Math.max(1.5, 0.18 * v.scale) : 1;
    ctx.beginPath();
    ys.forEach((y, i) => (i ? ctx.lineTo(...P(y, d)) : ctx.moveTo(...P(y, d))));
    ctx.stroke();
  });
  if (flash && flash.age < 0.6) {
    const g = ctx.createRadialGradient(px(v, flash.x), py(v, flash.y), 0, px(v, flash.x), py(v, flash.y), (1 + flash.age * 2) * v.scale);
    g.addColorStop(0, `rgba(255,245,170,${0.9 * (1 - flash.age / 0.6)})`);
    g.addColorStop(1, "rgba(255,245,170,0)");
    ctx.fillStyle = g;
    ctx.beginPath();
    ctx.arc(px(v, flash.x), py(v, flash.y), (1 + flash.age * 2) * v.scale, 0, Math.PI * 2);
    ctx.fill();
  }
  ctx.restore();
}

/** Latest NetHit / HitWoodwork before replay time t (for the net ripple and the frame flash). */
function netEffects(replay: Replay, t: number, side: number) {
  let ripple: { x: number; y: number; amp: number; peak: number } | null = null;
  let flash: { x: number; y: number; age: number } | null = null;
  for (const e of replay.events) {
    if (e.t > t) break;
    if (t - e.t > 2 || e.x === undefined || e.y === undefined || Math.sign(e.x) !== side) continue;
    const age = t - e.t;
    if (e.type === "NetHit") {
      // keep the strongest ripple still running (a soft second touch must not cancel a hard shot's bulge)
      const peak = Math.min(0.9, (e.v ?? 10) * 0.045) * Math.exp(-3 * age);
      if (!ripple || peak > Math.abs(ripple.peak)) ripple = { x: e.x, y: e.y, amp: peak * Math.cos(12 * age), peak };
    }
    if (e.type === "HitWoodwork") flash = { x: e.x, y: e.y, age };
  }
  return { ripple, flash };
}

const ball3d = new Ball3D();

/** Call when a different match is loaded (the ball's spin starts fresh). */
export function resetBall() {
  ball3d.reset();
}

/** The goal frame (crossbar over the line, the posts at its ends) stays white whatever colour the net is. */
function goalFrame(ctx: CanvasRenderingContext2D, v: View, side: number) {
  const s = v.scale;
  const gl = HL * side;
  ctx.save();
  ctx.strokeStyle = "#fbfbfb";
  ctx.lineWidth = Math.max(2, 0.24 * s);
  ctx.lineCap = "round";
  ctx.beginPath();
  ctx.moveTo(px(v, gl), py(v, -3.72));
  ctx.lineTo(px(v, gl), py(v, 3.72));
  ctx.stroke();
  ctx.fillStyle = "#ffffff";
  for (const y of [-3.72, 3.72]) {
    ctx.beginPath();
    ctx.arc(px(v, gl), py(v, y), Math.max(1.6, 0.16 * s), 0, Math.PI * 2);
    ctx.fill();
  }
  ctx.restore();
}

/** Offside: a small chequered square above the player who was caught (no text, no pole). */
function offsideMark(ctx: CanvasRenderingContext2D, x: number, y: number, s: number) {
  const size = 1.3 * s;
  const left = x - size / 2;
  const top = y - 2.6 * s;
  for (let cx = 0; cx < 2; cx++)
    for (let cy = 0; cy < 2; cy++) {
      ctx.fillStyle = (cx + cy) % 2 ? "#e53935" : "#ffd21a";
      ctx.fillRect(left + (cx * size) / 2, top + (cy * size) / 2, size / 2, size / 2);
    }
  ctx.strokeStyle = "rgba(0,0,0,0.6)";
  ctx.lineWidth = Math.max(1, 0.1 * s);
  ctx.strokeRect(left, top, size, size);
}

export function drawEntities(ctx: CanvasRenderingContext2D, v: View, replay: Replay, snap: Snapshot, time: number, dt: number, settings: DevSettings) {
  const s = v.scale;
  const r = 0.9 * s;
  const teams = [replay.home, replay.away];
  // Officials
  snap.officials.forEach((o, i) => {
    look.shadow(ctx, v, settings, o.x, o.y, px(v, o.x), py(v, o.y), r, r * 0.9, 0.45);
    const rc = refColours(settings.refs);
    // the referee watches the ball, the assistants face the pitch from their touchline
    feet(ctx, px(v, o.x), py(v, o.y), r, i === 0 ? Math.atan2(snap.ball.y - o.y, snap.ball.x - o.x) : Math.atan2(-o.y, 0), 0, false, rc.boots);
    disc(ctx, px(v, o.x), py(v, o.y), r, rc.shirt, rc.trim, 0.12); // the same marker as a player
    ctx.fillStyle = rc.letter;
    ctx.font = `800 ${Math.max(8, 1.05 * s)}px system-ui, sans-serif`;
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    fillCentered(ctx, i === 0 ? "R" : "L", px(v, o.x), py(v, o.y));
    if (i > 0 && o.flag) assistantFlag(ctx, px(v, o.x), py(v, o.y), s, time);
    if (i === 0 && o.card) refereeCard(ctx, px(v, o.x), py(v, o.y), s, o.card);
  });
  // Corner flags
  let flagIndex = 0;
  for (const cx of [-HL, HL]) for (const cy of [-HW, HW]) cornerFlag(ctx, px(v, cx), py(v, cy), s, time, flagIndex++, px(v, cx + Math.sign(cx)) - px(v, cx), py(v, cy + Math.sign(cy)) - py(v, cy));
  // Players: circle with border and number; lifted while jumping.
  snap.players.forEach((p, i) => {
    if (p.sentOff && Math.abs(p.y) > 35.5) return;
    const team = teams[i < 11 ? 0 : 1];
    const info = playerAt(replay, i, snap.t);
    const x = px(v, p.x);
    const gy = py(v, p.y);
    // Leap of 0.2-0.75 m: the body rises on screen, the shadow stays on the grass and shrinks/fades with height.
    const h = Math.min(1, Math.max(0, p.jump));
    const lift = h * 2.2 * s;
    const y = gy - lift;
    if (p.sliding || p.grounded) {
      drawDownPlayer(ctx, x, gy, r, s, -p.facing, p.sliding, p.gk ? team.kit.gk : team.kit.primary, p.gk ? (team.kit.gkTrim ?? team.kit.secondary) : team.kit.secondary);
      ctx.fillStyle = p.gk ? team.kit.gkNumber : team.kit.number;
      ctx.font = `bold ${Math.max(8, r * 1.05)}px system-ui, sans-serif`;
      ctx.textAlign = "center";
      ctx.textBaseline = "middle";
      fillCentered(ctx, String(info.number), x, gy);
      if (p.booked) {
        ctx.fillStyle = "#ffd600";
        ctx.fillRect(x + r * 0.55, gy - r * 1.05, r * 0.32, r * 0.45);
      }
      if (p.injured) injuryMark(ctx, x, gy, r);
      return;
    }
    const grow = 1 + h * 0.1;
    const shrink = 1 - h * 0.6;
    look.shadow(ctx, v, settings, p.x, p.y, x, gy, r * 1.05 * shrink, r * 0.95 * shrink, 0.5 * shrink);
    // the design's player marker: two small boots peeking out (pointing where he faces), a plain circle in the shirt
    // colour with a thin black edge, the number
    feet(ctx, x, y, r * grow, p.facing, p.kick, p.kickLeft, team.kit.boots);
    disc(ctx, x, y, r * grow, p.gk ? team.kit.gk : team.kit.primary, p.gk ? (team.kit.gkTrim ?? team.kit.secondary) : team.kit.secondary, 0.12);
    ctx.fillStyle = p.gk ? team.kit.gkNumber : team.kit.number;
    ctx.font = `bold ${Math.max(8, r * grow * 1.05)}px system-ui, sans-serif`;
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    fillCentered(ctx, String(info.number), x, y);
    if (p.booked) {
      ctx.fillStyle = "#ffd600";
      ctx.fillRect(x + r * 0.55, y - r * 1.05, r * 0.32, r * 0.45);
    }
    if (p.injured) injuryMark(ctx, x, y, r);
    if (look.inShade(settings, p.x, p.y)) {
      ctx.fillStyle = "rgba(10,24,40,0.3)";
      ctx.beginPath();
      ctx.arc(x, y, r * grow * 1.1, 0, Math.PI * 2);
      ctx.fill();
    }
  });
  snap.players.forEach((p) => {
    if (p.offside) offsideMark(ctx, px(v, p.x), py(v, p.y), s);
  });
  // Ball: grows with height, shadow stays below (smaller, lighter the higher it is).
  const b = snap.ball;
  const h = Math.max(0, b.z - 0.11);
  const bx = px(v, b.x);
  const by = py(v, b.y);
  const bsr = 0.42 * s * Math.max(0.45, 1 - h * 0.05);
  look.shadow(ctx, v, settings, b.x, b.y, bx, by, bsr, bsr, Math.max(0.18, 0.6 - h * 0.04));
  ball3d.winter = settings.sky === "Snow";
  const owner = b.owner >= 0 ? snap.players[b.owner] : null;
  // No spin while held (keeper's hands, carried overhead for a throw-in) or while play is stopped (the ball waits to be taken).
  ball3d.spin(b.x, b.y, h, dt, snap.phase !== 0 || (owner !== null && (owner.gk || b.z > 0.4)));
  ball3d.draw(ctx, bx, by - Math.min(20, h) * 0.55 * s, 0.36 * s * (1 + Math.min(20, h) * 0.07));
  // The nets go over a ball lying in the goal (roof netting seen from above).
  for (const side of [-1, 1]) {
    const fx = netEffects(replay, snap.t, side);
    drawNet(ctx, v, side, b, fx.ripple, fx.flash, settings.net);
    goalFrame(ctx, v, side);
  }
  // The name of the player in possession, centred on a plate under him
  if (b.owner >= 0 && snap.phase === 0) {
    const p = snap.players[b.owner];
    const info = playerAt(replay, b.owner, snap.t);
    const label = info.shortName;
    ctx.font = `600 ${Math.max(10, 0.95 * s)}px system-ui, sans-serif`;
    const m = ctx.measureText(label);
    const tw = m.actualBoundingBoxRight + m.actualBoundingBoxLeft;
    const th = m.actualBoundingBoxAscent + m.actualBoundingBoxDescent;
    const padX = 0.45 * s;
    const padY = 0.3 * s;
    const lx = px(v, p.x);
    const ly = py(v, p.y) + r * 2.1;
    ctx.fillStyle = "rgba(0,0,0,0.6)";
    ctx.fillRect(lx - tw / 2 - padX, ly - th / 2 - padY, tw + 2 * padX, th + 2 * padY);
    ctx.fillStyle = "#fff";
    fillCentered(ctx, label, lx, ly);
  }
}

/** Draws text so its glyph box is centred on (x, y), using real glyph metrics. */
export function fillCentered(ctx: CanvasRenderingContext2D, text: string, x: number, y: number) {
  ctx.textAlign = "left";
  ctx.textBaseline = "alphabetic";
  const m = ctx.measureText(text);
  ctx.fillText(text, x - (m.actualBoundingBoxRight - m.actualBoundingBoxLeft) / 2, y + (m.actualBoundingBoxAscent - m.actualBoundingBoxDescent) / 2);
}

/**
 * Cloth that moves the way a real flag does in a light breeze: no flapping, a slow sway of the whole flag and a soft
 * ripple running from the pole to the free edge, growing towards the free edge. Returns the top edge points.
 */
function clothEdge(x0: number, y0: number, w: number, h: number, t: number, seed: number, dir: number): [number, number][] {
  const sway = Math.sin(t * 0.45 + seed) * 0.06 + Math.sin(t * 0.17 + seed * 2) * 0.04;
  const pts: [number, number][] = [];
  const n = 6;
  for (let k = 0; k <= n; k++) {
    const u = k / n;
    const ripple = Math.sin(t * 1.7 + seed - u * 2.4) * 0.07 * u * h;
    pts.push([x0 + dir * u * w * (1 - 0.04 * u), y0 + ripple + sway * u * h + 0.1 * u * u * h]);
  }
  return pts;
}

function clothPath(ctx: CanvasRenderingContext2D, pts: [number, number][], y0: number, y1: number, from: number, to: number) {
  ctx.beginPath();
  for (let k = from; k <= to; k++) ctx.lineTo(pts[k][0], pts[k][1] + y0);
  for (let k = to; k >= from; k--) ctx.lineTo(pts[k][0], pts[k][1] + y1);
  ctx.closePath();
}

/**
 * Corner flag seen from above (top view, like the rest of the pitch): the white top of the pole and the yellow cloth
 * seen edge-on, hanging out away from the pitch (ax, ay: outward screen direction) and swaying slowly in the breeze.
 */
function cornerFlag(ctx: CanvasRenderingContext2D, x: number, y: number, s: number, t: number, seed: number, ax: number, ay: number) {
  // In a strong wind the cloth streams out downwind, stretched and rippling fast; in still air it hangs limp and sways.
  const windy = look.wind.speed > 0.5;
  const len = (windy ? 1.4 : 1.05) * s; // a breeze at least
  const wid = 0.42 * s;
  const n = 6;
  const rate = windy ? 4.2 : 1.7;
  void ax;
  void ay;
  const ang = look.wind.screenAngle + Math.sin(t * (windy ? 1.3 : 0.45) + seed * 1.7) * (windy ? 0.07 : 0.16) + Math.sin(t * 0.17 + seed * 3.4) * 0.1;
  const dx = Math.cos(ang);
  const dy = Math.sin(ang);
  const top: [number, number][] = [];
  const bot: [number, number][] = [];
  for (let k = 0; k <= n; k++) {
    const u = k / n;
    const bend = Math.sin(t * rate + seed * 1.7 - u * 2.4) * (windy ? 0.1 : 0.12) * u * s;
    const w = wid * (1 - 0.25 * u);
    const cx = x + dx * u * len - dy * bend;
    const cy = y + dy * u * len + dx * bend;
    top.push([cx - (dy * w) / 2, cy + (dx * w) / 2]);
    bot.push([cx + (dy * w) / 2, cy - (dx * w) / 2]);
  }
  const strip = (ox: number, oy: number) => {
    ctx.beginPath();
    for (const q of top) ctx.lineTo(q[0] + ox, q[1] + oy);
    for (let k = n; k >= 0; k--) ctx.lineTo(bot[k][0] + ox, bot[k][1] + oy);
    ctx.closePath();
  };
  ctx.fillStyle = "rgba(0,0,0,0.22)"; // shadow on the grass
  strip(0.35 * s, 0.35 * s);
  ctx.fill();
  ctx.fillStyle = "#ffd21f";
  strip(0, 0);
  ctx.fill();
  ctx.strokeStyle = "rgba(150,100,0,0.6)";
  ctx.lineWidth = Math.max(0.7, 0.04 * s);
  ctx.stroke();
  ctx.beginPath();
  ctx.arc(x, y, Math.max(1.6, 0.2 * s), 0, Math.PI * 2);
  ctx.fillStyle = "#f6f6f4";
  ctx.fill();
  ctx.lineWidth = Math.max(0.6, 0.05 * s);
  ctx.strokeStyle = "rgba(0,0,0,0.45)";
  ctx.stroke();
}

/** The assistant's flag, raised only when he signals: black handle, checked orange / neon-yellow cloth. */
function assistantFlag(ctx: CanvasRenderingContext2D, x: number, y: number, s: number, t: number) {
  const fx = x + 0.3 * s;
  ctx.strokeStyle = "#111";
  ctx.lineWidth = Math.max(1.5, 0.15 * s);
  ctx.beginPath();
  ctx.moveTo(fx, y - 0.5 * s);
  ctx.lineTo(fx, y - 2.6 * s);
  ctx.stroke();
  const w = 1.6 * s;
  const h = 1.1 * s;
  const windy = look.wind.speed > 0.5;
  const pts = clothEdge(fx, y - 2.6 * s, w, h, t * (windy ? 3.2 : 1.6), 3.1, Math.cos(look.wind.screenAngle) < 0 ? -1 : 1);
  for (let qx = 0; qx < 2; qx++)
    for (let qy = 0; qy < 2; qy++) {
      clothPath(ctx, pts, (qy * h) / 2, ((qy + 1) * h) / 2, qx * 3, qx * 3 + 3);
      ctx.fillStyle = (qx + qy) % 2 ? "#ff5a1f" : "#e4f53c";
      ctx.fill();
    }
  clothPath(ctx, pts, 0, h, 0, pts.length - 1);
  ctx.strokeStyle = "#ff5a1f";
  ctx.lineWidth = Math.max(0.8, 0.07 * s);
  ctx.stroke();
}

/** The referee holds a card up above his head: yellow, or red. */
function refereeCard(ctx: CanvasRenderingContext2D, x: number, y: number, s: number, card: number) {
  const w = 1.05 * s;
  const h = 1.45 * s;
  const cx = x + 0.45 * s;
  const top = y - 3.6 * s;
  ctx.strokeStyle = "#121212";
  ctx.lineWidth = Math.max(1.2, 0.14 * s);
  ctx.beginPath();
  ctx.moveTo(x + 0.25 * s, y - 0.5 * s);
  ctx.lineTo(cx, top + h);
  ctx.stroke();
  ctx.fillStyle = card === 2 ? "#e0262b" : "#ffd21a";
  ctx.fillRect(cx - w / 2, top, w, h);
  ctx.strokeStyle = "rgba(0,0,0,0.55)";
  ctx.lineWidth = Math.max(1, 0.06 * s);
  ctx.strokeRect(cx - w / 2, top, w, h);
}

/** An injured player (playing on): a small white square with a red cross, opposite the card marker. */
function injuryMark(ctx: CanvasRenderingContext2D, x: number, y: number, r: number) {
  const w = r * 0.56;
  const cx = x - r * 0.8;
  const cy = y - r * 0.85;
  ctx.fillStyle = "#fff";
  ctx.fillRect(cx - w / 2, cy - w / 2, w, w);
  ctx.fillStyle = "#e0262b";
  ctx.fillRect(cx - w * 0.09, cy - w * 0.34, w * 0.18, w * 0.68);
  ctx.fillRect(cx - w * 0.34, cy - w * 0.09, w * 0.68, w * 0.18);
}

/**
 * A player on the ground (slide tackle, then getting up): the body lies stretched along the slide (screen angle a),
 * with a streak of churned-up grass behind him while he is still sliding.
 */
function drawDownPlayer(ctx: CanvasRenderingContext2D, x: number, y: number, r: number, s: number, a: number, sliding: boolean, fill: string, stroke: string) {
  const c = Math.cos(a);
  const sn = Math.sin(a);
  if (sliding) {
    const g = ctx.createLinearGradient(x - c * r * 2.4, y - sn * r * 2.4, x, y);
    g.addColorStop(0, "rgba(225,240,205,0)");
    g.addColorStop(1, "rgba(225,240,205,0.3)");
    ctx.strokeStyle = g;
    ctx.lineWidth = r * 0.6;
    ctx.lineCap = "round";
    ctx.beginPath();
    ctx.moveTo(x - c * r * 2.4, y - sn * r * 2.4);
    ctx.lineTo(x - c * r * 0.6, y - sn * r * 0.6);
    ctx.stroke();
  }
  ctx.save();
  ctx.translate(x, y);
  ctx.rotate(a);
  ctx.fillStyle = "rgba(0,0,0,0.45)";
  ctx.beginPath();
  ctx.ellipse(0.15 * s, 0.2 * s, r * 1.2, r * 0.96, 0, 0, Math.PI * 2);
  ctx.fill();
  ctx.beginPath();
  ctx.ellipse(0, 0, r * 1.22, r * 0.93, 0, 0, Math.PI * 2);
  ctx.fillStyle = fill;
  ctx.fill();
  ctx.lineWidth = Math.max(1, r * 0.18);
  ctx.strokeStyle = stroke;
  ctx.stroke();
  ctx.restore();
}

/**
 * Two small boots close under the front edge of a player's circle, a little back, pointing where he faces. A kick or a
 * pass swings the kicking foot (back, through the ball, settle) - the legs never run on the spot. side -1 is the left foot.
 */
function feet(ctx: CanvasRenderingContext2D, x: number, y: number, r: number, facing: number, kick = 0, kickLeft = false, boots = BOOTS) {
  const a = -facing;
  const c = Math.cos(a);
  const sn = Math.sin(a);
  ctx.fillStyle = boots;
  ctx.strokeStyle = "rgba(0,0,0,0.45)";
  ctx.lineWidth = Math.max(0.6, r * 0.06);
  for (const side of [-1, 1]) {
    ctx.beginPath();
    const fwd = 0.8 + (side < 0 === kickLeft ? kick : 0);
    ctx.ellipse(x + c * r * fwd - sn * r * 0.32 * side, y + sn * r * fwd + c * r * 0.32 * side, r * 0.3, r * 0.21, a, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();
  }
}

/** Default boots colour (the edge of the marker is the team's trim colour, black by default). */
const BOOTS = "#e9ecef";

function disc(ctx: CanvasRenderingContext2D, x: number, y: number, r: number, fill: string, stroke: string, edge = 0.18) {
  const lw = Math.max(1, r * edge);
  ctx.beginPath();
  ctx.arc(x, y, edge < 0.18 ? r - lw / 2 : r, 0, Math.PI * 2);
  ctx.fillStyle = fill;
  ctx.fill();
  ctx.lineWidth = lw;
  ctx.strokeStyle = stroke;
  ctx.stroke();
}

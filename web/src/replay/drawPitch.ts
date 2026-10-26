import type { Replay, Snapshot } from "./replay";

// Field of play (IFAB Law 1) in metres; world origin at the centre mark.
const L = 105;
const W = 68;
const HL = L / 2;
const HW = W / 2;
export const MARGIN = 5;
export const WORLD_W = L + MARGIN * 2;
export const WORLD_H = W + MARGIN * 2;

export interface View {
  scale: number; // px per metre
  ox: number;
  oy: number;
}

export function makeView(width: number, height: number): View {
  const scale = Math.min(width / WORLD_W, height / WORLD_H);
  return { scale, ox: width / 2, oy: height / 2 };
}

const px = (v: View, x: number) => v.ox + x * v.scale;
const py = (v: View, y: number) => v.oy - y * v.scale;

export function drawPitch(ctx: CanvasRenderingContext2D, v: View) {
  const s = v.scale;
  ctx.fillStyle = "#24662a";
  ctx.fillRect(0, 0, ctx.canvas.width, ctx.canvas.height);
  const stripes = 18;
  for (let i = 0; i < stripes; i++) {
    ctx.fillStyle = i % 2 === 0 ? "#2e7d32" : "#388e3c";
    ctx.fillRect(px(v, -HL + (i * L) / stripes), py(v, HW), (L / stripes) * s + 1, W * s);
  }
  ctx.strokeStyle = "rgba(240,240,240,0.95)";
  ctx.lineWidth = Math.max(1, 0.12 * s);
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
  ctx.fillStyle = "#f0f0f0";
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
    // Goal frame; the net is drawn by drawNet (it deforms).
    ctx.save();
    ctx.lineWidth = Math.max(2, 0.2 * s);
    line(gl, -3.72, gl, 3.72);
    ctx.restore();
  }
}

/**
 * A goal's net from above as a mesh of strings: the ball presses the netting out where it lies, a hard shot makes
 * it bulge and ripple (NetHit), and the frame flashes when the ball rattles the woodwork. Same model as Unity's GoalNetView.
 */
function drawNet(ctx: CanvasRenderingContext2D, v: View, side: number, ball: { x: number; y: number }, ripple: { x: number; y: number; amp: number } | null, flash: { x: number; y: number; age: number } | null) {
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
  ctx.strokeStyle = "rgba(235,235,235,0.6)";
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

export function drawEntities(ctx: CanvasRenderingContext2D, v: View, replay: Replay, snap: Snapshot, time: number) {
  const s = v.scale;
  const r = 0.9 * s;
  const teams = [replay.home, replay.away];
  // Officials
  snap.officials.forEach((o, i) => {
    disc(ctx, px(v, o.x), py(v, o.y), 0.78 * s, "#141414", "#f6d43a");
    ctx.fillStyle = "#f6d43a";
    ctx.font = `800 ${Math.max(8, 0.95 * s)}px system-ui, sans-serif`;
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    fillCentered(ctx, i === 0 ? "R" : "L", px(v, o.x), py(v, o.y));
    if (i > 0 && o.flag) {
      // Flag held up above the assistant, waving gently (offside flags stay up for several seconds).
      const wave = Math.sin(time * 6) * 0.15;
      const fx = px(v, o.x);
      const fy = py(v, o.y);
      ctx.strokeStyle = "#222";
      ctx.lineWidth = Math.max(1.5, 0.15 * s);
      ctx.beginPath();
      ctx.moveTo(fx + 0.3 * s, fy - 0.5 * s);
      ctx.lineTo(fx + 0.3 * s, fy - 2.6 * s);
      ctx.stroke();
      ctx.fillStyle = "#ffd21a";
      ctx.beginPath();
      ctx.moveTo(fx + 0.3 * s, fy - 2.6 * s);
      ctx.lineTo(fx + (1.9 + wave) * s, fy - (2.2 + wave) * s);
      ctx.lineTo(fx + 0.3 * s, fy - 1.6 * s);
      ctx.fill();
    }
  });
  // Corner flags
  for (const cx of [-HL, HL])
    for (const cy of [-HW, HW]) {
      const wave = Math.sin(time * 3.2 + cx + cy) * 0.15;
      ctx.fillStyle = "#ffc400";
      ctx.beginPath();
      ctx.moveTo(px(v, cx), py(v, cy) - 1.4 * s);
      ctx.lineTo(px(v, cx) + 0.9 * s, py(v, cy) - (1.15 + wave) * s);
      ctx.lineTo(px(v, cx), py(v, cy) - 0.9 * s);
      ctx.fill();
    }
  // Players: circle with border, number, two small legs showing direction; lifted while jumping.
  snap.players.forEach((p, i) => {
    if (p.sentOff && Math.abs(p.y) > 35.5) return;
    const team = teams[i < 11 ? 0 : 1];
    const info = team.players[i % 11];
    const x = px(v, p.x);
    const gy = py(v, p.y);
    // Leap of 0.2-0.75 m: the body rises on screen, the shadow stays on the grass and shrinks/fades with height.
    const h = Math.min(1, Math.max(0, p.jump));
    const lift = h * 2.2 * s;
    const y = gy - lift;
    const grow = 1 + h * 0.2;
    const shrink = 1 - h * 0.6;
    ctx.fillStyle = `rgba(0,0,0,${0.5 * shrink})`;
    ctx.beginPath();
    ctx.ellipse(x + 0.15 * s, gy + 0.2 * s, r * 1.05 * shrink, r * 0.95 * shrink, 0, 0, Math.PI * 2);
    ctx.fill();
    const fx = Math.cos(p.facing);
    const fy = -Math.sin(p.facing);
    const sx = -fy;
    const sy = fx;
    ctx.fillStyle = "#151515";
    for (const k of [-1, 1]) {
      ctx.beginPath();
      ctx.arc(x + fx * r * 0.62 + sx * r * 0.42 * k, y + fy * r * 0.62 + sy * r * 0.42 * k, r * 0.3, 0, Math.PI * 2);
      ctx.fill();
    }
    disc(ctx, x, y, r * grow, p.gk ? team.kit.gk : team.kit.primary, team.kit.secondary);
    ctx.fillStyle = p.gk ? team.kit.gkNumber : team.kit.number;
    ctx.font = `bold ${Math.max(8, r * grow * 1.05)}px system-ui, sans-serif`;
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    fillCentered(ctx, String(info.number), x, y);
    if (p.booked) {
      ctx.fillStyle = "#ffd600";
      ctx.fillRect(x + r * 0.55, y - r * 1.05, r * 0.32, r * 0.45);
    }
  });
  // Ball: grows with height, shadow stays below (smaller, lighter the higher it is).
  const b = snap.ball;
  const h = Math.max(0, b.z - 0.11);
  const bx = px(v, b.x);
  const by = py(v, b.y);
  ctx.fillStyle = `rgba(0,0,0,${Math.max(0.18, 0.6 - h * 0.04)})`;
  ctx.beginPath();
  ctx.arc(bx, by, 0.42 * s * Math.max(0.45, 1 - h * 0.05), 0, Math.PI * 2);
  ctx.fill();
  disc(ctx, bx, by - Math.min(20, h) * 0.55 * s, 0.36 * s * (1 + Math.min(20, h) * 0.07), "#ffffff", "#111111");
  // The nets go over a ball lying in the goal (roof netting seen from above).
  for (const side of [-1, 1]) {
    const fx = netEffects(replay, snap.t, side);
    drawNet(ctx, v, side, b, fx.ripple, fx.flash);
  }
  // The name of the player in possession, centred on a plate under him
  if (b.owner >= 0 && snap.phase === 0) {
    const p = snap.players[b.owner];
    const team = teams[b.owner < 11 ? 0 : 1];
    const info = team.players[b.owner % 11];
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

function disc(ctx: CanvasRenderingContext2D, x: number, y: number, r: number, fill: string, stroke: string) {
  ctx.beginPath();
  ctx.arc(x, y, r, 0, Math.PI * 2);
  ctx.fillStyle = fill;
  ctx.fill();
  ctx.lineWidth = Math.max(1, r * 0.18);
  ctx.strokeStyle = stroke;
  ctx.stroke();
}

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
    // Goal and net
    ctx.save();
    ctx.strokeStyle = "rgba(230,230,230,0.5)";
    ctx.lineWidth = 1;
    for (let y = -3.66; y <= 3.66; y += 0.5) line(gl, y, gl + 2 * side, y);
    for (let d = 0; d <= 2; d += 0.5) line(gl + d * side, -3.66, gl + d * side, 3.66);
    ctx.restore();
    ctx.save();
    ctx.lineWidth = Math.max(2, 0.2 * s);
    line(gl, -3.72, gl + 2 * side, -3.72);
    line(gl, 3.72, gl + 2 * side, 3.72);
    line(gl + 2 * side, -3.72, gl + 2 * side, 3.72);
    ctx.restore();
  }
}

export function drawEntities(ctx: CanvasRenderingContext2D, v: View, replay: Replay, snap: Snapshot, time: number) {
  const s = v.scale;
  const r = 0.9 * s;
  const teams = [replay.home, replay.away];
  // Officials
  snap.officials.forEach((o, i) => {
    disc(ctx, px(v, o.x), py(v, o.y), 0.75 * s, "#141414", "#f6d43a");
    if (i > 0 && o.flag) {
      ctx.fillStyle = "#f6c90e";
      ctx.beginPath();
      ctx.moveTo(px(v, o.x) + 0.2 * s, py(v, o.y) - 1.6 * s);
      ctx.lineTo(px(v, o.x) + 1.3 * s, py(v, o.y) - 1.2 * s);
      ctx.lineTo(px(v, o.x) + 0.2 * s, py(v, o.y) - 0.8 * s);
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
  // Players: circle with border, number, two small legs showing direction; bigger while jumping.
  snap.players.forEach((p, i) => {
    if (p.sentOff && Math.abs(p.y) > 35.5) return;
    const team = teams[i < 11 ? 0 : 1];
    const info = team.players[i % 11];
    const x = px(v, p.x);
    const y = py(v, p.y);
    const grow = 1 + Math.min(1.2, Math.max(0, p.jump)) * 0.45;
    // shadow stays on the grass
    ctx.fillStyle = `rgba(0,0,0,${0.3 * (1 - Math.min(1, p.jump) * 0.4)})`;
    ctx.beginPath();
    ctx.ellipse(x + 0.15 * s, y + 0.2 * s, r * 1.05, r * 0.95, 0, 0, Math.PI * 2);
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
    ctx.fillText(String(info.number), x, y + 0.5);
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
  ctx.fillStyle = `rgba(0,0,0,${Math.max(0.12, 0.45 - h * 0.035)})`;
  ctx.beginPath();
  ctx.arc(bx, by, 0.42 * s * Math.max(0.45, 1 - h * 0.05), 0, Math.PI * 2);
  ctx.fill();
  disc(ctx, bx, by, 0.36 * s * (1 + Math.min(20, h) * 0.09), "#ffffff", "#111111");
  // "#10 - Name" under the player in possession
  if (b.owner >= 0 && snap.phase === 0) {
    const p = snap.players[b.owner];
    const team = teams[b.owner < 11 ? 0 : 1];
    const info = team.players[b.owner % 11];
    const label = `#${info.number} - ${info.shortName}`;
    ctx.font = `600 ${Math.max(10, 0.95 * s)}px system-ui, sans-serif`;
    const w = ctx.measureText(label).width + 10;
    const lx = px(v, p.x);
    const ly = py(v, p.y) + r * 2.1;
    ctx.fillStyle = "rgba(0,0,0,0.6)";
    ctx.fillRect(lx - w / 2, ly - 0.6 * s, w, 1.2 * s);
    ctx.fillStyle = "#fff";
    ctx.fillText(label, lx, ly);
  }
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

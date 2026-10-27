// Weather, pitch and light for the canvas replay (same look as the demo page): grass patterns, the engine's mud and
// puddle map broken up with value noise, snow cover trodden by the players, rain and snow falling with the wind, cloud
// shadows, floodlights at night and shadows that follow the light.
import type { DevSettings } from "../settings/devSettings";
import type { Replay, Snapshot } from "./replay";
import type { View } from "./drawPitch";

const px = (v: View, x: number) => v.ox + x * v.scale;
const py = (v: View, y: number) => v.oy - y * v.scale;

export interface Wind {
  /** m/s */
  speed: number;
  /** direction it blows towards (world radians) */
  angle: number;
  /** the same on screen (y down) */
  screenAngle: number;
}

export function windOf(replay: Replay): Wind {
  const c = replay.conditions;
  const speed = c?.wind ?? 0;
  const angle = c?.windAngle ?? 0;
  return { speed, angle, screenAngle: Math.atan2(-Math.sin(angle), Math.cos(angle)) };
}

function hash2(i: number, j: number) {
  let h = Math.imul(i, 374761393) ^ Math.imul(j, 668265263);
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  return ((h ^ (h >>> 16)) >>> 0) / 4294967295;
}
function vnoise(x: number, y: number) {
  const i = Math.floor(x);
  const j = Math.floor(y);
  let u = x - i;
  let v = y - j;
  u = u * u * (3 - 2 * u);
  v = v * v * (3 - 2 * v);
  const a = hash2(i, j);
  const b = hash2(i + 1, j);
  const c = hash2(i, j + 1);
  const d = hash2(i + 1, j + 1);
  return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
}
/** Layered value noise for the rough edges of mud and snow ("grunge"). */
function grunge(x: number, y: number) {
  return 0.55 * vnoise(x * 0.7, y * 0.7) + 0.3 * vnoise(x * 2.3 + 7, y * 2.3 - 3) + 0.15 * vnoise(x * 6.1 - 11, y * 6.1 + 5);
}
function smooth(a: number, b: number, x: number) {
  const k = Math.max(0, Math.min(1, (x - a) / (b - a)));
  return k * k * (3 - 2 * k);
}

const SPX = 5; // texture pixels per metre
const SNOW = { x: -60, y: 48, w: 120, h: 92 };
const TOWERS: [number, number][] = [
  [-60, -42],
  [-60, 42],
  [60, -42],
  [60, 42],
];

interface Drop {
  x: number;
  y: number;
  l: number;
  v: number;
}
interface Flake {
  x: number;
  y: number;
  r: number;
  ph: number;
  v: number;
}
interface Ring {
  x: number;
  y: number;
  t: number;
  max: number;
  life: number;
}

export class PitchLook {
  private surface: HTMLCanvasElement | null = null;
  private snow: HTMLCanvasElement | null = null;
  private trodden: HTMLCanvasElement | null = null;
  private snowTmp: HTMLCanvasElement | null = null;
  private puddles: [number, number][] = [];
  private drops: Drop[] = [];
  private flakes: Flake[] = [];
  private rings: Ring[] = [];
  private dark: HTMLCanvasElement | null = null;
  private grain: { ctx: CanvasRenderingContext2D; pattern: CanvasPattern } | null = null;
  private key = "";
  wind: Wind = { speed: 0, angle: 0, screenAngle: 0 };

  /** Rebuilds the mud, puddle and snow textures when the match or the sky changes. */
  update(replay: Replay, s: DevSettings) {
    const key = `${replay.seed}|${JSON.stringify(replay.conditions ?? null)}|${s.sky}`;
    this.wind = windOf(replay);
    if (key === this.key) return;
    this.key = key;
    this.buildSurface(replay, s);
    this.buildSnow(s);
  }

  private buildSurface(replay: Replay, s: DevSettings) {
    this.surface = null;
    this.puddles = [];
    const sf = replay.surface;
    if (!sf || (!sf.mud && !sf.water)) return;
    const w = 108 * SPX;
    const h = 72 * SPX;
    const cvs = document.createElement("canvas");
    cvs.width = w;
    cvs.height = h;
    const g = cvs.getContext("2d");
    if (!g) return;
    const img = g.createImageData(w, h);
    const d = img.data;
    const wet = s.sky === "Rain";
    const bil = (arr: number[] | null | undefined, x: number, y: number) => {
      if (!arr) return 0;
      const fx = Math.max(0, Math.min(sf.columns - 1.001, (x - sf.minX) / sf.cell));
      const fy = Math.max(0, Math.min(sf.rows - 1.001, (y - sf.minY) / sf.cell));
      const i = Math.floor(fx);
      const j = Math.floor(fy);
      const u = fx - i;
      const v = fy - j;
      const k = j * sf.columns + i;
      const top = arr[k] + (arr[k + 1] - arr[k]) * u;
      const bot = arr[k + sf.columns] + (arr[k + sf.columns + 1] - arr[k + sf.columns]) * u;
      return (top + (bot - top) * v) / 255;
    };
    for (let qy = 0; qy < h; qy++)
      for (let qx = 0; qx < w; qx++) {
        const x = -54 + (qx + 0.5) / SPX;
        const y = 36 - (qy + 0.5) / SPX;
        const gr = grunge(x, y);
        const o = (qy * w + qx) * 4;
        const mm = Math.max(0, Math.min(1, (bil(sf.mud, x, y) - 0.08 + (gr - 0.5) * 0.55) * 1.5));
        let r = 0;
        let gg = 0;
        let b = 0;
        let a = 0;
        if (mm > 0) {
          // thin, worn grass (khaki) giving way to bare mud; darker when it rains
          const k = smooth(0.3, 0.85, mm);
          const dk = wet ? 0.8 : 1;
          r = (150 - 54 * k) * dk;
          gg = (136 - 68 * k) * dk;
          b = (84 - 42 * k) * dk;
          a = smooth(0.04, 0.45, mm) * 0.93;
          if (vnoise(x * 9, y * 9) > 0.78) {
            r *= 0.82;
            gg *= 0.82;
            b *= 0.82;
          }
        }
        const wa = bil(sf.water, x, y);
        const ww = Math.max(0, Math.min(1, (wa - 0.1 + (vnoise(x * 0.9, y * 0.9) - 0.5) * 0.18) * 1.8));
        if (ww > 0) {
          // standing water: dark where deep, reflecting the grey sky towards the edge, with a light rim
          const deep = smooth(0.25, 0.9, ww);
          const sky = 0.5 + 0.5 * vnoise(x * 0.35 + 40, y * 0.35);
          let wr = 112 - 46 * deep + 14 * sky;
          let wg = 128 - 44 * deep + 14 * sky;
          let wb = 136 - 38 * deep + 16 * sky;
          let wA = smooth(0, 0.22, ww) * 0.9;
          if (ww < 0.12) {
            wr = 150;
            wg = 166;
            wb = 170;
            wA *= 0.7;
          }
          r = r * (1 - wA) + wr * wA;
          gg = gg * (1 - wA) + wg * wA;
          b = b * (1 - wA) + wb * wA;
          a = a + wA * (1 - a);
          if (wa > 0.55 && this.puddles.length < 260 && hash2(qx, qy) < 0.02) this.puddles.push([x, y]);
        }
        d[o] = r;
        d[o + 1] = gg;
        d[o + 2] = b;
        d[o + 3] = a * 255;
      }
    g.putImageData(img, 0, 0);
    this.surface = cvs;
  }

  private buildSnow(s: DevSettings) {
    this.snow = this.trodden = this.snowTmp = null;
    if (s.sky !== "Snow") return;
    const w = SNOW.w * SPX;
    const h = SNOW.h * SPX;
    const make = () => {
      const c = document.createElement("canvas");
      c.width = w;
      c.height = h;
      return c;
    };
    const snow = make();
    const g = snow.getContext("2d");
    if (!g) return;
    const img = g.createImageData(w, h);
    const d = img.data;
    for (let qy = 0; qy < h; qy++)
      for (let qx = 0; qx < w; qx++) {
        const x = SNOW.x + (qx + 0.5) / SPX;
        const y = SNOW.y - (qy + 0.5) / SPX;
        const n = grunge(x * 0.6, y * 0.6);
        const fine = vnoise(x * 4, y * 4);
        const o = (qy * w + qx) * 4;
        const shade = 238 + 12 * n + 5 * fine;
        d[o] = shade - 5;
        d[o + 1] = shade - 1;
        d[o + 2] = Math.min(255, shade + 7);
        d[o + 3] = 255 * (0.94 + 0.06 * smooth(0.25, 0.65, n) - (fine > 0.85 ? 0.05 : 0));
      }
    g.putImageData(img, 0, 0);
    this.snow = snow;
    this.trodden = make();
    this.snowTmp = make();
  }

  /** After a seek the trodden snow starts fresh. */
  clearTracks() {
    this.trodden?.getContext("2d")?.clearRect(0, 0, this.trodden.width, this.trodden.height);
  }

  /** Where players run and the ball rolls the snow is trodden down and the grass shows through. */
  tread(snap: Snapshot, dt: number) {
    const g = this.trodden?.getContext("2d");
    if (!g || dt <= 0) return;
    g.fillStyle = `rgba(0,0,0,${Math.min(0.12, dt * 0.35)})`;
    for (const p of snap.players) {
      if (p.sentOff) continue;
      g.beginPath();
      g.arc((p.x - SNOW.x) * SPX, (SNOW.y - p.y) * SPX, 0.45 * SPX, 0, Math.PI * 2);
      g.fill();
    }
    if (snap.ball.z < 0.3) {
      g.fillStyle = `rgba(0,0,0,${Math.min(0.3, dt * 1.2)})`;
      g.beginPath();
      g.arc((snap.ball.x - SNOW.x) * SPX, (SNOW.y - snap.ball.y) * SPX, 0.16 * SPX, 0, Math.PI * 2);
      g.fill();
    }
  }

  private grainPattern(ctx: CanvasRenderingContext2D) {
    if (this.grain && this.grain.ctx === ctx) return this.grain.pattern;
    const n = document.createElement("canvas");
    n.width = n.height = 192;
    const g = n.getContext("2d");
    if (!g) return null;
    const img = g.createImageData(192, 192);
    for (let i = 0; i < img.data.length; i += 4) {
      const r = Math.random();
      img.data[i] = img.data[i + 1] = img.data[i + 2] = r > 0.5 ? 255 : 0;
      img.data[i + 3] = Math.floor(Math.abs(r - 0.5) * 2 * 26);
    }
    g.putImageData(img, 0, 0);
    const pattern = ctx.createPattern(n, "repeat");
    if (pattern) this.grain = { ctx, pattern };
    return pattern;
  }

  /** Mown turf: bands along the length, bands across, or rings around the centre spot. */
  grass(ctx: CanvasRenderingContext2D, v: View, s: DevSettings, top: number, bottom: number) {
    const k = v.scale;
    const wet = s.sky === "Rain";
    const light = wet ? "#4f8530" : "#5b8733";
    const dark = wet ? "#46762b" : "#53792d";
    const w = ctx.canvas.width;
    const h = ctx.canvas.height;
    ctx.fillStyle = dark;
    ctx.fillRect(0, 0, w, h);
    ctx.fillStyle = light;
    if (s.grass === "across") {
      const bw = 105 / 18;
      for (let i = -2; i < 20; i += 2) ctx.fillRect(px(v, -52.5 + i * bw), 0, bw * k + 1, h);
    } else if (s.grass === "circles") {
      for (let r = 80; r > 0; r -= 5) {
        ctx.fillStyle = (r / 5) % 2 ? light : dark;
        ctx.beginPath();
        ctx.arc(px(v, 0), py(v, 0), r * k, 0, Math.PI * 2);
        ctx.fill();
      }
    } else {
      const bands = 12;
      const bw = (top - bottom) / bands;
      for (let i = 1; i < bands; i += 2) ctx.fillRect(0, py(v, top - i * bw), w, bw * k + 1);
    }
    const pattern = this.grainPattern(ctx);
    if (pattern) {
      ctx.fillStyle = pattern;
      ctx.fillRect(0, 0, w, h);
    }
    if (wet) {
      ctx.fillStyle = "rgba(170,210,230,0.05)";
      ctx.fillRect(0, 0, w, h);
    }
  }

  /** Mud, puddles and snow, between the grass and the lines. */
  ground(ctx: CanvasRenderingContext2D, v: View) {
    const k = v.scale;
    if (this.surface) ctx.drawImage(this.surface, px(v, -54), py(v, 36), 108 * k, 72 * k);
    if (this.snow && this.trodden && this.snowTmp) {
      const g = this.snowTmp.getContext("2d");
      if (!g) return;
      g.globalCompositeOperation = "copy";
      g.drawImage(this.snow, 0, 0);
      g.globalCompositeOperation = "destination-out";
      g.drawImage(this.trodden, 0, 0);
      g.globalCompositeOperation = "source-over";
      ctx.drawImage(this.snowTmp, px(v, SNOW.x), py(v, SNOW.y), SNOW.w * k, SNOW.h * k);
    }
  }

  /** Line paint: white, or blue in the snow. */
  linePaint(s: DevSettings) {
    return s.sky === "Snow" ? "rgba(40,98,205,0.95)" : "rgba(255,255,255,0.9)";
  }

  shadowOffset(s: DevSettings) {
    if (s.night) return { dx: 0.1, dy: 0.12, a: 0.7 };
    if (s.sky === "Sunny") return { dx: 0.6, dy: 0.45, a: 1.05 };
    return { dx: 0.1, dy: 0.14, a: 0.62 };
  }

  /** One long sharp shadow from the sun, a soft faint one under a grey sky, four faint ones from the floodlights. */
  shadow(ctx: CanvasRenderingContext2D, v: View, s: DevSettings, wx: number, wy: number, x: number, gy: number, rx: number, ry: number, alpha: number) {
    const k = v.scale;
    if (s.night) {
      for (const [lx, ly] of TOWERS) {
        const dx = wx - lx;
        const dy = wy - ly;
        const l = Math.hypot(dx, dy) || 1;
        const ux = dx / l;
        const uy = -dy / l;
        ctx.fillStyle = `rgba(0,0,0,${alpha * 0.32})`;
        ctx.beginPath();
        ctx.ellipse(x + ux * 0.55 * k, gy + uy * 0.55 * k, rx * 1.25, ry * 0.8, Math.atan2(uy, ux), 0, Math.PI * 2);
        ctx.fill();
      }
      return;
    }
    const o = this.shadowOffset(s);
    ctx.fillStyle = `rgba(0,0,0,${Math.min(0.8, alpha * o.a)})`;
    ctx.beginPath();
    if (s.sky === "Sunny") ctx.ellipse(x + o.dx * k, gy + o.dy * k, rx * 1.3, ry * 0.88, Math.atan2(o.dy, o.dx), 0, Math.PI * 2);
    else ctx.ellipse(x + o.dx * k, gy + o.dy * k, rx * 1.02, ry * 1.02, 0, 0, Math.PI * 2);
    ctx.fill();
  }

  /** Rain streaks and splashes, ripples in the puddles, snowflakes - all blown by the wind. */
  fx(ctx: CanvasRenderingContext2D, v: View, s: DevSettings, now: number, frameDt: number) {
    const dt = Math.max(0, frameDt);
    const w = ctx.canvas.width;
    const h = ctx.canvas.height;
    const k = v.scale;
    const dpr = window.devicePixelRatio || 1;
    const ws = (m: number) => ({ x: Math.cos(this.wind.screenAngle) * this.wind.speed * m, y: Math.sin(this.wind.screenAngle) * this.wind.speed * m });
    if (s.sky === "Rain") {
      const want = Math.round((w * h) / 2600);
      while (this.drops.length < want) this.drops.push({ x: Math.random() * w, y: Math.random() * h, l: 0.5 + Math.random(), v: 0.8 + Math.random() * 0.5 });
      this.drops.length = want;
      const wind = ws(0.06);
      let dx = wind.x + 0.12;
      let dy = 1 + wind.y * 0.3;
      const dl = Math.hypot(dx, dy);
      dx /= dl;
      dy /= dl;
      ctx.strokeStyle = s.night ? "rgba(225,235,255,0.42)" : "rgba(205,218,232,0.34)";
      ctx.lineWidth = Math.max(1, 0.7 * dpr);
      ctx.beginPath();
      for (const d of this.drops) {
        const step = d.v * 900 * dpr * dt;
        d.x += dx * step;
        d.y += dy * step;
        if (d.y > h) {
          d.y -= h;
          d.x = Math.random() * w;
        }
        if (d.x > w) d.x -= w;
        if (d.x < 0) d.x += w;
        ctx.moveTo(d.x, d.y);
        ctx.lineTo(d.x - dx * d.l * 1.1 * k, d.y - dy * d.l * 1.1 * k);
      }
      ctx.stroke();
      for (let i = 0; i < 9; i++) this.rings.push({ x: Math.random() * w, y: Math.random() * h, t: 0, max: 0.35, life: 0.3 });
      for (let i = 0; i < 3 && this.puddles.length; i++) {
        const [x, y] = this.puddles[Math.floor(Math.random() * this.puddles.length)];
        this.rings.push({ x: px(v, x + Math.random() - 0.5), y: py(v, y + Math.random() - 0.5), t: 0, max: 0.7, life: 0.9 });
      }
    }
    if (this.rings.length) {
      ctx.lineWidth = Math.max(0.7, 0.5 * dpr);
      this.rings = this.rings.filter((r) => {
        r.t += dt;
        if (r.t > r.life) return false;
        const q = r.t / r.life;
        ctx.strokeStyle = `rgba(220,232,240,${0.45 * (1 - q)})`;
        ctx.beginPath();
        ctx.ellipse(r.x, r.y, r.max * q * k, r.max * q * k * 0.8, 0, 0, Math.PI * 2);
        ctx.stroke();
        return true;
      });
      if (this.rings.length > 400) this.rings.splice(0, this.rings.length - 400);
    }
    if (s.sky === "Snow") {
      const want = Math.round((w * h) / 3800);
      while (this.flakes.length < want)
        this.flakes.push({ x: Math.random() * w, y: Math.random() * h, r: (0.6 + Math.random() * 1.6) * dpr, ph: Math.random() * 6.3, v: 0.5 + Math.random() * 0.8 });
      this.flakes.length = want;
      const wind = ws(9 * dpr);
      ctx.fillStyle = "rgba(255,255,255,0.88)";
      ctx.beginPath();
      for (const f of this.flakes) {
        f.x += (wind.x * f.v + Math.sin(now * 1.3 + f.ph) * 14 * dpr) * dt;
        f.y += (wind.y * f.v + 26 * dpr * f.v) * dt;
        if (f.y > h) {
          f.y -= h;
          f.x = Math.random() * w;
        }
        if (f.y < 0) f.y += h;
        if (f.x > w) f.x -= w;
        if (f.x < 0) f.x += w;
        ctx.moveTo(f.x + f.r, f.y);
        ctx.arc(f.x, f.y, f.r, 0, Math.PI * 2);
      }
      ctx.fill();
    }
  }

  private cloudShadows(ctx: CanvasRenderingContext2D, v: View, now: number, strength: number) {
    const k = v.scale;
    const drift = Math.max(1.5, this.wind.speed) * now * 0.6;
    const ang = this.wind.speed > 0.5 ? this.wind.angle : 0.4;
    const ux = Math.cos(ang);
    const uy = Math.sin(ang);
    for (let i = 0; i < 6; i++) {
      const ox = ((((hash2(i, 7) * 260 + drift * ux) % 260) + 260) % 260) - 130;
      const oy = ((((hash2(i, 9) * 160 + drift * uy) % 160) + 160) % 160) - 80;
      const rad = (22 + hash2(i, 3) * 26) * k;
      const g = ctx.createRadialGradient(px(v, ox), py(v, oy), 0, px(v, ox), py(v, oy), rad);
      g.addColorStop(0, `rgba(10,18,28,${strength})`);
      g.addColorStop(0.6, `rgba(10,18,28,${strength * 0.6})`);
      g.addColorStop(1, "rgba(10,18,28,0)");
      ctx.fillStyle = g;
      ctx.fillRect(px(v, ox) - rad, py(v, oy) - rad, rad * 2, rad * 2);
    }
  }

  private buildDark(ctx: CanvasRenderingContext2D, v: View) {
    const c = document.createElement("canvas");
    c.width = ctx.canvas.width;
    c.height = ctx.canvas.height;
    const g = c.getContext("2d");
    if (!g) return null;
    const k = v.scale;
    g.fillStyle = "rgba(2,7,22,0.8)";
    g.fillRect(0, 0, c.width, c.height);
    g.globalCompositeOperation = "destination-out";
    for (const [lx, ly] of TOWERS) {
      const cx = px(v, lx * 0.42);
      const cy = py(v, ly * 0.42);
      const gr = g.createRadialGradient(cx, cy, 0, cx, cy, 62 * k);
      gr.addColorStop(0, "rgba(0,0,0,0.22)");
      gr.addColorStop(0.6, "rgba(0,0,0,0.13)");
      gr.addColorStop(1, "rgba(0,0,0,0)");
      g.fillStyle = gr;
      g.fillRect(0, 0, c.width, c.height);
    }
    g.globalCompositeOperation = "source-over";
    for (const [lx, ly] of TOWERS) {
      const cx = px(v, lx);
      const cy = py(v, ly);
      const gr = g.createRadialGradient(cx, cy, 0, cx, cy, 7 * k);
      gr.addColorStop(0, "rgba(255,250,225,0.9)");
      gr.addColorStop(0.25, "rgba(255,245,200,0.35)");
      gr.addColorStop(1, "rgba(255,245,200,0)");
      g.fillStyle = gr;
      g.fillRect(cx - 7 * k, cy - 7 * k, 14 * k, 14 * k);
    }
    return c;
  }

  /** Sun, grey sky or floodlights over the finished picture. */
  light(ctx: CanvasRenderingContext2D, v: View, s: DevSettings, now: number) {
    const w = ctx.canvas.width;
    const h = ctx.canvas.height;
    if (s.night) {
      if (!this.dark || this.dark.width !== w || this.dark.height !== h) this.dark = this.buildDark(ctx, v);
      if (this.dark) ctx.drawImage(this.dark, 0, 0);
      return;
    }
    if (s.sky === "Sunny") {
      this.cloudShadows(ctx, v, now, 0.07);
      ctx.fillStyle = "rgba(255,214,140,0.05)";
      ctx.fillRect(0, 0, w, h);
      return;
    }
    this.cloudShadows(ctx, v, now, s.sky === "Cloudy" ? 0.1 : 0.08);
    ctx.fillStyle = s.sky === "Rain" ? "rgba(18,28,44,0.28)" : s.sky === "Snow" ? "rgba(40,52,74,0.12)" : "rgba(22,30,42,0.16)";
    ctx.fillRect(0, 0, w, h);
  }
}

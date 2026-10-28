// Weather, pitch and light for the canvas replay (same look as the demo page): grass patterns, the engine's mud and
// puddle map broken up with value noise, snow cover trodden by the players, rain and snow falling with the wind, cloud
// shadows, floodlights at night and shadows that follow the light.
import type { DevSettings } from "../settings/devSettings";
import type { Replay, Snapshot } from "./replay";
import type { View } from "./drawPitch";
import { SNOW_AREA, SURFACE_AREA, snowCanvas, surfaceCanvas } from "./textures";

const px = (v: View, x: number) => v.ox + x * v.scale;
const py = (v: View, y: number) => v.oy - y * v.scale;

export interface Wind {
  /** m/s */
  speed: number;
  /** direction it blows towards (world radians) */
  angle: number;
  /** the same on screen (y down) */
  screenAngle: number;
  /** What the flags and the chip show: the wind, or a light breeze when none is set (too light to move the ball). */
  breeze: number;
}

export function windOf(replay: Replay): Wind {
  const c = replay.conditions;
  const speed = c?.wind ?? 0;
  const angle = speed > 0.5 ? (c?.windAngle ?? 0) : (((replay.seed * 97) % 360) * Math.PI) / 180;
  const breeze = speed > 0.5 ? speed : 1.4 + (replay.seed % 9) * 0.35;
  return { speed, angle, screenAngle: Math.atan2(-Math.sin(angle), Math.cos(angle)), breeze };
}

function hash2(i: number, j: number) {
  let h = Math.imul(i, 374761393) ^ Math.imul(j, 668265263);
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  return ((h ^ (h >>> 16)) >>> 0) / 4294967295;
}
const SPX = 6; // snow texture pixels per metre
const SNOW = SNOW_AREA;
/** The main stand's roof line (where its shade ends), with the steps of its trusses. */
const ROOF: [number, number][] = [
  [-70, 5.5],
  [-47, 8],
  [-46, 9.6],
  [-7, 13.8],
  [-6, 15.5],
  [33, 19.8],
  [34, 21.5],
  [70, 25.5],
];
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
  private puddles: [number, number][] = [];
  private drops: Drop[] = [];
  private flakes: Flake[] = [];
  private rings: Ring[] = [];
  private dark: HTMLCanvasElement | null = null;
  private grain: { ctx: CanvasRenderingContext2D; pattern: CanvasPattern } | null = null;
  private key = "";
  wind: Wind = { speed: 0, angle: 0, screenAngle: 0, breeze: 0 };
  private cloud = document.createElement("canvas");

  /** Rebuilds the mud, puddle and snow textures when the match or the sky changes. */
  update(replay: Replay, s: DevSettings) {
    const key = `${replay.seed}|${JSON.stringify(replay.conditions ?? null)}|${s.sky}|${s.night}`;
    this.wind = windOf(replay);
    if (key === this.key) return;
    this.key = key;
    this.buildSurface(replay, s);
    this.buildSnow(s);
  }

  /** A new texture seed every time the page renders them (the patches stay where the engine has them). */
  private texSeed = Math.floor(Math.random() * 1e9);

  private buildSurface(replay: Replay, s: DevSettings) {
    this.surface = null;
    this.puddles = [];
    const sf = replay.surface;
    if (!sf || (!sf.mud && !sf.water)) return;
    const res = surfaceCanvas(sf, s.sky === "Rain", s.night, this.texSeed, 8);
    this.surface = res.canvas;
    this.puddles = res.puddles;
  }

  private buildSnow(s: DevSettings) {
    this.snow = this.trodden = null;
    if (s.sky !== "Snow") return;
    const snow = snowCanvas(this.texSeed, SPX);
    const make = () => {
      const c = document.createElement("canvas");
      c.width = snow.width;
      c.height = snow.height;
      return c;
    };
    this.snow = snow;
    // the snow as it lies now: a copy of the fresh layer that the players tread holes into
    this.trodden = make();
    this.trodden.getContext("2d")?.drawImage(snow, 0, 0);
  }

  /** After a seek the trodden snow starts fresh. */
  clearTracks() {
    const g = this.trodden?.getContext("2d");
    if (!g || !this.snow) return;
    g.globalCompositeOperation = "copy";
    g.drawImage(this.snow, 0, 0);
    g.globalCompositeOperation = "source-over";
    this.snowChanged = true;
  }

  /** The snow layer changed since it was last composed into the background. */
  snowChanged = true;

  /** Where players run and the ball rolls the snow is trodden down and the grass shows through. */
  tread(snap: Snapshot, dt: number) {
    const g = this.trodden?.getContext("2d");
    if (!g || dt <= 0) return;
    g.globalCompositeOperation = "destination-out";
    this.snowChanged = true;
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

  /** Mud and puddles (between the grass and the lines). */
  ground(ctx: CanvasRenderingContext2D, v: View) {
    const k = v.scale;
    const A = SURFACE_AREA;
    if (this.surface) ctx.drawImage(this.surface, px(v, A.x), py(v, A.y), A.w * k, A.h * k);
  }

  /** The snow as trodden so far (null without snow). */
  snowLayer(ctx: CanvasRenderingContext2D, v: View) {
    if (!this.trodden) return false;
    const k = v.scale;
    ctx.drawImage(this.trodden, px(v, SNOW.x), py(v, SNOW.y), SNOW.w * k, SNOW.h * k);
    return true;
  }

  /** Identifies the ground textures (for caching the background). */
  get groundKey() {
    return this.key;
  }

  /**
   * The light that does not move, baked into the background image instead of laid over the screen every frame: the
   * floodlit dark of a night match, the dull light of a grey sky, the warm sun and the stands' shade.
   */
  bakeLight(ctx: CanvasRenderingContext2D, v: View, s: DevSettings) {
    const w = ctx.canvas.width;
    const h = ctx.canvas.height;
    if (s.night) {
      if (!this.dark || this.dark.width !== w || this.dark.height !== h) this.dark = this.buildDark(ctx, v);
      if (this.dark) ctx.drawImage(this.dark, 0, 0);
      return;
    }
    if (s.sky === "Sunny") {
      ctx.drawImage(this.buildStand(ctx, v, s.stadium), 0, 0);
      return;
    }
    ctx.fillStyle = s.sky === "Rain" ? "rgba(18,28,44,0.28)" : s.sky === "Snow" ? "rgba(40,52,74,0.12)" : "rgba(22,30,42,0.16)";
    ctx.fillRect(0, 0, w, h);
  }

  /** Is this point in the stands' shade on a sunny day (same shapes as buildStand)? */
  inShade(s: DevSettings, x: number, y: number) {
    if (s.night || s.sky !== "Sunny" || s.stadium === "open") return false;
    if (s.stadium === "bowl") {
      const ex = (x - 14) / 66;
      const ey = (y + 9) / 47;
      return ex * ex + ey * ey > 1;
    }
    for (let i = 0; i < ROOF.length - 1; i++)
      if (x >= ROOF[i][0] && x <= ROOF[i + 1][0]) return y > ROOF[i][1] + ((ROOF[i + 1][1] - ROOF[i][1]) * (x - ROOF[i][0])) / (ROOF[i + 1][0] - ROOF[i][0]);
    return false;
  }

  /** Lower quality on a weak device: no cloud shadows, less rain and snow. */
  quality = 0;

  /** Line paint: white, or yellow / orange in the snow so the lines read on the white. */
  linePaint(s: DevSettings) {
    return s.sky === "Snow" ? (s.snowLines === "orange" ? "#f26a1b" : "#f5c518") : "rgba(255,255,255,0.9)";
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
    const dpr = Math.min(2, window.devicePixelRatio || 1);
    const ws = (m: number) => ({ x: Math.cos(this.wind.screenAngle) * this.wind.speed * m, y: Math.sin(this.wind.screenAngle) * this.wind.speed * m });
    if (s.sky === "Rain") {
      const want = Math.round(((w * h) / 2600) * (this.quality ? 0.55 : 1));
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
      const want = Math.round(((w * h) / 3800) * (this.quality ? 0.55 : 1));
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

  /** Cloud shadows drifting with the wind, drawn into an image an eighth of the size (soft, so no detail is lost). */
  private cloudShadows(ctx: CanvasRenderingContext2D, v: View, now: number, strength: number) {
    const f = 8;
    const cw = Math.ceil(ctx.canvas.width / f);
    const ch = Math.ceil(ctx.canvas.height / f);
    if (this.cloud.width !== cw || this.cloud.height !== ch) {
      this.cloud.width = cw;
      this.cloud.height = ch;
    }
    const g2 = this.cloud.getContext("2d");
    if (!g2) return;
    g2.clearRect(0, 0, cw, ch);
    const k = v.scale / f;
    const drift = Math.max(1.5, this.wind.breeze) * now * 0.6;
    const ux = Math.cos(this.wind.angle);
    const uy = Math.sin(this.wind.angle);
    for (let i = 0; i < 6; i++) {
      const ox = ((((hash2(i, 7) * 260 + drift * ux) % 260) + 260) % 260) - 130;
      const oy = ((((hash2(i, 9) * 160 + drift * uy) % 160) + 160) % 160) - 80;
      const cx = px(v, ox) / f;
      const cy = py(v, oy) / f;
      const rad = (22 + hash2(i, 3) * 26) * k;
      const g = g2.createRadialGradient(cx, cy, 0, cx, cy, rad);
      g.addColorStop(0, `rgba(10,18,28,${strength})`);
      g.addColorStop(0.6, `rgba(10,18,28,${strength * 0.6})`);
      g.addColorStop(1, "rgba(10,18,28,0)");
      g2.fillStyle = g;
      g2.fillRect(cx - rad, cy - rad, rad * 2, rad * 2);
    }
    ctx.drawImage(this.cloud, 0, 0, ctx.canvas.width, ctx.canvas.height);
  }

  /**
   * The low sun (from the top left, like the players' shadows) is blocked by the stands: a main stand along the far
   * touchline throws a slanting band of shade, a bowl's curved roof shades the top and the left. With the sun's warm
   * tint, drawn once per size into an image.
   */
  private buildStand(ctx: CanvasRenderingContext2D, v: View, stadium: DevSettings["stadium"]) {
    const c = document.createElement("canvas");
    c.width = ctx.canvas.width;
    c.height = ctx.canvas.height;
    const g = c.getContext("2d");
    if (!g) return c;
    g.fillStyle = "rgba(255,214,140,0.05)";
    g.fillRect(0, 0, c.width, c.height);
    if (stadium === "open") return c;
    g.fillStyle = "rgba(10,24,40,0.34)";
    g.filter = `blur(${Math.max(1, 0.35 * v.scale)}px)`;
    g.beginPath();
    if (stadium === "bowl") {
      g.rect(-50, -50, c.width + 100, c.height + 100); // everything outside the roof's curved opening
      g.ellipse(px(v, 14), py(v, -9), 66 * v.scale, 47 * v.scale, 0, 0, Math.PI * 2);
      g.fill("evenodd");
    } else {
      // the main stand's roof line, with the steps of its trusses
      const pts = [[-70, 60], [70, 60], ...ROOF.slice().reverse()];
      pts.forEach(([x, y], i) => (i ? g.lineTo(px(v, x), py(v, y)) : g.moveTo(px(v, x), py(v, y))));
      g.closePath();
      g.fill();
    }
    return c;
  }

  private buildDark(ctx: CanvasRenderingContext2D, v: View) {
    const c = document.createElement("canvas");
    c.width = ctx.canvas.width;
    c.height = ctx.canvas.height;
    const g = c.getContext("2d");
    if (!g) return null;
    const k = v.scale;
    g.fillStyle = "rgba(2,7,22,0.62)";
    g.fillRect(0, 0, c.width, c.height);
    g.globalCompositeOperation = "destination-out";
    for (const [lx, ly] of TOWERS) {
      const cx = px(v, lx * 0.4);
      const cy = py(v, ly * 0.4);
      const gr = g.createRadialGradient(cx, cy, 0, cx, cy, 72 * k);
      gr.addColorStop(0, "rgba(0,0,0,0.55)");
      gr.addColorStop(0.55, "rgba(0,0,0,0.45)");
      gr.addColorStop(0.85, "rgba(0,0,0,0.14)");
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

  /** What moves in the light over the finished picture: the cloud shadows (the rest is baked, see bakeLight). */
  light(ctx: CanvasRenderingContext2D, v: View, s: DevSettings, now: number) {
    if (s.night || this.quality >= 1) return;
    this.cloudShadows(ctx, v, now, s.sky === "Sunny" ? 0.06 : s.sky === "Cloudy" ? 0.1 : 0.08);
  }
}

// Ground textures for the canvas views (React and the standalone demo page share this module): worn grass, bare mud
// and standing water painted from the engine's 1 m mud / water map, and a powdery layer of snow.
//
// Layers: seeded Perlin gradient noise, its fractal sum ("cloud" noise) and a domain-warped cloud for organic, seamless
// edges; fine Perlin and per-pixel grain for grass tufts poking through the dirt and the water. Each render picks a new
// texture seed, so the detail differs every time while the patches stay exactly where the engine has them.

export interface SurfaceMap {
  minX: number;
  minY: number;
  cell: number;
  columns: number;
  rows: number;
  mud: number[] | null;
  water: number[] | null;
}

/** World rectangle the surface texture covers (metres): the pitch and its run-offs. */
export const SURFACE_AREA = { x: -54, y: 36, w: 108, h: 72 };
/** The snow covers the whole view. */
export const SNOW_AREA = { x: -60, y: 48, w: 120, h: 92 };

/** Seeded 2D Perlin noise, its fractal sum and a domain-warped "cloud". */
export class Noise {
  private p = new Uint8Array(512);

  constructor(seed: number) {
    let s = seed >>> 0 || 1;
    const rnd = () => {
      s = (Math.imul(s, 1664525) + 1013904223) >>> 0;
      return s / 4294967296;
    };
    const base = Array.from({ length: 256 }, (_, i) => i);
    for (let i = 255; i > 0; i--) {
      const j = Math.floor(rnd() * (i + 1));
      [base[i], base[j]] = [base[j], base[i]];
    }
    for (let i = 0; i < 512; i++) this.p[i] = base[i & 255];
  }

  /** Perlin gradient noise, about -1..1. */
  perlin(x: number, y: number): number {
    const xi = Math.floor(x);
    const yi = Math.floor(y);
    const xf = x - xi;
    const yf = y - yi;
    const X = xi & 255;
    const Y = yi & 255;
    const u = xf * xf * xf * (xf * (xf * 6 - 15) + 10);
    const v = yf * yf * yf * (yf * (yf * 6 - 15) + 10);
    const p = this.p;
    const g = (h: number, dx: number, dy: number) => {
      switch (h & 7) {
        case 0: return dx + dy;
        case 1: return -dx + dy;
        case 2: return dx - dy;
        case 3: return -dx - dy;
        case 4: return dx;
        case 5: return -dx;
        case 6: return dy;
        default: return -dy;
      }
    };
    const aa = p[p[X] + Y];
    const ab = p[p[X] + Y + 1];
    const ba = p[p[X + 1] + Y];
    const bb = p[p[X + 1] + Y + 1];
    const x1 = g(aa, xf, yf) + (g(ba, xf - 1, yf) - g(aa, xf, yf)) * u;
    const x2 = g(ab, xf, yf - 1) + (g(bb, xf - 1, yf - 1) - g(ab, xf, yf - 1)) * u;
    return x1 + (x2 - x1) * v;
  }

  /** Fractal sum ("cloud" noise), 0..1. */
  fbm(x: number, y: number, octaves = 5): number {
    let sum = 0;
    let amp = 0.5;
    let norm = 0;
    for (let o = 0; o < octaves; o++) {
      sum += this.perlin(x, y) * amp;
      norm += amp;
      amp *= 0.5;
      x = x * 2.03 + 17.1;
      y = y * 2.03 - 9.7;
    }
    return Math.max(0, Math.min(1, 0.5 + (sum / norm) * 0.9));
  }

  /** Domain-warped cloud: soft, swirling shapes without grid-like blobs. */
  warped(x: number, y: number, octaves = 5): number {
    const qx = this.fbm(x + 3.1, y - 1.7, 3) - 0.5;
    const qy = this.fbm(x - 5.2, y + 8.3, 3) - 0.5;
    return this.fbm(x + 2.2 * qx, y + 2.2 * qy, octaves);
  }

  /** Per-pixel grain, 0..1. */
  static hash(i: number, j: number): number {
    let h = Math.imul(i, 374761393) ^ Math.imul(j, 668265263);
    h = Math.imul(h ^ (h >>> 13), 1274126177);
    return ((h ^ (h >>> 16)) >>> 0) / 4294967295;
  }
}

function smooth(a: number, b: number, x: number) {
  const k = Math.max(0, Math.min(1, (x - a) / (b - a)));
  return k * k * (3 - 2 * k);
}
function mix(a: number, b: number, k: number) {
  return a + (b - a) * k;
}

/**
 * Worn grass, bare mud and standing water. Mud: sandy, yellowish dirt in several tones (dry and light, damp and darker,
 * scattered clods), broken up by grass tufts and fading into thin, yellowed grass around it. Water: a shallow sheet
 * that mirrors the sky - bright and silvery, with soft cloud reflections, sparkles and grass blades poking through - in
 * a ring of dark, soaked grass. Returns the canvas (SURFACE_AREA at `ppm` pixels per metre) and points inside deep
 * puddles (for raindrop rings).
 */
export function surfaceCanvas(sf: SurfaceMap, wet: boolean, night: boolean, seed: number, ppm = 8): { canvas: HTMLCanvasElement; puddles: [number, number][] } {
  const A = SURFACE_AREA;
  const w = Math.round(A.w * ppm);
  const h = Math.round(A.h * ppm);
  const canvas = document.createElement("canvas");
  canvas.width = w;
  canvas.height = h;
  const puddles: [number, number][] = [];
  const g = canvas.getContext("2d");
  if (!g) return { canvas, puddles };
  const n = new Noise(seed);
  const img = g.createImageData(w, h);
  const d = img.data;
  const bil = (arr: number[] | null, x: number, y: number) => {
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
  for (let py = 0; py < h; py++)
    for (let px = 0; px < w; px++) {
      const x = A.x + (px + 0.5) / ppm;
      const y = A.y - (py + 0.5) / ppm;
      const m = bil(sf.mud, x, y);
      const wa = bil(sf.water, x, y);
      const o = (py * w + px) * 4;
      if (m < 0.01 && wa < 0.01) continue;
      const grain = Noise.hash(px, py);
      const cloud = n.warped(x * 0.22, y * 0.22);
      const tuftNoise = n.perlin(x * 2.6, y * 2.6) * 0.5 + 0.5;
      let r = 0;
      let gg = 0;
      let b = 0;
      let a = 0;
      if (m > 0.01) {
        // where it is worn: thin yellowed grass, then dirt with tufts of grass, then bare earth
        const breakup = n.perlin(x * 0.75 + 9, y * 0.75 - 4) * 0.5 + 0.5;
        const bare = m * 1.3 + (cloud - 0.5) * 0.8 + (breakup - 0.5) * 0.35 + (tuftNoise - 0.5) * 0.18;
        const dirt = smooth(0.32, 0.9, bare);
        const halo = smooth(0.05, 0.4, bare) * (1 - dirt);
        const tuft = smooth(0.58, 0.8, tuftNoise * 0.55 + grain * 0.45) * (1 - smooth(0.75, 1.0, bare));
        const tone = n.fbm(x * 0.9 + 40, y * 0.9 - 12, 4);
        const damp = n.fbm(x * 0.35 - 70, y * 0.35 + 30, 3);
        // sandy palette: light dry sand, ochre, and a damp darker tone
        let dr = mix(196, 166, tone);
        let dg = mix(172, 140, tone);
        let db = mix(118, 90, tone);
        const dk = smooth(0.45, 0.85, damp) * (wet ? 0.6 : 0.3);
        dr = mix(dr, 128, dk);
        dg = mix(dg, 104, dk);
        db = mix(db, 66, dk);
        if (wet) {
          dr *= 0.9;
          dg *= 0.88;
          db *= 0.84;
        }
        // clods, stones and stud marks
        if (grain > 0.975) {
          dr *= 0.88;
          dg *= 0.87;
          db *= 0.86;
        } else if (grain < 0.04) {
          dr = Math.min(255, dr * 1.15);
          dg = Math.min(255, dg * 1.15);
          db = Math.min(255, db * 1.12);
        }
        const dirtA = dirt * (1 - 0.7 * tuft) * (0.9 + 0.1 * grain);
        // yellowed, thin grass around the bare earth
        const haloA = halo * 0.42 * (0.7 + 0.3 * grain);
        a = dirtA + haloA * (1 - dirtA);
        if (a > 0) {
          r = (dr * dirtA + 150 * haloA * (1 - dirtA)) / a;
          gg = (dg * dirtA + 152 * haloA * (1 - dirtA)) / a;
          b = (db * dirtA + 78 * haloA * (1 - dirtA)) / a;
        }
      }
      if (wa > 0.01) {
        const wv = wa + (cloud - 0.5) * 0.34 + (tuftNoise - 0.5) * 0.1;
        const pool = smooth(0.28, 0.5, wv);
        const soaked = smooth(0.1, 0.3, wv) * (1 - pool);
        if (soaked > 0) {
          // dark, soaked grass around the water
          const sA = soaked * 0.5;
          r = mix(r, 34, sA / Math.max(a + sA * (1 - a), 1e-3));
          gg = mix(gg, 62, sA / Math.max(a + sA * (1 - a), 1e-3));
          b = mix(b, 30, sA / Math.max(a + sA * (1 - a), 1e-3));
          a = a + sA * (1 - a);
        }
        if (pool > 0) {
          // a mirror of the grey sky: deep water reflects silver-blue with drifting cloud reflections and a sheen of
          // light; shallow water is a thin film the grass shows through; sparkles and grass blades break the surface
          const sky = n.warped(x * 0.1 + 90, y * 0.1 - 40, 4);
          const sheen = smooth(0.55, 0.95, n.fbm(x * 0.06 - 20, y * 0.25 + 5, 3)); // long soft highlight streaks
          const ripple = n.perlin(x * 1.7 + 11, y * 5.5) * 0.5 + 0.5;
          const depth = smooth(0.42, 0.85, wv);
          let wr = night ? mix(36, 96, sky) : mix(118, 196, sky);
          let wg = night ? mix(46, 106, sky) : mix(134, 206, sky);
          let wb = night ? mix(64, 130, sky) : mix(150, 218, sky);
          const lift = sheen * (night ? 60 : 48) + (ripple - 0.5) * 16;
          wr += lift;
          wg += lift;
          wb += lift;
          if (grain > 0.992) wr = wg = wb = 255; // sparkle
          const blade = grain > 0.86 && depth < 0.6 ? 0.6 * (1 - depth) : 0;
          const wA = pool * (0.42 + 0.5 * depth) * (1 - blade);
          const rim = (1 - smooth(0.3, 0.42, wv)) * pool; // a thin bright meniscus at the edge
          const rr = Math.min(255, wr + rim * 40);
          const rg = Math.min(255, wg + rim * 40);
          const rb = Math.min(255, wb + rim * 36);
          const tot = wA + a * (1 - wA);
          r = (rr * wA + r * a * (1 - wA)) / Math.max(tot, 1e-3);
          gg = (rg * wA + gg * a * (1 - wA)) / Math.max(tot, 1e-3);
          b = (rb * wA + b * a * (1 - wA)) / Math.max(tot, 1e-3);
          a = tot;
          if (wa > 0.55 && puddles.length < 260 && grain < 0.003) puddles.push([x, y]);
        }
      }
      const fade = smooth(0, 1.5, Math.min(x - A.x, A.x + A.w - x, A.y - y, y - (A.y - A.h)));
      d[o] = Math.min(255, r);
      d[o + 1] = Math.min(255, gg);
      d[o + 2] = Math.min(255, b);
      d[o + 3] = Math.max(0, Math.min(1, a)) * fade * 255;
    }
  g.putImageData(img, 0, 0);
  return { canvas, puddles };
}

/**
 * Powdery snow over the whole ground: white frost thick in places and thin in others, the green showing through in
 * specks and streaks (as on a real pitch after a snowfall). The lines stay white and crisp on top.
 */
export function snowCanvas(seed: number, ppm = 6): HTMLCanvasElement {
  const A = SNOW_AREA;
  const w = Math.round(A.w * ppm);
  const h = Math.round(A.h * ppm);
  const canvas = document.createElement("canvas");
  canvas.width = w;
  canvas.height = h;
  const g = canvas.getContext("2d");
  if (!g) return canvas;
  const n = new Noise(seed ^ 0x9e3779b9);
  const img = g.createImageData(w, h);
  const d = img.data;
  for (let py = 0; py < h; py++)
    for (let px = 0; px < w; px++) {
      const x = A.x + (px + 0.5) / ppm;
      const y = A.y - (py + 0.5) / ppm;
      // the pitch has been cleared to a light dusting; beyond the touchlines the snow lies thick, piled into a soft
      // bank just outside the lines where it was pushed off
      const out = Math.max(Math.abs(x) - 52.5, Math.abs(y) - 34);
      const offPitch = smooth(0.6, 2.6, out);
      const bank = Math.exp(-(((out - 2.2) / 1.1) ** 2));
      const drift = n.warped(x * 0.07, y * 0.07, 4);
      const cover = (0.3 + 0.22 * drift) * (1 - offPitch) + (0.93 + 0.07 * drift) * offPitch + 0.1 * bank;
      const streak = n.perlin(x * 0.35, y * 2.2) * 0.5 + 0.5; // drifts along the mowing direction
      const fine = n.perlin(x * 2.4, y * 2.4) * 0.5 + 0.5;
      const grain = Noise.hash(px, py);
      const v = fine * 0.6 + streak * 0.25 + grain * 0.15;
      const a = Math.min(1, smooth(1.05 - cover - 0.16, 1.05 - cover + 0.16, v) * (0.82 + 0.18 * offPitch));
      const o = (py * w + px) * 4;
      const shade = 228 + 18 * fine + 8 * grain - 10 * bank * (1 - fine); // a little shadow in the piled bank
      d[o] = shade - 6;
      d[o + 1] = shade - 2;
      d[o + 2] = Math.min(255, shade + 8);
      d[o + 3] = Math.min(1, a) * 255;
    }
  g.putImageData(img, 0, 0);
  return canvas;
}

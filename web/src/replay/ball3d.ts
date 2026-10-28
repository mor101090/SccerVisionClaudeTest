// A 3D-looking football for the 2D views: a truncated icosahedron (12 black pentagons, 20 white hexagons) rendered per
// pixel with simple lighting, and rotated as the ball rolls. Same model as Unity's BallView and the demo page.

const PHI = (1 + Math.sqrt(5)) / 2;
type V3 = [number, number, number];

const norm = (a: V3): V3 => {
  const l = Math.hypot(a[0], a[1], a[2]);
  return [a[0] / l, a[1] / l, a[2] / l];
};

const ICO: V3[] = [
  [0, 1, PHI], [0, -1, PHI], [0, 1, -PHI], [0, -1, -PHI],
  [1, PHI, 0], [-1, PHI, 0], [1, -PHI, 0], [-1, -PHI, 0],
  [PHI, 0, 1], [-PHI, 0, 1], [PHI, 0, -1], [-PHI, 0, -1],
];
/** Pentagon face normals = icosahedron vertices; hexagon face normals = icosahedron face centres. */
const PENT: V3[] = ICO.map(norm);
const HEX: V3[] = [];
{
  const d = (p: number, q: number) => Math.hypot(ICO[p][0] - ICO[q][0], ICO[p][1] - ICO[q][1], ICO[p][2] - ICO[q][2]);
  for (let i = 0; i < 12; i++)
    for (let j = i + 1; j < 12; j++)
      for (let k = j + 1; k < 12; k++)
        if (Math.abs(d(i, j) - 2) < 1e-6 && Math.abs(d(j, k) - 2) < 1e-6 && Math.abs(d(i, k) - 2) < 1e-6)
          HEX.push(norm([ICO[i][0] + ICO[j][0] + ICO[k][0], ICO[i][1] + ICO[j][1] + ICO[k][1], ICO[i][2] + ICO[j][2] + ICO[k][2]]));
}
// Face plane distances of a truncated icosahedron with edge 1: the face containing a direction is argmax(n.N / d).
const RP = 2.32744;
const RH = 2.26728;
const LIGHT = norm([-0.45, 0.55, 0.7]);
const HALF = norm([LIGHT[0], LIGHT[1], LIGHT[2] + 1]);

export class Ball3D {
  /** Body-to-world rotation (row major). */
  private rot = [1, 0, 0, 0, 1, 0, 0, 0, 1];
  private axis: V3 = [0, 1, 0];
  private w = 0;
  private prev: { x: number; y: number } | null = null;
  private canvas: HTMLCanvasElement | null = null;

  reset() {
    this.prev = null;
    this.w = 0;
  }

  /** Advances the spin from the ball's movement on the ground (metres) over dt seconds of playback. */
  spin(x: number, y: number, height: number, dt: number, held = false) {
    if (held) {
      // Held by the keeper or carried overhead for a throw-in: the ball moves with the hands and does not roll.
      this.w = 0;
      this.prev = { x, y };
      return;
    }
    if (this.prev && dt > 0) {
      const dx = x - this.prev.x;
      const dy = y - this.prev.y;
      const dist = Math.hypot(dx, dy);
      if (dist > 0.0005 && dist < 3) {
        // Rolling without slipping on the ground (axis = up x direction of travel); in the air a gentler spin. The visual
        // rate is capped so the spin stays readable.
        this.axis = [-dy / dist, dx / dist, 0];
        this.w = (Math.min(dist / 0.3, 0.5) / dt) * (height < 0.35 ? 1 : 0.35);
      } else this.w = 0;
      if (this.w > 0) this.rotate(this.axis, Math.min(0.5, this.w * dt));
    }
    this.prev = { x, y };
  }

  private rotate(ax: V3, ang: number) {
    const c = Math.cos(ang);
    const s = Math.sin(ang);
    const t = 1 - c;
    const [x, y, z] = ax;
    const r = [t * x * x + c, t * x * y - s * z, t * x * z + s * y, t * x * y + s * z, t * y * y + c, t * y * z - s * x, t * x * z - s * y, t * y * z + s * x, t * z * z + c];
    const m = this.rot;
    const o = new Array<number>(9);
    for (let i = 0; i < 3; i++) for (let j = 0; j < 3; j++) o[i * 3 + j] = r[i * 3] * m[j] + r[i * 3 + 1] * m[3 + j] + r[i * 3 + 2] * m[6 + j];
    this.rot = o;
  }

  /** Draws the ball centred at (cx, cy) with the given radius in canvas pixels. */
  /** High-visibility ball (orange with navy pentagons), used in the snow. */
  winter = false;

  draw(ctx: CanvasRenderingContext2D, cx: number, cy: number, rad: number) {
    const size = Math.ceil(rad * 2) + 2;
    const c = size / 2;
    if (!this.canvas) this.canvas = document.createElement("canvas");
    if (this.canvas.width !== size) {
      this.canvas.width = size;
      this.canvas.height = size;
    }
    const bctx = this.canvas.getContext("2d");
    if (!bctx) return;
    const img = bctx.createImageData(size, size);
    const d = img.data;
    const m = this.rot;
    for (let py = 0; py < size; py++)
      for (let px = 0; px < size; px++) {
        const nx = (px + 0.5 - c) / rad;
        const ny = -(py + 0.5 - c) / rad;
        const r2 = nx * nx + ny * ny;
        if (r2 > 1) continue;
        const nz = Math.sqrt(1 - r2);
        const bx = m[0] * nx + m[3] * ny + m[6] * nz;
        const by = m[1] * nx + m[4] * ny + m[7] * nz;
        const bz = m[2] * nx + m[5] * ny + m[8] * nz;
        let best = -9;
        let second = -9;
        let pent = false;
        for (const n of PENT) {
          const sc = (n[0] * bx + n[1] * by + n[2] * bz) / RP;
          if (sc > best) {
            second = best;
            best = sc;
            pent = true;
          } else if (sc > second) second = sc;
        }
        for (const n of HEX) {
          const sc = (n[0] * bx + n[1] * by + n[2] * bz) / RH;
          if (sc > best) {
            second = best;
            best = sc;
            pent = false;
          } else if (sc > second) second = sc;
        }
        let col = this.winter ? (pent ? [22, 30, 80] : [255, 104, 20]) : pent ? [28, 28, 28] : [246, 246, 246];
        if (best - second < 0.006) col = this.winter ? (pent ? [50, 56, 100] : [176, 64, 10]) : pent ? [60, 60, 60] : [150, 150, 150]; // stitched seams
        const diff = 0.38 + 0.62 * Math.max(0, nx * LIGHT[0] + ny * LIGHT[1] + nz * LIGHT[2]);
        const spec = Math.pow(Math.max(0, nx * HALF[0] + ny * HALF[1] + nz * HALF[2]), 36) * (pent ? 120 : 90);
        const o = (py * size + px) * 4;
        d[o] = Math.min(255, col[0] * diff + spec);
        d[o + 1] = Math.min(255, col[1] * diff + spec);
        d[o + 2] = Math.min(255, col[2] * diff + spec);
        d[o + 3] = 255 * Math.min(1, (1 - Math.sqrt(r2)) * rad + 0.5);
      }
    bctx.putImageData(img, 0, 0);
    ctx.drawImage(this.canvas, cx - c, cy - c);
    ctx.beginPath();
    ctx.arc(cx, cy, rad, 0, Math.PI * 2);
    ctx.lineWidth = Math.max(0.6, rad * 0.08);
    ctx.strokeStyle = "rgba(0,0,0,0.45)";
    ctx.stroke();
  }
}

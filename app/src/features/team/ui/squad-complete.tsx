"use client";

import { useEffect, useRef } from "react";

/*
 * "Squad complete" celebration, played by <Generating> once all 22 players are in, before it moves on.
 * Stage-clear timeline (ms from mount): 0 flash + shake + medallion pop + shockwaves + confetti, 200 title slam,
 * 500–800 stars, 550 second confetti burst, 750 title shine. Keyframes live in globals.css (`--animate-sv-*`); every
 * CSS animation is `motion-safe:` and the confetti checks reduced motion itself, so reduced motion sees the end frame.
 */

// Brand primitives from globals.css (celebration-specific, so Mor primitives rather than a semantic role).
const CONFETTI_VARS = [
  "--mor-light-green",
  "--mor-green",
  "--mor-yellow",
  "--mor-orange",
  "--mor-sky-blue",
  "--mor-button-text",
];

const STAR_PATH =
  "M12 2.5l2.94 5.96 6.56.95-4.75 4.63 1.12 6.54L12 17.5l-5.87 3.08 1.12-6.54L2.5 9.41l6.56-.95z";

const delay = (ms: number) => ({ animationDelay: `${ms}ms` });

/** Medallion that replaces the spinner. Fills the spinner's 64px slot; everything else overflows it, so no layout shift. */
export function CompleteBadge() {
  const ref = useRef<HTMLDivElement>(null);
  return (
    <div ref={ref} className="relative size-16" aria-hidden>
      {/* light rays */}
      <div className="pointer-events-none absolute top-1/2 left-1/2 size-65 -translate-x-1/2 -translate-y-1/2">
        <div className="size-full motion-safe:animate-sv-fade-in" style={delay(100)}>
          <div
            className="size-full rounded-full motion-safe:animate-sv-rays"
            style={{
              background:
                "repeating-conic-gradient(from 0deg, color-mix(in srgb, var(--mor-yellow) 30%, transparent) 0deg 9deg, transparent 9deg 30deg)",
              maskImage: "radial-gradient(circle, black 18%, transparent 68%)",
            }}
          />
        </div>
      </div>
      {/* glow */}
      <div className="absolute -inset-6 rounded-full bg-goal opacity-50 blur-2xl motion-safe:animate-sv-glow" />
      {/* shockwaves */}
      {[0, 160].map((d) => (
        <div
          key={d}
          className="absolute inset-0 rounded-full border-4 border-card opacity-0 motion-safe:animate-sv-shockwave"
          style={delay(d)}
        />
      ))}
      {/* medallion */}
      <div className="absolute -inset-4 motion-safe:animate-sv-medallion">
        <div className="relative flex size-full items-center justify-center overflow-hidden rounded-full bg-radial from-goal to-accent shadow-[0_6px_0_var(--mor-dark-purple)] ring-4 ring-card">
          <div className="absolute inset-x-2 top-1 h-1/2 rounded-full bg-white/25" />
          <svg
            width="48"
            height="48"
            viewBox="0 0 14 14"
            fill="none"
            className="relative drop-shadow-[0_2px_0_var(--mor-dark-purple)]"
          >
            <path
              d="M2.3335 7.29167L5.25016 10.2083L11.6668 3.79167"
              stroke="white"
              strokeWidth="2.2"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </svg>
        </div>
      </div>
      <Confetti originRef={ref} />
    </div>
  );
}

/** Gold slam-in title with a shine sweep. */
export function CompleteTitle({ children }: { children: string }) {
  return (
    <h1
      className="relative overflow-hidden px-2 text-[30px] leading-[1.15] font-black tracking-wide uppercase italic motion-safe:animate-sv-slam"
      style={delay(200)}
    >
      <span className="bg-linear-to-b from-white via-card to-chance bg-clip-text text-transparent drop-shadow-[0_3px_0_var(--mor-dark-purple)]">
        {children}
      </span>
      <span
        aria-hidden
        className="pointer-events-none absolute inset-y-0 left-0 w-1/3 translate-x-[-120%] bg-linear-to-r from-transparent via-white/50 to-transparent mix-blend-overlay motion-safe:animate-sv-shine"
        style={delay(750)}
      />
    </h1>
  );
}

/** Three "level clear" stars, the middle one raised and bigger. Same height as the progress bar block it replaces. */
export function CompleteStars() {
  const stars = [
    { size: 28, lift: 0, at: 500 },
    { size: 38, lift: -6, at: 650 },
    { size: 28, lift: 0, at: 800 },
  ];
  return (
    <div className="flex h-9 items-end justify-center gap-2" aria-hidden>
      {stars.map((s) => (
        <svg
          key={s.at}
          width={s.size}
          height={s.size}
          viewBox="0 0 24 24"
          className="drop-shadow-[0_2px_0_var(--mor-dark-purple)] motion-safe:animate-sv-star"
          style={{ ...delay(s.at), translate: `0 ${s.lift}px` }}
        >
          <path
            d={STAR_PATH}
            fill="var(--mor-yellow)"
            stroke="var(--mor-orange)"
            strokeWidth="1.5"
            strokeLinejoin="round"
          />
        </svg>
      ))}
    </div>
  );
}

type Piece = {
  x: number;
  y: number;
  vx: number;
  vy: number;
  rot: number;
  vr: number;
  size: number;
  color: string;
  star: boolean;
  life: number;
  ttl: number;
};

function burst(count: number, x: number, y: number, colors: string[], power: number): Piece[] {
  return Array.from({ length: count }, () => {
    const angle = Math.random() * Math.PI * 2;
    const speed = power * (0.45 + Math.random() * 0.55);
    return {
      x,
      y,
      vx: Math.cos(angle) * speed,
      vy: Math.sin(angle) * speed - power * 0.35, // bias upward, like a firework
      rot: Math.random() * Math.PI,
      vr: (Math.random() - 0.5) * 0.4,
      size: 5 + Math.random() * 6,
      color: colors[Math.floor(Math.random() * colors.length)]!,
      star: Math.random() < 0.3,
      life: 0,
      ttl: 1300 + Math.random() * 900,
    };
  });
}

function drawStar(ctx: CanvasRenderingContext2D, r: number) {
  ctx.beginPath();
  for (let i = 0; i < 10; i++) {
    const rad = i % 2 ? r * 0.45 : r;
    const a = (i * Math.PI) / 5 - Math.PI / 2;
    ctx.lineTo(Math.cos(a) * rad, Math.sin(a) * rad);
  }
  ctx.closePath();
  ctx.fill();
}

/** Full-viewport canvas burst from the badge: confetti + stars with gravity, drag and spin. Removes itself when done. */
function Confetti({ originRef }: { originRef: React.RefObject<HTMLElement | null> }) {
  const canvasRef = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    const origin = originRef.current;
    const ctx = canvas?.getContext("2d");
    if (!canvas || !origin || !ctx) return;
    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;

    const css = getComputedStyle(document.documentElement);
    const colors = CONFETTI_VARS.map((v) => css.getPropertyValue(v).trim()).filter(Boolean);
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    const resize = () => {
      canvas.width = window.innerWidth * dpr;
      canvas.height = window.innerHeight * dpr;
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    };
    resize();
    window.addEventListener("resize", resize);

    const center = () => {
      const r = origin.getBoundingClientRect();
      return { x: r.left + r.width / 2, y: r.top + r.height / 2 };
    };
    const c = center();
    let pieces = burst(90, c.x, c.y, colors, 13);
    const second = setTimeout(() => {
      const p = center();
      pieces = pieces.concat(burst(45, p.x, p.y, colors, 10));
    }, 550);

    let raf = 0;
    let last = performance.now();
    const frame = (now: number) => {
      const dt = Math.min(now - last, 50);
      last = now;
      const k = dt / 16.67; // per-60fps-frame units
      ctx.clearRect(0, 0, window.innerWidth, window.innerHeight);
      pieces = pieces.filter((p) => (p.life += dt) < p.ttl);
      for (const p of pieces) {
        p.vx *= Math.pow(0.975, k);
        p.vy = p.vy * Math.pow(0.975, k) + 0.32 * k;
        p.x += p.vx * k;
        p.y += p.vy * k;
        p.rot += p.vr * k;
        const fade = 1 - Math.max(0, (p.life - p.ttl * 0.65) / (p.ttl * 0.35));
        ctx.save();
        ctx.globalAlpha = fade;
        ctx.translate(p.x, p.y);
        ctx.rotate(p.rot);
        ctx.fillStyle = p.color;
        if (p.star) drawStar(ctx, p.size * 0.8);
        else ctx.fillRect(-p.size / 2, -p.size / 4, p.size, p.size / 2);
        ctx.restore();
      }
      if (pieces.length) raf = requestAnimationFrame(frame);
    };
    raf = requestAnimationFrame(frame);

    return () => {
      cancelAnimationFrame(raf);
      clearTimeout(second);
      window.removeEventListener("resize", resize);
    };
  }, [originRef]);

  return (
    <canvas
      ref={canvasRef}
      className="pointer-events-none fixed inset-0 z-50 size-full"
      aria-hidden
    />
  );
}

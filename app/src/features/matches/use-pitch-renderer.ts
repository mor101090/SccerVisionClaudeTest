"use client";

import { useEffect, useRef, type RefObject } from "react";

import {
  drawEntities,
  drawPitch,
  look,
  makeView,
  WORLD_H,
  WORLD_W,
} from "@/engine/replay/drawPitch";
import { sample, type Replay, type Snapshot } from "@/engine/replay/replay";
import { DEFAULT_SETTINGS, type DevSettings } from "@/engine/replay/settings";

export interface ReplaySource {
  replay: Replay;
  /** Seconds of replay available to play (grows while the engine is still simulating). */
  duration: number;
}

export interface Tick {
  time: number;
  snap: Snapshot;
  replay: Replay;
  ended: boolean;
}

/**
 * Plays a replay on a canvas: advances time by real time × speed (bounded by what is available)
 * and draws the pitch and players every animation frame. `onTick` fires ~10×/s for the HUD.
 */
export function usePitchRenderer(opts: {
  canvas: RefObject<HTMLCanvasElement | null>;
  wrap: RefObject<HTMLDivElement | null>;
  source: RefObject<ReplaySource | null>;
  speed: RefObject<number>;
  time: RefObject<number>;
  settings?: DevSettings;
  onTick?: (tick: Tick) => void;
}) {
  const onTick = useRef(opts.onTick);
  useEffect(() => {
    onTick.current = opts.onTick;
  });

  const { canvas, wrap, source, speed, time } = opts;
  const settings = opts.settings ?? DEFAULT_SETTINGS;

  useEffect(() => {
    let raf = 0;
    let last = performance.now();
    let uiTick = 0;
    const loop = (now: number) => {
      const dt = Math.max(0, Math.min(0.25, (now - last) / 1000));
      last = now;
      const src = source.current;
      const c = canvas.current;
      const w = wrap.current;
      if (src && src.replay.frames.length > 1 && c && w) {
        const { replay, duration } = src;
        const prev = time.current;
        const t = Math.min(duration, prev + dt * speed.current);
        time.current = t;
        const dpr = Math.min(2, window.devicePixelRatio || 1);
        const cssW = w.clientWidth;
        const cssH = (cssW * WORLD_H) / WORLD_W;
        if (c.width !== Math.round(cssW * dpr)) {
          c.width = Math.round(cssW * dpr);
          c.height = Math.round(cssH * dpr);
          c.style.height = `${cssH}px`;
        }
        const ctx = c.getContext("2d");
        if (ctx) {
          const view = makeView(c.width, c.height);
          const snap = sample(replay, t);
          look.update(replay, settings);
          look.tread(snap, t - prev);
          drawPitch(ctx, view, settings);
          drawEntities(ctx, view, replay, snap, now / 1000, t - prev, settings);
          look.fx(ctx, view, settings, now / 1000, dt);
          look.light(ctx, view, settings, now / 1000);
          if (now - uiTick > 100) {
            uiTick = now;
            onTick.current?.({ time: t, snap, replay, ended: t >= duration });
          }
        }
      }
      raf = requestAnimationFrame(loop);
    };
    raf = requestAnimationFrame(loop);
    return () => cancelAnimationFrame(raf);
  }, [canvas, wrap, source, speed, time, settings]);
}

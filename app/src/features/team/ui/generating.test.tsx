import { act, cleanup, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const refresh = vi.fn();
vi.mock("next/navigation", () => ({ useRouter: () => ({ refresh }) }));

let resolveAction: (v: { error: string } | null) => void = () => {};
vi.mock("../actions", () => ({
  generateSquadAction: () =>
    new Promise((r) => {
      resolveAction = r;
    }),
}));

import { CELEBRATE_MS, Generating, HOLD_MS } from "./generating";

/** Advance in small slices so React re-renders (and schedules the next tick) between timers. */
async function step(ms: number) {
  for (let t = 0; t < ms; t += 10) {
    await act(async () => void (await vi.advanceTimersByTimeAsync(10)));
  }
}

describe("Generating", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    refresh.mockReset();
    window.matchMedia = vi.fn().mockReturnValue({ matches: false }) as never;
    HTMLCanvasElement.prototype.getContext = vi.fn().mockReturnValue(null);
  });
  afterEach(() => {
    cleanup();
    vi.useRealTimers();
  });

  it("waits at 21 until the squad is stored, celebrates at 22/22, then refreshes", async () => {
    render(<Generating pools="English" />);
    await step(10_000);
    expect(screen.getByText("21 of 22 players")).toBeTruthy();
    expect(screen.queryByRole("heading", { name: /squad complete/i })).toBeNull();

    await act(async () => resolveAction(null));
    await step(50); // last tick to 22/22 (40 ms once stored)
    expect(screen.getByText("22 of 22 players")).toBeTruthy();
    expect(refresh).not.toHaveBeenCalled();

    await step(HOLD_MS);
    expect(screen.getByRole("heading", { name: /squad complete/i })).toBeTruthy();
    expect(refresh).not.toHaveBeenCalled();

    await step(CELEBRATE_MS - 100);
    expect(refresh).not.toHaveBeenCalled();
    await step(100);
    expect(refresh).toHaveBeenCalledTimes(1);
  });

  it("never refreshes or celebrates when generation fails", async () => {
    render(<Generating pools="English" />);
    await act(async () => resolveAction({ error: "Generator is down." }));
    await step(10_000);
    expect(screen.getByText("Generator is down.")).toBeTruthy();
    expect(screen.queryByRole("heading", { name: /squad complete/i })).toBeNull();
    expect(refresh).not.toHaveBeenCalled();
  });
});

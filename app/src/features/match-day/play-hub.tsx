"use client";

import Link from "next/link";
import { useRef, useState } from "react";

import { AppIcon, type IconName } from "@/components/app-shell/app-icon";
import { Card, Chip, PageHeader } from "@/components/app-shell/ui";
import type { TeamStrength } from "@/engine/protocol";

import { btn, Segmented } from "./ui";
import type { MatchSetup, Phase } from "./use-match";

export type HubClub = {
  name: string;
  text: string;
  plan: string | null;
  kits: { home: string; away: string; keeper: string } | null;
  /** "4-4-2 · Balanced" from the saved plan. */
  tactics: string;
};
export type HubResult = {
  id: string;
  homeName: string;
  awayName: string;
  homeScore: number;
  awayScore: number;
  when: string;
  /** W / D / L from the manager's side, null when the club played neither side. */
  result: "W" | "D" | "L" | null;
};
export type HubData = {
  signedIn: boolean;
  club: HubClub | null;
  /** Signed in with a club still being created. */
  clubUnfinished: boolean;
  recent: HubResult[];
  savedCount: number;
};

const STRENGTHS = ["Weak", "Medium", "Strong"] as const;
const LENGTHS = ["Watch live", "Instant result"] as const;

function OptionCard({
  icon,
  title,
  text,
  chips,
  action,
  highlight = false,
}: {
  icon: IconName;
  title: string;
  text: string;
  chips: React.ReactNode;
  action: React.ReactNode;
  highlight?: boolean;
}) {
  return (
    <section
      className={`flex flex-col gap-3 rounded-2xl border bg-panel-2 p-5 ${highlight ? "border-accent" : "border-border"}`}
    >
      <span
        className={`flex size-10 items-center justify-center rounded-lg ${highlight ? "bg-accent text-on-accent" : "bg-selected/25 text-link"}`}
      >
        <AppIcon name={icon} size={20} />
      </span>
      <h2 className="text-lg font-extrabold text-text">{title}</h2>
      <p className="text-[13px] leading-5 text-muted">{text}</p>
      <div className="flex flex-wrap gap-2">{chips}</div>
      <div className="mt-auto pt-1">{action}</div>
    </section>
  );
}

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="grid grid-cols-1 items-center gap-2 border-b border-border/50 py-3 last:border-0 sm:grid-cols-[140px_1fr]">
      <span className="text-[13px] text-muted">{label}</span>
      <div className="flex min-w-0 flex-wrap items-center gap-2 text-[13px] text-text">
        {children}
      </div>
    </div>
  );
}

const RESULT_CLASS = {
  W: "bg-goal text-accent-ink",
  D: "bg-border text-text",
  L: "bg-foul text-on-accent",
};

export function PlayHub({
  data,
  phase,
  error,
  onStart,
}: {
  data: HubData;
  phase: Phase;
  error: string | null;
  onStart: (setup: MatchSetup) => void;
}) {
  const [strength, setStrength] = useState<(typeof STRENGTHS)[number]>("Medium");
  const [length, setLength] = useState<(typeof LENGTHS)[number]>("Watch live");
  const setupRef = useRef<HTMLDivElement>(null);
  const busy = phase === "loading" || phase === "preparing";
  const { club } = data;

  const start = () =>
    onStart({
      ...(club ? { homeTeam: club.text, plan: club.plan } : {}),
      awayStrength: strength.toLowerCase() as TeamStrength,
      instant: length === "Instant result",
    });

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        crumb="Play"
        title="Play a match"
        sub="Friendlies: a full match against the computer, a friend online, or a saved replay."
      />

      <div className="grid gap-4 md:grid-cols-3">
        <OptionCard
          highlight
          icon="play"
          title="Play the computer"
          text={
            club
              ? "Your club and saved tactics in a live match. Change the mentality, tactics and players while it plays."
              : "A live match with a generated side. Change the mentality, tactics and players while it plays."
          }
          chips={
            <>
              <Chip tone="goal">Live</Chip>
              <Chip>Saved to My Games</Chip>
            </>
          }
          action={
            <button
              type="button"
              className={btn("primary", "sm")}
              onClick={() => {
                setupRef.current?.scrollIntoView({ behavior: "smooth", block: "center" });
                setupRef.current
                  ?.querySelector<HTMLElement>("[role=radio]")
                  ?.focus({ preventScroll: true });
              }}
            >
              Set up the match
            </button>
          }
        />
        <OptionCard
          icon="users"
          title="Play a friend online"
          text="Create a game and share the six-digit code or link, or join with theirs. Same engine on both screens."
          chips={
            <>
              <Chip tone="info">Real time</Chip>
              <Chip>Code or link</Chip>
            </>
          }
          action={
            <Link href="/lobby" className={btn("secondary", "sm")}>
              Create or join
            </Link>
          }
        />
        <OptionCard
          icon="refresh"
          title="Watch a replay"
          text="Every saved match replays exactly: same seed, same goals. Jump to any moment."
          chips={<Chip>{data.signedIn ? `${data.savedCount} saved` : "Log in to save"}</Chip>}
          action={
            <Link href="/matches" className={btn("secondary", "sm")}>
              My Games
            </Link>
          }
        />
      </div>

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1.6fr)_minmax(0,1fr)]">
        <Card title="Match setup" icon="settings">
          <div ref={setupRef} className="flex flex-col">
            <Row label="Your club">
              {club ? (
                <>
                  {club.kits && (
                    <span className="flex gap-1" aria-label="Home kit, away kit, keeper">
                      {[club.kits.home, club.kits.away, club.kits.keeper].map((c, i) => (
                        <span
                          key={i}
                          className="size-4 rounded-full border border-white/40"
                          style={{ background: c }}
                        />
                      ))}
                    </span>
                  )}
                  <span className="font-semibold">{club.name}</span>
                </>
              ) : data.clubUnfinished ? (
                <span>
                  Your club is almost ready.{" "}
                  <Link href="/team/new" className="font-semibold text-link hover:underline">
                    Finish your team
                  </Link>{" "}
                  to play with it. Until then you manage a generated side.
                </span>
              ) : (
                <span>
                  A generated side.{" "}
                  <Link href="/team/new" className="font-semibold text-link hover:underline">
                    Create your team
                  </Link>{" "}
                  to play with your own club.
                </span>
              )}
            </Row>
            {club && (
              <Row label="Saved tactics">
                <span className="font-semibold">{club.tactics}</span>
                <Link href="/tactics" className="font-semibold text-link hover:underline">
                  Edit tactics
                </Link>
              </Row>
            )}
            <Row label="Match">
              <Segmented
                label="How to play it"
                options={LENGTHS}
                value={length}
                onChange={setLength}
              />
              <span className="text-xs text-muted">
                {length === "Watch live"
                  ? "Two halves, speed up to 16×"
                  : "Straight to the full-time report"}
              </span>
            </Row>
            <Row label="Opponent">
              <Segmented
                label="Opponent strength"
                options={STRENGTHS}
                value={strength}
                onChange={setStrength}
              />
            </Row>
            <Row label="Conditions">
              <span className="flex items-start gap-2 text-muted">
                <AppIcon name="cloud" size={16} className="mt-0.5" />
                Weather, pitch and wind are drawn for each match: rain and wind change how the ball
                moves.
              </span>
            </Row>
            {phase === "error" && (
              <p role="alert" className="mt-2 rounded-lg bg-foul/15 p-3 text-[13px] text-text">
                The match engine didn&apos;t load: {error}. Reload the page to try again.
              </p>
            )}
            <div className="mt-3 flex justify-end">
              <button
                type="button"
                className={btn("primary")}
                disabled={busy || phase === "error"}
                onClick={start}
              >
                {phase === "loading"
                  ? "Loading the match engine…"
                  : phase === "preparing"
                    ? "Preparing…"
                    : "Start match"}
              </button>
            </div>
          </div>
        </Card>

        <Card
          title="Recent results"
          icon="chart"
          link={data.signedIn ? { href: "/matches", label: "My Games" } : undefined}
        >
          {data.recent.length ? (
            <ul className="flex flex-col gap-1">
              {data.recent.map((m) => (
                <li key={m.id}>
                  <Link
                    href={`/matches/${m.id}`}
                    className="flex items-center gap-3 rounded-lg p-2 hover:bg-panel focus-visible:outline-2 focus-visible:outline-link"
                  >
                    <span
                      className={`flex size-6 shrink-0 items-center justify-center rounded-md text-[11px] font-bold ${m.result ? RESULT_CLASS[m.result] : "bg-border text-muted"}`}
                    >
                      {m.result ?? "–"}
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className="block truncate text-[13px] font-semibold text-text">
                        {m.homeName} {m.homeScore}–{m.awayScore} {m.awayName}
                      </span>
                      <span className="block text-xs text-muted">{m.when}</span>
                    </span>
                    <AppIcon name="refresh" size={16} className="text-muted" />
                  </Link>
                </li>
              ))}
            </ul>
          ) : (
            <p className="text-[13px] text-muted">
              {data.signedIn
                ? "No matches yet. Your first result shows up here."
                : "Log in to keep your results."}
            </p>
          )}
        </Card>
      </div>
    </div>
  );
}

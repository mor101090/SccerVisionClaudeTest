import type { Metadata } from "next";
import Link from "next/link";
import { Suspense } from "react";

import { AppIcon } from "@/components/app-shell/app-icon";
import { PageSkeleton } from "@/components/app-shell/shell-skeleton";
import { ActionLink, Card, Chip, EmptyState, PageHeader } from "@/components/app-shell/ui";
import { FollowButton } from "@/features/profile/follow-button";
import { requireUser } from "@/server/auth";
import { listLeagues } from "@/server/leagues";
import { clubOf } from "@/server/managers";

export const metadata: Metadata = { title: "Leagues" };

/** Figma "Leagues – browse". */
async function Leagues({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const user = await requireUser();
  const [club, sp] = await Promise.all([clubOf(user.id), searchParams]);
  const all = await listLeagues(user.id, club?.id ?? null);
  const filter = sp.show === "following" ? "following" : sp.show === "mine" ? "mine" : "all";
  const q = typeof sp.q === "string" ? sp.q.trim().toLowerCase() : "";
  const list = all
    .filter((l) => (filter === "following" ? l.following : filter === "mine" ? l.mine : true))
    .filter((l) => !q || l.name.toLowerCase().includes(q));
  return (
    <>
      <PageHeader
        crumb="Leagues"
        title="Leagues"
        sub="Every league in SoccerVision. Follow one to keep an eye on its table."
      />
      <div className="flex flex-wrap items-center gap-3">
        <nav
          aria-label="Show"
          className="inline-flex rounded-lg border border-border bg-panel-2 p-1"
        >
          {[
            ["all", "All", "/leagues"],
            ["following", "Following", "/leagues?show=following"],
            ["mine", "My league", "/leagues?show=mine"],
          ].map(([k, label, href]) => (
            <Link
              key={k}
              href={href!}
              aria-current={filter === k ? "page" : undefined}
              className={`rounded-md px-3.5 py-1.5 text-[13px] font-medium ${filter === k ? "bg-selected text-white" : "text-muted hover:text-text"}`}
            >
              {label}
            </Link>
          ))}
        </nav>
        <form
          action="/leagues"
          className="ml-auto flex items-center gap-2 rounded-lg border border-border bg-panel-2 px-3"
        >
          {filter !== "all" && <input type="hidden" name="show" value={filter} />}
          <AppIcon name="search" size={16} className="text-muted" />
          <input
            name="q"
            defaultValue={q}
            placeholder="Search leagues"
            aria-label="Search leagues"
            className="h-10 w-52 bg-transparent text-sm text-text placeholder:text-muted focus:outline-none"
          />
        </form>
      </div>
      {list.length ? (
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {list.map((l) => (
            <section
              key={l.id}
              className={`flex flex-col gap-3 rounded-2xl border bg-panel-2 p-5 ${l.mine ? "border-accent" : "border-border"}`}
            >
              <div className="flex items-center gap-3">
                <span className="flex size-9 items-center justify-center rounded-full bg-panel text-link">
                  <AppIcon name="trophy" size={18} />
                </span>
                <span className="min-w-0">
                  <span className="block truncate text-sm font-bold text-text">{l.name}</span>
                  <span className="block text-xs text-muted">
                    {l.clubs} {l.clubs === 1 ? "club" : "clubs"} · Season {l.season}
                  </span>
                </span>
              </div>
              <div className="flex flex-wrap gap-2">
                {l.mine && <Chip tone="goal">Your league</Chip>}
                {l.following && !l.mine && <Chip tone="info">Following</Chip>}
              </div>
              <p className="flex items-center gap-2 text-[13px] text-text">
                <AppIcon name="trophy" size={14} className="text-card" />
                {l.leader ? `Leader: ${l.leader}` : "Not started yet"}
              </p>
              <div className="mt-auto flex gap-2">
                <ActionLink href={`/leagues/${l.id}`} variant="secondary">
                  Open
                </ActionLink>
                {!l.mine && <FollowButton leagueId={l.id} following={l.following} />}
              </div>
            </section>
          ))}
        </div>
      ) : (
        <Card>
          <EmptyState
            icon="trophy"
            title={all.length ? "No league matches" : "Leagues are coming"}
            action={
              <ActionLink href="/play" icon="play">
                Play a friendly
              </ActionLink>
            }
          >
            {all.length
              ? "Try another search or show all leagues."
              : "There are no leagues yet. Friendlies count for your record and achievements in the meantime."}
          </EmptyState>
        </Card>
      )}
    </>
  );
}

export default function LeaguesPage({ searchParams }: PageProps<"/leagues">) {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <Leagues searchParams={searchParams} />
    </Suspense>
  );
}

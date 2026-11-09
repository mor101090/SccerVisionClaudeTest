import type { Metadata } from "next";
import Link from "next/link";
import { notFound, redirect } from "next/navigation";
import { Suspense } from "react";

import { AppIcon } from "@/components/app-shell/app-icon";
import { PageSkeleton } from "@/components/app-shell/shell-skeleton";
import { ActionLink, Card, Chip, EmptyState, PageHeader } from "@/components/app-shell/ui";
import {
  Avatar,
  BadgeTile,
  Crest,
  Flag,
  monthYear,
  RecordTiles,
  ResultDot,
  Row,
  StatTile,
} from "@/features/profile/ui";
import { currentUser } from "@/server/auth";
import { clubOf, headToHead, managerByUsername } from "@/server/managers";
import { profileData } from "@/server/profile-page";

export const metadata: Metadata = { title: "Manager" };

async function Manager({ params }: { params: Promise<{ username: string }> }) {
  const { username } = await params;
  const user = await currentUser();
  if (!user) redirect(`/auth/sign-in?redirectTo=/managers/${encodeURIComponent(username)}`);
  const found = await managerByUsername(username);
  if (!found) notFound();
  if (found.userId === user.id) redirect("/profile");
  const name = found.displayName ?? found.username;
  if (!found.showProfile) {
    const club = await clubOf(found.userId);
    return (
      <>
        <PageHeader crumb="Managers" title={name} sub={`@${found.username}`} />
        <Card>
          <EmptyState icon="lock" title="This profile is private">
            {name} keeps their profile to themselves.
            {club && (
              <>
                {" "}
                You can still see their club,{" "}
                <Link
                  href={`/clubs/${club.id}`}
                  className="font-semibold text-link hover:underline"
                >
                  {club.name}
                </Link>
                .
              </>
            )}
          </EmptyState>
        </Card>
      </>
    );
  }
  const d = (await profileData(found.userId))!;
  const myClub = await clubOf(user.id);
  const h2h = d.club ? await headToHead(user.id, myClub?.name ?? null, d.club.name) : null;
  const earned = d.achievements.filter((a) => a.earned);
  return (
    <>
      <PageHeader
        crumb="Managers"
        title={name}
        sub={`@${found.username} · Manager since ${monthYear(found.since)}`}
        actions={
          <ActionLink href="/lobby" icon="users">
            Play them online
          </ActionLink>
        }
      />
      <section className="flex flex-wrap items-center gap-5 rounded-2xl border border-border bg-panel-2 p-5 sm:p-6">
        <Avatar name={name} size={88} />
        <div className="flex min-w-0 flex-1 basis-64 flex-col gap-1.5">
          <h2 className="truncate text-2xl font-extrabold text-text">{name}</h2>
          <p className="flex flex-wrap items-center gap-x-2 text-[13px] text-muted">
            <span>@{found.username}</span>
            {d.country && (
              <span className="flex items-center gap-1.5">
                · <Flag code={found.nationality} /> {d.country}
              </span>
            )}
          </p>
          {found.bio && <p className="text-sm text-text">“{found.bio}”</p>}
          <div className="flex flex-wrap gap-2">
            {d.supporter && (
              <Chip tone="info" icon="heart">
                Supporter
              </Chip>
            )}
            {found.isAdmin && <Chip tone="accent">SoccerVision team</Chip>}
            {found.favouriteClub && <Chip>Supports {found.favouriteClub}</Chip>}
          </div>
        </div>
        {d.club && (
          <Link
            href={`/clubs/${d.club.id}`}
            className="flex items-center gap-3 rounded-xl bg-panel px-4 py-3 hover:bg-border focus-visible:outline-2 focus-visible:outline-link"
          >
            <Crest
              name={d.club.name}
              shortName={d.club.shortName}
              color={d.club.kits?.home.shirt}
              ink={d.club.kits?.home.number}
            />
            <span>
              <span className="block text-sm font-bold text-text">{d.club.name}</span>
              <span className="block text-xs text-muted">View club</span>
            </span>
            <AppIcon name="chevronRight" size={16} className="text-muted" />
          </Link>
        )}
      </section>
      {found.showHistory && <RecordTiles r={d.record} />}
      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_360px]">
        <div className="flex min-w-0 flex-col gap-4">
          {h2h && myClub && (
            <Card title={`You vs ${name}`} icon="users">
              <div className="flex flex-wrap gap-3">
                <StatTile value={h2h.won} label="Your wins" tone="text-goal" />
                <StatTile value={h2h.drawn} label="Draws" />
                <StatTile value={h2h.lost} label={`${name}’s wins`} tone="text-foul" />
              </div>
              {h2h.recent.length ? (
                <div>
                  {h2h.recent.map((r) => (
                    <Row
                      key={r.id}
                      href={`/matches/${r.id}`}
                      lead={<ResultDot r={r.result} />}
                      title={`${r.homeName} ${r.homeScore}–${r.awayScore} ${r.awayName}`}
                      sub={monthYear(r.playedAt)}
                    />
                  ))}
                </div>
              ) : (
                <p className="text-[13px] text-muted">You haven’t played {d.club!.name} yet.</p>
              )}
            </Card>
          )}
          {found.showHistory && (
            <Card title="Recent results" icon="ball">
              {d.recent.length ? (
                <div>
                  {d.recent.slice(0, 5).map((r) => (
                    <Row
                      key={r.id}
                      lead={<ResultDot r={r.result} />}
                      title={`${r.homeName} ${r.homeScore}–${r.awayScore} ${r.awayName}`}
                      sub={`${monthYear(r.playedAt)} · ${r.online ? "Online" : "Friendly"}`}
                    />
                  ))}
                </div>
              ) : (
                <p className="text-[13px] text-muted">No matches yet.</p>
              )}
            </Card>
          )}
        </div>
        <Card title={`Achievements · ${earned.length}`} icon="trophy" className="h-fit">
          {earned.length ? (
            <div className="grid grid-cols-2 gap-3">
              {earned.slice(0, 6).map((a) => (
                <BadgeTile key={a.id} a={a} />
              ))}
            </div>
          ) : (
            <p className="text-[13px] text-muted">None yet.</p>
          )}
        </Card>
      </div>
    </>
  );
}

export default function ManagerPage({ params }: PageProps<"/managers/[username]">) {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <Manager params={params} />
    </Suspense>
  );
}

import type { Metadata } from "next";
import Link from "next/link";
import { notFound, redirect } from "next/navigation";
import { Suspense } from "react";
import { z } from "zod";

import { PageSkeleton } from "@/components/app-shell/shell-skeleton";
import { ActionLink, Card, Ovr, PageHeader } from "@/components/app-shell/ui";
import { Avatar, Crest, monthYear, ResultDot, Row, StatTile } from "@/features/profile/ui";
import { overall } from "@/features/team/names";
import { PositionBadge } from "@/features/team/ui/pieces";
import { currentUser } from "@/server/auth";
import { clubById, clubOf, headToHead, managerById, matchFacts } from "@/server/managers";
import { getSquad } from "@/server/teams";

export const metadata: Metadata = { title: "Club" };

/** Figma "Club – another club" / "…, squad": what any manager may see of a club. */
async function Club({
  params,
  searchParams,
}: {
  params: Promise<{ id: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const [{ id }, sp] = await Promise.all([params, searchParams]);
  const user = await currentUser();
  if (!user) redirect(`/auth/sign-in?redirectTo=/clubs/${id}`);
  if (!z.uuid().safeParse(id).success) notFound();
  const club = await clubById(id);
  if (!club) notFound();
  const tab = sp.tab === "squad" ? "squad" : "overview";
  const [owner, squad, myClub] = await Promise.all([
    managerById(club.ownerId),
    getSquad(club.id),
    clubOf(user.id),
  ]);
  const players = squad
    .map((s) => ({ ...s.p, ovr: overall(s.p.stats), starter: s.starterSlot !== null }))
    .sort((a, b) => Number(b.starter) - Number(a.starter) || b.ovr - a.ovr);
  const rating = players.length
    ? Math.round(
        players.filter((p) => p.starter).reduce((n, p) => n + p.ovr, 0) /
          Math.max(1, players.filter((p) => p.starter).length),
      )
    : null;
  const avgAge = players.length
    ? (players.reduce((n, p) => n + p.age, 0) / players.length).toFixed(1)
    : "—";
  const history = owner?.showHistory ? await matchFacts(club.ownerId, club.name, 30) : null;
  const h2h =
    myClub && myClub.id !== club.id ? await headToHead(user.id, myClub.name, club.name) : null;
  const managerName = owner ? (owner.displayName ?? owner.username) : null;

  return (
    <>
      <PageHeader
        crumb="Clubs"
        title={club.name}
        sub={[managerName && `Managed by ${managerName}`, club.city, club.stadiumName]
          .filter(Boolean)
          .join(" · ")}
        actions={
          myClub && myClub.id !== club.id ? (
            <ActionLink href="/lobby" icon="users">
              Play them online
            </ActionLink>
          ) : undefined
        }
      />
      <nav aria-label="Club" className="flex gap-1 border-b border-border">
        {[
          ["overview", "Overview", `/clubs/${club.id}`],
          ["squad", "Squad", `/clubs/${club.id}?tab=squad`],
        ].map(([k, label, href]) => (
          <Link
            key={k}
            href={href!}
            aria-current={tab === k ? "page" : undefined}
            className={`-mb-px border-b-2 px-4 py-2.5 text-sm font-semibold ${tab === k ? "border-selected text-text" : "border-transparent text-muted hover:text-text"}`}
          >
            {label}
          </Link>
        ))}
      </nav>

      {tab === "overview" ? (
        <>
          <section className="flex flex-wrap items-center gap-5 rounded-2xl border border-border bg-panel-2 p-5 sm:p-6">
            <Crest
              name={club.name}
              shortName={club.shortName}
              color={club.kits?.home.shirt}
              ink={club.kits?.home.number}
              size={88}
              square
            />
            <div className="flex min-w-0 flex-1 basis-56 flex-col gap-2">
              <h2 className="truncate text-2xl font-extrabold text-text">{club.name}</h2>
              {owner && (
                <Link
                  href={`/managers/${owner.username}`}
                  className="flex items-center gap-2 text-[13px] text-link hover:underline"
                >
                  <Avatar name={managerName!} size={24} /> {managerName} · @{owner.username}
                </Link>
              )}
              {history && history.recent.length > 0 && (
                <span className="flex items-center gap-2 text-xs text-muted">
                  Form
                  <span className="flex gap-1">
                    {history.recent.slice(0, 5).map((r) => (
                      <ResultDot key={r.id} r={r.result} />
                    ))}
                  </span>
                </span>
              )}
            </div>
            <div className="flex gap-3">
              <StatTile value={rating ?? "—"} label="Team rating (XI)" />
              <StatTile value={players.length} label="Players" />
            </div>
          </section>
          <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_360px]">
            <div className="flex min-w-0 flex-col gap-4">
              <Card
                title="Key players"
                icon="star"
                link={{ href: `/clubs/${club.id}?tab=squad`, label: "Full squad" }}
              >
                <div>
                  {players.slice(0, 5).map((p) => (
                    <Row
                      key={p.shirt_number}
                      lead={
                        <span className="w-6 text-xs text-muted tabular-nums">
                          {p.shirt_number}
                        </span>
                      }
                      title={p.name}
                      sub={`Age ${p.age}`}
                      trail={
                        <span className="flex items-center gap-2">
                          <PositionBadge position={p.position} />
                          <Ovr value={p.ovr} />
                        </span>
                      }
                    />
                  ))}
                </div>
              </Card>
              <Card title="Recent results" icon="ball">
                {!history ? (
                  <p className="text-[13px] text-muted">
                    {managerName ?? "The manager"} keeps their results private.
                  </p>
                ) : history.recent.length ? (
                  <div>
                    {history.recent.slice(0, 5).map((r) => (
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
            </div>
            <div className="flex flex-col gap-4">
              <Card title="Club info" icon="info">
                {[
                  ["Founded", monthYear(club.createdAt)],
                  ["Ground", club.stadiumName ?? "—"],
                  ["Formation", club.formation],
                  ["Average age", avgAge],
                ].map(([k, v]) => (
                  <div key={k} className="flex justify-between text-[13px]">
                    <span className="text-muted">{k}</span>
                    <span className="font-semibold text-text">{v}</span>
                  </div>
                ))}
                {club.kits && (
                  <div className="flex justify-between text-[13px]">
                    <span className="text-muted">Kits</span>
                    <span className="flex gap-1.5" aria-label="Home, away and keeper shirts">
                      {[club.kits.home, club.kits.away, club.kits.keeper].map((k, i) => (
                        <span
                          key={i}
                          className="size-4 rounded-full border border-white/40"
                          style={{ background: k.shirt }}
                        />
                      ))}
                    </span>
                  </div>
                )}
              </Card>
              {h2h && (
                <Card title="Against you" icon="users">
                  <div className="flex flex-wrap gap-2">
                    <StatTile value={h2h.won} label="Your wins" tone="text-goal" />
                    <StatTile value={h2h.drawn} label="Draws" />
                    <StatTile value={h2h.lost} label="Their wins" tone="text-foul" />
                  </div>
                </Card>
              )}
            </div>
          </div>
        </>
      ) : (
        <>
          <Card className="overflow-x-auto p-2!">
            <table className="w-full min-w-[560px] text-left text-[13px]">
              <thead>
                <tr className="border-b border-divider text-[11px] tracking-[0.06em] text-muted uppercase">
                  <th className="px-3 py-2 font-bold">#</th>
                  <th className="px-3 py-2 font-bold">Player</th>
                  <th className="px-3 py-2 font-bold">Pos</th>
                  <th className="px-3 py-2 font-bold">Age</th>
                  <th className="px-3 py-2 font-bold">Nationality</th>
                  <th className="px-3 py-2 font-bold">Overall</th>
                </tr>
              </thead>
              <tbody>
                {players.map((p) => (
                  <tr key={p.shirt_number} className="border-b border-divider last:border-0">
                    <td className="px-3 py-2.5 text-muted tabular-nums">{p.shirt_number}</td>
                    <td className="px-3 py-2.5 font-semibold text-text">
                      {p.name}
                      {p.starter && (
                        <span className="ml-2 text-[10px] font-bold text-link">XI</span>
                      )}
                    </td>
                    <td className="px-3 py-2.5">
                      <PositionBadge position={p.position} />
                    </td>
                    <td className="px-3 py-2.5 text-text tabular-nums">{p.age}</td>
                    <td className="px-3 py-2.5 text-muted">{p.nationality}</td>
                    <td className="px-3 py-2.5">
                      <Ovr value={p.ovr} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </Card>
          <p className="rounded-lg bg-info/16 p-3 text-[13px] text-text">
            You see positions, ages and overall ratings. Detailed attributes and fitness stay
            private to the manager.
          </p>
        </>
      )}
    </>
  );
}

export default function ClubPage({ params, searchParams }: PageProps<"/clubs/[id]">) {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <Club params={params} searchParams={searchParams} />
    </Suspense>
  );
}

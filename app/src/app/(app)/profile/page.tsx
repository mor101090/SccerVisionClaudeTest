import type { Metadata } from "next";
import Link from "next/link";
import { Suspense } from "react";

import { AppIcon } from "@/components/app-shell/app-icon";
import { PageSkeleton } from "@/components/app-shell/shell-skeleton";
import { ActionLink, Card, Chip, PageHeader } from "@/components/app-shell/ui";
import { CopyField } from "@/features/profile/copy-field";
import {
  Avatar,
  BadgeTile,
  Crest,
  Flag,
  monthYear,
  RecordTiles,
  ResultDot,
  Row,
} from "@/features/profile/ui";
import { requestOrigin } from "@/server/auth-flow";
import { myProfileData } from "@/server/profile-page";

export const metadata: Metadata = { title: "Your profile" };

async function Profile() {
  const d = await myProfileData("/profile");
  const { manager: m, club, record } = d;
  const name = m.displayName ?? m.username;
  const earned = d.achievements.filter((a) => a.earned).length;
  const shown = [...d.achievements].sort((a, b) => Number(b.earned) - Number(a.earned)).slice(0, 6);
  const inviteUrl = `${await requestOrigin()}/i/${m.username}`;
  return (
    <>
      <PageHeader
        crumb="Profile"
        title="Your profile"
        sub="How other managers see you. Only you see the support and invite cards."
        actions={
          <>
            <ActionLink href="/profile/edit" variant="secondary" icon="edit">
              Edit profile
            </ActionLink>
            <ActionLink href="/invite" icon="gift">
              Invite a friend
            </ActionLink>
          </>
        }
      />

      <section className="flex flex-wrap items-center gap-5 rounded-2xl border border-border bg-panel-2 p-5 sm:p-6">
        <Avatar name={name} size={88} />
        <div className="flex min-w-0 flex-1 basis-64 flex-col gap-1.5">
          <h2 className="truncate text-2xl font-extrabold text-text">{name}</h2>
          <p className="flex flex-wrap items-center gap-x-2 text-[13px] text-muted">
            <span>@{m.username}</span>
            {d.country && (
              <span className="flex items-center gap-1.5">
                · <Flag code={m.nationality} /> {d.country}
              </span>
            )}
            <span>· Manager since {monthYear(m.since)}</span>
          </p>
          {m.bio && <p className="text-sm text-text">{m.bio}</p>}
          <div className="flex flex-wrap gap-2">
            {d.supporter && (
              <Chip tone="info" icon="heart">
                Supporter
              </Chip>
            )}
            {m.isAdmin && <Chip tone="accent">SoccerVision team</Chip>}
            {m.favouriteClub && <Chip>Supports {m.favouriteClub}</Chip>}
          </div>
        </div>
        {club ? (
          <Link
            href="/club/settings"
            className="flex items-center gap-3 rounded-xl bg-panel px-4 py-3 hover:bg-border focus-visible:outline-2 focus-visible:outline-link"
          >
            <Crest
              name={club.name}
              shortName={club.shortName}
              color={club.kits?.home.shirt}
              ink={club.kits?.home.number}
            />
            <span>
              <span className="block text-sm font-bold text-text">{club.name}</span>
              <span className="block text-xs text-muted">Club settings</span>
            </span>
            <AppIcon name="chevronRight" size={16} className="text-muted" />
          </Link>
        ) : (
          <ActionLink href="/team/new">Create your club</ActionLink>
        )}
      </section>

      <RecordTiles r={record} />

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_360px]">
        <div className="flex min-w-0 flex-col gap-4">
          <Card title="Recent matches" icon="ball" link={{ href: "/matches", label: "My Games" }}>
            {d.recent.length ? (
              <div>
                {d.recent.slice(0, 5).map((r) => (
                  <Row
                    key={r.id}
                    href={`/matches/${r.id}`}
                    lead={<ResultDot r={r.result} />}
                    title={`${r.homeName} ${r.homeScore}–${r.awayScore} ${r.awayName}`}
                    sub={`${monthYear(r.playedAt)} · ${r.online ? "Online" : "Friendly"}`}
                    trail={<AppIcon name="refresh" size={16} className="text-muted" />}
                  />
                ))}
              </div>
            ) : (
              <p className="text-sm text-muted">
                No saved matches yet.{" "}
                <Link href="/play" className="font-semibold text-link hover:underline">
                  Play one
                </Link>
                .
              </p>
            )}
          </Card>
          <Card
            title={`Achievements · ${earned} of ${d.achievements.length}`}
            icon="trophy"
            link={{ href: "/profile/achievements", label: "All" }}
          >
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
              {shown.map((a) => (
                <BadgeTile key={a.id} a={a} />
              ))}
            </div>
          </Card>
        </div>

        <div className="flex flex-col gap-4">
          <Card title="Support the game" icon="heart">
            <p className="text-[13px] leading-5 text-muted">
              SoccerVision is made by a small team. A donation keeps the servers on and the next
              features coming.
            </p>
            <ActionLink href="/support" icon="heart">
              Support SoccerVision
            </ActionLink>
            {d.supporter && <p className="text-xs text-goal">Thank you for supporting the game.</p>}
          </Card>
          <Card title="Invite friends" icon="gift" link={{ href: "/invite", label: "Details" }}>
            <CopyField value={inviteUrl} label="Your invite link" />
            <div className="flex justify-between text-[13px]">
              <span className="text-muted">Friends joined</span>
              <span className="font-bold text-text">{d.invited.length}</span>
            </div>
            <div className="flex justify-between text-[13px]">
              <span className="text-muted">Played their first match</span>
              <span className="font-bold text-text">
                {d.invited.filter((i) => i.played).length}
              </span>
            </div>
          </Card>
          <Card title="Your public page" icon="user">
            <p className="text-[13px] text-muted">
              {m.showProfile
                ? "Other managers can open your profile and see your record."
                : "Your profile is hidden from other managers. Change it in Edit profile."}
            </p>
            {m.showProfile && (
              <ActionLink href={`/managers/${m.username}`} variant="secondary" icon="external">
                See it as others do
              </ActionLink>
            )}
          </Card>
        </div>
      </div>
    </>
  );
}

export default function ProfilePage() {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <Profile />
    </Suspense>
  );
}

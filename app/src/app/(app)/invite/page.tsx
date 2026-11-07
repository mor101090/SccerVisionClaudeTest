import type { Metadata } from "next";
import { Suspense } from "react";

import { PageSkeleton } from "@/components/app-shell/shell-skeleton";
import { Card, Chip, PageHeader } from "@/components/app-shell/ui";
import { CopyField } from "@/features/profile/copy-field";
import { ShareButtons } from "@/features/profile/share-buttons";
import { Avatar, IconBubble, monthYear, Row } from "@/features/profile/ui";
import { requestOrigin } from "@/server/auth-flow";
import { myProfileData } from "@/server/profile-page";

export const metadata: Metadata = { title: "Invite a friend" };

async function Invite() {
  const d = await myProfileData("/invite");
  const url = `${await requestOrigin()}/i/${d.manager.username}`;
  const played = d.invited.filter((i) => i.played).length;
  const text = `Build your club and play me in a friendly on SoccerVision.`;
  return (
    <>
      <PageHeader
        crumb="Profile"
        title="Invite a friend"
        sub="Play your friends, not just the computer. Every friend gets their own club."
      />
      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_360px]">
        <div className="flex min-w-0 flex-col gap-4">
          <Card title="Your invite link" icon="link">
            <CopyField value={url} label="Your invite link" big />
            <ShareButtons url={url} text={text} />
            <p className="text-xs text-muted">
              Or tell them your username,{" "}
              <strong className="text-text">{d.manager.username}</strong>: they type it in “User
              that invited you” when they create their club.
            </p>
          </Card>
          <Card title={`Your invites · ${d.invited.length}`} icon="users">
            {d.invited.length ? (
              <div>
                {d.invited.map((i) => (
                  <Row
                    key={i.username}
                    href={`/managers/${i.username}`}
                    lead={<Avatar name={i.displayName ?? i.username} size={32} />}
                    title={i.displayName ?? i.username}
                    sub={`@${i.username} · joined ${monthYear(i.joinedAt)}`}
                    trail={
                      i.played ? <Chip tone="goal">Played a match</Chip> : <Chip>No match yet</Chip>
                    }
                  />
                ))}
              </div>
            ) : (
              <p className="text-[13px] text-muted">Nobody has joined with your link yet.</p>
            )}
          </Card>
        </div>
        <div className="flex flex-col gap-4">
          <Card title="How it works" icon="info">
            {[
              ["Share your link", "By message, email or your username"],
              ["Your friend creates a club", "Free, a few minutes"],
              ["Play each other", "Online friendlies in real time"],
            ].map(([t, s], i) => (
              <div key={t} className="flex items-start gap-3">
                <span className="flex size-7 shrink-0 items-center justify-center rounded-full bg-selected text-xs font-bold text-white">
                  {i + 1}
                </span>
                <span>
                  <span className="block text-sm font-semibold text-text">{t}</span>
                  <span className="block text-xs text-muted">{s}</span>
                </span>
              </div>
            ))}
          </Card>
          <Card title="Rewards" icon="gift">
            <Row
              lead={<IconBubble icon="share" on={played >= 1} />}
              title="Recruiter badge"
              sub="A friend you invited plays a match"
              trail={
                played >= 1 ? (
                  <Chip tone="goal">Earned</Chip>
                ) : (
                  <span className="text-xs text-muted">0 / 1</span>
                )
              }
            />
            <Row
              lead={<IconBubble icon="users" on={played >= 10} />}
              title="Club founder badge"
              sub="10 friends you invited play a match"
              trail={
                played >= 10 ? (
                  <Chip tone="goal">Earned</Chip>
                ) : (
                  <span className="text-xs text-muted">{played} / 10</span>
                )
              }
            />
            <p className="text-xs text-muted">
              Rewards are badges: they never change how a match plays.
            </p>
          </Card>
        </div>
      </div>
    </>
  );
}

export default function InvitePage() {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <Invite />
    </Suspense>
  );
}

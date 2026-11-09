import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { Suspense } from "react";

import { AppIcon, type IconName } from "@/components/app-shell/app-icon";
import { PageSkeleton } from "@/components/app-shell/shell-skeleton";
import { Avatar } from "@/features/profile/ui";
import { currentUser } from "@/server/auth";
import { clubOf, managerByUsername } from "@/server/managers";

export const metadata: Metadata = { title: "You're invited" };

const PERKS: [IconName, string, string][] = [
  ["shield", "Your own club", "Name, crest and three kits"],
  ["users", "A squad of 22", "Generated players with real attributes"],
  ["flag", "Tactics that matter", "Formations, roles and instructions"],
  [
    "ball",
    "Live friendlies",
    "Against the computer or your friend, the same engine on both screens",
  ],
];

/** Figma "Invite – friend opens the link": the page a friend lands on from /i/<username>. */
async function Landing({ params }: { params: Promise<{ username: string }> }) {
  const { username } = await params;
  const inviter = await managerByUsername(username);
  if (!inviter) notFound();
  const [club, user] = await Promise.all([clubOf(inviter.userId), currentUser()]);
  const name = inviter.displayName ?? inviter.username;
  const first = name.split(/\s+/)[0] ?? name;
  return (
    <div className="flex flex-1 flex-wrap items-center gap-10 py-6 lg:py-16">
      <div className="flex min-w-0 flex-1 basis-[420px] flex-col gap-5">
        <div className="flex items-center gap-3">
          <Avatar name={name} size={48} />
          <span>
            <span className="block text-sm font-bold text-text">{name} invited you</span>
            {club && <span className="block text-xs text-muted">Manager of {club.name}</span>}
          </span>
        </div>
        <h1 className="text-4xl leading-tight font-extrabold text-text sm:text-5xl">
          Build your club and play {first} in a friendly
        </h1>
        <p className="max-w-xl text-lg text-muted">
          Pick your kits, sign a generated squad of 22 and take them into live matches: tactics,
          substitutions and a real match engine, in your browser.
        </p>
        {user ? (
          <div className="flex flex-col gap-2">
            <p className="text-sm text-text">You already have an account.</p>
            <div className="flex flex-wrap gap-3">
              <Link
                href="/lobby"
                className="inline-flex h-11 items-center rounded-lg bg-accent px-5 font-semibold text-on-accent hover:brightness-110"
              >
                Play {first} online
              </Link>
              <Link
                href={`/managers/${inviter.username}`}
                className="inline-flex h-11 items-center rounded-lg border border-border bg-panel-2 px-5 font-semibold text-text hover:bg-panel"
              >
                See {first}’s profile
              </Link>
            </div>
          </div>
        ) : (
          <>
            <div className="flex flex-wrap gap-3">
              {/* Plain links: the route handler stores who invited, then opens sign-up / log-in. */}
              <a
                href={`/api/invite?u=${encodeURIComponent(inviter.username)}`}
                className="inline-flex h-11 items-center rounded-lg bg-accent px-5 font-semibold text-on-accent hover:brightness-110"
              >
                Create my club
              </a>
              <a
                href={`/api/invite?u=${encodeURIComponent(inviter.username)}&next=sign-in`}
                className="inline-flex h-11 items-center rounded-lg border border-border bg-panel-2 px-5 font-semibold text-text hover:bg-panel"
              >
                I already have an account
              </a>
            </div>
            <p className="text-xs text-muted">
              Free, no card needed. {first} is filled in as the manager who invited you.
            </p>
          </>
        )}
      </div>
      <section
        aria-label="What you get"
        className="flex w-full max-w-md flex-col gap-4 rounded-2xl border border-border bg-panel-2 p-6"
      >
        <h2 className="text-[11px] font-bold tracking-[0.08em] text-muted uppercase">
          What you get
        </h2>
        {PERKS.map(([icon, t, s]) => (
          <div key={t} className="flex items-start gap-3">
            <span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-panel text-link">
              <AppIcon name={icon} size={18} />
            </span>
            <span>
              <span className="block text-sm font-semibold text-text">{t}</span>
              <span className="block text-xs text-muted">{s}</span>
            </span>
          </div>
        ))}
      </section>
    </div>
  );
}

export default function InviteLandingPage({ params }: PageProps<"/i/[username]">) {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <Landing params={params} />
    </Suspense>
  );
}

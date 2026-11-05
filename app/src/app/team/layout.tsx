import Image from "next/image";
import { Suspense } from "react";

import { Brand, HeaderBar } from "@/components/brand";
import { signOutAction } from "@/features/auth/actions";
import { initialsOf } from "@/features/team/ui/pieces";
import { ManagerSkeleton } from "@/features/team/ui/team-skeleton";
import { currentUser } from "@/server/auth";

import heroTeam from "../../../public/auth/hero-team.jpg";

async function Manager() {
  const user = await currentUser();
  if (!user) return null;
  return (
    <div className="flex items-center gap-4">
      <span
        className="flex size-8 items-center justify-center rounded-full bg-accent text-xs font-bold text-accent-ink"
        aria-hidden
      >
        {initialsOf(user.name)}
      </span>
      <span className="font-medium text-text" data-testid="user-name">
        {user.name}
      </span>
      <form action={signOutAction}>
        <button type="submit" className="text-muted hover:text-text">
          Log out
        </button>
      </form>
    </div>
  );
}

/** Create your team: Figma header (brand, avatar, username, Log out) over the squad photo. */
export default function TeamLayout({ children }: { children: React.ReactNode }) {
  return (
    <>
      <HeaderBar>
        <Brand />
        <span className="flex-1" />
        <Suspense fallback={<ManagerSkeleton />}>
          <Manager />
        </Suspense>
      </HeaderBar>
      <div className="relative flex flex-1 flex-col">
        <div aria-hidden className="absolute inset-x-0 top-0 h-[520px] overflow-hidden">
          <Image
            src={heroTeam}
            alt=""
            fill
            priority
            sizes="100vw"
            placeholder="blur"
            className="object-cover"
          />
          {/* Photo overlay (Figma: team art): raw hex is allowed for photo treatments. */}
          <div className="absolute inset-0 bg-[linear-gradient(180deg,rgb(15_20_25/0.15)_0%,var(--mor-dark-purple)_100%)]" />
        </div>
        <main className="relative flex flex-col items-center gap-7 px-4 pt-10 pb-12">
          {children}
        </main>
      </div>
    </>
  );
}

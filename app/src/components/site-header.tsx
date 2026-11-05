import Link from "next/link";
import { Suspense } from "react";

import { Brand, HeaderBar } from "@/components/brand";
import { Skeleton } from "@/components/ui/skeleton";
import { signOutAction } from "@/features/auth/actions";
import { currentUser } from "@/server/auth";

async function UserNav() {
  // No try/catch: during prerender this suspends on cookies() and Next must see that signal.
  const user = await currentUser();
  if (!user) {
    return (
      <Link href="/auth/sign-in" className="font-semibold text-link hover:underline">
        Login
      </Link>
    );
  }
  return (
    <div className="flex items-center gap-4">
      <Link href="/matches" className="hover:underline">
        My matches
      </Link>
      <Link href="/lobby" className="hover:underline">
        Online
      </Link>
      <span className="text-muted" data-testid="user-name">
        {user.name}
      </span>
      <Link href="/settings" className="text-muted hover:text-text">
        Settings
      </Link>
      <form action={signOutAction}>
        <button type="submit" className="text-muted hover:text-text">
          Sign out
        </button>
      </form>
    </div>
  );
}

export function SiteHeader() {
  return (
    <HeaderBar>
      <nav className="flex flex-1 items-center gap-6">
        <Brand />
        <Link href="/play" className="hover:underline">
          Play
        </Link>
      </nav>
      <Suspense
        fallback={
          <div aria-hidden className="flex items-center gap-4">
            <Skeleton className="h-4 w-20" />
            <Skeleton className="h-4 w-14" />
            <Skeleton className="h-4 w-24" />
          </div>
        }
      >
        <UserNav />
      </Suspense>
    </HeaderBar>
  );
}

import Link from "next/link";
import { Suspense } from "react";

import { Brand, HeaderBar } from "@/components/brand";
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
      <Suspense fallback={<span className="text-muted">…</span>}>
        <UserNav />
      </Suspense>
    </HeaderBar>
  );
}

import Link from "next/link";

import { BrandLogo } from "@/components/brand";

const secondary =
  "rounded-lg border border-border bg-panel-2 px-5 py-2.5 font-semibold text-text hover:bg-panel";

/** The public landing for visitors who are not signed in (signed-in managers get the dashboard). */
export function Landing() {
  return (
    <div className="flex flex-1 flex-col items-center justify-center gap-6 p-8 text-center">
      <h1>
        <BrandLogo className="h-24 sm:h-32" priority />
      </h1>
      <p className="max-w-md text-muted">
        Football management with a deterministic match engine running in your browser.
      </p>
      <div className="flex flex-wrap justify-center gap-3">
        {/* The one call to action that keeps what you build: an account and a club. */}
        <Link
          href="/auth/sign-up"
          className="rounded-lg bg-button px-5 py-2.5 font-semibold text-button-text hover:bg-border"
        >
          Create your club
        </Link>
        <Link href="/play" className={secondary}>
          Play a match
        </Link>
        <Link href="/lobby" className={secondary}>
          Play a friend online
        </Link>
        {/* Plain <a>: a static HTML page outside the Next.js router. */}
        <a href="/classic/index.html" className={secondary}>
          Classic game
        </a>
      </div>
      <p className="text-sm text-muted">
        Have a club?{" "}
        <Link href="/auth/sign-in" className="font-semibold text-link hover:underline">
          Log in
        </Link>
      </p>
    </div>
  );
}

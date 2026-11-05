import Link from "next/link";

import { BrandLogo } from "@/components/brand";

const secondary =
  "rounded-lg border border-border bg-panel-2 px-5 py-2.5 font-semibold text-text hover:bg-panel";

export default function Home() {
  return (
    <main className="flex flex-1 flex-col items-center justify-center gap-6 p-8 text-center">
      <h1>
        <BrandLogo className="h-24 sm:h-32" priority />
      </h1>
      <p className="max-w-md text-muted">
        Football management with a deterministic match engine running in your browser.
      </p>
      <div className="flex flex-wrap justify-center gap-3">
        <Link
          href="/play"
          className="rounded-lg bg-button px-5 py-2.5 font-semibold text-button-text hover:bg-border"
        >
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
    </main>
  );
}

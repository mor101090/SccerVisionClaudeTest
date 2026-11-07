import type { Metadata } from "next";
import Link from "next/link";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { PageSkeleton } from "@/components/app-shell/shell-skeleton";
import { Card, EmptyState, PageHeader } from "@/components/app-shell/ui";
import { initials } from "@/features/home/rules";
import { currentUser } from "@/server/auth";
import { search } from "@/server/club-world";

export const metadata: Metadata = { title: "Search" };

async function Results({ searchParams }: { searchParams: Promise<{ q?: string }> }) {
  const user = await currentUser();
  const { q = "" } = await searchParams;
  if (!user) redirect(`/auth/sign-in?redirectTo=${encodeURIComponent(`/search?q=${q}`)}`);
  const term = q.trim().slice(0, 60);
  const found = term.length >= 2 ? await search(term) : null;
  return (
    <>
      <PageHeader crumb="Search" title={term ? `Results for “${term}”` : "Search"} />
      <form action="/search" role="search" className="flex gap-2">
        <input
          name="q"
          defaultValue={term}
          aria-label="Search clubs and managers"
          placeholder="Search clubs and managers"
          className="h-11 flex-1 rounded-lg border border-border bg-panel-2 px-3.5 text-sm text-text placeholder:text-muted focus:border-link focus:outline-none"
        />
        <button
          type="submit"
          className="h-11 rounded-lg bg-button px-5 text-sm font-semibold text-button-text hover:bg-border"
        >
          Search
        </button>
      </form>
      {!found ? (
        <p className="text-sm text-muted">Type at least 2 characters.</p>
      ) : found.clubs.length + found.managers.length === 0 ? (
        <Card>
          <EmptyState icon="search" title="No matches">
            Try a club name, short name or a manager&apos;s username.
          </EmptyState>
        </Card>
      ) : (
        <div className="grid gap-5 md:grid-cols-2">
          <Card title={`Clubs (${found.clubs.length})`}>
            <ul className="flex flex-col gap-2">
              {found.clubs.map((c) => (
                <li key={c.id}>
                  <Link
                    href={`/clubs/${c.id}`}
                    className="-mx-2 flex items-center gap-3 rounded-lg px-2 py-1 text-sm hover:bg-panel"
                  >
                    <span className="flex size-8 items-center justify-center rounded-full bg-info text-[11px] font-bold text-white">
                      {initials(c.name)}
                    </span>
                    <span className="font-semibold text-text">{c.name}</span>
                    <span className="text-muted">{c.shortName}</span>
                  </Link>
                </li>
              ))}
            </ul>
          </Card>
          <Card title={`Managers (${found.managers.length})`}>
            <ul className="flex flex-col gap-2">
              {found.managers.map((m) => (
                <li key={m}>
                  <Link
                    href={`/managers/${m}`}
                    className="-mx-2 flex items-center gap-3 rounded-lg px-2 py-1 text-sm hover:bg-panel"
                  >
                    <span className="flex size-8 items-center justify-center rounded-full bg-link text-[11px] font-bold text-white">
                      {initials(m)}
                    </span>
                    <span className="font-semibold text-text">{m}</span>
                  </Link>
                </li>
              ))}
            </ul>
          </Card>
        </div>
      )}
    </>
  );
}

export default function SearchPage(props: PageProps<"/search">) {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <Results searchParams={props.searchParams as Promise<{ q?: string }>} />
    </Suspense>
  );
}

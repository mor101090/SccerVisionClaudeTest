import type { Metadata } from "next";
import Link from "next/link";
import { Suspense } from "react";

import { AppIcon } from "@/components/app-shell/app-icon";
import { FilterTabs } from "@/components/app-shell/filter-tabs";
import { PageSkeleton } from "@/components/app-shell/shell-skeleton";
import { Card, EmptyState, PageHeader } from "@/components/app-shell/ui";
import { NewThreadForm } from "@/features/forum/forms";
import { relativeTime } from "@/lib/relative-time";
import { forumThreadList } from "@/server/club-world";
import { loadClub } from "@/server/team-flow";

export const metadata: Metadata = { title: "Forum" };

const CATS = [
  ["", "Latest"],
  ["general", "General"],
  ["tactics", "Tactics"],
  ["transfers", "Transfers"],
  ["friendlies", "Friendlies"],
  ["bugs", "Bug reports"],
] as const;

async function Forum() {
  const { profile } = await loadClub("/forum");
  const threads = await forumThreadList(null);
  const label = Object.fromEntries(CATS) as Record<string, string>;
  const counts: Record<string, number> = {};
  for (const t of threads) counts[t.category] = (counts[t.category] ?? 0) + 1;
  return (
    <>
      <PageHeader
        crumb="Forum"
        title="Forum"
        sub="Talk tactics, trade players and find friendly opponents"
      />
      {threads.length === 0 ? (
        <Card className="gap-0! py-1.5!">
          <EmptyState icon="forum" title="No threads yet">
            Announcements and guides from the Soccer Vision team will appear here.
          </EmptyState>
        </Card>
      ) : (
        <FilterTabs
          label="Categories"
          counts={counts}
          tabs={CATS.map(([id, l]) => ({ id, label: l }))}
          empty={
            <Card className="gap-0! py-1.5!">
              <EmptyState icon="forum" title="No threads yet">
                Announcements and guides from the Soccer Vision team will appear here.
              </EmptyState>
            </Card>
          }
        >
          <Card className="gap-0! py-1.5!">
            <ul className="divide-y divide-divider">
              {threads.map((t) => (
                <li key={t.id} data-filter={t.category}>
                  <Link
                    href={`/forum/${t.id}`}
                    className="flex items-center gap-3.5 py-3.5 hover:bg-panel"
                  >
                    <span className="flex size-[38px] shrink-0 items-center justify-center rounded-full bg-panel text-muted">
                      <AppIcon name="forum" size={18} />
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className="block truncate text-sm font-semibold text-text">
                        {t.title}
                      </span>
                      <span className="block text-xs text-muted">
                        {label[t.category] ?? t.category} · started by {t.author ?? "a manager"}
                      </span>
                    </span>
                    <span className="text-right">
                      <span className="block text-[13px] font-semibold text-text">
                        {t.replies} replies
                      </span>
                      <span className="block text-[11px] text-muted">
                        last {relativeTime(t.lastPostAt)}
                      </span>
                    </span>
                  </Link>
                </li>
              ))}
            </ul>
          </Card>
        </FilterTabs>
      )}
      {profile.isAdmin ? (
        <Card title="New thread" icon="forum">
          <NewThreadForm />
        </Card>
      ) : (
        <p className="flex items-start gap-2 rounded-lg bg-info/16 p-3 text-[13px] leading-5 text-text">
          The forum is read-only for now: posts come from the Soccer Vision team.
        </p>
      )}
    </>
  );
}

export default function ForumPage() {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <Forum />
    </Suspense>
  );
}

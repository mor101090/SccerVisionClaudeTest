import type { Metadata } from "next";
import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import { PageSkeleton } from "@/components/app-shell/shell-skeleton";
import { Card, EmptyState, PageHeader } from "@/components/app-shell/ui";
import { relativeTime } from "@/lib/relative-time";
import { currentUser } from "@/server/auth";
import { allNotifications } from "@/server/club-world";

export const metadata: Metadata = { title: "Notifications" };

async function Notifications() {
  await connection(); // fresh per request: other people write this list (and it shows "2m ago")
  const user = await currentUser();
  if (!user) redirect("/auth/sign-in?redirectTo=/notifications");
  const rows = await allNotifications(user.id);
  return (
    <>
      <PageHeader crumb="Notifications" title="Notifications" />
      <Card className="gap-0!">
        {rows.length === 0 ? (
          <EmptyState icon="bell" title="Nothing yet">
            Match results, training reports and transfer offers show up here.
          </EmptyState>
        ) : (
          <ul className="divide-y divide-divider">
            {rows.map((n) => {
              const body = (
                <>
                  {!n.readAt && (
                    <span
                      className="mt-1.5 size-2 shrink-0 rounded-full bg-link"
                      aria-label="Unread"
                    />
                  )}
                  <span className="flex-1">
                    <span className="block text-sm font-semibold text-text">{n.title}</span>
                    {n.body && <span className="block text-[13px] text-muted">{n.body}</span>}
                  </span>
                  <span className="text-xs text-muted">{relativeTime(n.createdAt)}</span>
                </>
              );
              return (
                <li key={n.id}>
                  {n.href ? (
                    <Link href={n.href} className="flex gap-3 py-3 hover:bg-panel">
                      {body}
                    </Link>
                  ) : (
                    <div className="flex gap-3 py-3">{body}</div>
                  )}
                </li>
              );
            })}
          </ul>
        )}
      </Card>
    </>
  );
}

export default function NotificationsPage() {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <Notifications />
    </Suspense>
  );
}

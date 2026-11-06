import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { PageSkeleton } from "@/components/app-shell/shell-skeleton";
import { Card, EmptyState, PageHeader } from "@/components/app-shell/ui";
import { initials } from "@/features/home/rules";
import { relativeTime } from "@/lib/relative-time";
import { currentUser } from "@/server/auth";
import { inbox } from "@/server/club-world";

export const metadata: Metadata = { title: "Messages" };

async function Messages() {
  const user = await currentUser();
  if (!user) redirect("/auth/sign-in?redirectTo=/messages");
  const rows = await inbox(user.id);
  return (
    <>
      <PageHeader crumb="Messages" title="Messages" />
      <Card className="gap-0!">
        {rows.length === 0 ? (
          <EmptyState icon="mail" title="No messages yet">
            Managers you play online can message you here.
          </EmptyState>
        ) : (
          <ul className="divide-y divide-divider">
            {rows.map((m) => (
              <li key={m.id} className="flex items-start gap-3 py-3">
                <span className="flex size-9 shrink-0 items-center justify-center rounded-full bg-info text-xs font-bold text-white">
                  {initials(m.from ?? "M")}
                </span>
                <span className="flex-1">
                  <span
                    className={`block text-sm text-text ${m.readAt ? "font-semibold" : "font-bold"}`}
                  >
                    {m.from ?? "A manager"}
                  </span>
                  <span className="block text-[13px] whitespace-pre-wrap text-muted">{m.body}</span>
                </span>
                <span className="text-xs text-muted">{relativeTime(m.createdAt)}</span>
              </li>
            ))}
          </ul>
        )}
      </Card>
    </>
  );
}

export default function MessagesPage() {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <Messages />
    </Suspense>
  );
}

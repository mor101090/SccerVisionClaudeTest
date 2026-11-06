import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { Suspense } from "react";

import { PageSkeleton } from "@/components/app-shell/shell-skeleton";
import { Card, PageHeader } from "@/components/app-shell/ui";
import { ReplyForm } from "@/features/forum/forms";
import { initials } from "@/features/home/rules";
import { relativeTime } from "@/lib/relative-time";
import { forumThread } from "@/server/club-world";
import { loadClub } from "@/server/team-flow";

export const metadata: Metadata = { title: "Forum" };

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

async function Thread({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const { profile } = await loadClub(`/forum/${id}`);
  const t = UUID.test(id) ? await forumThread(id) : null;
  if (!t) notFound();
  return (
    <>
      <PageHeader crumb="Forum" title={t.title} sub={`${t.posts.length} posts`} />
      <ol className="flex flex-col gap-3">
        {t.posts.map((p, i) => (
          <li key={p.id}>
            <Card delay={i} className="gap-2.5!">
              <div className="flex items-center gap-2.5 text-[13px]">
                <span className="flex size-8 items-center justify-center rounded-full bg-info text-xs font-bold text-white">
                  {initials(p.author ?? "M")}
                </span>
                <span className="font-semibold text-text">{p.author ?? "A manager"}</span>
                <span className="text-muted">{relativeTime(p.createdAt)}</span>
              </div>
              <p className="text-sm leading-[22px] whitespace-pre-wrap text-text">{p.body}</p>
            </Card>
          </li>
        ))}
      </ol>
      {profile.isAdmin ? (
        <Card title="Reply">
          <ReplyForm threadId={t.id} />
        </Card>
      ) : (
        <p className="flex items-start gap-2 rounded-lg bg-info/16 p-3 text-[13px] leading-5 text-text">
          The forum is read-only for now: posts come from the Soccer Vision team.
        </p>
      )}
    </>
  );
}

export default function ThreadPage(props: PageProps<"/forum/[id]">) {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <Thread params={props.params} />
    </Suspense>
  );
}

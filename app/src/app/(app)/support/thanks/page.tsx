import type { Metadata } from "next";
import Link from "next/link";
import { Suspense } from "react";

import { AppIcon } from "@/components/app-shell/app-icon";
import { PageSkeleton } from "@/components/app-shell/shell-skeleton";
import { requireUser } from "@/server/auth";
import { confirmDonation, money } from "@/server/donations";
import { managerById } from "@/server/managers";

export const metadata: Metadata = { title: "Thank you" };

/** Stripe sends the manager back here with ?session_id=: the donation is checked with Stripe and recorded once. */
async function Thanks({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const user = await requireUser();
  const sid = (await searchParams).session_id;
  const result =
    typeof sid === "string" ? await confirmDonation(user.id, sid).catch(() => null) : null;
  const me = await managerById(user.id);
  const first = (me?.displayName ?? me?.username ?? "").split(/\s+/)[0];
  return (
    <div className="flex flex-1 items-start justify-center py-10 sm:py-20">
      <section className="flex w-full max-w-lg flex-col items-center gap-4 rounded-2xl border border-border bg-panel-2 p-8 text-center sm:p-10">
        <span className="flex size-18 items-center justify-center rounded-full bg-selected text-white">
          <AppIcon name="heart" size={32} />
        </span>
        {result ? (
          <>
            <h1 className="text-3xl font-extrabold text-text">
              Thank you{first ? `, ${first}` : ""}!
            </h1>
            <p className="text-muted">
              {money(result.amount, result.currency)} received. Stripe emails your receipt.
            </p>
          </>
        ) : (
          <>
            <h1 className="text-3xl font-extrabold text-text">We couldn’t confirm this payment</h1>
            <p className="text-muted">
              If you paid, it can take a moment: reload this page. Nothing is charged for a payment
              you cancelled.
            </p>
          </>
        )}
        <div className="flex flex-wrap justify-center gap-3 pt-2">
          <Link
            href="/profile"
            className="inline-flex h-11 items-center rounded-lg bg-accent px-5 font-semibold text-on-accent hover:brightness-110"
          >
            Back to profile
          </Link>
          <Link
            href="/support"
            className="inline-flex h-11 items-center rounded-lg border border-border bg-panel px-5 font-semibold text-text hover:bg-border"
          >
            Support page
          </Link>
        </div>
      </section>
    </div>
  );
}

export default function ThanksPage({ searchParams }: PageProps<"/support/thanks">) {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <Thanks searchParams={searchParams} />
    </Suspense>
  );
}

import type { Metadata } from "next";
import { Suspense } from "react";

import { PageSkeleton } from "@/components/app-shell/shell-skeleton";
import { Card, EmptyState, OpensLater, PageHeader } from "@/components/app-shell/ui";
import { euros } from "@/features/home/rules";
import { offersOf } from "@/server/club-world";
import { loadClub } from "@/server/team-flow";

export const metadata: Metadata = { title: "Transfer market" };

type Offer = Awaited<ReturnType<typeof offersOf>>["incoming"][number];

function OfferList({ offers, empty, verb }: { offers: Offer[]; empty: string; verb: string }) {
  if (!offers.length) return <p className="py-4 text-center text-[13px] text-muted">{empty}</p>;
  return (
    <ul className="flex flex-col gap-2.5">
      {offers.map((o) => (
        <li key={o.id} className="flex flex-col gap-1 rounded-xl bg-panel p-3.5">
          <span className="flex text-sm font-bold text-text">
            <span className="flex-1">{o.player}</span>
            <span className="text-goal">{euros(o.amount)}</span>
          </span>
          <span className="text-xs text-muted">
            {verb} {o.club} · expires{" "}
            {o.expiresAt.toLocaleDateString("en-GB", { day: "numeric", month: "short" })}
          </span>
        </li>
      ))}
    </ul>
  );
}

async function Transfer() {
  const { team } = await loadClub("/transfer");
  const { incoming, outgoing } = await offersOf(team.id);
  return (
    <>
      <PageHeader
        crumb="Transfer"
        title="Transfer market"
        sub={`${incoming.length} incoming · ${outgoing.length} outgoing offers`}
      />
      <OpensLater>
        Transfers are coming soon. Offers made to and by your club will show up here.
      </OpensLater>
      <div className="flex flex-col gap-5 lg:flex-row">
        <Card title="Market" icon="search" className="flex-1">
          <EmptyState icon="transfer" title="The market isn't open yet">
            When transfers launch, you&apos;ll browse and bid for players here.
          </EmptyState>
        </Card>
        <div className="flex flex-col gap-5 lg:w-[360px]">
          <Card title="Incoming offers" icon="transfer">
            <OfferList offers={incoming} empty="No offers for your players." verb="From" />
          </Card>
          <Card title="Your offers" icon="wallet">
            <OfferList offers={outgoing} empty="You have no open bids." verb="To" />
          </Card>
        </div>
      </div>
    </>
  );
}

export default function TransferPage() {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <Transfer />
    </Suspense>
  );
}

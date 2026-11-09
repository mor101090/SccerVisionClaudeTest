import type { Metadata } from "next";
import { Suspense } from "react";

import { AppIcon } from "@/components/app-shell/app-icon";
import { PageSkeleton } from "@/components/app-shell/shell-skeleton";
import { Card, PageHeader } from "@/components/app-shell/ui";
import { DonateForm } from "@/features/profile/donate-form";
import { requireUser } from "@/server/auth";
import { DONATION_AMOUNTS, donationsOpen } from "@/server/donations";
import { isSupporter } from "@/server/managers";

export const metadata: Metadata = { title: "Support SoccerVision" };

async function Support() {
  const user = await requireUser();
  const supporter = await isSupporter(user.id);
  return (
    <>
      <PageHeader
        crumb="Profile"
        title="Support SoccerVision"
        sub="A small team builds this game. Donations pay for the servers and the time to build what comes next."
      />
      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_440px]">
        <div className="flex min-w-0 flex-col gap-4">
          <Card title="Why donate" icon="heart">
            <p className="text-sm leading-6 text-text">
              No ads and no pay-to-win: a donation never makes your team stronger. It keeps the
              match engine running for everyone and funds the next features: leagues, cups and more
              online play.
            </p>
            {supporter && (
              <p className="flex items-center gap-2 text-[13px] text-goal">
                <AppIcon name="check" size={16} /> You already support the game. Thank you!
              </p>
            )}
          </Card>
          <Card title="Where it goes" icon="info">
            {[
              ["Servers, database and match replays", "Keeping every match and replay online"],
              ["The match engine and new features", "Leagues, cups, online play"],
              ["Design, testing and support", "So it works on every screen"],
            ].map(([t, s]) => (
              <div
                key={t}
                className="flex items-start gap-3 border-b border-divider pb-3 last:border-0 last:pb-0"
              >
                <AppIcon name="check" size={16} className="mt-0.5 text-goal" />
                <span>
                  <span className="block text-sm font-semibold text-text">{t}</span>
                  <span className="block text-xs text-muted">{s}</span>
                </span>
              </div>
            ))}
          </Card>
        </div>
        <div className="flex flex-col gap-4">
          <Card title="Your donation" icon="gift">
            <DonateForm amounts={DONATION_AMOUNTS} open={donationsOpen()} />
          </Card>
          <Card title="As a thank you" icon="star">
            <div className="flex items-start gap-3">
              <AppIcon name="check" size={16} className="mt-0.5 text-goal" />
              <span>
                <span className="block text-sm font-semibold text-text">Supporter badge</span>
                <span className="block text-xs text-muted">
                  On your profile, if you choose to show it
                </span>
              </span>
            </div>
          </Card>
        </div>
      </div>
    </>
  );
}

export default function SupportPage() {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <Support />
    </Suspense>
  );
}

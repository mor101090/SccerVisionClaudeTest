import type { Metadata } from "next";
import { Suspense } from "react";

import { AppIcon } from "@/components/app-shell/app-icon";
import { PageSkeleton } from "@/components/app-shell/shell-skeleton";
import { ActionLink, Card, PageHeader } from "@/components/app-shell/ui";
import { ClubSettingsForm, DeleteClub } from "@/features/profile/club-forms";
import { Crest } from "@/features/profile/ui";
import { matchFacts } from "@/server/managers";
import { loadClub } from "@/server/team-flow";
import { getSquad } from "@/server/teams";

export const metadata: Metadata = { title: "Club settings" };

function KitSwatch({ label, shirt, trim }: { label: string; shirt: string; trim: string }) {
  return (
    <div className="flex flex-1 basis-28 flex-col items-center gap-2 rounded-xl bg-panel p-4">
      <svg width="44" height="44" viewBox="0 0 24 24" aria-hidden>
        <path
          d="M20.4 6.6 16 3h-2a2 2 0 0 1-4 0H8L3.6 6.6l2 3.4L8 9v12h8V9l2.4 1z"
          fill={shirt}
          stroke={trim}
          strokeWidth="1.2"
          strokeLinejoin="round"
        />
      </svg>
      <span className="text-sm font-semibold text-text">{label}</span>
    </div>
  );
}

async function Settings() {
  const { user, team } = await loadClub("/club/settings");
  const [squad, { facts }] = await Promise.all([getSquad(team.id), matchFacts(user.id, team.name)]);
  const k = team.kits;
  return (
    <>
      <PageHeader
        crumb="Club"
        title="Club settings"
        sub={`Name, crest, ground and kits of ${team.name}. Players and tactics are edited in Squad and Tactics.`}
      />
      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_360px]">
        <div className="flex min-w-0 flex-col gap-4">
          <Card title="Identity" icon="shield">
            <div className="flex items-center gap-4">
              <Crest
                name={team.name}
                shortName={team.shortName}
                color={k?.home.shirt}
                ink={k?.home.number}
                size={72}
                square
              />
              <p className="text-[13px] text-muted">
                The crest uses your home shirt colour and short name.
              </p>
            </div>
            <ClubSettingsForm
              initial={{
                name: team.name,
                shortName: team.shortName,
                city: team.city ?? "",
                stadiumName: team.stadiumName ?? "",
                crestPrompt: team.crest?.prompt ?? "",
              }}
            />
          </Card>
          <Card title="Kits" icon="shirt" link={{ href: "/team/new/kits", label: "Edit kits" }}>
            {k ? (
              <div className="flex flex-wrap gap-3">
                <KitSwatch label="Home" shirt={k.home.shirt} trim={k.home.trim} />
                <KitSwatch label="Away" shirt={k.away.shirt} trim={k.away.trim} />
                <KitSwatch label="Keeper" shirt={k.keeper.shirt} trim={k.keeper.trim} />
              </div>
            ) : (
              <p className="text-[13px] text-muted">No kits yet.</p>
            )}
            <p className="text-xs text-muted">
              If your shirt clashes with the opponent’s, you play in the away kit.
            </p>
          </Card>
        </div>
        <div className="flex flex-col gap-4">
          <Card title="Squad" icon="users">
            <div className="flex justify-between text-[13px]">
              <span className="text-muted">Players</span>
              <span className="font-bold text-text">{squad.length}</span>
            </div>
            <div className="flex justify-between text-[13px]">
              <span className="text-muted">Matches played</span>
              <span className="font-bold text-text">{facts.length}</span>
            </div>
            <ActionLink href="/team/new/squad" variant="secondary">
              Edit line-up
            </ActionLink>
            <p className="text-xs text-muted">
              {team.regenerationsLeft > 0 && facts.length === 0
                ? `You can still ask for a new squad ${team.regenerationsLeft} more ${team.regenerationsLeft === 1 ? "time" : "times"} before your first match.`
                : "A new squad is only possible before the first match."}
            </p>
          </Card>
          <section
            aria-label="Danger zone"
            className="flex flex-col gap-3 rounded-2xl border border-foul/60 bg-panel-2 p-5"
          >
            <h2 className="flex items-center gap-2 text-[13px] font-bold tracking-[0.06em] text-foul uppercase">
              <AppIcon name="alert" size={16} /> Danger zone
            </h2>
            <p className="text-sm font-semibold text-text">Delete this club</p>
            <p className="text-xs text-muted">
              The club, squad and tactics go. Saved matches and your profile stay.
            </p>
            <DeleteClub clubName={team.name} matches={facts.length} />
          </section>
        </div>
      </div>
    </>
  );
}

export default function ClubSettingsPage() {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <Settings />
    </Suspense>
  );
}

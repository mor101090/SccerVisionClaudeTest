import type { Metadata } from "next";
import { Suspense } from "react";

import { PageSkeleton } from "@/components/app-shell/shell-skeleton";
import { Card, PageHeader } from "@/components/app-shell/ui";
import { EditProfileForm } from "@/features/profile/edit-form";
import { Avatar } from "@/features/profile/ui";
import { getCountries } from "@/server/countries";
import { myProfileData } from "@/server/profile-page";

export const metadata: Metadata = { title: "Edit profile" };

async function Edit() {
  const [d, countries] = await Promise.all([myProfileData("/profile/edit"), getCountries()]);
  const m = d.manager;
  return (
    <>
      <PageHeader
        crumb="Profile"
        title="Edit profile"
        sub="Your name and what other managers can see. Email, password and log-in methods are in Settings."
      />
      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_360px]">
        <EditProfileForm
          countries={countries}
          initial={{
            displayName: m.displayName ?? m.username,
            username: m.username,
            nationality: m.nationality ?? "",
            bio: m.bio ?? "",
            favouriteClub: m.favouriteClub ?? "",
            showProfile: m.showProfile,
            showHistory: m.showHistory,
          }}
        />
        <Card title="Preview" icon="external" className="h-fit">
          <div className="flex flex-col items-center gap-2 rounded-xl bg-panel p-4 text-center">
            <Avatar name={m.displayName ?? m.username} size={64} />
            <span className="text-lg font-extrabold text-text">{m.displayName ?? m.username}</span>
            <span className="text-xs text-muted">@{m.username}</span>
            {m.bio && <span className="text-[13px] text-muted">{m.bio}</span>}
          </div>
          <p className="text-xs text-muted">
            Your club name and crest are edited in Club settings.
          </p>
        </Card>
      </div>
    </>
  );
}

export default function EditProfilePage() {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <Edit />
    </Suspense>
  );
}

"use client";

import { useState } from "react";

import { Alert } from "@/components/ui/alert";
import { Button, ButtonLink } from "@/components/ui/button";
import { TextField } from "@/components/ui/text-field";
import { useAuthForm } from "@/features/auth/use-auth-form";
import type { TeamKits } from "@/server/db/schema";

import { saveClubAction } from "../actions";
import { NAME_POOLS } from "../generator";
import { DEFAULT_KITS, kitHex } from "../kits";
import { Crest, initialsOf, ScoreboardPreview } from "./pieces";

type Club = { name: string; shortName: string; namePools: string[]; crestPrompt: string };

/** Figma: Create team – 2 Club. */
export function ClubForm({ club, kits }: { club: Club | null; kits: TeamKits | null }) {
  const { state, pending, formProps } = useAuthForm(saveClubAction);
  const f = state?.fields ?? {};
  const [name, setName] = useState(club?.name ?? "");
  const [short, setShort] = useState(club?.shortName ?? "");
  const [pools, setPools] = useState<string[]>(club?.namePools ?? NAME_POOLS.map((p) => p.id));
  const home = (kits ?? DEFAULT_KITS).home;

  return (
    <form {...formProps} className="flex flex-col gap-[22px]">
      {state?.error && <Alert tone="error">{state.error}</Alert>}
      <div className="grid gap-8 md:grid-cols-[1fr_380px]">
        <div className="flex flex-col gap-[18px]">
          <TextField
            label="Team name"
            name="name"
            value={name}
            onChange={(e) => {
              setName(e.target.value);
              if (!club?.shortName)
                setShort(
                  e.target.value
                    .replace(/[^A-Za-z]/g, "")
                    .slice(0, 3)
                    .toUpperCase(),
                );
            }}
            placeholder="Red Rovers"
            error={f.name}
            required
          />
          <TextField
            label="Short name"
            name="shortName"
            value={short}
            onChange={(e) =>
              setShort(
                e.target.value
                  .replace(/[^A-Za-z]/g, "")
                  .slice(0, 3)
                  .toUpperCase(),
              )
            }
            placeholder="RED"
            maxLength={3}
            helper="3 letters, shown on the scoreboard."
            error={f.shortName}
            required
          />
          <fieldset className="flex flex-col gap-2">
            <legend className="mb-2 text-[13px] leading-[1.45] font-medium text-text">
              Player names from
            </legend>
            <div className="flex flex-wrap gap-1.5">
              {NAME_POOLS.map((p) => {
                const on = pools.includes(p.id);
                return (
                  <label
                    key={p.id}
                    className={`cursor-pointer rounded-[5px] border px-2 py-1.5 text-xs has-focus-visible:outline-2 has-focus-visible:outline-link ${
                      on
                        ? "border-chance bg-chance text-accent-ink"
                        : "border-border bg-panel text-text"
                    }`}
                  >
                    <input
                      type="checkbox"
                      name="namePools"
                      value={p.id}
                      checked={on}
                      onChange={() =>
                        setPools((xs) => (on ? xs.filter((x) => x !== p.id) : [...xs, p.id]))
                      }
                      className="sr-only"
                    />
                    {on ? "✓ " : ""}
                    {p.label}
                  </label>
                );
              })}
            </div>
            <p
              role={f.namePools ? "alert" : undefined}
              className={`text-xs ${f.namePools ? "text-foul" : "text-muted"}`}
            >
              {f.namePools ?? "Your 22 players get names from these countries. Pick at least one."}
            </p>
          </fieldset>
        </div>

        <div className="flex flex-col gap-3.5">
          <span className="text-[13px] font-medium text-text">Club crest</span>
          <div className="flex items-center gap-3.5">
            <div className="flex size-28 shrink-0 items-center justify-center rounded-xl border-[1.5px] border-border bg-panel">
              <Crest kit={home} initials={initialsOf(name || "Club")} size={56} />
            </div>
            <p className="text-[13px] text-muted">
              Describe it and we&apos;ll draw it, or keep the simple shield.
            </p>
          </div>
          <TextField
            label="What should the crest look like?"
            name="crestPrompt"
            defaultValue={club?.crestPrompt}
            placeholder="A red fox head on a shield, bold, flat"
            maxLength={120}
            error={f.crestPrompt}
          />
          <Button variant="secondary" disabled title="The crest server is not connected yet">
            ✨ Create crest
          </Button>
          <p className="text-xs text-muted">
            The crest server isn&apos;t connected yet. We keep your description and your club plays
            with the shield for now.
          </p>
          <div className="flex flex-col gap-2">
            <span className="text-[13px] font-medium text-text">Scoreboard preview</span>
            <ScoreboardPreview home={short} homeColor={kitHex(home.shirt)} />
          </div>
        </div>
      </div>
      <div className="grid gap-3 sm:grid-cols-2">
        <ButtonLink href="/team/new/manager" variant="secondary">
          ← Back
        </ButtonLink>
        <Button type="submit" disabled={pending}>
          {pending ? "Saving…" : "Continue"}
        </Button>
      </div>
    </form>
  );
}

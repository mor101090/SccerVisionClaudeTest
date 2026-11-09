"use client";

import { useState } from "react";

import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { TextField } from "@/components/ui/text-field";
import { useAuthForm } from "@/features/auth/use-auth-form";

import { donateAction } from "./actions";

/** Figma "Support the game – donate": pick an amount (or type one), an optional message, then Stripe Checkout. */
export function DonateForm({ amounts, open }: { amounts: readonly number[]; open: boolean }) {
  const { state, pending, formProps } = useAuthForm(donateAction);
  const [amount, setAmount] = useState<number>(amounts[1] ?? 5);
  const [other, setOther] = useState("");
  const shown = other ? Number(other.replace(",", ".")) : amount;
  const f = state?.fields ?? {};
  return (
    <form {...formProps} className="flex flex-col gap-4">
      {state?.error && <Alert tone="error">{state.error}</Alert>}
      <fieldset className="flex flex-col gap-2">
        <legend className="mb-2 text-[13px] text-text">Amount</legend>
        <div className="grid grid-cols-4 gap-2">
          {amounts.map((a) => (
            <label key={a} className="cursor-pointer">
              <input
                type="radio"
                name="amount"
                value={a}
                checked={!other && amount === a}
                onChange={() => {
                  setAmount(a);
                  setOther("");
                }}
                className="peer sr-only"
              />
              <span className="flex h-12 items-center justify-center rounded-xl border border-border bg-panel text-lg font-extrabold text-text peer-checked:border-selected peer-checked:bg-selected peer-checked:text-white peer-focus-visible:outline-2 peer-focus-visible:outline-offset-2 peer-focus-visible:outline-link">
                €{a}
              </span>
            </label>
          ))}
        </div>
      </fieldset>
      <TextField
        label="Other amount in euros (optional)"
        name="other"
        inputMode="decimal"
        value={other}
        onChange={(e) => setOther(e.target.value.replace(/[^0-9.,]/g, ""))}
        helper="From €1 to €500"
        error={f.other}
      />
      <TextField
        label="Message to the team (optional)"
        name="message"
        maxLength={300}
        defaultValue={state?.values?.message}
      />
      <Checkbox name="showBadge" defaultChecked>
        Show the Supporter badge on my profile
      </Checkbox>
      <Button type="submit" disabled={pending || !open || !Number.isFinite(shown) || shown < 1}>
        {pending
          ? "Opening the payment page…"
          : `Donate €${Number.isFinite(shown) && shown > 0 ? shown : "…"}`}
      </Button>
      <p className="text-xs leading-5 text-muted">
        {open
          ? "Secure payment by Stripe: card, Apple Pay or Google Pay where available. Stripe emails your receipt. A donation never unlocks a gameplay advantage."
          : "Donations aren't open yet: the payment page is still being set up."}
      </p>
    </form>
  );
}

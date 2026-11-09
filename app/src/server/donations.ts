import "server-only";

import { serverEnv as env } from "@/env";

import { db, schema } from "./db";

const { donations } = schema;

/**
 * Donations through Stripe Checkout (REST API, no SDK). One-time payments in euros. The donation is recorded when
 * the manager comes back to /support/thanks: the Checkout session is read back from Stripe and must be paid and
 * belong to this manager. The session id is unique, so a reload records it once.
 */

export const DONATION_AMOUNTS = [3, 5, 10, 25] as const;
export const MIN_EUROS = 1;
export const MAX_EUROS = 500;

export function donationsOpen(): boolean {
  return Boolean(env.STRIPE_SECRET_KEY);
}

async function stripe(
  path: string,
  init: { method?: "GET" | "POST"; form?: Record<string, string> } = {},
) {
  const key = env.STRIPE_SECRET_KEY;
  if (!key) throw new Error("Donations are not open yet.");
  const res = await fetch(`https://api.stripe.com/v1/${path}`, {
    method: init.method ?? "GET",
    headers: {
      Authorization: `Bearer ${key}`,
      ...(init.form ? { "Content-Type": "application/x-www-form-urlencoded" } : {}),
    },
    body: init.form ? new URLSearchParams(init.form) : undefined,
    cache: "no-store",
  });
  const body = (await res.json()) as Record<string, unknown> & { error?: { message?: string } };
  if (!res.ok) throw new Error(body.error?.message ?? `Stripe answered ${res.status}`);
  return body;
}

/** A Checkout session for `euros`; returns the URL to send the manager to. */
export async function startDonation(input: {
  userId: string;
  euros: number;
  message: string;
  showBadge: boolean;
  origin: string;
}): Promise<string> {
  const session = await stripe("checkout/sessions", {
    method: "POST",
    form: {
      mode: "payment",
      "line_items[0][quantity]": "1",
      "line_items[0][price_data][currency]": "eur",
      "line_items[0][price_data][unit_amount]": String(Math.round(input.euros * 100)),
      "line_items[0][price_data][product_data][name]": "Support SoccerVision",
      "line_items[0][price_data][product_data][description]":
        "A donation to keep the servers on and the game in development. No gameplay advantage.",
      client_reference_id: input.userId,
      "metadata[show_badge]": input.showBadge ? "1" : "0",
      ...(input.message ? { "metadata[message]": input.message.slice(0, 450) } : {}),
      success_url: `${input.origin}/support/thanks?session_id={CHECKOUT_SESSION_ID}`,
      cancel_url: `${input.origin}/support`,
    },
  });
  const url = session.url;
  if (typeof url !== "string") throw new Error("Stripe didn't return a payment page.");
  return url;
}

export type DonationResult = { amount: number; currency: string };

/** Reads a finished Checkout session back and records it once. Null: not paid, or not this manager's. */
export async function confirmDonation(
  userId: string,
  sessionId: string,
): Promise<DonationResult | null> {
  if (!/^cs_[A-Za-z0-9_]+$/.test(sessionId)) return null;
  const s = await stripe(`checkout/sessions/${sessionId}`);
  if (s.client_reference_id !== userId || s.payment_status !== "paid") return null;
  const amount = Number(s.amount_total) || 0;
  const currency = String(s.currency ?? "eur");
  const meta = (s.metadata ?? {}) as Record<string, string>;
  await db()
    .insert(donations)
    .values({
      userId,
      stripeSessionId: sessionId,
      amount,
      currency,
      showBadge: meta.show_badge !== "0",
    })
    .onConflictDoNothing({ target: donations.stripeSessionId });
  return { amount, currency };
}

export function money(cents: number, currency: string): string {
  return new Intl.NumberFormat("en-IE", {
    style: "currency",
    currency: currency.toUpperCase(),
  }).format(cents / 100);
}

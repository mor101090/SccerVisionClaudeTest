import "server-only";

import { cacheLife } from "next/cache";
import { headers } from "next/headers";
import { z } from "zod";

import {
  FALLBACK_CODES,
  guessCountry,
  NOT_A_NATIONALITY,
  type Country,
} from "@/features/team/countries";

const codesSchema = z.record(z.string(), z.string().min(1).max(80));

/** flagcdn.com's country list (code -> English name), cached for a day. Throws when unreachable (not cached). */
async function flagcdnCountries(): Promise<Country[]> {
  "use cache";
  cacheLife("days");
  const res = await fetch("https://flagcdn.com/en/codes.json", {
    signal: AbortSignal.timeout(5000),
  });
  if (!res.ok) throw new Error(`flagcdn ${res.status}`);
  const codes = codesSchema.parse(await res.json());
  return Object.entries(codes)
    .map(([code, name]) => ({ code: code.toUpperCase(), name }))
    .filter((c) => !c.code.startsWith("US-") && !NOT_A_NATIONALITY.has(c.code))
    .sort((a, b) => a.name.localeCompare(b.name, "en"));
}

function fallbackCountries(): Country[] {
  const names = new Intl.DisplayNames(["en"], { type: "region" });
  return FALLBACK_CODES.filter((c) => !NOT_A_NATIONALITY.has(c))
    .map((code) => ({ code, name: names.of(code) ?? code }))
    .sort((a, b) => a.name.localeCompare(b.name, "en"));
}

/** Every nationality a manager can pick, sorted by name: the API's list, or the built-in one if it is down. */
export async function getCountries(): Promise<Country[]> {
  try {
    return await flagcdnCountries();
  } catch {
    return fallbackCountries();
  }
}

export async function isCountry(code: string): Promise<boolean> {
  return (await getCountries()).some((c) => c.code === code);
}

/** This request's country by IP (Vercel's geolocation header) or browser language: see guessCountry. */
export async function countryFromRequest(countries: Country[]): Promise<string | null> {
  const h = await headers();
  return guessCountry(h.get("x-vercel-ip-country"), h.get("accept-language"), countries);
}

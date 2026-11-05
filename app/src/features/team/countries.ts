/**
 * Countries for the manager's nationality. The list and the flag images come from flagcdn.com
 * (server/countries.ts); FALLBACK_CODES is only used when that API can't be reached.
 */
export type Country = { code: string; name: string };

/** Flag image for a country code ("IL", "GB-ENG"), from flagcdn.com. */
export function flagUrl(code: string): string {
  return `https://flagcdn.com/${code.toLowerCase()}.svg`;
}

/**
 * The visitor's country without asking for location permission: the IP geolocation header (Vercel's
 * `x-vercel-ip-country`), else the region of the browser's first language ("he-IL" -> IL). Null when neither is a
 * country of the list.
 */
export function guessCountry(
  ipCountry: string | null | undefined,
  acceptLanguage: string | null | undefined,
  countries: Country[],
): string | null {
  const fromLanguage = acceptLanguage?.trim().match(/^[a-z]{2,3}-([a-z]{2})\b/i)?.[1];
  for (const code of [ipCountry, fromLanguage].map((c) => c?.toUpperCase()))
    if (code && countries.some((c) => c.code === code)) return code;
  return null;
}

/** Codes of flagcdn's list that are not a nationality to pick: supranational bodies and uninhabited territories. */
export const NOT_A_NATIONALITY = new Set(["EU", "UN", "PS", "AQ", "BV", "HM", "GS", "TF", "UM"]);

/** ISO 3166-1 alpha-2, for when flagcdn is down (names from Intl.DisplayNames, on the server). */
export const FALLBACK_CODES = (
  "AD AE AF AG AI AL AM AO AR AS AT AU AW AX AZ BA BB BD BE BF BG BH BI BJ BL BM BN BO BQ BR BS BT BW BY BZ " +
  "CA CC CD CF CG CH CI CK CL CM CN CO CR CU CV CW CX CY CZ DE DJ DK DM DO DZ EC EE EG EH ER ES ET FI FJ FK FM FO FR " +
  "GA GB GD GE GF GG GH GI GL GM GN GP GQ GR GT GU GW GY HK HN HR HT HU ID IE IL IM IN IQ IR IS IT JE JM JO " +
  "JP KE KG KH KI KM KN KP KR KW KY KZ LA LB LC LI LK LR LS LT LU LV LY MA MC MD ME MF MG MH MK ML MM MN MO MP MQ MR " +
  "MS MT MU MV MW MX MY MZ NA NC NE NF NG NI NL NO NP NR NU NZ OM PA PE PF PG PH PK PL PM PN PR PT PW PY QA RE RO " +
  "RS RU RW SA SB SC SD SE SG SH SI SJ SK SL SM SN SO SR SS ST SV SX SY SZ TC TD TG TH TJ TK TL TM TN TO TR TT TV " +
  "TW TZ UA UG US UY UZ VA VC VE VG VI VN VU WF WS XK YE YT ZA ZM ZW"
).split(" ");

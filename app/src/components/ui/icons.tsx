/**
 * Stroke icons exported from Figma (Auth & Team Creation — Components). Paths are unchanged;
 * the stroke is currentColor so each state takes its color from a palette token.
 * Brand logos (Google, Facebook) are files in public/auth/ and keep their own colors.
 */
type IconProps = { className?: string };

export function EyeIcon({ className }: IconProps) {
  return (
    <svg width="18" height="18" viewBox="0 0 18 18" fill="none" aria-hidden className={className}>
      <path
        d="M0.75 9C0.75 9 3.75 3 9 3C14.25 3 17.25 9 17.25 9C17.25 9 14.25 15 9 15C3.75 15 0.75 9 0.75 9Z"
        stroke="currentColor"
        strokeWidth="1.5"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <path
        d="M11.25 9C11.25 10.2426 10.2426 11.25 9 11.25C7.75736 11.25 6.75 10.2426 6.75 9C6.75 7.75736 7.75736 6.75 9 6.75C10.2426 6.75 11.25 7.75736 11.25 9Z"
        stroke="currentColor"
        strokeWidth="1.5"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

/** Passkey (a key). Not in the Figma export yet; drawn in the same 18px, 1.5px-stroke style. */
export function PasskeyIcon({ className }: IconProps) {
  return (
    <svg width="18" height="18" viewBox="0 0 18 18" fill="none" aria-hidden className={className}>
      <circle cx="6" cy="9" r="3.75" stroke="currentColor" strokeWidth="1.5" />
      <path
        d="M9.75 9H16.5M14.25 9V11.25M16.5 9V10.5"
        stroke="currentColor"
        strokeWidth="1.5"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

export function CheckIcon({ className }: IconProps) {
  return (
    <svg width="12" height="12" viewBox="0 0 12 12" fill="none" aria-hidden className={className}>
      <path
        d="M2 6.25L4.5 8.75L10 3.25"
        stroke="currentColor"
        strokeWidth="1.75"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

export function FeatureCheckIcon({ className }: IconProps) {
  return (
    <svg width="14" height="14" viewBox="0 0 14 14" fill="none" aria-hidden className={className}>
      <path
        d="M2.3335 7.29167L5.25016 10.2083L11.6668 3.79167"
        stroke="currentColor"
        strokeWidth="1.86667"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

const CIRCLE =
  "M16.5 9C16.5 13.1421 13.1421 16.5 9 16.5C4.85786 16.5 1.5 13.1421 1.5 9C1.5 4.85786 4.85786 1.5 9 1.5C13.1421 1.5 16.5 4.85786 16.5 9Z";

export function ErrorIcon({ className }: IconProps) {
  return (
    <svg width="18" height="18" viewBox="0 0 18 18" fill="none" aria-hidden className={className}>
      <path d={CIRCLE} stroke="currentColor" strokeWidth="1.5" />
      <path
        d="M9 5.25V9.75M9 12.375V12.75"
        stroke="currentColor"
        strokeWidth="1.65"
        strokeLinecap="round"
      />
    </svg>
  );
}

export function InfoIcon({ className }: IconProps) {
  return (
    <svg width="18" height="18" viewBox="0 0 18 18" fill="none" aria-hidden className={className}>
      <path d={CIRCLE} stroke="currentColor" strokeWidth="1.5" />
      <path
        d="M9 8.25V12.75M9 5.625V6"
        stroke="currentColor"
        strokeWidth="1.65"
        strokeLinecap="round"
      />
    </svg>
  );
}

export function SuccessIcon({ className }: IconProps) {
  return (
    <svg width="18" height="18" viewBox="0 0 18 18" fill="none" aria-hidden className={className}>
      <path d={CIRCLE} stroke="currentColor" strokeWidth="1.5" />
      <path
        d="M5.625 9.375L7.875 11.625L12.375 6.75"
        stroke="currentColor"
        strokeWidth="1.65"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

/** Track in color/border, arc in color/accent (Figma: connecting dialog / spinner). */
export function SpinnerIcon({ className }: IconProps) {
  return (
    <svg width="28" height="28" viewBox="0 0 28 28" fill="none" aria-hidden className={className}>
      <path
        d="M25 14C25 20.0751 20.0751 25 14 25C7.92487 25 3 20.0751 3 14C3 7.92487 7.92487 3 14 3C20.0751 3 25 7.92487 25 14Z"
        className="stroke-border"
        strokeWidth="3"
      />
      <path
        d="M14 3C15.4445 3 16.8749 3.28452 18.2095 3.83733C19.5441 4.39013 20.7567 5.20038 21.7782 6.22183C22.7996 7.24327 23.6099 8.4559 24.1627 9.79048C24.7155 11.1251 25 12.5555 25 14"
        className="stroke-accent"
        strokeWidth="3"
        strokeLinecap="round"
      />
    </svg>
  );
}

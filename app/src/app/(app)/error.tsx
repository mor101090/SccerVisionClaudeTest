"use client";

import Link from "next/link";
import { useEffect } from "react";

import { primaryLink, secondaryLink, StatusPage } from "@/components/ui/status-page";
import { clientEnv } from "@/env";

/** Figma "Error – something went wrong": any page in the app that throws. */
export default function AppError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error(error);
  }, [error]);
  const email = clientEnv.NEXT_PUBLIC_SUPPORT_EMAIL;
  return (
    <StatusPage
      title="Something went wrong on our side"
      actions={
        <>
          <button type="button" onClick={reset} className={primaryLink}>
            Try again
          </button>
          {email ? (
            <a
              href={`mailto:${email}?subject=${encodeURIComponent(`Error ${error.digest ?? ""}`)}`}
              className={secondaryLink}
            >
              Contact support
            </a>
          ) : (
            <Link href="/home" className={secondaryLink}>
              Back to Home
            </Link>
          )}
        </>
      }
    >
      <p>
        Your club and matches are safe. Try again; if it keeps happening, tell us the code below.
      </p>
      {error.digest && <p className="mt-2 text-xs">Error code {error.digest}</p>}
    </StatusPage>
  );
}

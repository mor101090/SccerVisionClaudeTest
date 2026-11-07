import Link from "next/link";

/**
 * Figma "Error – page not found (404)" / "Error – something went wrong": a centred message with the way out.
 * Plain links (no client code) so it renders even when the rest of the page failed.
 */
export function StatusPage({
  code,
  title,
  children,
  actions,
}: {
  code?: string;
  title: string;
  children: React.ReactNode;
  actions: React.ReactNode;
}) {
  return (
    <div className="flex flex-1 flex-col items-center justify-center gap-4 px-4 py-20 text-center">
      {code && <p className="text-7xl font-extrabold text-selected tabular-nums">{code}</p>}
      <h1 className="text-3xl font-extrabold text-text">{title}</h1>
      <div className="max-w-md text-muted">{children}</div>
      <div className="flex flex-wrap justify-center gap-3 pt-2">{actions}</div>
    </div>
  );
}

export const primaryLink =
  "inline-flex h-11 items-center rounded-lg bg-accent px-5 font-semibold text-on-accent hover:brightness-110 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-link";
export const secondaryLink =
  "inline-flex h-11 items-center rounded-lg border border-border bg-panel-2 px-5 font-semibold text-text hover:bg-panel focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-link";

export function NotFoundView() {
  return (
    <StatusPage
      code="404"
      title="This page went out of play"
      actions={
        <>
          <Link href="/home" className={primaryLink}>
            Back to Home
          </Link>
          <Link href="/search" className={secondaryLink}>
            Search
          </Link>
        </>
      }
    >
      The link may be old or mistyped. A club or match may also have been deleted by its manager.
    </StatusPage>
  );
}

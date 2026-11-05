/** "SoccerVision" wordmark (Figma: Auth Header / Brand). */
export function Brand() {
  return (
    // Plain <a>: "/" is the classic game, a static page outside the Next.js router.
    <a
      href="/"
      className="text-xl leading-[1.45] font-extrabold tracking-[0.5px] whitespace-nowrap"
      aria-label="SoccerVision home"
    >
      <span className="text-text">Soccer</span>
      <span className="text-accent">Vision</span>
    </a>
  );
}

/** Header bar shell (Figma: Auth Header): gradient panel → panel-2, a 0.5px ButtonText divider under it (no box). */
export function HeaderBar({ children }: { children: React.ReactNode }) {
  return (
    <header className="flex min-h-[57px] items-center gap-4 border-b-[0.5px] border-button-text bg-linear-to-b from-panel to-panel-2 px-4 py-2.5 text-[15px] leading-[1.45] text-text">
      {children}
    </header>
  );
}

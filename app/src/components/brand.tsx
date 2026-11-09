import Image from "next/image";

import logo from "../../public/brand/logo.png";
import mark from "../../public/brand/mark.png";

/** Soccer Vision logo (Figma: Auth Header / Brand). Drawn for dark backgrounds. */
export function Brand() {
  return (
    // Plain <a>: "/" is the classic game, a static page outside the Next.js router.
    <a href="/" className="shrink-0" aria-label="Soccer Vision home">
      <Image src={logo} alt="" priority className="h-8 w-auto" />
    </a>
  );
}

/** The full logo at a given height (Tailwind height class), e.g. the home page title. */
export function BrandLogo({ className, priority }: { className: string; priority?: boolean }) {
  return (
    <Image src={logo} alt="Soccer Vision" priority={priority} className={`w-auto ${className}`} />
  );
}

/** The logo's eye on its own, for small square spots (dialogs, account cards). */
export function BrandMark({ size }: { size: number }) {
  return <Image src={mark} alt="" width={size} height={size} className="shrink-0" />;
}

/** Header bar shell (Figma: Auth Header): gradient panel → panel-2, a 0.5px ButtonText divider under it (no box). */
export function HeaderBar({ children }: { children: React.ReactNode }) {
  return (
    <header className="flex min-h-[57px] items-center gap-4 border-b-[0.5px] border-button-text bg-linear-to-b from-panel to-panel-2 px-4 py-2.5 text-[15px] leading-[1.45] text-text">
      {children}
    </header>
  );
}

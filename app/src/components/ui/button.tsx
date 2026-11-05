import Link from "next/link";

type Variant = "primary" | "secondary" | "text";

/**
 * Figma "Button Large": 44px, radius 8, 15px semibold label.
 * primary   DarkBlueMor fill, ButtonText label (reference: Login)
 * secondary panel-2 fill, color/border stroke (dialog Cancel)
 * text      no fill, link-colored label ("← Back to login", "Resend code")
 */
const dim = "disabled:opacity-45 aria-disabled:opacity-45";
const variants: Record<Variant, string> = {
  primary: `bg-button text-button-text enabled:hover:bg-border ${dim}`,
  secondary: `border border-border bg-panel-2 text-text enabled:hover:bg-panel ${dim}`,
  // Countdown labels ("Resend code in 0:52") stay full color while disabled, as in the design.
  text: "text-link enabled:hover:underline",
};

export function buttonClass(variant: Variant = "primary", extra = ""): string {
  return `inline-flex h-11 w-full items-center justify-center gap-2 rounded-lg px-4 text-[15px] leading-[1.45] font-semibold transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-link disabled:cursor-not-allowed aria-disabled:cursor-not-allowed ${variants[variant]} ${extra}`;
}

export function Button({
  variant = "primary",
  className = "",
  ...rest
}: React.ButtonHTMLAttributes<HTMLButtonElement> & { variant?: Variant }) {
  return <button type="button" className={buttonClass(variant, className)} {...rest} />;
}

export function ButtonLink({
  variant = "primary",
  className = "",
  ...rest
}: React.ComponentProps<typeof Link> & { variant?: Variant }) {
  // `enabled:` does not match <a>, so links get their hover state here.
  const hover = {
    primary: "hover:bg-border",
    secondary: "hover:bg-panel",
    text: "hover:underline",
  }[variant];
  return <Link className={buttonClass(variant, `${hover} ${className}`)} {...rest} />;
}

import { Brand, HeaderBar } from "@/components/brand";
import { clientEnv } from "@/env";

/**
 * Auth screens use the Figma "Auth Header" (brand + Contact Us) instead of the site navigation. Contact Us mails
 * NEXT_PUBLIC_SUPPORT_EMAIL; without one there is no link (it used to point at a private repository: a 404).
 */
export default function AuthLayout({ children }: { children: React.ReactNode }) {
  const support = clientEnv.NEXT_PUBLIC_SUPPORT_EMAIL;
  return (
    <>
      <HeaderBar>
        <Brand />
        <span className="flex-1" />
        {support && (
          <a
            href={`mailto:${support}`}
            className="inline-flex min-h-6 items-center text-text hover:underline"
          >
            Contact Us
          </a>
        )}
      </HeaderBar>
      {children}
    </>
  );
}

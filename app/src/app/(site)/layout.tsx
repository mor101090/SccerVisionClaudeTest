import { SiteHeader } from "@/components/site-header";
import { PasskeyOffer } from "@/features/auth/passkey-offer";

export default function SiteLayout({ children }: { children: React.ReactNode }) {
  return (
    <>
      <SiteHeader />
      {children}
      <PasskeyOffer />
    </>
  );
}

import { Brand, HeaderBar } from "@/components/brand";

/** Auth screens use the Figma "Auth Header" (brand + Contact Us) instead of the site navigation. */
export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return (
    <>
      <HeaderBar>
        <Brand />
        <span className="flex-1" />
        <a
          href="https://github.com/SoccerVision/soccer-vision/issues"
          target="_blank"
          rel="noreferrer"
          className="text-text hover:underline"
        >
          Contact Us
        </a>
      </HeaderBar>
      {children}
    </>
  );
}

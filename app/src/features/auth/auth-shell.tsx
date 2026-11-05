import Image from "next/image";

import { FeatureCheckIcon } from "@/components/ui/icons";

import heroLogin from "../../../public/auth/hero-login.jpg";
import heroRecovery from "../../../public/auth/hero-recovery.jpg";
import heroSignup from "../../../public/auth/hero-signup.jpg";
import heroSocial from "../../../public/auth/hero-social.jpg";
import heroVerify from "../../../public/auth/hero-verify.png";

/** Left-hand "Auth Hero" variants, one per screen group in Figma. */
const HEROES = {
  login: {
    image: heroLogin,
    eyebrow: "FOOTBALL MANAGER · LIVE ENGINE",
    title: ["Build your club.", "Watch it play."],
    body: "Pick your kits, get a generated 22-player squad with real attributes, and watch the engine play your tactics in real time.",
  },
  recovery: {
    image: heroRecovery,
    eyebrow: "ACCOUNT RECOVERY",
    title: ["Back in the", "dugout soon."],
    body: "We'll get you a new password in a minute. Your squad and tactics are safe.",
  },
  signup: {
    image: heroSignup,
    eyebrow: "NEW MANAGER",
    title: ["Your club", "starts here."],
    body: "Create your account, name your club, pick your kits — and meet the 22 players generated just for you.",
  },
  verify: {
    image: heroVerify,
    eyebrow: "ALMOST READY",
    title: ["One code", "from kick-off."],
    body: "Confirm your email and your club is ready to be built.",
  },
  social: {
    image: heroSocial,
    eyebrow: "GOOGLE & FACEBOOK",
    title: ["One tap to", "the touchline."],
    body: "Sign in with the account you already use. Your club, kits and squad stay with you on every device.",
  },
  link: {
    image: heroSocial,
    eyebrow: "GOOGLE & FACEBOOK",
    title: ["One tap to", "the touchline."],
    body: "Link Google or Facebook once, then skip the password every time you come back.",
  },
} as const;

export type HeroKind = keyof typeof HEROES;

const FEATURES = [
  "22 generated players: 2 keepers, defenders, midfielders, wingers, strikers",
  "Your own kits and crest",
  "Live matches against the computer or a friend",
];

function Hero({ kind }: { kind: HeroKind }) {
  const h = HEROES[kind];
  return (
    <section aria-hidden className="relative hidden overflow-hidden bg-panel lg:block">
      <Image
        src={h.image}
        alt=""
        fill
        priority
        sizes="50vw"
        placeholder="blur"
        className="object-cover"
      />
      {/* Photo overlay (Figma: overlay) — raw hex is allowed for photo treatments. */}
      <div className="absolute inset-0 bg-[linear-gradient(180deg,rgb(15_20_25/0.05)_0%,rgb(15_20_25/0.35)_45%,rgb(15_20_25/0.88)_72%,rgb(15_20_25/0.98)_100%)]" />
      <div className="absolute top-[50.5%] left-16 flex w-[min(560px,calc(100%-128px))] flex-col gap-4">
        <p className="text-xs leading-[1.45] font-bold tracking-[1.6px] text-accent">{h.eyebrow}</p>
        <h2 className="text-[46px] leading-[1.08] font-extrabold text-text">
          {h.title[0]}
          <br />
          {h.title[1]}
        </h2>
        <p className="text-base leading-[1.45] text-muted">{h.body}</p>
        <ul className="flex flex-col gap-2.5 pt-2">
          {FEATURES.map((f) => (
            <li key={f} className="flex items-center gap-2.5 text-sm leading-[1.45] text-text">
              <span className="flex size-[22px] shrink-0 items-center justify-center rounded-full bg-accent">
                <FeatureCheckIcon className="text-accent" />
              </span>
              {f}
            </li>
          ))}
        </ul>
      </div>
    </section>
  );
}

/** Two-column auth screen: hero (desktop only) + form side (Figma: body 1440×843). */
export function AuthShell({ hero, children }: { hero: HeroKind; children: React.ReactNode }) {
  return (
    <div className="grid flex-1 lg:grid-cols-2">
      <Hero kind={hero} />
      <main className="flex items-center justify-center bg-panel-2 px-4 py-12">
        <div className="flex w-full max-w-[400px] flex-col gap-[18px]">{children}</div>
      </main>
    </div>
  );
}

/** Title block (Figma: title): 30px extra-bold heading, 15px muted subtitle, 6px gap. */
export function AuthTitle({ title, children }: { title: string; children?: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-1.5">
      <h1 className="text-[30px] leading-[1.2] font-extrabold text-text">{title}</h1>
      {children && <p className="text-[15px] leading-[1.45] text-muted">{children}</p>}
    </div>
  );
}

/** Centered 14px footer line with a link (e.g. "Don't have an account yet? Sign up"). */
export function AuthFooter({ children }: { children: React.ReactNode }) {
  return <p className="text-center text-sm leading-[1.45] text-muted">{children}</p>;
}

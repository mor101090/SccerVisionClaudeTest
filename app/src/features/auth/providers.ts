/** Social sign-in providers enabled in Supabase Auth. */
export type SocialProvider = "google" | "facebook" | "discord";

export const SOCIAL_PROVIDERS: readonly SocialProvider[] = ["google", "facebook", "discord"];

export const PROVIDER_LABEL: Record<SocialProvider, string> = {
  google: "Google",
  facebook: "Facebook",
  discord: "Discord",
};

export const isSocialProvider = (p: string): p is SocialProvider =>
  (SOCIAL_PROVIDERS as readonly string[]).includes(p);

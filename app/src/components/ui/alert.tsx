import { ErrorIcon, InfoIcon, SuccessIcon } from "./icons";

type Tone = "error" | "success" | "info";

const tones: Record<Tone, { box: string; icon: string; Icon: typeof ErrorIcon }> = {
  error: { box: "border-foul/45 bg-foul/10", icon: "text-foul", Icon: ErrorIcon },
  success: { box: "border-goal/45 bg-goal/10", icon: "text-goal", Icon: SuccessIcon },
  info: { box: "border-info/45 bg-info/10", icon: "text-info", Icon: InfoIcon },
};

/** Figma "Alert": 8px radius, 10/12 padding, 18px icon, 14px text. */
export function Alert({ tone, children }: { tone: Tone; children: React.ReactNode }) {
  const { box, icon, Icon } = tones[tone];
  return (
    <div
      role={tone === "error" ? "alert" : "status"}
      className={`flex gap-2.5 rounded-lg border px-3 py-2.5 text-sm leading-[1.45] text-text ${box}`}
    >
      <Icon className={`mt-px shrink-0 ${icon}`} />
      <div className="min-w-0">{children}</div>
    </div>
  );
}

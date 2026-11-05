import { CheckIcon } from "./icons";

type Props = Omit<React.InputHTMLAttributes<HTMLInputElement>, "type" | "className"> & {
  children: React.ReactNode;
};

/** Figma "Checkbox": 18px box, radius 4. Checked: BrightPurpleMor fill + accent-ink check (reference: Login). */
export function Checkbox({ children, ...rest }: Props) {
  return (
    <label className="flex cursor-pointer items-center gap-2.5 text-sm leading-[1.45] text-text">
      <span className="relative inline-flex size-[18px] shrink-0">
        <input
          type="checkbox"
          className="peer size-[18px] cursor-pointer appearance-none rounded border border-border bg-panel-2 checked:border-link checked:bg-link focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-link"
          {...rest}
        />
        <CheckIcon className="pointer-events-none absolute top-[3px] left-[3px] hidden text-accent-ink peer-checked:block" />
      </span>
      <span>{children}</span>
    </label>
  );
}

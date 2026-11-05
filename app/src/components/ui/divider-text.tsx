/** Figma "Divider Text": two 1px color/divider rules around 13px muted text. */
export function DividerText({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex items-center gap-3 text-[13px] leading-[1.45] text-muted">
      <span className="h-px flex-1 bg-divider" />
      <span>{children}</span>
      <span className="h-px flex-1 bg-divider" />
    </div>
  );
}

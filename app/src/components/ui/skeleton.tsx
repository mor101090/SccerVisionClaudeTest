/**
 * Loading placeholders. Every Suspense boundary with visible content shows one shaped like what it will render,
 * so a page never shows only its title while data loads. Pulses unless the user asked for reduced motion.
 */

/** One grey block. Size and shape come from `className` (height, width, radius). */
export function Skeleton({ className = "" }: { className?: string }) {
  return (
    <span
      aria-hidden
      className={`block animate-pulse rounded-md bg-border motion-reduce:animate-none ${className}`}
    />
  );
}

/** Wraps a skeleton layout: announced once as "Loading…" to screen readers, the blocks themselves are hidden. */
export function Loading({
  label = "Loading…",
  className = "",
  children,
}: {
  label?: string;
  className?: string;
  children: React.ReactNode;
}) {
  return (
    <div role="status" aria-busy="true" className={className}>
      <span className="sr-only">{label}</span>
      {children}
    </div>
  );
}

/** Lines of body text; the last one shorter, like a real paragraph. */
export function SkeletonText({
  lines = 2,
  className = "",
}: {
  lines?: number;
  className?: string;
}) {
  return (
    <div className={`flex flex-col gap-2 ${className}`}>
      {Array.from({ length: lines }, (_, i) => (
        <Skeleton
          key={i}
          className={`h-3.5 ${i === lines - 1 && lines > 1 ? "w-2/3" : "w-full"}`}
        />
      ))}
    </div>
  );
}

/** Figma "Text field": label above a 44px input. */
export function FieldSkeleton({ helper = false }: { helper?: boolean }) {
  return (
    <div className="flex flex-col gap-1.5">
      <Skeleton className="h-3.5 w-28" />
      <Skeleton className="h-11 w-full rounded-lg" />
      {helper && <Skeleton className="h-3 w-3/4" />}
    </div>
  );
}

/** Figma "Button Large": 44px, radius 8. */
export function ButtonSkeleton({ className = "w-full" }: { className?: string }) {
  return <Skeleton className={`h-11 rounded-lg ${className}`} />;
}

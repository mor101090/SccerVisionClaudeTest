import { Loading, Skeleton } from "@/components/ui/skeleton";

/** Placeholder of the app frame while the session and club load (Figma: Home – loading). */
export function ShellSkeleton() {
  return (
    <Loading label="Loading your club…" className="flex min-h-dvh bg-bg">
      <div className="hidden w-[232px] shrink-0 flex-col gap-3 border-r border-divider bg-panel-2 p-5 xl:flex">
        <Skeleton className="h-12 w-40" />
        {Array.from({ length: 7 }, (_, i) => (
          <Skeleton key={i} className="h-9 w-full rounded-[10px]" />
        ))}
      </div>
      <div className="flex flex-1 flex-col">
        <div className="h-16 border-b border-divider bg-panel" />
        <div className="flex flex-col gap-5 p-7">
          <Skeleton className="h-8 w-80" />
          <DashboardSkeleton />
        </div>
      </div>
    </Loading>
  );
}

/** Card-shaped blocks in the homepage layout. */
export function DashboardSkeleton() {
  return (
    <div className="grid gap-5 lg:grid-cols-[1fr_330px]">
      <Skeleton className="h-60 rounded-2xl" />
      <Skeleton className="h-60 rounded-2xl" />
      <div className="grid gap-5 sm:grid-cols-2 lg:col-span-2 xl:grid-cols-4">
        {Array.from({ length: 4 }, (_, i) => (
          <Skeleton key={i} className="h-44 rounded-2xl" />
        ))}
      </div>
      <Skeleton className="h-96 rounded-2xl lg:col-span-2" />
    </div>
  );
}

/** Generic page placeholder: header, then content blocks. */
export function PageSkeleton() {
  return (
    <Loading className="flex flex-col gap-5">
      <Skeleton className="h-14 w-80" />
      <Skeleton className="h-10 w-full" />
      <Skeleton className="h-[420px] rounded-2xl" />
    </Loading>
  );
}

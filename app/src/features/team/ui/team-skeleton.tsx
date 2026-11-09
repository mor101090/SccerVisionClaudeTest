import { ButtonSkeleton, FieldSkeleton, Loading, Skeleton } from "@/components/ui/skeleton";

/** Manager / club / kits steps while the saved draft loads: fields in two columns and the continue button. */
export function TeamFormSkeleton({ fields = 4 }: { fields?: number }) {
  return (
    <Loading className="flex flex-col gap-6">
      <div className="grid gap-[18px] sm:grid-cols-2">
        {Array.from({ length: fields }, (_, i) => (
          <FieldSkeleton key={i} />
        ))}
      </div>
      <div className="flex justify-end">
        <ButtonSkeleton className="w-44" />
      </div>
    </Loading>
  );
}

/** Squad step while the squad loads: club header and player rows. */
export function SquadSkeleton() {
  return (
    <Loading className="flex w-full max-w-[960px] flex-col gap-4 rounded-[14px] border border-border bg-panel p-6">
      <div className="flex items-center gap-4">
        <Skeleton className="size-14 rounded-full" />
        <div className="flex flex-1 flex-col gap-2">
          <Skeleton className="h-6 w-48" />
          <Skeleton className="h-3.5 w-32" />
        </div>
      </div>
      {Array.from({ length: 8 }, (_, i) => (
        <div key={i} className="flex items-center gap-3">
          <Skeleton className="h-6 w-10 rounded" />
          <Skeleton className="h-4 flex-1" />
          <Skeleton className="h-4 w-20" />
        </div>
      ))}
    </Loading>
  );
}

/** Header: the manager's avatar and name. */
export function ManagerSkeleton() {
  return (
    <div aria-hidden className="flex items-center gap-4">
      <Skeleton className="size-8 rounded-full" />
      <Skeleton className="h-4 w-24" />
    </div>
  );
}

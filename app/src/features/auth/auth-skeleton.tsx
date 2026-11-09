import {
  ButtonSkeleton,
  FieldSkeleton,
  Loading,
  Skeleton,
  SkeletonText,
} from "@/components/ui/skeleton";

/** Title + intro line, as AuthTitle renders them. */
export function AuthTitleSkeleton() {
  return (
    <div className="flex flex-col gap-2.5">
      <Skeleton className="h-9 w-2/3" />
      <SkeletonText lines={2} />
    </div>
  );
}

/** Figma "provider account" card (Almost there, link account). */
function ProviderCardSkeleton() {
  return (
    <div className="flex items-center gap-3 rounded-[10px] border border-border bg-panel px-3.5 py-3">
      <Skeleton className="size-[33px] rounded-full" />
      <div className="flex flex-1 flex-col gap-1.5">
        <Skeleton className="h-4 w-32" />
        <Skeleton className="h-3 w-48" />
      </div>
      <Skeleton className="h-4 w-14" />
    </div>
  );
}

/**
 * The form part of an auth screen while it loads: an optional provider card or notice, N fields, an optional checkbox
 * row, the main button and optional secondary (text) buttons. Pass `title` when the title is not already shown.
 */
export function AuthFormSkeleton({
  title = false,
  card = false,
  notice = false,
  fields = 0,
  checkbox = false,
  code = false,
  extraButtons = 0,
  text = 0,
}: {
  title?: boolean;
  card?: boolean;
  notice?: boolean;
  fields?: number;
  checkbox?: boolean;
  /** The six one-digit boxes of the verify screen. */
  code?: boolean;
  extraButtons?: number;
  /** Lines of body text before the button (e.g. "we sent a link to …"). */
  text?: number;
}) {
  return (
    <Loading className="flex flex-col gap-[18px]">
      {title && <AuthTitleSkeleton />}
      {card && <ProviderCardSkeleton />}
      {notice && <Skeleton className="h-16 w-full rounded-lg" />}
      {text > 0 && <SkeletonText lines={text} />}
      {Array.from({ length: fields }, (_, i) => (
        <FieldSkeleton key={i} helper />
      ))}
      {code && (
        <div className="flex gap-2.5">
          {Array.from({ length: 6 }, (_, i) => (
            <Skeleton key={i} className="h-14 flex-1 rounded-lg" />
          ))}
        </div>
      )}
      {checkbox && (
        <div className="flex items-center gap-2.5">
          <Skeleton className="size-[18px] rounded" />
          <Skeleton className="h-3.5 w-56" />
        </div>
      )}
      <ButtonSkeleton />
      {Array.from({ length: extraButtons }, (_, i) => (
        <Skeleton key={i} className="mx-auto h-4 w-40" />
      ))}
    </Loading>
  );
}

/** Google / Facebook / Discord row on the login and sign-up screens. */
export function SocialButtonsSkeleton() {
  return (
    <div aria-hidden className="flex gap-3">
      {[0, 1, 2].map((i) => (
        <ButtonSkeleton key={i} className="flex-1" />
      ))}
    </div>
  );
}

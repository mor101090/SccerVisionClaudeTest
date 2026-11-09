import { PageSkeleton } from "@/components/app-shell/shell-skeleton";

/** Shown the instant a tab is clicked, before that page's data arrives. The sidebar stays. */
export default function Loading() {
  return <PageSkeleton />;
}

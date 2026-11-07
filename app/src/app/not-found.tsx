import type { Metadata } from "next";

import { NotFoundView } from "@/components/ui/status-page";

export const metadata: Metadata = { title: "Page not found" };

export default function NotFound() {
  return (
    <main id="main" className="flex flex-1 flex-col">
      <NotFoundView />
    </main>
  );
}

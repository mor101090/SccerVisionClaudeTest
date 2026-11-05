import { redirect } from "next/navigation";
import { Suspense } from "react";

import { loadStep } from "@/server/team-flow";

async function Go(): Promise<null> {
  const { reached } = await loadStep("manager");
  redirect(`/team/new/${reached}`);
}

/** /team/new opens the step the manager has reached. */
export default function Page() {
  return (
    <Suspense fallback={null}>
      <Go />
    </Suspense>
  );
}

import type { Metadata } from "next";

export const metadata: Metadata = { title: "Terms and Conditions" };

/** Placeholder: the project owner publishes the real terms here before launch. */
export default function Page() {
  return (
    <main className="mx-auto flex w-full max-w-2xl flex-1 flex-col gap-4 px-4 py-12">
      <h1 className="text-[30px] leading-[1.2] font-extrabold">Terms and Conditions</h1>
      <p className="text-muted">The full terms will be published here before launch.</p>
    </main>
  );
}

"use client";

import { useId, useState } from "react";

/**
 * Position and category tabs. The rows are already on the page (`data-filter`); switching only
 * hides the others, so it does not navigate or wait on the server.
 */
export function FilterTabs({
  label,
  tabs,
  counts,
  empty,
  children,
}: {
  label: string;
  tabs: { id: string; label: string }[];
  /** Rows per id. When the active tab has none, `empty` replaces the list. */
  counts?: Record<string, number>;
  empty?: React.ReactNode;
  children: React.ReactNode;
}) {
  const [id, setId] = useState("");
  const scope = useId().replace(/:/g, "");
  const active = tabs.some((t) => t.id === id) ? id : "";
  const none = active !== "" && counts !== undefined && (counts[active] ?? 0) === 0;
  return (
    <>
      <nav aria-label={label} className="flex gap-1 overflow-x-auto border-b border-divider">
        {tabs.map((t) => (
          <button
            key={t.id || "all"}
            type="button"
            aria-current={active === t.id ? "page" : undefined}
            onClick={() => setId(t.id)}
            className={`border-b-2 px-3.5 pt-2 pb-2.5 text-sm whitespace-nowrap ${
              active === t.id
                ? "border-link font-semibold text-text"
                : "border-transparent font-medium text-muted hover:text-text"
            }`}
          >
            {t.label}
          </button>
        ))}
      </nav>
      {active !== "" && (
        <style>{`[data-filter-scope="${scope}"] [data-filter]:not([data-filter="${active}"]){display:none}`}</style>
      )}
      {none ? empty : <div data-filter-scope={scope}>{children}</div>}
    </>
  );
}

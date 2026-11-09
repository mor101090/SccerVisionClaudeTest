"use client";

import { useState } from "react";

import { AppIcon } from "@/components/app-shell/app-icon";

/** A read-only value with a Copy button (invite link, code). */
export function CopyField({
  value,
  label,
  big = false,
}: {
  value: string;
  label: string;
  big?: boolean;
}) {
  const [copied, setCopied] = useState(false);
  const copy = async () => {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      setCopied(false);
    }
  };
  return (
    <div className="flex items-center gap-2">
      <input
        readOnly
        value={value}
        aria-label={label}
        onFocus={(e) => e.currentTarget.select()}
        className={`min-w-0 flex-1 rounded-lg border border-border bg-panel px-3 text-text ${big ? "h-11 text-[15px]" : "h-10 text-[13px]"}`}
      />
      <button
        type="button"
        onClick={() => void copy()}
        className={`inline-flex shrink-0 items-center gap-2 rounded-lg font-semibold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-link ${big ? "h-11 bg-accent px-4 text-sm text-on-accent hover:brightness-110" : "h-10 px-3 text-[13px] text-link hover:bg-panel"}`}
      >
        <AppIcon name={copied ? "check" : "copy"} size={16} />
        {copied ? "Copied" : big ? "Copy link" : "Copy"}
      </button>
      <span className="sr-only" aria-live="polite">
        {copied ? "Copied to the clipboard" : ""}
      </span>
    </div>
  );
}

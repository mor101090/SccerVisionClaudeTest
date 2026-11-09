"use client";

import { useSyncExternalStore } from "react";

const noop = () => () => undefined;

/** Share the invite link: WhatsApp, Telegram, email, and the system share sheet where the browser has one. */
export function ShareButtons({ url, text }: { url: string; text: string }) {
  // The system share sheet exists only in some browsers (and never on the server).
  const canShare = useSyncExternalStore(
    noop,
    () => "share" in navigator,
    () => false,
  );
  const enc = encodeURIComponent;
  const cls =
    "inline-flex h-9 items-center rounded-lg border border-border bg-panel px-3 text-[13px] font-semibold text-text hover:bg-border focus-visible:outline-2 focus-visible:outline-link";
  return (
    <div className="flex flex-wrap gap-2">
      <a
        className={cls}
        href={`https://wa.me/?text=${enc(`${text} ${url}`)}`}
        target="_blank"
        rel="noopener noreferrer"
      >
        WhatsApp
      </a>
      <a
        className={cls}
        href={`https://t.me/share/url?url=${enc(url)}&text=${enc(text)}`}
        target="_blank"
        rel="noopener noreferrer"
      >
        Telegram
      </a>
      <a
        className={cls}
        href={`mailto:?subject=${enc("Play me on SoccerVision")}&body=${enc(`${text}\n\n${url}`)}`}
      >
        Email
      </a>
      {canShare && (
        <button
          type="button"
          className={cls}
          onClick={() => void navigator.share({ url, text }).catch(() => undefined)}
        >
          More…
        </button>
      )}
    </div>
  );
}

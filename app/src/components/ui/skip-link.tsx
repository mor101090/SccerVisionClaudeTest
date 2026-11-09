/** First focusable element on the page: jumps keyboard users past the navigation to <main id="main">. */
export function SkipLink() {
  return (
    <a
      href="#main"
      className="sr-only z-[100] rounded-lg bg-button px-4 py-2 text-sm font-semibold text-button-text focus:not-sr-only focus:fixed focus:top-3 focus:left-3"
    >
      Skip to content
    </a>
  );
}

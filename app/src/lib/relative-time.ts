/** Short age of an event for lists: "now", "5m", "3h", "Yesterday", "Mon", "12 Jan". */
export function relativeTime(date: Date, now: Date = new Date()): string {
  const s = Math.max(0, Math.floor((now.getTime() - date.getTime()) / 1000));
  if (s < 60) return "now";
  if (s < 3600) return `${Math.floor(s / 60)}m`;
  if (s < 86_400) return `${Math.floor(s / 3600)}h`;
  if (s < 2 * 86_400) return "Yesterday";
  if (s < 7 * 86_400) return date.toLocaleDateString("en-GB", { weekday: "short" });
  return date.toLocaleDateString("en-GB", { day: "numeric", month: "short" });
}

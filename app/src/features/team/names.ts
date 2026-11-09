/** "João Silva" → "J. Silva"; a single name stays as it is. */
export function shortName(name: string): string {
  const parts = name.trim().split(/\s+/);
  if (parts.length < 2) return parts[0] ?? name;
  return `${parts[0]![0]}. ${parts.slice(1).join(" ")}`;
}

/** Overall rating 1–99: the mean of every skill the generator gave the player. */
export function overall(stats: Record<string, Record<string, number>>): number {
  const all = Object.values(stats).flatMap((g) => Object.values(g));
  return all.length ? Math.round(all.reduce((a, b) => a + b, 0) / all.length) : 0;
}

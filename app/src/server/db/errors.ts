/** Postgres unique violation (23505). postgres-js sets `code`; Drizzle may wrap the error under `cause`. */
export function isUniqueViolation(err: unknown): boolean {
  const e = err as { code?: string; cause?: { code?: string } } | null;
  return e?.code === "23505" || e?.cause?.code === "23505";
}

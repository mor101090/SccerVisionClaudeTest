import "server-only";

import { NextResponse } from "next/server";
import type { z } from "zod";

import { currentUser } from "./auth";
import { LobbyError } from "./lobbies";

type User = NonNullable<Awaited<ReturnType<typeof currentUser>>>;

/** Route handler wrapper: requires a session, maps domain errors to HTTP. */
export function withUser<Args extends unknown[]>(
  handler: (user: User, ...args: Args) => Promise<Response>,
) {
  return async (...args: Args): Promise<Response> => {
    const user = await currentUser();
    if (!user) return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
    try {
      return await handler(user, ...args);
    } catch (err) {
      if (err instanceof LobbyError) {
        return NextResponse.json({ error: err.message }, { status: err.status });
      }
      console.error(err);
      return NextResponse.json({ error: "Server error" }, { status: 500 });
    }
  };
}

export async function parseJson<T extends z.ZodType>(
  request: Request,
  schema: T,
): Promise<{ data: z.infer<T> } | { error: Response }> {
  let body: unknown;
  try {
    body = await request.json();
  } catch {
    return { error: NextResponse.json({ error: "Invalid JSON" }, { status: 400 }) };
  }
  const parsed = schema.safeParse(body);
  if (!parsed.success) {
    return {
      error: NextResponse.json(
        { error: parsed.error.issues[0]?.message ?? "Invalid body" },
        { status: 400 },
      ),
    };
  }
  return { data: parsed.data };
}

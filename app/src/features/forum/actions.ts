"use server";

import { eq, sql } from "drizzle-orm";
import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { z } from "zod";

import { requireUser } from "@/server/auth";
import { db, schema } from "@/server/db";
import { getProfile, isComplete } from "@/server/profiles";

export type ForumState = { error?: string } | undefined;

const CATEGORIES = ["general", "tactics", "transfers", "friendlies", "bugs"] as const;
const threadSchema = z.object({
  category: z.enum(CATEGORIES),
  title: z.string().trim().min(4, "Give the thread a title (4+ characters).").max(120),
  body: z.string().trim().min(2, "Write a first post.").max(5000),
});
const replySchema = z.object({
  threadId: z.uuid(),
  body: z.string().trim().min(2, "Write a reply.").max(5000),
});

/** The forum is read-only for now: only admins (profiles.is_admin) may post. */
async function poster() {
  const user = await requireUser();
  const profile = await getProfile(user.id);
  if (!isComplete(profile)) redirect("/auth/finish?redirectTo=/forum");
  return profile?.isAdmin ? user : null;
}

const READ_ONLY: ForumState = { error: "The forum is read-only for now." };

export async function createThreadAction(_: ForumState, form: FormData): Promise<ForumState> {
  const user = await poster();
  if (!user) return READ_ONLY;
  const parsed = threadSchema.safeParse(Object.fromEntries(form));
  if (!parsed.success) return { error: parsed.error.issues[0]?.message ?? "Check the form." };
  const id = await db().transaction(async (tx) => {
    const [t] = await tx
      .insert(schema.forumThreads)
      .values({ authorId: user.id, category: parsed.data.category, title: parsed.data.title })
      .returning({ id: schema.forumThreads.id });
    await tx
      .insert(schema.forumPosts)
      .values({ threadId: t!.id, authorId: user.id, body: parsed.data.body });
    return t!.id;
  });
  revalidatePath("/forum", "layout");
  redirect(`/forum/${id}`);
}

export async function replyAction(_: ForumState, form: FormData): Promise<ForumState> {
  const user = await poster();
  if (!user) return READ_ONLY;
  const parsed = replySchema.safeParse(Object.fromEntries(form));
  if (!parsed.success) return { error: parsed.error.issues[0]?.message ?? "Check the reply." };
  await db().transaction(async (tx) => {
    const [t] = await tx
      .update(schema.forumThreads)
      .set({ replies: sql`${schema.forumThreads.replies} + 1`, lastPostAt: new Date() })
      .where(eq(schema.forumThreads.id, parsed.data.threadId))
      .returning({ id: schema.forumThreads.id });
    if (!t) throw new Error("Thread not found");
    await tx
      .insert(schema.forumPosts)
      .values({ threadId: t.id, authorId: user.id, body: parsed.data.body });
  });
  revalidatePath("/forum", "layout");
  redirect(`/forum/${parsed.data.threadId}`);
}

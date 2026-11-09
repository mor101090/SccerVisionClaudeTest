"use server";

import { and, eq, isNull } from "drizzle-orm";
import { revalidatePath } from "next/cache";

import { requireUser } from "@/server/auth";
import { markMessagesRead } from "@/server/club-world";
import { db, schema } from "@/server/db";

/** Marks every notification of the signed-in manager as read ("Mark all as read"). */
export async function markNotificationsReadAction(): Promise<void> {
  const user = await requireUser();
  await db()
    .update(schema.notifications)
    .set({ readAt: new Date() })
    .where(and(eq(schema.notifications.userId, user.id), isNull(schema.notifications.readAt)));
  revalidatePath("/", "layout");
}

/** Opening Messages reads them: clears the unread badge in the top bar. */
export async function markMessagesReadAction(): Promise<void> {
  const user = await requireUser();
  await markMessagesRead(user.id);
  revalidatePath("/", "layout");
}

/** The first-visit tour ran (finished or skipped): don't show it again. */
export async function markOnboardingSeenAction(): Promise<void> {
  const user = await requireUser();
  await db()
    .update(schema.profiles)
    .set({ onboardingSeenAt: new Date() })
    .where(eq(schema.profiles.userId, user.id));
  revalidatePath("/home");
}

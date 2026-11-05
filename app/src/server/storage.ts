import "server-only";

import { StorageApiError } from "@supabase/supabase-js";

import { supabaseAdmin } from "./supabase";

/**
 * Supabase Storage buckets (private; created by the `storage_buckets` migration on each project).
 * Accessed with the secret key from the server only; replays are streamed by /api/matches/[id]/replay.
 */
export const BUCKETS = { replays: "replays" } as const;
export type Bucket = (typeof BUCKETS)[keyof typeof BUCKETS];

const storage = (bucket: Bucket) => supabaseAdmin().storage.from(bucket);

/** `max-age` of a Cache-Control header, which is what Supabase Storage stores per object. */
function maxAge(cacheControl?: string): string | undefined {
  return cacheControl?.match(/max-age=(\d+)/)?.[1];
}

export async function putObject(
  bucket: Bucket,
  key: string,
  body: Uint8Array,
  contentType: string,
  cacheControl?: string,
): Promise<void> {
  const { error } = await storage(bucket).upload(key, body, {
    contentType,
    cacheControl: maxAge(cacheControl),
    upsert: true,
  });
  if (error) throw error;
}

export async function getObjectBytes(bucket: Bucket, key: string): Promise<Uint8Array | null> {
  const { data, error } = await storage(bucket).download(key);
  if (error) {
    if (error instanceof StorageApiError && (error.status === 404 || error.code === "NoSuchKey"))
      return null;
    throw error;
  }
  return new Uint8Array(await data.arrayBuffer());
}

export async function deleteObject(bucket: Bucket, key: string): Promise<void> {
  const { error } = await storage(bucket).remove([key]);
  if (error) throw error;
}

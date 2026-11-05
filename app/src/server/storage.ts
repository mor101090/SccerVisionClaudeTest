import "server-only";

import {
  DeleteObjectCommand,
  GetObjectCommand,
  PutObjectCommand,
  S3Client,
} from "@aws-sdk/client-s3";

import { serverEnv } from "@/env";

/** Buckets declared in neon.ts. Branch-scoped: previews never touch production files. */
export const BUCKETS = { replays: "replays", media: "media" } as const;
export type Bucket = (typeof BUCKETS)[keyof typeof BUCKETS];

let client: S3Client | null = null;

function s3(): S3Client {
  if (client) return client;
  const { AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY, AWS_ENDPOINT_URL_S3, AWS_REGION } = serverEnv;
  if (!AWS_ACCESS_KEY_ID || !AWS_SECRET_ACCESS_KEY || !AWS_ENDPOINT_URL_S3) {
    throw new Error("Neon Object Storage env (AWS_*) is not set");
  }
  client = new S3Client({
    endpoint: AWS_ENDPOINT_URL_S3,
    region: AWS_REGION ?? "auto",
    credentials: { accessKeyId: AWS_ACCESS_KEY_ID, secretAccessKey: AWS_SECRET_ACCESS_KEY },
    forcePathStyle: true, // required by Neon
  });
  return client;
}

export async function putObject(
  bucket: Bucket,
  key: string,
  body: Uint8Array,
  contentType: string,
  cacheControl?: string,
): Promise<void> {
  await s3().send(
    new PutObjectCommand({
      Bucket: bucket,
      Key: key,
      Body: body,
      ContentType: contentType,
      CacheControl: cacheControl,
    }),
  );
}

export async function getObjectBytes(bucket: Bucket, key: string): Promise<Uint8Array | null> {
  try {
    const res = await s3().send(new GetObjectCommand({ Bucket: bucket, Key: key }));
    return res.Body ? await res.Body.transformToByteArray() : null;
  } catch (err) {
    if (err instanceof Error && (err.name === "NoSuchKey" || err.name === "NotFound")) return null;
    throw err;
  }
}

export async function deleteObject(bucket: Bucket, key: string): Promise<void> {
  await s3().send(new DeleteObjectCommand({ Bucket: bucket, Key: key }));
}

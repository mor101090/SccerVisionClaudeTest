import type { Replay } from "@/engine/replay/replay";

/** Replays are stored as gzipped JSON in the `replays` bucket (same shape the SimRunner writes). */
export const REPLAY_CONTENT_TYPE = "application/gzip";

async function pipe(bytes: Uint8Array, transform: GenericTransformStream): Promise<Uint8Array> {
  const stream = new Response(bytes as BodyInit).body!.pipeThrough(transform);
  return new Uint8Array(await new Response(stream).arrayBuffer());
}

export async function encodeReplay(replay: Replay): Promise<Uint8Array> {
  return pipe(new TextEncoder().encode(JSON.stringify(replay)), new CompressionStream("gzip"));
}

export async function decodeReplay(gz: Uint8Array): Promise<Replay> {
  const json = await pipe(gz, new DecompressionStream("gzip"));
  return JSON.parse(new TextDecoder().decode(json)) as Replay;
}

/** First bytes of every gzip stream. */
export function isGzip(bytes: Uint8Array): boolean {
  return bytes.length > 2 && bytes[0] === 0x1f && bytes[1] === 0x8b;
}

/** FNV-1a over every number of every frame: equal on both clients iff the matches are identical. */
export function framesChecksum(frames: number[][]): string {
  let h = 0x811c9dc5;
  for (const f of frames) {
    for (const v of f) {
      h ^= v & 0xff;
      h = Math.imul(h, 0x01000193);
      h ^= (v >>> 8) & 0xff;
      h = Math.imul(h, 0x01000193);
      h ^= (v >>> 16) & 0xff;
      h = Math.imul(h, 0x01000193);
      h ^= (v >>> 24) & 0xff;
      h = Math.imul(h, 0x01000193);
    }
  }
  return (h >>> 0).toString(16).padStart(8, "0");
}

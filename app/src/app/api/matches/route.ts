import { revalidatePath } from "next/cache";
import { NextResponse } from "next/server";

import { MAX_REPLAY_BYTES, saveMatchSchema } from "@/features/matches/match-schema";
import { isGzip } from "@/features/matches/replay-codec";
import { currentUser } from "@/server/auth";
import { listMatches, saveMatch } from "@/server/matches";
import { getProfile } from "@/server/profiles";
import { engineTeamFor, getTeam } from "@/server/teams";

const unauthorized = () => NextResponse.json({ error: "Unauthorized" }, { status: 401 });
const bad = (error: string) => NextResponse.json({ error }, { status: 400 });

/** Save a finished match: multipart with `meta` (JSON) and `replay` (gzipped replay JSON). */
export async function POST(request: Request) {
  const user = await currentUser();
  if (!user) return unauthorized();

  let form: FormData;
  try {
    form = await request.formData();
  } catch {
    return bad("Expected multipart form data");
  }

  const meta = form.get("meta");
  const replay = form.get("replay");
  if (typeof meta !== "string" || !(replay instanceof Blob)) return bad("Missing meta or replay");
  if (replay.size > MAX_REPLAY_BYTES) return bad("Replay too large");

  let json: unknown;
  try {
    json = JSON.parse(meta);
  } catch {
    return bad("Invalid meta JSON");
  }
  const parsed = saveMatchSchema.safeParse(json);
  if (!parsed.success) return bad(parsed.error.issues[0]?.message ?? "Invalid match");

  const bytes = new Uint8Array(await replay.arrayBuffer());
  if (!isGzip(bytes)) return bad("Replay must be gzip");

  // Played with the manager's club: link the match to it, and only if the squad sent is the stored one (it is what
  // a re-simulation uses, so a client can't save a result for a squad it doesn't have).
  let homeTeamId: string | null = null;
  if (parsed.data.homeSquad) {
    const [profile, team] = await Promise.all([getProfile(user.id), getTeam(user.id)]);
    const club = await engineTeamFor(team, profile?.username ?? user.name);
    if (!team || club?.text !== parsed.data.homeSquad)
      return NextResponse.json(
        { error: "Your squad changed during the match, so it can't be saved." },
        { status: 409 },
      );
    homeTeamId = team.id;
  }

  const saved = await saveMatch(user.id, parsed.data, bytes, homeTeamId);
  // the homepage stats and My Games are cached per manager for a few minutes
  revalidatePath("/home");
  revalidatePath("/games");
  return NextResponse.json(saved, { status: 201 });
}

export async function GET() {
  const user = await currentUser();
  if (!user) return unauthorized();
  return NextResponse.json(await listMatches(user.id));
}

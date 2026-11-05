import { sql } from "drizzle-orm";
import {
  boolean,
  index,
  integer,
  jsonb,
  pgEnum,
  pgSchema,
  pgTable,
  primaryKey,
  smallint,
  text,
  timestamp,
  uniqueIndex,
  uuid,
} from "drizzle-orm/pg-core";

/**
 * Supabase Auth owns the `auth` schema. `auth.users` is declared here (id only) so our tables can
 * reference users; drizzle.config.ts limits migrations to `public`. Display names live in
 * `profiles.display_name`, since `auth.users` has no name column.
 */
const supabaseAuth = pgSchema("auth");
export const authUsers = supabaseAuth.table("users", {
  id: uuid("id").primaryKey(),
});

/*
 * Every table has RLS enabled and no policies: Supabase's Data API (anon / authenticated keys) can't read
 * or write them. The server connects as the `postgres` role, which bypasses RLS.
 */

const timestamps = {
  createdAt: timestamp("created_at", { withTimezone: true }).notNull().defaultNow(),
  updatedAt: timestamp("updated_at", { withTimezone: true })
    .notNull()
    .defaultNow()
    .$onUpdate(() => new Date()),
};

const ownerId = () =>
  uuid("owner_id")
    .notNull()
    .references(() => authUsers.id, { onDelete: "cascade" });

// ---------------------------------------------------------------- accounts

/**
 * App-side profile for every user: the public username (league tables), display name and terms consent.
 * Created at sign-up (email) or on the "Almost there" screen (social sign-in).
 */
export const profiles = pgTable(
  "profiles",
  {
    userId: uuid("user_id")
      .primaryKey()
      .references(() => authUsers.id, { onDelete: "cascade" }),
    /** 3–20 letters, numbers or _. Unique ignoring case. Null until chosen. */
    username: text("username"),
    /** Manager name shown in lobbies and the header (from sign-up, the social profile, or Create your team). */
    displayName: text("display_name"),
    termsAcceptedAt: timestamp("terms_accepted_at", { withTimezone: true }),
    /** ISO 3166 country code (the flag next to the name). */
    nationality: text("nationality"),
    /** The manager who invited this one (step 1 of Create your team). */
    invitedBy: uuid("invited_by").references(() => authUsers.id, { onDelete: "set null" }),
    ...timestamps,
  },
  (t) => [uniqueIndex("profiles_username_lower_uq").on(sql`lower(${t.username})`)],
).enableRLS();

/**
 * Failed password logins per email (sha256 of the normalized address, never the address itself).
 * MAX failures inside the window pause password login for that email (see server/login-throttle.ts).
 */
export const loginAttempts = pgTable("login_attempts", {
  key: text("key").primaryKey(),
  failures: integer("failures").notNull().default(0),
  windowStartedAt: timestamp("window_started_at", { withTimezone: true }).notNull().defaultNow(),
  lockedUntil: timestamp("locked_until", { withTimezone: true }),
  /** When the current pause began. Stays put if later failures extend the window. */
  lockedAt: timestamp("locked_at", { withTimezone: true }),
}).enableRLS();

/** Password sessions created by a Server Action. A direct Auth API grant is not in this table. */
export const passwordSessions = pgTable("password_sessions", {
  sessionId: text("session_id").primaryKey(),
  userId: uuid("user_id").notNull(),
  createdAt: timestamp("created_at", { withTimezone: true }).notNull().defaultNow(),
}).enableRLS();

/**
 * Passkeys the app approved: added through Settings or the offer by a session with a recent login (checked on the
 * server). Supabase lets any valid session register one directly; such a passkey is not here and can't sign in.
 */
export const approvedPasskeys = pgTable(
  "approved_passkeys",
  {
    passkeyId: text("passkey_id").primaryKey(),
    userId: uuid("user_id")
      .notNull()
      .references(() => authUsers.id, { onDelete: "cascade" }),
    approvedAt: timestamp("approved_at", { withTimezone: true }).notNull().defaultNow(),
  },
  // Foreign key column: the cascade from auth.users looks rows up by it.
  (t) => [index("approved_passkeys_user_id_idx").on(t.userId)],
).enableRLS();

/** Passkey sessions the app vetted (/auth/callback: the passkey used is approved). Others are not signed in. */
export const passkeySessions = pgTable("passkey_sessions", {
  sessionId: text("session_id").primaryKey(),
  userId: uuid("user_id").notNull(),
  createdAt: timestamp("created_at", { withTimezone: true }).notNull().defaultNow(),
}).enableRLS();

// ---------------------------------------------------------------- teams

export type Kit = {
  primary: string;
  secondary: string;
  number: string;
  gk: string;
  gkNumber: string;
  gkTrim?: string;
  boots?: string;
};

/** One kit as picked in Create your team: colors are hex values from features/team/kits.ts. */
export type KitPick = { shirt: string; trim: string; socks: string; number: string };
export type TeamKits = { home: KitPick; away: KitPick; keeper: KitPick };
export type Crest = { kind: "shield"; prompt?: string };

export const teams = pgTable(
  "teams",
  {
    id: uuid("id").primaryKey().defaultRandom(),
    ownerId: ownerId(),
    name: text("name").notNull(),
    shortName: text("short_name").notNull(),
    kit: jsonb("kit").$type<Kit>().notNull(),
    /** Default plan text (TeamPlan.cs format): formation, eleven, roles, instructions. */
    plan: text("plan"),
    /** The three kits picked in Create your team (the engine kit `kit` is derived from home + keeper). */
    kits: jsonb("kits").$type<TeamKits>(),
    /** Crest: the simple shield, or a description for the crest server to draw. */
    crest: jsonb("crest").$type<Crest>(),
    /** Generator nationalities the players are named from (american, israel, spanish). */
    namePools: jsonb("name_pools").$type<string[]>(),
    formation: text("formation").notNull().default("4-4-2"),
    /** Seed of the current squad at the team generator (same seed + options = same squad). */
    squadSeed: integer("squad_seed"),
    /** New squads allowed before the first match. */
    regenerationsLeft: smallint("regenerations_left").notNull().default(2),
    captainId: uuid("captain_id"),
    /** draft while Create your team is in progress; ready once the manager starts playing. */
    status: text("status", { enum: ["draft", "ready"] })
      .notNull()
      .default("draft"),
    ...timestamps,
  },
  // one club per manager for now (the unique index also serves lookups by owner)
  (t) => [uniqueIndex("teams_owner_uq").on(t.ownerId)],
).enableRLS();

/** 27 engine attributes, 1..100 each. Keys follow the C# PlayerAttributes names. */
export type PlayerAttributes = Record<string, number>;

export const players = pgTable(
  "players",
  {
    id: uuid("id").primaryKey().defaultRandom(),
    teamId: uuid("team_id")
      .notNull()
      .references(() => teams.id, { onDelete: "cascade" }),
    number: smallint("number").notNull(),
    name: text("name").notNull(),
    shortName: text("short_name").notNull(),
    role: text("role").notNull(),
    attributes: jsonb("attributes").$type<PlayerAttributes>().notNull(),
    /** Generator position: goalkeeper | fullback | defender | midfielder | winger | striker. */
    position: text("position"),
    level: text("level"),
    /** The player exactly as the team generator made him (stats, feet, age, height...). */
    profile: jsonb("profile").$type<Record<string, unknown>>(),
    /** 0–10: 4-4-2 slot in the starting XI (GK, RB, CB r, CB l, LB, RM, CM r, CM l, LM, ST r, ST l). Null: not starting. */
    starterSlot: smallint("starter_slot"),
    /** 0–8: order on the bench. Null with no starter slot: in the squad, not in the match-day 20. */
    benchOrder: smallint("bench_order"),
    ...timestamps,
  },
  (t) => [
    uniqueIndex("players_team_number_uq").on(t.teamId, t.number),
    // one player per XI slot: a swap based on a stale read fails instead of fielding 12
    uniqueIndex("players_team_starter_uq")
      .on(t.teamId, t.starterSlot)
      .where(sql`${t.starterSlot} is not null`),
  ],
).enableRLS();

// ---------------------------------------------------------------- leagues

export const leagues = pgTable(
  "leagues",
  {
    id: uuid("id").primaryKey().defaultRandom(),
    ownerId: ownerId(),
    name: text("name").notNull(),
    season: integer("season").notNull().default(1),
    ...timestamps,
  },
  (t) => [index("leagues_owner_idx").on(t.ownerId)],
).enableRLS();

export const leagueMembers = pgTable(
  "league_members",
  {
    leagueId: uuid("league_id")
      .notNull()
      .references(() => leagues.id, { onDelete: "cascade" }),
    teamId: uuid("team_id")
      .notNull()
      .references(() => teams.id, { onDelete: "cascade" }),
    joinedAt: timestamp("joined_at", { withTimezone: true }).notNull().defaultNow(),
  },
  (t) => [primaryKey({ columns: [t.leagueId, t.teamId] })],
).enableRLS();

// ---------------------------------------------------------------- matches

export const matchMode = pgEnum("match_mode", ["staged", "random", "live", "online"]);

export type MatchConditions = {
  weather: string;
  maintenance: number;
  wind: number;
  windAngle: number;
  night: boolean;
};

/**
 * A played match. The engine is deterministic, so seed + setups + conditions + engine version
 * are enough to re-simulate and verify the stored result. Replay frames live in the
 * `replays` bucket under `replayKey`.
 */
export const matches = pgTable(
  "matches",
  {
    id: uuid("id").primaryKey().defaultRandom(),
    userId: uuid("user_id")
      .notNull()
      .references(() => authUsers.id, { onDelete: "cascade" }),
    leagueId: uuid("league_id").references(() => leagues.id, { onDelete: "set null" }),
    homeTeamId: uuid("home_team_id").references(() => teams.id, { onDelete: "set null" }),
    awayTeamId: uuid("away_team_id").references(() => teams.id, { onDelete: "set null" }),
    homeName: text("home_name").notNull(),
    awayName: text("away_name").notNull(),
    mode: matchMode("mode").notNull(),
    seed: integer("seed").notNull(),
    engineVersion: text("engine_version").notNull(),
    conditions: jsonb("conditions").$type<MatchConditions>().notNull(),
    homePlan: text("home_plan"),
    /** The home squad as LiveEngine.UseHomeTeam text, when a manager's club played: with the seed it re-simulates the match. */
    homeSquad: text("home_squad"),
    awayPlan: text("away_plan"),
    homeScore: smallint("home_score").notNull(),
    awayScore: smallint("away_score").notNull(),
    /** Full-time header from the engine (team stats). */
    summary: jsonb("summary").$type<Record<string, unknown>>(),
    replayKey: text("replay_key"),
    verified: boolean("verified").notNull().default(false),
    playedAt: timestamp("played_at", { withTimezone: true }).notNull().defaultNow(),
  },
  (t) => [
    index("matches_user_played_idx").on(t.userId, t.playedAt.desc()),
    index("matches_league_idx").on(t.leagueId),
  ],
).enableRLS();

// ---------------------------------------------------------------- online lobbies

export const lobbyStatus = pgEnum("lobby_status", [
  "open",
  "ready",
  "playing",
  "finished",
  "closed",
]);

export const lobbies = pgTable(
  "lobbies",
  {
    id: uuid("id").primaryKey().defaultRandom(),
    /** Six-digit join code, unique while the lobby is active. */
    code: text("code").notNull(),
    hostId: uuid("host_id")
      .notNull()
      .references(() => authUsers.id, { onDelete: "cascade" }),
    guestId: uuid("guest_id").references(() => authUsers.id, { onDelete: "set null" }),
    status: lobbyStatus("status").notNull().default("open"),
    seed: integer("seed").notNull(),
    conditions: jsonb("conditions").$type<MatchConditions>().notNull(),
    hostPlan: text("host_plan"),
    guestPlan: text("guest_plan"),
    /** Host's engine watermark: frames simulated so far. The guest never simulates past it. */
    hostFrame: integer("host_frame").notNull().default(0),
    /** Set once the host's engine reports full time. */
    finalFrame: integer("final_frame"),
    matchId: uuid("match_id").references(() => matches.id, { onDelete: "set null" }),
    expiresAt: timestamp("expires_at", { withTimezone: true })
      .notNull()
      .default(sql`now() + interval '2 hours'`),
    ...timestamps,
  },
  (t) => [
    uniqueIndex("lobbies_active_code_uq")
      .on(t.code)
      .where(sql`${t.status} in ('open', 'ready', 'playing')`),
  ],
).enableRLS();

/**
 * In-match manager inputs (plan changes, substitutions), ordered per lobby.
 * Both clients run the same deterministic engine and apply inputs at the same frame.
 */
export const lobbyInputs = pgTable(
  "lobby_inputs",
  {
    id: uuid("id").primaryKey().defaultRandom(),
    lobbyId: uuid("lobby_id")
      .notNull()
      .references(() => lobbies.id, { onDelete: "cascade" }),
    /** Order the host's engine applied inputs in (null until applied). Guests replay in (frame, seq) order. */
    seq: integer("seq"),
    userId: uuid("user_id")
      .notNull()
      .references(() => authUsers.id, { onDelete: "cascade" }),
    team: smallint("team").notNull(),
    kind: text("kind", { enum: ["plan", "sub"] }).notNull(),
    payload: jsonb("payload").$type<Record<string, unknown>>().notNull(),
    /**
     * Engine frame the input applies at, assigned by the host when its engine applied it.
     * Null: a guest request the host has not applied yet.
     */
    frame: integer("frame"),
    createdAt: timestamp("created_at", { withTimezone: true }).notNull().defaultNow(),
  },
  (t) => [uniqueIndex("lobby_inputs_seq_uq").on(t.lobbyId, t.seq)],
).enableRLS();

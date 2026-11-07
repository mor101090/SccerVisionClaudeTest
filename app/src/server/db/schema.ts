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
    /** When the manager finished or skipped the homepage tour. Null: show it on the next visit. */
    onboardingSeenAt: timestamp("onboarding_seen_at", { withTimezone: true }),
    /** Soccer Vision staff: may post in the forum (read-only for everyone else). Set in Supabase, never by the app. */
    isAdmin: boolean("is_admin").notNull().default(false),
    /** "About you" on the profile, up to 120 characters. */
    bio: text("bio"),
    /** The real club the manager supports (free text, optional). */
    favouriteClub: text("favourite_club"),
    /** Other managers may open this profile (/managers/<username>). Off: only the club page shows. */
    showProfile: boolean("show_profile").notNull().default(true),
    /** Other managers see this manager's saved match results. */
    showHistory: boolean("show_history").notNull().default(true),
    ...timestamps,
  },
  (t) => [
    uniqueIndex("profiles_username_lower_uq").on(sql`lower(${t.username})`),
    index("profiles_invited_by_idx").on(t.invitedBy),
  ],
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
    /** Home city and ground name, edited in Club settings (optional). */
    city: text("city"),
    stadiumName: text("stadium_name"),
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
  (t) => [
    primaryKey({ columns: [t.leagueId, t.teamId] }),
    index("league_members_team_idx").on(t.teamId),
  ],
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
    index("matches_home_team_idx").on(t.homeTeamId),
    index("matches_away_team_idx").on(t.awayTeamId),
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
    index("lobbies_host_idx").on(t.hostId),
    index("lobbies_guest_idx").on(t.guestId),
    index("lobbies_match_idx").on(t.matchId),
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
  (t) => [
    uniqueIndex("lobby_inputs_seq_uq").on(t.lobbyId, t.seq),
    index("lobby_inputs_user_idx").on(t.userId),
  ],
).enableRLS();

// ---------------------------------------------------------------- club world (homepage data)
/*
 * League play, money, ratings, fans, stadiums, transfers, staff, training and the inbox. A brand-new club has no
 * rows in these tables: every reader treats "no row" as the new-club state (see server/home.ts), so the tables can
 * fill in feature by feature without the homepage changing shape.
 */

export const seasonStatus = pgEnum("season_status", ["preseason", "active", "break", "finished"]);

/** One game-wide season. `break` is the off week between the last round and the next season. */
export const seasons = pgTable(
  "seasons",
  {
    id: uuid("id").primaryKey().defaultRandom(),
    number: integer("number").notNull(),
    status: seasonStatus("status").notNull().default("preseason"),
    startsAt: timestamp("starts_at", { withTimezone: true }).notNull(),
    endsAt: timestamp("ends_at", { withTimezone: true }).notNull(),
    ...timestamps,
  },
  (t) => [uniqueIndex("seasons_number_uq").on(t.number)],
).enableRLS();

export const competition = pgEnum("competition", ["league", "cup", "friendly"]);
export const fixtureStatus = pgEnum("fixture_status", [
  "scheduled",
  "live",
  "finished",
  "postponed",
]);

/** A scheduled match. A side without a club id is the computer (`homeName` / `awayName` say who). */
export const fixtures = pgTable(
  "fixtures",
  {
    id: uuid("id").primaryKey().defaultRandom(),
    seasonId: uuid("season_id").references(() => seasons.id, { onDelete: "set null" }),
    leagueId: uuid("league_id").references(() => leagues.id, { onDelete: "cascade" }),
    competition: competition("competition").notNull(),
    /** League round or cup round, 1-based. Null for friendlies. */
    round: smallint("round"),
    homeTeamId: uuid("home_team_id").references(() => teams.id, { onDelete: "set null" }),
    awayTeamId: uuid("away_team_id").references(() => teams.id, { onDelete: "set null" }),
    homeName: text("home_name").notNull(),
    awayName: text("away_name").notNull(),
    kickoffAt: timestamp("kickoff_at", { withTimezone: true }).notNull(),
    status: fixtureStatus("status").notNull().default("scheduled"),
    homeScore: smallint("home_score"),
    awayScore: smallint("away_score"),
    matchId: uuid("match_id").references(() => matches.id, { onDelete: "set null" }),
    ...timestamps,
  },
  (t) => [
    index("fixtures_home_kickoff_idx").on(t.homeTeamId, t.kickoffAt),
    index("fixtures_away_kickoff_idx").on(t.awayTeamId, t.kickoffAt),
    index("fixtures_league_round_idx").on(t.leagueId, t.round),
    index("fixtures_season_idx").on(t.seasonId),
    index("fixtures_match_idx").on(t.matchId),
  ],
).enableRLS();

/** League table row, kept up to date when a league fixture finishes. `form`: last five results, newest last ("WWDLW"). */
export const standings = pgTable(
  "standings",
  {
    leagueId: uuid("league_id")
      .notNull()
      .references(() => leagues.id, { onDelete: "cascade" }),
    teamId: uuid("team_id")
      .notNull()
      .references(() => teams.id, { onDelete: "cascade" }),
    position: smallint("position").notNull(),
    played: smallint("played").notNull().default(0),
    won: smallint("won").notNull().default(0),
    drawn: smallint("drawn").notNull().default(0),
    lost: smallint("lost").notNull().default(0),
    goalsFor: smallint("goals_for").notNull().default(0),
    goalsAgainst: smallint("goals_against").notNull().default(0),
    points: smallint("points").notNull().default(0),
    form: text("form").notNull().default(""),
    updatedAt: timestamps.updatedAt,
  },
  (t) => [
    primaryKey({ columns: [t.leagueId, t.teamId] }),
    index("standings_team_idx").on(t.teamId),
  ],
).enableRLS();

/** World rating. A club is unranked (no row) until it has played RATED_AFTER matches. */
export const teamRatings = pgTable("team_ratings", {
  teamId: uuid("team_id")
    .primaryKey()
    .references(() => teams.id, { onDelete: "cascade" }),
  rating: integer("rating").notNull(),
  worldRank: integer("world_rank").notNull(),
  /** Rank at the previous weekly update (Monday); null on the first one. */
  previousRank: integer("previous_rank"),
  ...timestamps,
}).enableRLS();

/** Balance in whole euros. No row: finances have not started (no league season played yet). */
export const clubFinances = pgTable("club_finances", {
  teamId: uuid("team_id")
    .primaryKey()
    .references(() => teams.id, { onDelete: "cascade" }),
  balance: integer("balance").notNull(),
  ...timestamps,
}).enableRLS();

export const financeKind = pgEnum("finance_kind", [
  "tickets",
  "sponsors",
  "tv",
  "prize",
  "player_wages",
  "staff_wages",
  "transfer",
  "construction",
]);

/** One income (positive) or expense (negative) in whole euros, booked to the week it happened in. */
export const financeEntries = pgTable(
  "finance_entries",
  {
    id: uuid("id").primaryKey().defaultRandom(),
    teamId: uuid("team_id")
      .notNull()
      .references(() => teams.id, { onDelete: "cascade" }),
    weekOf: timestamp("week_of", { withTimezone: true }).notNull(),
    kind: financeKind("kind").notNull(),
    amount: integer("amount").notNull(),
    note: text("note"),
    createdAt: timestamps.createdAt,
  },
  (t) => [index("finance_entries_team_week_idx").on(t.teamId, t.weekOf)],
).enableRLS();

/** Supporters. No row: mood comes from the last five results (server/home.ts). */
export const clubFans = pgTable("club_fans", {
  teamId: uuid("team_id")
    .primaryKey()
    .references(() => teams.id, { onDelete: "cascade" }),
  /** 1 (furious) … 5 (euphoric). */
  mood: smallint("mood").notNull(),
  members: integer("members").notNull().default(0),
  ...timestamps,
}).enableRLS();

/** Home ground. No row: it opens with league play. */
export const stadiums = pgTable("stadiums", {
  teamId: uuid("team_id")
    .primaryKey()
    .references(() => teams.id, { onDelete: "cascade" }),
  name: text("name").notNull(),
  capacity: integer("capacity").notNull(),
  roof: boolean("roof").notNull().default(false),
  seasonTickets: integer("season_tickets").notNull().default(0),
  /** Whole euros. */
  ticketPrice: integer("ticket_price").notNull(),
  ...timestamps,
}).enableRLS();

export const offerStatus = pgEnum("offer_status", [
  "pending",
  "accepted",
  "rejected",
  "withdrawn",
  "expired",
]);

/** A bid by `bidderTeamId` for a player of `sellerTeamId`. */
export const transferOffers = pgTable(
  "transfer_offers",
  {
    id: uuid("id").primaryKey().defaultRandom(),
    playerId: uuid("player_id")
      .notNull()
      .references(() => players.id, { onDelete: "cascade" }),
    sellerTeamId: uuid("seller_team_id")
      .notNull()
      .references(() => teams.id, { onDelete: "cascade" }),
    bidderTeamId: uuid("bidder_team_id")
      .notNull()
      .references(() => teams.id, { onDelete: "cascade" }),
    amount: integer("amount").notNull(),
    status: offerStatus("status").notNull().default("pending"),
    expiresAt: timestamp("expires_at", { withTimezone: true }).notNull(),
    ...timestamps,
  },
  (t) => [
    index("transfer_offers_seller_idx").on(t.sellerTeamId, t.status),
    index("transfer_offers_bidder_idx").on(t.bidderTeamId, t.status),
    index("transfer_offers_player_idx").on(t.playerId),
  ],
).enableRLS();

export const notificationKind = pgEnum("notification_kind", [
  "match",
  "transfer",
  "training",
  "injury",
  "finance",
  "club",
  "system",
]);

export const notifications = pgTable(
  "notifications",
  {
    id: uuid("id").primaryKey().defaultRandom(),
    userId: uuid("user_id")
      .notNull()
      .references(() => authUsers.id, { onDelete: "cascade" }),
    kind: notificationKind("kind").notNull(),
    title: text("title").notNull(),
    body: text("body"),
    /** Same-site path the notification opens. */
    href: text("href"),
    readAt: timestamp("read_at", { withTimezone: true }),
    createdAt: timestamps.createdAt,
  },
  (t) => [index("notifications_user_created_idx").on(t.userId, t.createdAt.desc())],
).enableRLS();

/** Direct messages between managers. */
export const messages = pgTable(
  "messages",
  {
    id: uuid("id").primaryKey().defaultRandom(),
    fromUserId: uuid("from_user_id")
      .notNull()
      .references(() => authUsers.id, { onDelete: "cascade" }),
    toUserId: uuid("to_user_id")
      .notNull()
      .references(() => authUsers.id, { onDelete: "cascade" }),
    body: text("body").notNull(),
    readAt: timestamp("read_at", { withTimezone: true }),
    createdAt: timestamps.createdAt,
  },
  (t) => [
    index("messages_to_created_idx").on(t.toUserId, t.createdAt.desc()),
    index("messages_from_idx").on(t.fromUserId),
  ],
).enableRLS();

export const staffRole = pgEnum("staff_role", [
  "head_coach",
  "assistant",
  "goalkeeping",
  "fitness",
  "scout",
  "youth",
]);

/** Coaching staff. `ratings`: 1–20 per skill, e.g. { tactics: 17, motivation: 15 }. */
export const staff = pgTable(
  "staff",
  {
    id: uuid("id").primaryKey().defaultRandom(),
    teamId: uuid("team_id")
      .notNull()
      .references(() => teams.id, { onDelete: "cascade" }),
    role: staffRole("role").notNull(),
    name: text("name").notNull(),
    ratings: jsonb("ratings").$type<Record<string, number>>().notNull(),
    /** Whole euros per week. */
    wage: integer("wage").notNull(),
    contractUntil: timestamp("contract_until", { withTimezone: true }),
    ...timestamps,
  },
  (t) => [uniqueIndex("staff_team_role_uq").on(t.teamId, t.role)],
).enableRLS();

/** The weekly training plan: one focus per weekday (0 = Monday). Intensity 0–100; 0 is rest. */
export const trainingPlans = pgTable(
  "training_plans",
  {
    teamId: uuid("team_id")
      .notNull()
      .references(() => teams.id, { onDelete: "cascade" }),
    weekday: smallint("weekday").notNull(),
    focus: text("focus").notNull(),
    intensity: smallint("intensity").notNull(),
    updatedAt: timestamps.updatedAt,
  },
  (t) => [primaryKey({ columns: [t.teamId, t.weekday] })],
).enableRLS();

export const forumCategory = pgEnum("forum_category", [
  "general",
  "tactics",
  "transfers",
  "friendlies",
  "bugs",
]);

export const forumThreads = pgTable(
  "forum_threads",
  {
    id: uuid("id").primaryKey().defaultRandom(),
    authorId: uuid("author_id")
      .notNull()
      .references(() => authUsers.id, { onDelete: "cascade" }),
    category: forumCategory("category").notNull(),
    title: text("title").notNull(),
    lastPostAt: timestamp("last_post_at", { withTimezone: true }).notNull().defaultNow(),
    replies: integer("replies").notNull().default(0),
    ...timestamps,
  },
  (t) => [
    index("forum_threads_last_post_idx").on(t.lastPostAt.desc()),
    index("forum_threads_author_idx").on(t.authorId),
  ],
).enableRLS();

export const forumPosts = pgTable(
  "forum_posts",
  {
    id: uuid("id").primaryKey().defaultRandom(),
    threadId: uuid("thread_id")
      .notNull()
      .references(() => forumThreads.id, { onDelete: "cascade" }),
    authorId: uuid("author_id")
      .notNull()
      .references(() => authUsers.id, { onDelete: "cascade" }),
    body: text("body").notNull(),
    ...timestamps,
  },
  (t) => [
    index("forum_posts_thread_idx").on(t.threadId, t.createdAt),
    index("forum_posts_author_idx").on(t.authorId),
  ],
).enableRLS();

// ---------------------------------------------------------------- support (donations)

/**
 * A donation paid through Stripe Checkout, recorded once when the manager returns to /support/thanks (the Checkout
 * session is read back from Stripe; the session id makes the write idempotent). Amount in cents.
 */
export const donations = pgTable(
  "donations",
  {
    id: uuid("id").primaryKey().defaultRandom(),
    userId: uuid("user_id")
      .notNull()
      .references(() => authUsers.id, { onDelete: "cascade" }),
    stripeSessionId: text("stripe_session_id").notNull(),
    amount: integer("amount").notNull(),
    currency: text("currency").notNull(),
    /** Show the Supporter badge on the profile. */
    showBadge: boolean("show_badge").notNull().default(true),
    createdAt: timestamps.createdAt,
  },
  (t) => [
    uniqueIndex("donations_session_uq").on(t.stripeSessionId),
    index("donations_user_idx").on(t.userId),
  ],
).enableRLS();

/** Leagues a manager follows (Leagues → Follow). */
export const leagueFollows = pgTable(
  "league_follows",
  {
    userId: uuid("user_id")
      .notNull()
      .references(() => authUsers.id, { onDelete: "cascade" }),
    leagueId: uuid("league_id")
      .notNull()
      .references(() => leagues.id, { onDelete: "cascade" }),
    createdAt: timestamps.createdAt,
  },
  (t) => [
    primaryKey({ columns: [t.userId, t.leagueId] }),
    index("league_follows_league_idx").on(t.leagueId),
  ],
).enableRLS();

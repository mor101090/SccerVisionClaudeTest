CREATE TYPE "public"."lobby_status" AS ENUM('open', 'ready', 'playing', 'finished', 'closed');--> statement-breakpoint
CREATE TYPE "public"."match_mode" AS ENUM('staged', 'random', 'live', 'online');--> statement-breakpoint
-- neon_auth.user is managed by Neon Auth (not created here).
CREATE TABLE "league_members" (
	"league_id" uuid NOT NULL,
	"team_id" uuid NOT NULL,
	"joined_at" timestamp with time zone DEFAULT now() NOT NULL,
	CONSTRAINT "league_members_league_id_team_id_pk" PRIMARY KEY("league_id","team_id")
);
--> statement-breakpoint
CREATE TABLE "leagues" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"owner_id" uuid NOT NULL,
	"name" text NOT NULL,
	"season" integer DEFAULT 1 NOT NULL,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL,
	"updated_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
CREATE TABLE "lobbies" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"code" text NOT NULL,
	"host_id" uuid NOT NULL,
	"guest_id" uuid,
	"status" "lobby_status" DEFAULT 'open' NOT NULL,
	"seed" integer NOT NULL,
	"conditions" jsonb NOT NULL,
	"host_plan" text,
	"guest_plan" text,
	"match_id" uuid,
	"expires_at" timestamp with time zone DEFAULT now() + interval '2 hours' NOT NULL,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL,
	"updated_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
CREATE TABLE "lobby_inputs" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"lobby_id" uuid NOT NULL,
	"seq" integer NOT NULL,
	"user_id" uuid NOT NULL,
	"team" smallint NOT NULL,
	"kind" text NOT NULL,
	"payload" jsonb NOT NULL,
	"frame" integer NOT NULL,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
CREATE TABLE "matches" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"user_id" uuid NOT NULL,
	"league_id" uuid,
	"home_team_id" uuid,
	"away_team_id" uuid,
	"home_name" text NOT NULL,
	"away_name" text NOT NULL,
	"mode" "match_mode" NOT NULL,
	"seed" integer NOT NULL,
	"engine_version" text NOT NULL,
	"conditions" jsonb NOT NULL,
	"home_plan" text,
	"away_plan" text,
	"home_score" smallint NOT NULL,
	"away_score" smallint NOT NULL,
	"summary" jsonb,
	"replay_key" text,
	"verified" boolean DEFAULT false NOT NULL,
	"played_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
CREATE TABLE "players" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"team_id" uuid NOT NULL,
	"number" smallint NOT NULL,
	"name" text NOT NULL,
	"short_name" text NOT NULL,
	"role" text NOT NULL,
	"attributes" jsonb NOT NULL,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL,
	"updated_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
CREATE TABLE "teams" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"owner_id" uuid NOT NULL,
	"name" text NOT NULL,
	"short_name" text NOT NULL,
	"kit" jsonb NOT NULL,
	"plan" text,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL,
	"updated_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
ALTER TABLE "league_members" ADD CONSTRAINT "league_members_league_id_leagues_id_fk" FOREIGN KEY ("league_id") REFERENCES "public"."leagues"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "league_members" ADD CONSTRAINT "league_members_team_id_teams_id_fk" FOREIGN KEY ("team_id") REFERENCES "public"."teams"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "leagues" ADD CONSTRAINT "leagues_owner_id_user_id_fk" FOREIGN KEY ("owner_id") REFERENCES "neon_auth"."user"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "lobbies" ADD CONSTRAINT "lobbies_host_id_user_id_fk" FOREIGN KEY ("host_id") REFERENCES "neon_auth"."user"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "lobbies" ADD CONSTRAINT "lobbies_guest_id_user_id_fk" FOREIGN KEY ("guest_id") REFERENCES "neon_auth"."user"("id") ON DELETE set null ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "lobbies" ADD CONSTRAINT "lobbies_match_id_matches_id_fk" FOREIGN KEY ("match_id") REFERENCES "public"."matches"("id") ON DELETE set null ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "lobby_inputs" ADD CONSTRAINT "lobby_inputs_lobby_id_lobbies_id_fk" FOREIGN KEY ("lobby_id") REFERENCES "public"."lobbies"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "lobby_inputs" ADD CONSTRAINT "lobby_inputs_user_id_user_id_fk" FOREIGN KEY ("user_id") REFERENCES "neon_auth"."user"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "matches" ADD CONSTRAINT "matches_user_id_user_id_fk" FOREIGN KEY ("user_id") REFERENCES "neon_auth"."user"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "matches" ADD CONSTRAINT "matches_league_id_leagues_id_fk" FOREIGN KEY ("league_id") REFERENCES "public"."leagues"("id") ON DELETE set null ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "matches" ADD CONSTRAINT "matches_home_team_id_teams_id_fk" FOREIGN KEY ("home_team_id") REFERENCES "public"."teams"("id") ON DELETE set null ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "matches" ADD CONSTRAINT "matches_away_team_id_teams_id_fk" FOREIGN KEY ("away_team_id") REFERENCES "public"."teams"("id") ON DELETE set null ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "players" ADD CONSTRAINT "players_team_id_teams_id_fk" FOREIGN KEY ("team_id") REFERENCES "public"."teams"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "teams" ADD CONSTRAINT "teams_owner_id_user_id_fk" FOREIGN KEY ("owner_id") REFERENCES "neon_auth"."user"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
CREATE INDEX "leagues_owner_idx" ON "leagues" USING btree ("owner_id");--> statement-breakpoint
CREATE UNIQUE INDEX "lobbies_active_code_uq" ON "lobbies" USING btree ("code") WHERE "lobbies"."status" in ('open', 'ready', 'playing');--> statement-breakpoint
CREATE UNIQUE INDEX "lobby_inputs_seq_uq" ON "lobby_inputs" USING btree ("lobby_id","seq");--> statement-breakpoint
CREATE INDEX "matches_user_played_idx" ON "matches" USING btree ("user_id","played_at" DESC NULLS LAST);--> statement-breakpoint
CREATE INDEX "matches_league_idx" ON "matches" USING btree ("league_id");--> statement-breakpoint
CREATE UNIQUE INDEX "players_team_number_uq" ON "players" USING btree ("team_id","number");--> statement-breakpoint
CREATE INDEX "teams_owner_idx" ON "teams" USING btree ("owner_id");
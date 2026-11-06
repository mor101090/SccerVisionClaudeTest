CREATE TYPE "public"."competition" AS ENUM('league', 'cup', 'friendly');--> statement-breakpoint
CREATE TYPE "public"."finance_kind" AS ENUM('tickets', 'sponsors', 'tv', 'prize', 'player_wages', 'staff_wages', 'transfer', 'construction');--> statement-breakpoint
CREATE TYPE "public"."fixture_status" AS ENUM('scheduled', 'live', 'finished', 'postponed');--> statement-breakpoint
CREATE TYPE "public"."forum_category" AS ENUM('general', 'tactics', 'transfers', 'friendlies', 'bugs');--> statement-breakpoint
CREATE TYPE "public"."notification_kind" AS ENUM('match', 'transfer', 'training', 'injury', 'finance', 'club', 'system');--> statement-breakpoint
CREATE TYPE "public"."offer_status" AS ENUM('pending', 'accepted', 'rejected', 'withdrawn', 'expired');--> statement-breakpoint
CREATE TYPE "public"."season_status" AS ENUM('preseason', 'active', 'break', 'finished');--> statement-breakpoint
CREATE TYPE "public"."staff_role" AS ENUM('head_coach', 'assistant', 'goalkeeping', 'fitness', 'scout', 'youth');--> statement-breakpoint
CREATE TABLE "club_fans" (
	"team_id" uuid PRIMARY KEY NOT NULL,
	"mood" smallint NOT NULL,
	"members" integer DEFAULT 0 NOT NULL,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL,
	"updated_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
ALTER TABLE "club_fans" ENABLE ROW LEVEL SECURITY;--> statement-breakpoint
CREATE TABLE "club_finances" (
	"team_id" uuid PRIMARY KEY NOT NULL,
	"balance" integer NOT NULL,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL,
	"updated_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
ALTER TABLE "club_finances" ENABLE ROW LEVEL SECURITY;--> statement-breakpoint
CREATE TABLE "finance_entries" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"team_id" uuid NOT NULL,
	"week_of" timestamp with time zone NOT NULL,
	"kind" "finance_kind" NOT NULL,
	"amount" integer NOT NULL,
	"note" text,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
ALTER TABLE "finance_entries" ENABLE ROW LEVEL SECURITY;--> statement-breakpoint
CREATE TABLE "fixtures" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"season_id" uuid,
	"league_id" uuid,
	"competition" "competition" NOT NULL,
	"round" smallint,
	"home_team_id" uuid,
	"away_team_id" uuid,
	"home_name" text NOT NULL,
	"away_name" text NOT NULL,
	"kickoff_at" timestamp with time zone NOT NULL,
	"status" "fixture_status" DEFAULT 'scheduled' NOT NULL,
	"home_score" smallint,
	"away_score" smallint,
	"match_id" uuid,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL,
	"updated_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
ALTER TABLE "fixtures" ENABLE ROW LEVEL SECURITY;--> statement-breakpoint
CREATE TABLE "forum_posts" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"thread_id" uuid NOT NULL,
	"author_id" uuid NOT NULL,
	"body" text NOT NULL,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL,
	"updated_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
ALTER TABLE "forum_posts" ENABLE ROW LEVEL SECURITY;--> statement-breakpoint
CREATE TABLE "forum_threads" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"author_id" uuid NOT NULL,
	"category" "forum_category" NOT NULL,
	"title" text NOT NULL,
	"last_post_at" timestamp with time zone DEFAULT now() NOT NULL,
	"replies" integer DEFAULT 0 NOT NULL,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL,
	"updated_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
ALTER TABLE "forum_threads" ENABLE ROW LEVEL SECURITY;--> statement-breakpoint
CREATE TABLE "messages" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"from_user_id" uuid NOT NULL,
	"to_user_id" uuid NOT NULL,
	"body" text NOT NULL,
	"read_at" timestamp with time zone,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
ALTER TABLE "messages" ENABLE ROW LEVEL SECURITY;--> statement-breakpoint
CREATE TABLE "notifications" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"user_id" uuid NOT NULL,
	"kind" "notification_kind" NOT NULL,
	"title" text NOT NULL,
	"body" text,
	"href" text,
	"read_at" timestamp with time zone,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
ALTER TABLE "notifications" ENABLE ROW LEVEL SECURITY;--> statement-breakpoint
CREATE TABLE "seasons" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"number" integer NOT NULL,
	"status" "season_status" DEFAULT 'preseason' NOT NULL,
	"starts_at" timestamp with time zone NOT NULL,
	"ends_at" timestamp with time zone NOT NULL,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL,
	"updated_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
ALTER TABLE "seasons" ENABLE ROW LEVEL SECURITY;--> statement-breakpoint
CREATE TABLE "stadiums" (
	"team_id" uuid PRIMARY KEY NOT NULL,
	"name" text NOT NULL,
	"capacity" integer NOT NULL,
	"roof" boolean DEFAULT false NOT NULL,
	"season_tickets" integer DEFAULT 0 NOT NULL,
	"ticket_price" integer NOT NULL,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL,
	"updated_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
ALTER TABLE "stadiums" ENABLE ROW LEVEL SECURITY;--> statement-breakpoint
CREATE TABLE "staff" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"team_id" uuid NOT NULL,
	"role" "staff_role" NOT NULL,
	"name" text NOT NULL,
	"ratings" jsonb NOT NULL,
	"wage" integer NOT NULL,
	"contract_until" timestamp with time zone,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL,
	"updated_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
ALTER TABLE "staff" ENABLE ROW LEVEL SECURITY;--> statement-breakpoint
CREATE TABLE "standings" (
	"league_id" uuid NOT NULL,
	"team_id" uuid NOT NULL,
	"position" smallint NOT NULL,
	"played" smallint DEFAULT 0 NOT NULL,
	"won" smallint DEFAULT 0 NOT NULL,
	"drawn" smallint DEFAULT 0 NOT NULL,
	"lost" smallint DEFAULT 0 NOT NULL,
	"goals_for" smallint DEFAULT 0 NOT NULL,
	"goals_against" smallint DEFAULT 0 NOT NULL,
	"points" smallint DEFAULT 0 NOT NULL,
	"form" text DEFAULT '' NOT NULL,
	"updated_at" timestamp with time zone DEFAULT now() NOT NULL,
	CONSTRAINT "standings_league_id_team_id_pk" PRIMARY KEY("league_id","team_id")
);
--> statement-breakpoint
ALTER TABLE "standings" ENABLE ROW LEVEL SECURITY;--> statement-breakpoint
CREATE TABLE "team_ratings" (
	"team_id" uuid PRIMARY KEY NOT NULL,
	"rating" integer NOT NULL,
	"world_rank" integer NOT NULL,
	"previous_rank" integer,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL,
	"updated_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
ALTER TABLE "team_ratings" ENABLE ROW LEVEL SECURITY;--> statement-breakpoint
CREATE TABLE "training_plans" (
	"team_id" uuid NOT NULL,
	"weekday" smallint NOT NULL,
	"focus" text NOT NULL,
	"intensity" smallint NOT NULL,
	"updated_at" timestamp with time zone DEFAULT now() NOT NULL,
	CONSTRAINT "training_plans_team_id_weekday_pk" PRIMARY KEY("team_id","weekday")
);
--> statement-breakpoint
ALTER TABLE "training_plans" ENABLE ROW LEVEL SECURITY;--> statement-breakpoint
CREATE TABLE "transfer_offers" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"player_id" uuid NOT NULL,
	"seller_team_id" uuid NOT NULL,
	"bidder_team_id" uuid NOT NULL,
	"amount" integer NOT NULL,
	"status" "offer_status" DEFAULT 'pending' NOT NULL,
	"expires_at" timestamp with time zone NOT NULL,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL,
	"updated_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
ALTER TABLE "transfer_offers" ENABLE ROW LEVEL SECURITY;--> statement-breakpoint
ALTER TABLE "profiles" ADD COLUMN "onboarding_seen_at" timestamp with time zone;--> statement-breakpoint
ALTER TABLE "club_fans" ADD CONSTRAINT "club_fans_team_id_teams_id_fk" FOREIGN KEY ("team_id") REFERENCES "public"."teams"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "club_finances" ADD CONSTRAINT "club_finances_team_id_teams_id_fk" FOREIGN KEY ("team_id") REFERENCES "public"."teams"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "finance_entries" ADD CONSTRAINT "finance_entries_team_id_teams_id_fk" FOREIGN KEY ("team_id") REFERENCES "public"."teams"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "fixtures" ADD CONSTRAINT "fixtures_season_id_seasons_id_fk" FOREIGN KEY ("season_id") REFERENCES "public"."seasons"("id") ON DELETE set null ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "fixtures" ADD CONSTRAINT "fixtures_league_id_leagues_id_fk" FOREIGN KEY ("league_id") REFERENCES "public"."leagues"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "fixtures" ADD CONSTRAINT "fixtures_home_team_id_teams_id_fk" FOREIGN KEY ("home_team_id") REFERENCES "public"."teams"("id") ON DELETE set null ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "fixtures" ADD CONSTRAINT "fixtures_away_team_id_teams_id_fk" FOREIGN KEY ("away_team_id") REFERENCES "public"."teams"("id") ON DELETE set null ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "fixtures" ADD CONSTRAINT "fixtures_match_id_matches_id_fk" FOREIGN KEY ("match_id") REFERENCES "public"."matches"("id") ON DELETE set null ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "forum_posts" ADD CONSTRAINT "forum_posts_thread_id_forum_threads_id_fk" FOREIGN KEY ("thread_id") REFERENCES "public"."forum_threads"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "forum_posts" ADD CONSTRAINT "forum_posts_author_id_users_id_fk" FOREIGN KEY ("author_id") REFERENCES "auth"."users"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "forum_threads" ADD CONSTRAINT "forum_threads_author_id_users_id_fk" FOREIGN KEY ("author_id") REFERENCES "auth"."users"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "messages" ADD CONSTRAINT "messages_from_user_id_users_id_fk" FOREIGN KEY ("from_user_id") REFERENCES "auth"."users"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "messages" ADD CONSTRAINT "messages_to_user_id_users_id_fk" FOREIGN KEY ("to_user_id") REFERENCES "auth"."users"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "notifications" ADD CONSTRAINT "notifications_user_id_users_id_fk" FOREIGN KEY ("user_id") REFERENCES "auth"."users"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "stadiums" ADD CONSTRAINT "stadiums_team_id_teams_id_fk" FOREIGN KEY ("team_id") REFERENCES "public"."teams"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "staff" ADD CONSTRAINT "staff_team_id_teams_id_fk" FOREIGN KEY ("team_id") REFERENCES "public"."teams"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "standings" ADD CONSTRAINT "standings_league_id_leagues_id_fk" FOREIGN KEY ("league_id") REFERENCES "public"."leagues"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "standings" ADD CONSTRAINT "standings_team_id_teams_id_fk" FOREIGN KEY ("team_id") REFERENCES "public"."teams"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "team_ratings" ADD CONSTRAINT "team_ratings_team_id_teams_id_fk" FOREIGN KEY ("team_id") REFERENCES "public"."teams"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "training_plans" ADD CONSTRAINT "training_plans_team_id_teams_id_fk" FOREIGN KEY ("team_id") REFERENCES "public"."teams"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "transfer_offers" ADD CONSTRAINT "transfer_offers_player_id_players_id_fk" FOREIGN KEY ("player_id") REFERENCES "public"."players"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "transfer_offers" ADD CONSTRAINT "transfer_offers_seller_team_id_teams_id_fk" FOREIGN KEY ("seller_team_id") REFERENCES "public"."teams"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "transfer_offers" ADD CONSTRAINT "transfer_offers_bidder_team_id_teams_id_fk" FOREIGN KEY ("bidder_team_id") REFERENCES "public"."teams"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
CREATE INDEX "finance_entries_team_week_idx" ON "finance_entries" USING btree ("team_id","week_of");--> statement-breakpoint
CREATE INDEX "fixtures_home_kickoff_idx" ON "fixtures" USING btree ("home_team_id","kickoff_at");--> statement-breakpoint
CREATE INDEX "fixtures_away_kickoff_idx" ON "fixtures" USING btree ("away_team_id","kickoff_at");--> statement-breakpoint
CREATE INDEX "fixtures_league_round_idx" ON "fixtures" USING btree ("league_id","round");--> statement-breakpoint
CREATE INDEX "forum_posts_thread_idx" ON "forum_posts" USING btree ("thread_id","created_at");--> statement-breakpoint
CREATE INDEX "forum_threads_last_post_idx" ON "forum_threads" USING btree ("last_post_at" DESC NULLS LAST);--> statement-breakpoint
CREATE INDEX "messages_to_created_idx" ON "messages" USING btree ("to_user_id","created_at" DESC NULLS LAST);--> statement-breakpoint
CREATE INDEX "notifications_user_created_idx" ON "notifications" USING btree ("user_id","created_at" DESC NULLS LAST);--> statement-breakpoint
CREATE UNIQUE INDEX "seasons_number_uq" ON "seasons" USING btree ("number");--> statement-breakpoint
CREATE UNIQUE INDEX "staff_team_role_uq" ON "staff" USING btree ("team_id","role");--> statement-breakpoint
CREATE INDEX "standings_team_idx" ON "standings" USING btree ("team_id");--> statement-breakpoint
CREATE INDEX "transfer_offers_seller_idx" ON "transfer_offers" USING btree ("seller_team_id","status");--> statement-breakpoint
CREATE INDEX "transfer_offers_bidder_idx" ON "transfer_offers" USING btree ("bidder_team_id","status");
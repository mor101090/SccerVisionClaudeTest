ALTER TABLE "players" ADD COLUMN "position" text;--> statement-breakpoint
ALTER TABLE "players" ADD COLUMN "level" text;--> statement-breakpoint
ALTER TABLE "players" ADD COLUMN "profile" jsonb;--> statement-breakpoint
ALTER TABLE "players" ADD COLUMN "starter_slot" smallint;--> statement-breakpoint
ALTER TABLE "players" ADD COLUMN "bench_order" smallint;--> statement-breakpoint
ALTER TABLE "profiles" ADD COLUMN "nationality" text;--> statement-breakpoint
ALTER TABLE "profiles" ADD COLUMN "invited_by" uuid;--> statement-breakpoint
ALTER TABLE "teams" ADD COLUMN "kits" jsonb;--> statement-breakpoint
ALTER TABLE "teams" ADD COLUMN "crest" jsonb;--> statement-breakpoint
ALTER TABLE "teams" ADD COLUMN "name_pools" jsonb;--> statement-breakpoint
ALTER TABLE "teams" ADD COLUMN "formation" text DEFAULT '4-4-2' NOT NULL;--> statement-breakpoint
ALTER TABLE "teams" ADD COLUMN "squad_seed" integer;--> statement-breakpoint
ALTER TABLE "teams" ADD COLUMN "regenerations_left" smallint DEFAULT 2 NOT NULL;--> statement-breakpoint
ALTER TABLE "teams" ADD COLUMN "captain_id" uuid;--> statement-breakpoint
ALTER TABLE "teams" ADD COLUMN "status" text DEFAULT 'draft' NOT NULL;--> statement-breakpoint
ALTER TABLE "profiles" ADD CONSTRAINT "profiles_invited_by_user_id_fk" FOREIGN KEY ("invited_by") REFERENCES "neon_auth"."user"("id") ON DELETE set null ON UPDATE no action;--> statement-breakpoint
CREATE UNIQUE INDEX "teams_owner_uq" ON "teams" USING btree ("owner_id");
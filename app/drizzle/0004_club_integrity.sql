DROP INDEX IF EXISTS "teams_owner_idx";--> statement-breakpoint
ALTER TABLE "matches" ADD COLUMN "home_squad" text;--> statement-breakpoint
CREATE UNIQUE INDEX "players_team_starter_uq" ON "players" USING btree ("team_id","starter_slot") WHERE "players"."starter_slot" is not null;
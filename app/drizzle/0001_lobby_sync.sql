ALTER TABLE "lobby_inputs" ALTER COLUMN "seq" DROP NOT NULL;--> statement-breakpoint
ALTER TABLE "lobby_inputs" ALTER COLUMN "frame" DROP NOT NULL;--> statement-breakpoint
ALTER TABLE "lobbies" ADD COLUMN "host_frame" integer DEFAULT 0 NOT NULL;--> statement-breakpoint
ALTER TABLE "lobbies" ADD COLUMN "final_frame" integer;
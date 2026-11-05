-- Passkeys the app approved, and passkey sessions it vetted. Supabase lets any valid session register a passkey
-- directly (no recent-login rule there), so the app approves a passkey only when the server sees a recent login, and a
-- passkey sign-in counts only when the passkey used is approved (server/passkeys.ts).
-- password_sessions and login_attempts.locked_at already exist (0003, 0004 were hand-written): drizzle-kit emitted them
-- again because its snapshot didn't have them; they are left out here.

CREATE TABLE "approved_passkeys" (
	"passkey_id" text PRIMARY KEY NOT NULL,
	"user_id" uuid NOT NULL,
	"approved_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
ALTER TABLE "approved_passkeys" ENABLE ROW LEVEL SECURITY;--> statement-breakpoint
CREATE TABLE "passkey_sessions" (
	"session_id" text PRIMARY KEY NOT NULL,
	"user_id" uuid NOT NULL,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
ALTER TABLE "passkey_sessions" ENABLE ROW LEVEL SECURITY;--> statement-breakpoint
ALTER TABLE "approved_passkeys" ADD CONSTRAINT "approved_passkeys_user_id_users_id_fk" FOREIGN KEY ("user_id") REFERENCES "auth"."users"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
CREATE INDEX "approved_passkeys_user_id_idx" ON "approved_passkeys" USING btree ("user_id");

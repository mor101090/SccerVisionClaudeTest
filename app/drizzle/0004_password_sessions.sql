-- Password sessions the app itself created. A direct Auth API password grant is a real Supabase
-- session, but it is not in this table, so the app ignores it. Supabase's password-verification hook
-- is a paid feature and is not used. The Server Actions insert a row after signInWithPassword.

CREATE TABLE "password_sessions" (
  "session_id" text PRIMARY KEY,
  "user_id" uuid NOT NULL,
  "created_at" timestamp with time zone DEFAULT now() NOT NULL
);--> statement-breakpoint
ALTER TABLE "password_sessions" ENABLE ROW LEVEL SECURITY;

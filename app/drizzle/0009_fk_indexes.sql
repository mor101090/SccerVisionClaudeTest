CREATE INDEX "fixtures_season_idx" ON "fixtures" USING btree ("season_id");--> statement-breakpoint
CREATE INDEX "fixtures_match_idx" ON "fixtures" USING btree ("match_id");--> statement-breakpoint
CREATE INDEX "forum_posts_author_idx" ON "forum_posts" USING btree ("author_id");--> statement-breakpoint
CREATE INDEX "forum_threads_author_idx" ON "forum_threads" USING btree ("author_id");--> statement-breakpoint
CREATE INDEX "league_members_team_idx" ON "league_members" USING btree ("team_id");--> statement-breakpoint
CREATE INDEX "lobbies_host_idx" ON "lobbies" USING btree ("host_id");--> statement-breakpoint
CREATE INDEX "lobbies_guest_idx" ON "lobbies" USING btree ("guest_id");--> statement-breakpoint
CREATE INDEX "lobbies_match_idx" ON "lobbies" USING btree ("match_id");--> statement-breakpoint
CREATE INDEX "lobby_inputs_user_idx" ON "lobby_inputs" USING btree ("user_id");--> statement-breakpoint
CREATE INDEX "matches_home_team_idx" ON "matches" USING btree ("home_team_id");--> statement-breakpoint
CREATE INDEX "matches_away_team_idx" ON "matches" USING btree ("away_team_id");--> statement-breakpoint
CREATE INDEX "messages_from_idx" ON "messages" USING btree ("from_user_id");--> statement-breakpoint
CREATE INDEX "profiles_invited_by_idx" ON "profiles" USING btree ("invited_by");--> statement-breakpoint
CREATE INDEX "transfer_offers_player_idx" ON "transfer_offers" USING btree ("player_id");--> statement-breakpoint
-- Supabase's ensure_rls event trigger function must not be callable through the REST API (security advisor
-- 0028 / 0029). The event trigger keeps running as its owner.
DO $$ BEGIN
  IF EXISTS (SELECT 1 FROM pg_proc WHERE proname = 'rls_auto_enable' AND pronamespace = 'public'::regnamespace) THEN
    REVOKE EXECUTE ON FUNCTION public.rls_auto_enable() FROM PUBLIC, anon, authenticated;
  END IF;
END $$;

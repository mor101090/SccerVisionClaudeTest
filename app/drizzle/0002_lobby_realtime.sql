-- Live lobbies on Supabase Realtime (Broadcast from Database).
-- Every committed change to a lobby or its inputs sends a small "changed" message on the private channel
-- `lobby:<lobby id>`; clients then re-read the lobby through GET /api/lobbies/[id] (one consistent read).
-- Only the lobby's host and guest may receive it (RLS on realtime.messages).

CREATE SCHEMA IF NOT EXISTS private;--> statement-breakpoint
REVOKE ALL ON SCHEMA private FROM public, anon;--> statement-breakpoint
GRANT USAGE ON SCHEMA private TO authenticated;--> statement-breakpoint

-- Topic membership check for the RLS policy. SECURITY DEFINER: lobbies has RLS with no policies.
CREATE OR REPLACE FUNCTION private.is_lobby_member(topic text)
RETURNS boolean
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = ''
AS $$
  SELECT EXISTS (
    SELECT 1 FROM public.lobbies l
     WHERE topic = 'lobby:' || l.id::text
       AND (l.host_id = (SELECT auth.uid()) OR l.guest_id = (SELECT auth.uid()))
  );
$$;--> statement-breakpoint
REVOKE ALL ON FUNCTION private.is_lobby_member(text) FROM public, anon;--> statement-breakpoint
GRANT EXECUTE ON FUNCTION private.is_lobby_member(text) TO authenticated;--> statement-breakpoint

CREATE POLICY "lobby members receive lobby broadcasts"
  ON realtime.messages FOR SELECT TO authenticated
  USING (
    realtime.messages.extension = 'broadcast'
    AND (SELECT private.is_lobby_member((SELECT realtime.topic())))
  );--> statement-breakpoint

-- TG_ARGV[0] names the column holding the lobby id ('id' on lobbies, 'lobby_id' on lobby_inputs).
CREATE OR REPLACE FUNCTION private.broadcast_lobby_change()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
BEGIN
  PERFORM realtime.send(
    jsonb_build_object('table', TG_TABLE_NAME),
    'changed',
    'lobby:' || (to_jsonb(NEW) ->> TG_ARGV[0]),
    true
  );
  RETURN NULL;
END;
$$;--> statement-breakpoint
REVOKE ALL ON FUNCTION private.broadcast_lobby_change() FROM public, anon, authenticated;--> statement-breakpoint

CREATE TRIGGER lobbies_broadcast
  AFTER INSERT OR UPDATE ON public.lobbies
  FOR EACH ROW EXECUTE FUNCTION private.broadcast_lobby_change('id');--> statement-breakpoint
CREATE TRIGGER lobby_inputs_broadcast
  AFTER INSERT OR UPDATE ON public.lobby_inputs
  FOR EACH ROW EXECUTE FUNCTION private.broadcast_lobby_change('lobby_id');

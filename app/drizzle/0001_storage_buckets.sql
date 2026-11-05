-- Supabase Storage: private bucket for match replays (gzip, written and read by the server with the secret key).
INSERT INTO storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
VALUES ('replays', 'replays', false, 52428800, ARRAY['application/gzip'])
ON CONFLICT (id) DO NOTHING;

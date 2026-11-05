import { defineConfig } from "@neon/config/v1";

/**
 * Neon infrastructure for Soccer Vision (region aws-eu-central-1).
 *
 * Branch model:
 *   main             production
 *   dev              shared by every PR preview (default)
 *   preview/pr-<n>   own branch for PRs labeled `own-db` (created from dev, auto-expires)
 *
 * Buckets, auth state and rows all branch together, so a preview never touches prod data.
 */
export default defineConfig({
  // Managed Better Auth: users and sessions live in the `neon_auth` schema.
  auth: true,

  buckets: {
    // Match replay frames + events. Private; served via presigned URLs.
    replays: { access: "private" },
    // Crests, avatars, portraits. Public read; content-hashed keys.
    media: { access: "public_read" },
  },

  // Live lobbies (beta): browsers subscribe to a sealed live query (src/server/realtime.ts).
  // Only these origins may open the WebSocket (local dev, the PR e2e job, production); anywhere else
  // the lobby falls back to polling.
  preview: {
    realtime: {
      allowedOrigins: [
        "http://localhost:3000",
        "http://localhost:3100",
        "https://soccer.dev-spirit.com",
      ],
    },
  },

  branch: (branch) => {
    if (branch.exists) return {};
    if (branch.name.startsWith("preview/")) {
      return {
        ttl: "14d",
        postgres: {
          computeSettings: {
            autoscalingLimitMinCu: 0.25,
            autoscalingLimitMaxCu: 0.5,
            suspendTimeout: "5m",
          },
        },
      };
    }
    return {};
  },
});

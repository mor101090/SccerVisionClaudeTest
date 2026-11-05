/** Test account on the shared `dev` Neon branch only. Never valid on production. */
export const TEST_USER = {
  name: "E2E Tester",
  username: "E2E_Tester",
  email: "e2e-tester@soccer-vision.test",
  password: "sv-3a734a3b8666dd15",
};

/** Second account for two-player (online) tests. Shared `dev` branch only. */
export const GUEST_USER = {
  name: "E2E Guest",
  username: "E2E_Guest",
  email: "e2e-guest@soccer-vision.test",
  password: "sv-8480ad76f679b7c4",
};

/** Fixed address for the sign-up → verify test. Never receives mail (.test). Shared `dev` branch only. */
export const SIGNUP_EMAIL = "e2e-signup@soccer-vision.test";

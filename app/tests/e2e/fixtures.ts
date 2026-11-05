/** Test account on the Supabase dev project only (created by `yarn e2e:seed`). Never valid on production. */
export const TEST_USER = {
  name: "E2E Tester",
  username: "E2E_Tester",
  email: "e2e-tester@soccer-vision.test",
  password: "sv-3a734a3b8666dd15",
};

/** Second account for two-player (online) tests. Supabase dev project only. */
export const GUEST_USER = {
  name: "E2E Guest",
  username: "E2E_Guest",
  email: "e2e-guest@soccer-vision.test",
  password: "sv-8480ad76f679b7c4",
};

/** Fixed address for the sign-up → verify test. Never receives mail (.test). Supabase dev project only. */
export const SIGNUP_EMAIL = "e2e-signup@soccer-vision.test";

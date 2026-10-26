import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// The Unity WebGL build is copied to public/unity/SoccerVision (Unity menu: SoccerVision > Build WebGL for React).
export default defineConfig({
  plugins: [react()],
  server: { port: 5173 },
});

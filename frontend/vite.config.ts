import { defineConfig } from "vitest/config";
import vue from "@vitejs/plugin-vue";
import vueDevTools from "vite-plugin-vue-devtools";
import path from "path";

// Backend that /api and /login are proxied to: the Flask backend by default; the Aspire AppHost
// (backend_new/apphost) sets API_PROXY_TARGET to the .NET backend.
const apiProxyTarget = process.env.API_PROXY_TARGET ?? "http://127.0.0.1:5000";

export default defineConfig({
  plugins: [vue(), vueDevTools()],
  resolve: {
    alias: {
      "@": path.resolve(import.meta.dirname, "./src"),
    },
  },
  test: {
    environment: "node",
  },
  server: {
    proxy: {
      "/api": {
        target: apiProxyTarget,
        changeOrigin: false,
      },
      "/login": {
        target: apiProxyTarget,
        changeOrigin: false,
      },
    },
  },
  build: {
    outDir: "../backend/public",
    emptyOutDir: true,
  },
});

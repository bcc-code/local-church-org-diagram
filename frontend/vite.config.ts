import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";
import path from "path";

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
  server: {
    proxy: {
      "/api": {
        target: "http://127.0.0.1:5000",
        changeOrigin: false,
      },
      "/login": {
        target: "http://127.0.0.1:5000",
        changeOrigin: false,
      },
    },
  },
  build: {
    outDir: "../backend/public",
    emptyOutDir: true,
  },
});

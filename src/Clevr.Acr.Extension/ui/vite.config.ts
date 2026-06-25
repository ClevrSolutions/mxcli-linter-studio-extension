import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import cssInjectedByJsPlugin from "vite-plugin-css-injected-by-js";
import { resolve } from "path";

export default defineConfig({
  plugins: [react(), cssInjectedByJsPlugin()],
  base: "./",
  root: "src",
  publicDir: "../public",
  build: {
    outDir: "../../wwwroot",
    emptyOutDir: true,
    rollupOptions: {
      output: {
        entryFileNames: "assets/main.js",
        chunkFileNames: "assets/[name].js",
        assetFileNames: "assets/[name].[ext]",
      },
    },
  },
  resolve: {
    alias: {
      "@": resolve(__dirname, "src"),
    },
  },
});

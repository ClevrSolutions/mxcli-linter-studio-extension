import { defineConfig, type Plugin } from "vite";
import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import cssInjectedByJsPlugin from "vite-plugin-css-injected-by-js";
import { resolve } from "path";

// Port where the C# TestHarness serves the API (--serve mode).
const HARNESS_PORT = 5174;

// References the chrome.webview shim served by the C# TestHarness (the single
// source: ChromeWebViewShimJs() in Program.cs), fetched through the /api proxy
// below. Loaded as a classic script in <head>, so it runs before the deferred
// module entry — window.chrome.webview exists when React loads.
// Only injected during `vite dev` (apply: "serve"), not in production builds.
function injectWebviewShim(): Plugin {
  return {
    name: "inject-webview-shim",
    apply: "serve",
    transformIndexHtml() {
      return [{ tag: "script", attrs: { src: "/chrome-webview-shim.js" }, injectTo: "head-prepend" }];
    },
  };
}

export default defineConfig({
  plugins: [tailwindcss(), react(), cssInjectedByJsPlugin(), injectWebviewShim()],
  base: "./",
  root: "src",
  publicDir: "../public",
  server: {
    port: 5173,
    proxy: {
      // Forward all /api/* calls to the C# harness so the UI can trigger scans,
      // manage exclusions, etc. while Vite handles HMR for the React components.
      "/api": {
        target: `http://localhost:${HARNESS_PORT}`,
        changeOrigin: true,
      },
      // The webview shim itself also comes from the harness (single source).
      "/chrome-webview-shim.js": {
        target: `http://localhost:${HARNESS_PORT}`,
        changeOrigin: true,
      },
    },
  },
  build: {
    outDir: "../../wwwroot",
    emptyOutDir: true,
    rollupOptions: {
      output: {
        entryFileNames: "main.js",
        chunkFileNames: "[name].js",
        assetFileNames: "[name].[ext]",
      },
    },
  },
  resolve: {
    alias: {
      "@": resolve(__dirname, "src"),
    },
  },
});


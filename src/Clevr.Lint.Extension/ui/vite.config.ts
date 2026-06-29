import { defineConfig, type Plugin } from "vite";
import react from "@vitejs/plugin-react";
import cssInjectedByJsPlugin from "vite-plugin-css-injected-by-js";
import { resolve } from "path";

// Port where the C# TestHarness serves the API (--serve mode).
const HARNESS_PORT = 5174;

// Mirrors ChromeWebViewShimJs() in Program.cs: mocks window.chrome.webview so the
// React app works in a plain browser without Studio Pro's WebView2 bridge.
const WEBVIEW_SHIM = `(function () {
  'use strict';
  var handlers = [];
  var evtSource = new EventSource('/api/events');
  evtSource.onmessage = function (e) {
    var msg;
    try { msg = JSON.parse(e.data); } catch { return; }
    var evt = new MessageEvent('message', {
      data: { message: msg.Message || msg.message, data: msg.Data || msg.data }
    });
    handlers.forEach(function (h) { try { h(evt); } catch (err) { console.error('[shim]', err); } });
  };
  evtSource.onerror = function () {
    console.warn('[shim] SSE connection lost — reload to reconnect.');
  };
  window.chrome = {
    webview: {
      postMessage: function (msg) {
        fetch('/api/message', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(msg),
        }).catch(function (err) { console.error('[shim] postMessage failed:', err); });
      },
      addEventListener: function (type, handler) {
        if (type === 'message' && handlers.indexOf(handler) === -1) handlers.push(handler);
      },
      removeEventListener: function (type, handler) {
        var idx = handlers.indexOf(handler);
        if (idx >= 0) handlers.splice(idx, 1);
      },
    },
  };
  console.log('[shim] chrome.webview mock active — Vite HMR dev server.');
})();`;

// Only injected during `vite dev` (apply: "serve"), not in production builds.
function injectWebviewShim(): Plugin {
  return {
    name: "inject-webview-shim",
    apply: "serve",
    transformIndexHtml() {
      return [{ tag: "script", children: WEBVIEW_SHIM, injectTo: "head-prepend" }];
    },
  };
}

export default defineConfig({
  plugins: [react(), cssInjectedByJsPlugin(), injectWebviewShim()],
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


import "./styles.css";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App } from "./App";

declare global {
  interface Window {
    chrome: {
      webview: {
        postMessage(msg: unknown): void;
        addEventListener(type: "message", handler: (e: MessageEvent) => void): void;
        removeEventListener(type: "message", handler: (e: MessageEvent) => void): void;
      };
    };
  }
}

const root = document.getElementById("root");
if (!root) throw new Error("No #root element found");
createRoot(root).render(
  <StrictMode>
    <App />
  </StrictMode>,
);

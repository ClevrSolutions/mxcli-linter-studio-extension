# CLEVR Lint Extension

A Mendix Studio Pro 11 extension that runs **mxcli** to lint a Mendix project and displays the findings as a structured report inside the IDE — grouped by category and severity, with filtering, exclusions, and HTML export.

![Build & Test](https://github.com/clevr/clevr-lint-extension/actions/workflows/build-and-test.yml/badge.svg)

## Architecture

```
mxcli (Go CLI, Apache-2.0)
  └─► LintScanService  (C# orchestrator)
        └─► Clevr.Lint.Normalizer  (pure .NET library — mxcli output → Violation[])
              └─► DockablePaneViewModel  (message bus, Studio Pro API)
                    └─► React UI  (WebView2 — filters, groups, export)
```

## Repo layout

| Folder | Purpose |
|--------|---------|
| `src/Clevr.Lint.Extension/` | Extension backend (C#) + React UI |
| `src/Clevr.Lint.Normalizer/` | Pure normalization library + 232 unit tests |
| `src/Clevr.Lint.TestHarness/` | Standalone CLI for debugging the normalizer |
| `dist/` | Pre-built distribution package for end users |
| `docs/` | Architecture and rules inventory |
| `.github/workflows/` | CI: build + test on every push/PR |

## Prerequisites

| Tool | Version |
|------|---------|
| .NET SDK | 10.0 |
| Mendix Studio Pro | 11.x |
| mxcli | v0.12.0 (downloaded by installer) |

## Build & test

```powershell
dotnet build src/Clevr.Lint.Normalizer/Clevr.Lint.Normalizer/Clevr.Lint.Normalizer.csproj
dotnet build src/Clevr.Lint.Extension/Clevr.Lint.Extension.csproj
dotnet test src/Clevr.Lint.Normalizer/Clevr.Lint.Normalizer.Tests/Clevr.Lint.Normalizer.Tests.csproj
.\Pack-Dist.ps1   # builds UI + extension, assembles dist/
```

## Install (end users)

See [`dist/README.md`](dist/README.md).

## Running without Studio Pro

The test harness lets you develop and test the full scan + UI cycle without a Studio Pro installation.

### Serve mode (full UI in a browser)

```powershell
dotnet run --project src/Clevr.Lint.TestHarness -- --serve "C:\Mendix\YourProject"
```

This starts an HTTP server on `http://localhost:5174/` and opens the browser automatically. The WebView2 bridge that Studio Pro normally provides is replaced by a JavaScript shim:

- **browser → C#**: `POST /api/message` (same message payloads as the real extension)
- **C# → browser**: `GET /api/events` (Server-Sent Events)

All features work the same as in Studio Pro: run scans, add/remove exclusions, export HTML reports, open URLs. The only limitation is that "Open Document" is unavailable — the harness prints a message instead.

The `extensionDir` argument (where `lint-scan-settings.json` lives) defaults to the extension's Debug build output. Provide it as a second argument to override:

```powershell
dotnet run --project src/Clevr.Lint.TestHarness -- --serve "C:\Mendix\YourProject" "C:\path\to\extensionDir"
```

Stop the server with **Ctrl+C**.

### Hot reload (instant UI changes)

Run the harness and Vite dev server side-by-side. Vite proxies all `/api/*` calls to the harness while React HMR applies component changes without a page refresh.

**Terminal 1 — C# harness (API):**
```powershell
dotnet run --project src/Clevr.Lint.TestHarness -- --serve "C:\Mendix\YourProject"
```

**Terminal 2 — Vite dev server (HMR):**
```powershell
cd src/Clevr.Lint.Extension/ui
npm run dev
```

Open **`http://localhost:5173`** in the browser. Edits to `.tsx` / `.ts` / `.css` files appear immediately — no rebuild, no refresh needed.

> `npm run build` produces the production bundle in `wwwroot/`. `npm run watch` runs `vite build --watch` (rebuild on save, no HMR) if you need the old workflow.

### Scan-only mode (JSON to stdout)

```powershell
dotnet run --project src/Clevr.Lint.TestHarness -- "C:\Mendix\YourProject"
```

Runs a full scan and writes the JSON result to stdout; progress and log lines go to stderr. Useful for piping output or quick regression checks without opening a browser.

## Development — UI

The React UI lives in `src/Clevr.Lint.Extension/ui/`. It is a standard Vite + React 19 + TypeScript project with no external UI library.

### Tech stack

| Layer | Technology |
|-------|-----------|
| Framework | React 19 + TypeScript |
| Bundler | Vite 6 |
| Styling | Tailwind CSS v4 (utility-first; injected by `vite-plugin-css-injected-by-js`) |
| State | Context + Reducer (`src/context/`) |
| Bridge | `window.chrome.webview` (WebView2 in Studio Pro; shim in dev) |

### Setup

```powershell
cd src/Clevr.Lint.Extension/ui
npm install
```

### Scripts

| Script | Command | When to use |
|--------|---------|-------------|
| `npm run dev` | `vite` | Day-to-day UI work — Vite dev server on port 5173 with React HMR |
| `npm run build` | `vite build` | Production bundle → `wwwroot/main.js` (required before `Pack-Dist.ps1`) |
| `npm run watch` | `vite build --watch` | Rebuild on save without the dev server (e.g. when testing inside Studio Pro) |

### Key source files

| File | Role |
|------|------|
| `src/main.tsx` | Entry point — mounts the React app |
| `src/App.tsx` | Root component |
| `src/context/AppReducer.ts` | All app state transitions |
| `src/hooks/useMessageBus.ts` | Wires `window.chrome.webview` events to the reducer |
| `src/components/` | Report, FilterBar, CategoryGroup, RuleCard, Toolbar, Settings, dialogs |
| `src/utils/filters.ts` | Pure filter logic (no React) |
| `vite.config.ts` | Build config + dev-server proxy + webview shim injection |

### Dev workflow with hot reload

Run the C# harness (API) and Vite (HMR) side-by-side — see [Hot reload](#hot-reload-instant-ui-changes) above.

Changes to `.tsx` / `.ts` / `.css` appear instantly in the browser at `http://localhost:5173` without a rebuild or page refresh. The webview shim is injected automatically by a Vite plugin (`apply: "serve"`) so the message bus works identically to Studio Pro.

### Committing UI changes

After editing the UI, run `npm run build` to regenerate `wwwroot/main.js` before committing, or run `.\Pack-Dist.ps1` which builds everything in one step.

## Documentation

- [`CLAUDE.md`](CLAUDE.md) — Developer quick-start: key files, commands, common tasks
- [`docs/architecture.md`](docs/architecture.md) — Architecture, components, data contract, design decisions
- [`docs/rules.md`](docs/rules.md) — Rule inventory: active rules, deferred, parked reactivations

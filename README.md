# CLEVR Lint Extension

A Mendix Studio Pro 11 extension that runs **mxcli** to lint a Mendix project and displays the findings as a structured report inside the IDE — grouped by category and severity, with filtering, exclusions, and HTML export.

![Build & Test](https://github.com/clevr/clevr-lint-extension/actions/workflows/build-and-test.yml/badge.svg)

## Install (end users)

See ![Mendix Marketplace](https://marketplace.mendix.com/link/component/301023) for the released version.
1. Download
1. Open the CLEVR Linter extenion via the Menu > Extensions > clevrlint > CLEVR Lint
1. Requires mxcli which can be installed through the extension
  1. Settings
  1. Configuration
  1. Download Latest, or select from Path.
1. Adding lint rules from different source via extension Settings > Sources
  1. mxcli lint comes with a set of build in rules
  1. Add additional mxcli rules (if not already installed via mxcli init) 
  https://github.com/mendixlabs/mxcli/tree/main/.claude/lint-rules
  1. Add Clevr ACR replace rules
  https://github.com/ClevrSolutions/mxcli-linter-studio-extension/tree/main/rules 


## Usage (end users)

1. Open the CLEVR Linter extenion via the Menu > Extensions > clevrlint > CLEVR Lint
1. Go to Settings
  1. Select the modules you would like to scan
  1. Select the rules you would like to use
  1. Close settings
1. Click Scan
1. Check your results
1. Save as baseline
1. Fix your issues
1. Scan again.

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
| `package.json`, `scripts/` | Root `npm run build`/`test`/`dev` wrapper commands |

## Prerequisites

| Tool | Version |
|------|---------|
| .NET SDK | 10.0 |
| Node.js | 20+ |
| Mendix Studio Pro | >11.12 (only needed to run the extension for real) |
| mxcli | v0.13.0 (downloaded extension; not needed for `npm run dev` — mock mode has canned data) |

## Configure your dev project (optional)

By default `npm run dev` uses canned mock data — no setup needed. To scan a real Mendix project instead:

```powershell
cp src/Clevr.Lint.Extension/lint-scan-settings.example.json src/Clevr.Lint.Extension/lint-scan-settings.json

# then edit projectPath in the file
# src/Clevr.Lint.Extension/lint-scan-settings.json

{
  "mxcliPath": "C:\\mxcli",
  "projectPath": "C:\\Mendix\\MyTestProject"
}

```

This file is gitignored — it holds your personal machine path and is never committed.

## Build

```powershell
npm install   # first time only — installs root tooling + UI dependencies (postinstall)
npm run build
```

Wraps `Pack-Dist.ps1`: builds the UI, builds the extension (Release), and assembles `dist/clevrlint`.

## Test

```powershell
npm run test
```

Runs the Normalizer's 232 unit tests — the same command CI runs.

## Develop with hot reload

```powershell
npm run dev
```

Starts the C# test harness (in mock mode, `--serve --mock`) and the Vite dev server together. Open **`http://localhost:5173`** — edits to `.tsx` / `.ts` / `.css` hot-reload instantly, and the UI shows canned violations out of the box (no mxcli or project needed).

To point at a real project instead of mock data, set `CLEVR_DEV_PROJECT` (or configure `lint-scan-settings.json` — see above) before running:

```powershell
$env:CLEVR_DEV_PROJECT = "C:\Mendix\YourProject"
npm run dev
```

Stop with **Ctrl+C**.

<details>
<summary>Advanced: running the harness directly</summary>

The underlying commands `npm run dev` wraps are still available for finer control:

```powershell
# Full UI in a browser, no hot reload, mock data
dotnet run --project src/Clevr.Lint.TestHarness -- --serve --mock

# Full UI against a real project
dotnet run --project src/Clevr.Lint.TestHarness -- --serve "C:\Mendix\YourProject"

# Scan-only: JSON to stdout, no browser
dotnet run --project src/Clevr.Lint.TestHarness -- "C:\Mendix\YourProject"
```

The WebView2 bridge that Studio Pro normally provides is replaced by a JavaScript shim (`POST /api/message`, `GET /api/events` SSE). All features work the same as in Studio Pro except "Open Document" — the harness prints a message instead.

</details>

## Development — UI internals

The React UI lives in `src/Clevr.Lint.Extension/ui/`. It is a standard Vite + React 19 + TypeScript project with no external UI library.

### Tech stack

| Layer | Technology |
|-------|-----------|
| Framework | React 19 + TypeScript |
| Bundler | Vite 6 |
| Styling | Tailwind CSS v4 (utility-first; injected by `vite-plugin-css-injected-by-js`) |
| State | Context + Reducer (`src/context/`) |
| Bridge | `window.chrome.webview` (WebView2 in Studio Pro; shim in dev) |

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

After editing the UI, `npm run build` (from `src/Clevr.Lint.Extension/ui`) regenerates `wwwroot/main.js` before committing — or run the root `npm run build`, which does this as part of the full package.

## Documentation

- [`CLAUDE.md`](CLAUDE.md) — Developer quick-start: key files, commands, common tasks
- [`docs/architecture.md`](docs/architecture.md) — Architecture, components, data contract, design decisions
- [`docs/rules.md`](docs/rules.md) — Rule inventory: active rules, deferred, parked reactivations

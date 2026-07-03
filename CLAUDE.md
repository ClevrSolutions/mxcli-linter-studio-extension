# CLEVR Lint Extension — Developer Context

CLEVR Lint is a **Mendix Studio Pro 11 extension** that runs **mxcli** against a Mendix project and shows the findings in the IDE as a structured report: six categories, four severities, filtering, exclusions, and HTML export.

## Repo layout

```
src/
  Clevr.Lint.Extension/     ← Extension backend (C# .NET 10) + React UI
  Clevr.Lint.Normalizer/    ← Pure normalization library + 232 unit tests
  Clevr.Lint.TestHarness/   ← Standalone CLI for debugging the normalizer
dist/                       ← Dev/test build output (Pack-Dist.ps1); read directly by TestHarness
docs/                       ← Architecture, rules inventory
.github/workflows/          ← CI: build + test on every push/PR
Pack-Dist.ps1               ← Builds UI + extension (Release), assembles dist/clevrlint
```

## Architecture flow

```
User clicks Scan / Deepscan in Studio Pro
  → DockablePaneViewModel (message bus, Studio Pro API)
    → LintScanService (orchestrator, streaming)
      → ProcessRunner (wraps mxcli via Process.Start)
        → Clevr.Lint.Normalizer (raw mxcli JSON → Violation[])
          → PostMessage to WebView2
            → React UI (wwwroot/main.tsx) renders report
```

## Key source files

| File | Role |
|------|------|
| `src/Clevr.Lint.Extension/DockablePaneViewModel.cs` | Extension entry point — message bus, scan triggers, WebView2 lifecycle |
| `src/Clevr.Lint.Extension/LintScanService.cs` | Orchestrates mxcli execution, batches, streams findings to UI |
| `src/Clevr.Lint.Extension/ProcessRunner.cs` | Runs mxcli as a subprocess, captures stdout/stderr |
| `src/Clevr.Lint.Extension/ExclusionStore.cs` | Persists user exclusions to `.clevr-lint/exclusions.json` |
| `src/Clevr.Lint.Extension/LinterConfigStore.cs` | Persists scan settings to `lint-scan-settings.json` |
| `src/Clevr.Lint.Extension/ReportExporter.cs` | Generates the HTML report |
| `src/Clevr.Lint.Extension/GitChangedDocumentsService.cs` | Identifies git-changed documents for scoped scans |
| `src/Clevr.Lint.Normalizer/MxcliNormalizer.cs` | Core normalizer: raw violations → `Violation[]` |
| `src/Clevr.Lint.Normalizer/Violation.cs` | The data contract — only form the UI and exclusions know |
| `src/Clevr.Lint.Normalizer/Exclusions.cs` | Exclusion logic and fingerprint matching |
| `src/Clevr.Lint.Extension/ui/src/App.tsx` | React root component |
| `src/Clevr.Lint.Extension/ui/src/context/` | AppContext + AppReducer (Redux-like state) |
| `src/Clevr.Lint.Extension/ui/src/components/` | Report, FilterBar, CategoryGroup, RuleCard, Toolbar, Settings |

## Build commands

```powershell
# Run tests (must stay green — 232 tests)
dotnet test src/Clevr.Lint.Normalizer/Clevr.Lint.Normalizer.Tests/Clevr.Lint.Normalizer.Tests.csproj

# Build normalizer library
dotnet build src/Clevr.Lint.Normalizer/Clevr.Lint.Normalizer/Clevr.Lint.Normalizer.csproj

# Build extension
dotnet build src/Clevr.Lint.Extension/Clevr.Lint.Extension.csproj

# Build UI only
cd src/Clevr.Lint.Extension/ui && npm run build

# Build + assemble full distribution package
.\Pack-Dist.ps1
```

## Tech stack

| Layer | Technology |
|-------|-----------|
| Extension backend | C# / .NET 10 |
| UI | React 19 + TypeScript, Vite 6, Tailwind CSS v4 |
| Normalization library | C# / .NET 10, zero external dependencies |
| Mendix API | Mendix.StudioPro.ExtensionsAPI 11.10 |
| External tool | mxcli v0.12.0 (Apache-2.0, auto-detected/downloaded at runtime by `MxcliService.cs`) |

End users install CLEVR Lint from the **Mendix Marketplace** (not from this repo's `dist/` folder or any install script). `dist/` exists only so `Clevr.Lint.TestHarness` has a Release build to load during local dev/testing.

## After completing any code change

Run these steps in order after every prompt that changes C# or UI source:

```powershell
# 1. Verify tests still pass
dotnet test src/Clevr.Lint.Normalizer/Clevr.Lint.Normalizer.Tests/Clevr.Lint.Normalizer.Tests.csproj

# 2. Full rebuild (UI + C# Release) and update dist/clevrlint
.\Pack-Dist.ps1

# 3. Relaunch the test harness in serve mode (opens browser automatically)
npm run dev
```

Step 3 uses mock data by default (canned violations, no mxcli or Mendix project required). To scan a real project, either set `projectPath` in your local (gitignored) `src/Clevr.Lint.Extension/lint-scan-settings.json` — copy it from `lint-scan-settings.example.json` on first use — or set `CLEVR_DEV_PROJECT` before running `npm run dev`. If the harness is already running, stop it with Ctrl+C before relaunching.

## Validating UI changes with Playwright

For React/reducer changes, don't stop at `tsc`/`npm run build` passing — that only proves the types line up, not that the app renders or behaves correctly. Drive the actual UI with Playwright against the test harness:

```powershell
# One-time setup (already installed as a devDependency in ui/)
cd src/Clevr.Lint.Extension/ui
npm install -D playwright
npx playwright install chromium

# 1. Rebuild + repack (see "After completing any code change" above), then
#    start the harness in the background and wait for it to be ready
dotnet run --project src/Clevr.Lint.TestHarness -- --serve --mock
#   (from another shell) poll http://localhost:5174/index until it responds

# 2. From src/Clevr.Lint.Extension/ui, run a Playwright script (plain Node, not @playwright/test)
#    that launches chromium, navigates to http://localhost:5174/index, drives the flow
#    that touches your change (click Scan, toggle filters, open Settings/tabs, toggle a
#    rule or module checkbox), and asserts on rendered content — plus logs every
#    `console` "error" event and `pageerror`. Zero console errors is the bar to clear.
#
# 3. Stop the harness (Ctrl+C, or kill the background process) when done.
```

Playwright must run from inside `src/Clevr.Lint.Extension/ui` (not repo root) so Node resolves the locally installed `playwright` package. There's no committed test file for this — write a throwaway script per session scoped to whatever you changed (e.g. if you touched the reducer/AppState shape, exercise every slice: a scan, a filter toggle, and a Settings save/cancel).

## Common tasks

**Add a new rule:** implement the rule logic in `Clevr.Lint.Normalizer` (pure C#, no Mendix dependency), add a unit test, then wire the result into `LintScanService`. The UI renders any `Violation[]` automatically — no UI changes needed for a new rule.

**Update mxcli version:** nothing to do here — `MxcliService.cs` always resolves the latest GitHub release at runtime and verifies its sha256 before use.

**Rebuild for local testing:** run `Pack-Dist.ps1`, then relaunch `Clevr.Lint.TestHarness --serve` (it reads `dist\clevrlint` directly). For testing inside Studio Pro itself, copy `dist\clevrlint` into `<project>\extensions\clevrlint` and restart Studio Pro — changes apply to new scans only.

**Debug log:** `<project>\.clevr-lint\mxlint-debug.log`

## Known limitations

- **String length unavailable:** `CATALOG.ATTRIBUTES.Length = 0` for all strings; `describe entity` also returns `String(unlimited)` regardless of actual limit. SEC-006 is parked until mxcli exposes this.
- **Page widget attributes unavailable:** mxcli does not expose `WIDGETS.Style` or alt-text. MAINT-015 and REL-003 are parked until mxcli exposes these.
- **Deepscan is slow by design:** each `describe` call costs ~0.5–1.1s (hard mxcli floor, not fixable in this codebase). Streaming makes this bearable. See `docs/architecture.md` for details.
- **Severity calibration pending:** MAINT-007, MAINT-008, MAINT-010 produce high volumes on large projects; severity thresholds not yet tuned.

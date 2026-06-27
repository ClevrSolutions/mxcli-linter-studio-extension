# CLEVR Lint Extension — Developer Context

CLEVR Lint is a **Mendix Studio Pro 11 extension** that runs **mxcli** against a Mendix project and shows the findings in the IDE as a structured report: six categories, four severities, filtering, exclusions, and HTML export.

## Repo layout

```
src/
  Clevr.Lint.Extension/     ← Extension backend (C# .NET 10) + React UI
  Clevr.Lint.Normalizer/    ← Pure normalization library + 232 unit tests
  Clevr.Lint.TestHarness/   ← Standalone CLI for debugging the normalizer
dist/                       ← Pre-built end-user package (installer + bundle)
docs/                       ← Architecture, rules inventory
.github/workflows/          ← CI: build + test on every push/PR
Pack-Dist.ps1               ← Builds UI + extension, assembles dist/
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
| UI | React 19 + TypeScript, Vite bundler |
| Normalization library | C# / .NET 10, zero external dependencies |
| Mendix API | Mendix.StudioPro.ExtensionsAPI 11.10 |
| External tool | mxcli v0.12.0 (Apache-2.0, auto-downloaded by installer) |

## After completing any code change

Run these steps in order after every prompt that changes C# or UI source:

```powershell
# 1. Verify tests still pass
dotnet test src/Clevr.Lint.Normalizer/Clevr.Lint.Normalizer.Tests/Clevr.Lint.Normalizer.Tests.csproj

# 2. Full rebuild (UI + C# Release) and update dist/
.\Pack-Dist.ps1

# 3. Push updated extension into the test Mendix project
Push-Location dist
.\Install-ClevrLint.ps1 -ProjectPath "C:\Mendix\AcrToLintTest-main"
Pop-Location

# 4. Relaunch the test harness in serve mode (opens browser automatically)
dotnet run --project src/Clevr.Lint.TestHarness -- --serve "C:\Mendix\AcrToLintTest-main"
```

Steps 3 and 4 require `C:\Mendix\AcrToLintTest-main` to exist on this machine. The test harness reads the extension DLLs from `dist\clevrlint` (Release build written by `Pack-Dist.ps1`) when a projectDir is passed. If the harness is already running, stop it with Ctrl+C before relaunching.

## Common tasks

**Add a new rule:** implement the rule logic in `Clevr.Lint.Normalizer` (pure C#, no Mendix dependency), add a unit test, then wire the result into `LintScanService`. The UI renders any `Violation[]` automatically — no UI changes needed for a new rule.

**Update mxcli version:** update the expected version + sha256 in `Install-ClevrLint.ps1`, then rebuild and repack with `Pack-Dist.ps1`.

**Rebuild and install after a change:** run `Pack-Dist.ps1`, then run `Install-ClevrLint.ps1` in the target project. Restart Studio Pro. Changes apply to new scans only.

**Debug log:** `<project>\.clevr-lint\mxlint-debug.log`

## Known limitations

- **String length unavailable:** `CATALOG.ATTRIBUTES.Length = 0` for all strings; `describe entity` also returns `String(unlimited)` regardless of actual limit. SEC-006 is parked until mxcli exposes this.
- **Page widget attributes unavailable:** mxcli does not expose `WIDGETS.Style` or alt-text. MAINT-015 and REL-003 are parked until mxcli exposes these.
- **Deepscan is slow by design:** each `describe` call costs ~0.5–1.1s (hard mxcli floor, not fixable in this codebase). Streaming makes this bearable. See `docs/architecture.md` for details.
- **Severity calibration pending:** MAINT-007, MAINT-008, MAINT-010 produce high volumes on large projects; severity thresholds not yet tuned.

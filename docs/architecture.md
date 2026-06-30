# CLEVR Lint — Architecture

## What it does

CLEVR Lint runs **mxcli** against a Mendix project and surfaces the findings inside Studio Pro as a structured report. It is an *aggregator* — it presents mxcli's own rules alongside CLEVR's calibrated rules derived from the same mxcli data sources, all normalized into a single `Violation` contract. It is not a replacement for Studio Pro's built-in analysis.

Requires Mendix Studio Pro 11+ and .NET 10. Does not work on Mendix 10.

## Components

### Extension backend (`src/Clevr.Lint.Extension/`)

C# .NET 10 DLL loaded in-process by Studio Pro via the Extensions API.

- **`DockablePaneViewModel`** — Studio Pro entry point. Registers the dockable pane, handles the WebView2 message bus (receive scan requests, send findings batches), manages the scan lifecycle.
- **`LintScanService`** — Orchestrator. Calls mxcli, gathers findings from all data sources, normalizes, and streams batches to the UI via `PostMessage`.
- **`ProcessRunner`** — Thin wrapper around `System.Diagnostics.Process` to run mxcli and capture output.
- **`ExclusionStore`** — Persists user exclusions to `<project>/.clevr-lint/exclusions.json`.
- **`LinterConfigStore`** — Persists scan settings to `lint-scan-settings.json` in the project root.
- **`ReportExporter`** — Generates an HTML report from the current findings.
- **`GitChangedDocumentsService`** — Identifies git-changed documents to support scoped (changed-files-only) scans.

### Normalization library (`src/Clevr.Lint.Normalizer/`)

Pure .NET 10 library — zero external dependencies, no Mendix API references. This is what makes the rule logic fully unit-testable in isolation.

- **`MxcliNormalizer`** — Converts raw mxcli violations to `Violation[]`, applying category/severity mapping.
- **`MxcliOutputParser`** — Parses mxcli's JSON lint output.
- **`Exclusions`** — Fingerprint-based exclusion matching.
- **`Fingerprint`** — Computes `sha1(ruleId|documentQualifiedName|elementName)`.

### React UI (`src/Clevr.Lint.Extension/ui/`)

TypeScript + React 19 + Vite + Tailwind CSS v4. Compiled to `wwwroot/` and served to the WebView2 panel.

- Receives `Violation[]` batches via `window.chrome.webview.onmessage`.
- `AppContext` + `AppReducer` manage state (Redux-like, no external state library).
- Renders grouped by category → rule, with severity badges, filtering, exclusions, and HTML export.
- Displays "Total (so far)" during streaming; only shows final totals when the scan completes.

## Data flow

```
mxcli lint --format json       → MxcliOutputParser → MxcliNormalizer
mxcli -c "SELECT … FROM CATALOG.*"  → (catalog rules in LintScanService)
mxcli describe <type> <name>   → (describe rules in LintScanService)
mxcli describe projectsecurity → (security rules in LintScanService)
                ↓
        Violation[]  (serialized as JSON)
                ↓
        DockablePaneViewModel.PostMessage()
                ↓
        React UI — AppReducer merges batches → Report renders
```

## Violation data contract

`Violation` is the only form the UI, exclusions, and report know. Defined in `Clevr.Lint.Normalizer/Violation.cs`.

| Field | Type | Notes |
|-------|------|-------|
| `RuleId` | string | e.g. `"MPR001"`, `"ACR_ENT_ATTRS"`, `"MAINT-007"` |
| `Kind` | `ViolationKind` | Always `"mxcli"` in current state |
| `Category` | string | One of the six Lint categories |
| `Severity` | string | `error` / `warning` / `info` / `hint` |
| `DocumentType` | string | Mendix document type (e.g. `"Microflow"`, `"Entity"`) |
| `DocumentQualifiedName` | string | Module-qualified name (e.g. `"MyModule.MyMicroflow"`) |
| `ElementName` | string | Sub-element (widget/attribute); `""` if not applicable |
| `Reason` | string | Human-readable finding description |
| `Suggestion` | string? | Optional remediation hint |
| `Fingerprint` | string | `sha1(ruleId\|documentQualifiedName\|elementName)` — used for exclusions |
| `DocumentationUrl` | string? | Link to rule documentation |
| `DocumentId` | string? | Mendix GUID — stable navigation handle into the IDE |

Exclusions are stored as a set of fingerprints. A violation is hidden if its fingerprint is in the exclusion set.

## Scan

A single **Scan** button runs `mxcli lint --format json` and streams results to the UI. Cold start is slower (~55s) because mxcli rebuilds `catalog.db` on first run; subsequent scans are ~17s.

`LintScanService.RunScanStreaming(emit)` emits one `Violation[]` batch with `phase: "fast"` and `final: true`. The `SCAN_DESCRIBE_BATCH` reducer path in the UI is wired for future use (see Roadmap below) but is currently never triggered.

## Roadmap — mxcli deep analysis

Rules that require per-document `mxcli describe` calls (microflow complexity, nested-if, empty-string checks, default ReadWrite access) are **not yet implemented**. Each describe call costs ~0.5–1.1s (hard mxcli compute floor — not fixable at the extension layer). Running 500 describe calls from the extension would take ~7 minutes.

The preferred path is a mxcli feature request. Three options in priority order:

1. **`mxcli lint --deep`** — run all rules including describe-based ones in a single pass, returning the same JSON format. Extension changes: swap one command string.
2. **`mxcli lint --deep --scope <guid,...>`** — scoped deep scan, combined with `GitChangedDocumentsService` to limit analysis to changed documents. Keeps runtime proportional to change size.
3. **Streaming NDJSON from mxcli** — emit partial results as documents are processed, so the extension can show progress without managing concurrency itself.

Until mxcli exposes one of these, the describe-based rules remain parked (see CLAUDE.md `Known limitations`).

A chunk that returns fewer results than requested triggers a visible warning — no silent data loss.

## Why these design decisions

**C# hosts the WebView (not a web-only extension).** The Mendix Extensions API requires in-process DLLs; web-only extensions cannot start subprocesses. The spike proved this constraint.

**Normalizer is a pure library.** No Mendix API dependency means 232 unit tests run without Studio Pro installed. Rule correctness is verified against real mxcli output in the test suite.

**Streaming, not batch-at-end.** The deepscan `describe` floor is ~0.5–1.1s per element (hard mxcli compute floor — not fixable here; see below). Streaming makes multi-minute scans usable.

**Single engine: mxcli only.** A second engine (mxlint/Rego) was removed. mxcli is Apache-2.0, its output is the ground truth, and a single engine eliminates cross-engine deduplication complexity.

## Describe performance ceiling

Each `mxcli describe` call costs ~0.5–1.1s (warm/cold). This is a mxcli compute floor that persists across process boundaries — running all describes in a single mxcli session (`-c`, `exec`, REPL) does not break it. There is no bulk-describe API today.

Extrapolation: 472 microflows ≈ 4–10 minutes; 2000 ≈ 20–45 minutes; 5000 ≈ 50–110 minutes. The only order-of-magnitude gain would come from a `--deep` flag in mxcli (see Roadmap).

## Security notes

- **mxcli** (Apache-2.0) is auto-downloaded by the installer from the official GitHub release. The installer verifies sha256 + byte size before accepting the binary. Stored in `%LOCALAPPDATA%\clevr-lint\mxcli\`.
- **NuGet:** `YamlDotNet` 16.x (present but only used by inactive backup code), `Mendix.StudioPro.ExtensionsAPI` 11.10 (supplied by Studio Pro, not bundled).
- Periodically run `dotnet list package --vulnerable --include-transitive`.

# CLEVR Lint Extension

A Mendix Studio Pro 11 extension that runs **mxcli** to lint a Mendix project and displays the findings as a structured Lint (Architecture Code Review) report inside the IDE — grouped by category and severity, with filtering, exclusions, and HTML export.

![Build & Test](https://github.com/clevr/clevr-lint-extension/actions/workflows/build-and-test.yml/badge.svg)

---

## Architecture

```
mxcli (Go CLI, Apache-2.0)
  └─► LintScanService  (C# orchestrator)
        └─► Clevr.Lint.Normalizer  (pure .NET library — mxcli output → Violation[])
              └─► SpikeDockablePaneViewModel  (message bus, Studio Pro API)
                    └─► wwwroot/main.js  (WebView2 UI — filters, groups, export)
```

The normalizer library has zero external dependencies and is fully unit-tested (232 tests). The extension DLL is loaded in-process by Studio Pro via the Extensions API.

---

## Repo Layout

| Folder | Purpose |
|--------|---------|
| `src/Clevr.AcrSpike/` | Extension source — orchestrates mxcli, manages state, serves the web UI |
| `src/Clevr.Lint.Normalizer/` | Pure normalization library + 232 unit tests |
| `dist/` | Pre-built distribution package for end users (installer + compiled bundle) |
| `docs/` | Architecture, spec, status, and developer guide |
| `.github/workflows/` | CI pipeline (build + test on every push/PR) |

---

## Prerequisites

| Tool | Version |
|------|---------|
| .NET SDK | 10.0 |
| Mendix Studio Pro | 11.x |
| mxcli | v0.12.0 (downloaded by installer) |

---

## Build

```powershell
# Normalizer library
dotnet build src/Clevr.Lint.Normalizer/Clevr.Lint.Normalizer/Clevr.Lint.Normalizer.csproj

# Extension
dotnet build src/Clevr.AcrSpike/Clevr.AcrSpike.csproj
```

## Test

```powershell
dotnet test src/Clevr.Lint.Normalizer/Clevr.Lint.Normalizer.Tests/Clevr.Lint.Normalizer.Tests.csproj
```

All 232 tests should pass.

---

## Install (end users)

See [`dist/README.md`](dist/README.md) for the step-by-step installation guide.

---

## Documentation

- [`docs/handover.md`](docs/handover.md) — Architecture overview and project handover
- [`docs/spec.md`](docs/spec.md) — Functional specification: Lint categories, `Violation` data contract, rule registry
- [`docs/status.md`](docs/status.md) — Current rule inventory, known issues, deferred items
- [`docs/dev-guide.md`](docs/dev-guide.md) — Developer guide for the extension source

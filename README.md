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

## Documentation

- [`CLAUDE.md`](CLAUDE.md) — Developer quick-start: key files, commands, common tasks
- [`docs/architecture.md`](docs/architecture.md) — Architecture, components, data contract, design decisions
- [`docs/rules.md`](docs/rules.md) — Rule inventory: active rules, deferred, parked reactivations

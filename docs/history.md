# Development History & Prompts

This document reconstructs the major Claude prompts and development phases that shaped the CLEVR Lint Extension, organized chronologically by git commits.

---
## Phase 0: Research spike

Vibecoded with chat: Research spike linting as replacement for ACR with various methods; direct, manual, mxlint and mxcli lint. Setup scanning and showing lint violation

## Phase 1: Foundation & Repository Setup

**Commits:** `dab7cba Initial commit: restructure repo for GitHub sharing` → `bf0c86a Initial commit`

**Reconstructed Prompt:**
> Set up a GitHub-ready structure for a Mendix Studio Pro extension project. Create a repo layout with clear separation between the C# extension backend, a normalizer library, a test harness, and UI assets. Add a README with high-level architecture overview and get the project ready for open-source sharing.

This phase established the foundational directory structure (`src/Clevr.Lint.Extension`, `src/Clevr.Lint.Normalizer`, `src/Clevr.Lint.TestHarness`, `dist/`) and prepared the project for GitHub.

---

## Phase 2: Rule Engine & Linter Foundation

**Commits:** `6fc51c0 Remove mxcli — app now supports mxcli linting rules only` → `3074f70 Remove all extension-implemented rules; support only mxcli linter rules`

**Reconstructed Prompt:**
> Simplify the extension by removing all custom rule implementations. Instead, make the extension a thin wrapper around the mxcli tool: delegate all linting logic to mxcli, normalize its JSON output into a standard Violation model, and render the results in the UI. This reduces maintenance burden and keeps the extension focused on UX.

This critical pivot removed duplicate rule logic from the extension and established mxcli as the single source of truth for linting.

---

## Phase 3: Frontend Modernization

**Commits:** `0e372e5 Refactor frontend from vanilla JS to TypeScript + React` → `4c2113d Translate all Dutch documentation to English`

**Reconstructed Prompt:**
> Rewrite the UI from vanilla JavaScript to a modern React + TypeScript stack. Add type safety throughout the component hierarchy, break the monolithic UI into smaller, reusable components (FilterBar, CategoryGroup, RuleCard, etc.), and improve maintainability. Also translate all Dutch comments and documentation to English for the open-source community.

This modernization improved code quality and set the stage for future UI features.

**Related Commit:** `5f9e7bc Replace pre-built dist with Pack-Dist.ps1; flatten wwwroot JS output path`

> Create a PowerShell build script (Pack-Dist.ps1) that orchestrates the full build pipeline: compile React UI via Vite, build the C# extension in Release mode, and assemble the `dist/clevrlint` distribution package. This replaces manual dist updates and ensures builds are reproducible.

---

## Phase 4: Suppression & Baseline Comparison

**Commits:** `226e3af Translate Dutch comments to English; implement claim-table suppression` → `ec44a80 Add baseline comparison feature for tracking new and fixed violations`

**Reconstructed Prompt:**
> Implement exclusion/suppression logic so users can exclude violations from the report. Use a fingerprint-based matching strategy to identify violations even if they change slightly across scans. Persist exclusions to `.clevr-lint/exclusions.json`. Later, build on this to add baseline snapshots: allow users to capture a baseline, then show new violations introduced since that baseline and violations fixed since the baseline.

This feature gave users control over which violations to see and enabled tracking progress over time.

---

## Phase 5: mxcli Lifecycle & Settings

**Commits:** `bd32206 Add mxcli lifecycle management: auto-detect, download, and location picker` → `140109c Add git change tracking, linter config store, and settings UI; simplify normalizer`

**Reconstructed Prompt:**
> Add automatic mxcli detection and download: detect the mxcli version needed, download it from GitHub releases, verify its SHA-256, and cache it locally. Also build a Settings UI that lets users configure the linter: choose which mxcli version to use, set module exclusions, and optionally limit scans to uncommitted documents (git-tracked changes only). Persist linter config to `lint-scan-settings.json`.

These features made the extension self-contained and configurable without manual mxcli setup.

---

## Phase 6: Scan Streaming & Cancellation

**Commits:** `e019744 Remove manual checks, add scan cancellation, simplify scan pipeline` → `ce8698b Fix uncommitted filter passing through violations without a documentId`

**Reconstructed Prompt:**
> Refactor the scan pipeline to stream violations to the UI as they arrive instead of batching them at the end. Allow users to cancel a running scan at any time. Remove any manual validation checks that duplicate mxcli's own validation. This makes large scans feel responsive and gives users control to abort slow scans.

Streaming fundamentally improved the UX for large Mendix projects where mxcli takes many seconds to complete.

---

## Phase 7: HTML Export & Rule Management

**Commits:** `3ecafb5 Set version to 0.1.0 across all projects` → `c67343a Add lint rules` → `b5f1491 Add lint rule sources: fetch, replace, and delete files from GitHub directories`

**Reconstructed Prompt:**
> Create an HTML export feature so users can save a formatted linting report as a static HTML file. Also build a "Rule Sources" management UI in Settings: allow users to add custom linting rules by fetching rule files from GitHub directories, replace existing rules, and delete rules they no longer need. This makes the extension customizable per team.

These features enabled sharing reports and extending the linter with team-specific rules.

---

## Phase 8: UI Refactoring & Collapsible Cards

**Commits:** `a7ee4a5 Fix rule source data loss, partial delete abort, path traversal, and code duplication` → `1c78923 Set version to 0.1.1 across all projects` → `380315a Add per-module filter section to FilterBar` → `c61fd67 Make summary cards collapsible and move module filter into card`

**Reconstructed Prompt:**
> Fix critical bugs in rule source management (data loss on add, partial deletes leaving stale data, path traversal vulnerability). Then enhance the FilterBar: make the summary cards collapsible so users can focus on one rule category at a time, add a per-module filter section so users can exclude entire modules from the report, and reorganize the layout for better UX on large reports.

These fixes improved reliability and UX for power users with large Mendix projects.

---

## Phase 9: Tailwind CSS Migration

**Commits:** `342db90 Migrate UI to Tailwind CSS v4 with style harmonization` → `ead956b Fix HTML export formatting broken by Tailwind CSS migration`

**Reconstructed Prompt:**
> Migrate the UI from custom CSS to Tailwind CSS v4 for better maintainability and consistency. Use Tailwind's utility classes for responsive design and theming. Create a shared utilities file (src/utils/classes.ts) for common class combinations to reduce duplication. Ensure the HTML export still renders correctly with the new CSS approach, and keep named CSS classes (like `.sev-*`) for semantic meaning where appropriate.

Tailwind provided a cleaner, more maintainable styling approach as the UI grew.

---

## Phase 10: Settings Architecture & Coordinators

**Commits:** `3bc417c UI improvements` → `81bb2bf Add rule Info dialog to Settings > Rules tab` → `d6108eb Add NavigationCoordinator; move install docs to Marketplace, backstop module exclusion in scan` → `9e643f6 Add SettingsCoordinator and ScanCoordinator` → `abdce4b Add LinterConfigCoordinator` → `4b03027 Split AppState into domain slices (scan/config/filters/baseline/ui)`

**Reconstructed Prompt:**
> Refactor the Settings panel to show a Rules tab with detailed info dialogs for each rule. Split the monolithic AppState into logical slices (scan, config, filters, baseline, ui) for better separation of concerns. Introduce Coordinators (SettingsCoordinator, ScanCoordinator, LinterConfigCoordinator) to centralize side effects and navigation logic. This makes the state machine clearer and easier to test.

This architectural refactoring paved the way for complex features by making state management predictable.

---

## Phase 11: Baseline & Scope Features

**Commits:** `30a1d96 Deepen exclusion handling into ExclusionCoordinator + ProjectDirResolver` → `3692b66 Remove hardcoded rule suppression; fix mxcli exclude-modules key mismatch` → `6f3682c Scope baseline 'fixed' comparison to current scan configuration` → `1026807 Show scanning message in report while scan is in progress` → `876a91e Add Snapshots tab to Settings for baseline management` → `3a4d5b4 Add Outside Baseline filter for scope-changed violations`

**Reconstructed Prompt:**
> Deepen the baseline feature: introduce a Snapshots tab in Settings where users can view, save, and delete baseline snapshots. Make baseline comparisons scope-aware so the "fixed" count respects the current filter/module/severity settings. Add an "Outside Baseline" filter to highlight violations that are new or changed since the last snapshot. Fix mxcli exclude-modules key mismatches and ensure the UI shows a "scanning..." message during long-running scans.

These features made baseline tracking more powerful and transparent.

---

## Phase 12: Bug Fixes & Stability

**Commits:** `7d60097 Add changelog entries for Outside Baseline filter and scan-streaming fix` → `f0681f9 Simplify dev onboarding: npm build/test/dev, mock UI mode, gitignored project config`

**Reconstructed Prompt:**
> Simplify local development: create a mock UI mode so contributors can iterate on the UI without needing a real Mendix project or mxcli installed. Add clear npm scripts (npm build, npm test, npm dev) and a gitignored local project config so each developer can test against their own project path. Update the README and dev onboarding docs. Fix a scan-streaming edge case and document the Outside Baseline filter in the changelog.

This phase focused on developer experience and the final polish before release.

---

## Phase 13: Documentation & Polish

**Commits:** `1393940 fix readme`

**Reconstructed Prompt:**
> Fix remaining README and documentation issues. Ensure all examples are accurate, the quickstart is clear, and any outdated links or instructions are corrected. Prepare the project for users and contributors.

---

## Summary: Thematic Arcs

### User Control & Transparency
- Exclusions → Baseline snapshots → Scope-aware comparisons → Outside Baseline filter
- Users went from simply hiding violations to tracking progress over time with full visibility

### Developer Experience
- mxcli auto-download → Settings UI → Mock mode → npm build/dev scripts
- Reduced friction from manual setup to zero-setup local development

### Architectural Maturity
- Vanilla JS → React + TypeScript → Coordinator pattern → State slices
- Improved maintainability and testability through structured patterns

### Feature Richness
- Read-only report → Filtered report → Exportable report → Customizable rules → Baseline tracking
- Evolved from a simple display to a comprehensive linting platform

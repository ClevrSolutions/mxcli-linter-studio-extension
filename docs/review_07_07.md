# CLEVR Lint — Full Codebase Review

**Date:** 2026-07-07
**Scope:** All source in `src/` (extension backend, React UI, normalizer + tests, test harness), build scripts, CI, and repo setup. Read-only review; ~9,000 LOC across four projects. Every high-severity finding below was re-verified against the actual source before inclusion.

> **Fix status (updated 2026-07-07, same day):** All quick wins (§9 items 1–8) and all high-severity findings have been fixed and verified — normalizer test suite green (34 tests, up from 25), `Pack-Dist.ps1` clean, and a Playwright smoke run against the harness confirmed the Settings save/diff behavior with zero console errors. Fixed findings are marked ✅ **FIXED** inline below; unmarked findings remain open.

---

## 1. Executive summary

The codebase is in **good overall health for its maturity stage**. The architecture is sound: the coordinator/store/service seam in the C# backend is genuinely testable, the normalizer is a real zero-dependency pure library, fingerprint identity is single-sourced in C# (no drift possible with the UI), and persistence uses atomic tmp-file writes. The message contract between C# and TypeScript is largely clean — camelCase serialization is consistent and the `Violation` shape matches field-for-field.

The problems cluster in five places:

| # | Top issue | Where | Status |
|---|-----------|-------|--------|
| 1 | **Live PostBuild target with a hardcoded personal path** — `dotnet build` on a fresh clone copies output to `C:\Mendix\AcrToLintTest-main\...` and can fail outright without elevation | `Clevr.Lint.Extension.csproj:41-46` | ✅ Fixed |
| 2 | **Scan lifecycle races** — restarting a scan disposes the live CancellationTokenSource (orphaning an uncancellable concurrent scan), and the changed-files pipeline has infinite-timeout subprocess calls that can pin the spinner forever | `DockablePaneViewModel.cs:102-108`, `ChangedElementsResolver.cs:199-228`, `ScanCoordinator.cs:95` | ✅ Fixed |
| 3 | **Dev harness serves arbitrary local files cross-origin** — path traversal + `Access-Control-Allow-Origin: *` means any web page can read local files while `npm run dev` runs | `TestHarness/Program.cs:220-322` | ✅ Fixed |
| 4 | **Settings save/diff logic is wrong** — the UI commits unstripped pending config as "saved," desyncing UI state from the file on disk; the change detector can both miss real changes and report phantom ones | `Settings.tsx:23-40, 92-104` | ✅ Fixed |
| 5 | **Documentation vs reality drift** — CLAUDE.md claims 232 tests (actual: **25**, verified via `dotnet test`), the documented `lint-scan-settings.json` dev flow doesn't work, the debug log filename is wrong, and three files give three different mxcli version stories | CLAUDE.md, README.md, `dev-harness.js` | ✅ Fixed |

Nothing found is a data-loss or production-security emergency — the worst runtime bugs degrade to stuck spinners, silent failures, or dev-only exposure. But issues 1 and 5 actively burn every new contributor, and issue 3 is a real (if dev-only) security hole.

---

## 2. Architecture & structure

### What works well

- **The coordinator seam is real, not cosmetic.** `ScanCoordinator` reports via plain `IProgress<ScanEvent>`, `NavigationCoordinator` returns a typed `Resolution` with a human-readable trace, and neither touches `IWebView`, JSON, or file I/O. The 725-line `DockablePaneViewModel` is mostly a thin message dispatcher, not a true god object.
- **The normalizer's zero-dependency purity is genuine** — no I/O, no process, no Mendix types anywhere; every parser is string-in/DTO-out. This is what makes the TestHarness and unit testing possible.
- **Atomic persistence** (`ExclusionStore`, `BaselineStore`, `LinterConfigStore`): write `.tmp`, then `File.Move(overwrite: true)`. A crash mid-save can't corrupt version-controlled team files. (Exception: settings — see 3.1 M2.)
- **`ProcessRunner` is textbook-correct**: stdout/stderr drained concurrently before waiting for exit (no full-pipe deadlock), timeouts kill the process tree, partial output salvaged after kill.
- **Honest failure modes**: `ChangedElementsResolver` returns typed statuses (`NotGit`, `NoMxTool`, `VersionUpgradeNotCommitted`) instead of an empty list that would read as "all clean"; `MxcliService` refuses to install a binary with no published digest.
- **UI state discipline**: immutability in the reducers is correct throughout (no in-place mutations found despite heavy `Set`/`Map` use); the `Draft<T>` saved/pending pattern in `context/draft.ts` gives Settings proper edit/cancel/commit semantics in 24 lines; the listener-before-request handshake in `useMessageBus.ts:243-250` cleanly avoids the WebView2 startup race.
- **Fingerprint identity is single-sourced.** The TS side never computes fingerprints — it only string-matches values produced by `Fingerprint.Compute` in C#. Nothing can drift.

### Structural weaknesses

- **No `.sln`/`.slnx` file.** `dotnet build`/`test` from the repo root fail; CI enumerates projects by hand — which is exactly how the TestHarness got skipped from CI (see §7). A checked-in solution plus a `Directory.Build.props` (centralizing `TargetFramework`, `Nullable`, `Version` — currently `0.1.1` repeated in three csprojs) would remove both drift classes.
- ~~**`TestHarness/Program.cs` (789 LOC) duplicates the extension's message router.** Its ~350-line `DispatchMessage` switch re-implements the same message→coordinator wiring as `DockablePaneViewModel`. Every new message type must be added twice; a miss makes dev mode silently diverge from Studio Pro (several already have — see §6).~~ ✅ **FIXED** — see item 15: both hosts now share `LintMessageRouter`.
- ~~**The WebView shim exists twice** — as a C# raw string (`Program.cs:713-759`) and a TS template literal (`vite.config.ts:12-46`), kept in sync by comment only. Fix a bug in one and the other dev path keeps it.~~ ✅ **FIXED** — the TS template literal is deleted; the Vite dev server injects `<script src="/chrome-webview-shim.js">` and proxies that path to the harness, making `ChromeWebViewShimJs()` the single source.
- **`Settings.tsx` (356 LOC) is four components in one**: tab shell, modules table, rules table, and save/diff logic, with the modules/rules sections as inline JSX constants sharing one scope. The other four tabs *are* separate files; extracting `ModulesTab`/`RulesTab` would make the save/diff bugs (§4.2) much easier to see.

---

## 3. Bugs & correctness — C# extension backend

### High

**B1 — Scan restart disposes the running scan's CTS; two scans run concurrently, the first uncancellable.** ✅ **FIXED** — old CTS is cancelled but no longer disposed while in use; a scan-generation counter makes `PostScanEvent` drop events from superseded scans.
`DockablePaneViewModel.cs:102-108`. `RunFullScan` unconditionally does `_scanCts?.Dispose(); _scanCts = new CancellationTokenSource();` without cancelling/awaiting an in-flight scan. Scenario: user clicks Scan, clicks Scan again mid-run. Scan 1's next `CreateLinkedTokenSource(ct)` in `ProcessRunner.cs:59` hits a disposed source → `ObjectDisposedException` surfaced as a bogus scan error — or both scans run mxcli concurrently and interleave `LintViolations`/`ScanFinished` events into the UI (scan 1's Finished re-enables the button while scan 2 runs). `CancelScan` only reaches the newest CTS. Fix: cancel + await (or refuse) before starting a new scan.

**B2 — Legacy `RunLintScan` handler runs the entire mxcli scan synchronously on the Studio Pro UI thread.** ✅ **FIXED** — `RunLintScan`/`RunCommand` handlers, `ProcessRunner.RunSpikeCommand`, and `ScanCoordinator.RunLintScan` deleted (nothing referenced them).
`DockablePaneViewModel.cs:89-95` — `var json = _scanCoordinator.RunLintScan();` inside `MessageReceived`, with a comment acknowledging it's spike code. Worst case ~10.5 minutes of hard-blocked UI ("Not Responding") before timeouts fire. The UI no longer sends this message (Toolbar sends `RunFullScan`), so it's dead-but-live code — delete it along with the `RunCommand`/`cmd /c echo` spike path (`ProcessRunner.cs:101-109`, `DockablePaneViewModel.cs:79-88`), which keeps a `postMessage → cmd.exe` invocation one message away from any future XSS in the report renderer.

**B3 — Changed-files pipeline is uncancellable with infinite-timeout subprocess calls; a hung mxcli pins the spinner forever.** ✅ **FIXED** — the scan's CancellationToken is threaded through `Resolve()` into every subprocess call, and the two unbounded catalog queries got 120 s timeouts; failures degrade through the existing typed-status paths.
`ChangedElementsResolver.cs:199-201, 226-228` call `ProcessRunner.Run` with no `timeoutMs` (0 = infinite) and no token; `ScanCoordinator.cs:95` then blocks unconditionally on `changedTask.GetAwaiter().GetResult()`. Scenario: mxcli hangs on a locked `.mpr` (Studio Pro saving) → the task never completes → `ScanEvent.Finished()` never fires → spinner stuck, Scan button disabled until Studio Pro restarts. Even without a hang, Cancel doesn't reach this pipeline — the user waits up to 10 minutes (the `mx diff` 600 s timeout) for "Finished."

### Medium

- **B4 — Corrupt `lint-scan-settings.json` silently kills the Settings panel.** `LintScanSettings.cs:33` deserializes with no try/catch; the `RequestMxcliInfo` handler (`DockablePaneViewModel.cs:559-570`) catches the exception but posts *nothing* back — the UI waits forever for `MxcliInfo`. Compounded by **B5**: `SettingsCoordinator.WriteSettings` (`SettingsCoordinator.cs:84-88`) is a direct `File.WriteAllText` — the only non-atomic write in the codebase — so a crash mid-save *produces* the corrupt file that triggers B4.
- **B6 — Mendix model accessed off the UI thread.** `RunFullScan`/`PostRulesCatalog` run in `Task.Run` and call `CurrentApp?.Root?.DirectoryPath` via `ProjectDirResolver` (`DockablePaneViewModel.cs:108,156,707`; `ScanCoordinator.cs:63`). The Extensibility API doesn't document thread safety; closing/switching the app while a scan starts races teardown → intermittent NRE swallowed in a fire-and-forget task. Resolve the project dir on the UI thread before `Task.Run` and pass the string in.
- **B7 — `LinterConfigStore.Load` writes as a side effect with a fixed `.tmp` name.** `LinterConfigStore.cs:38-45,74-77`. Background scan (`LintScanService.cs:134`) and the Settings tab can both hit `Load` simultaneously on a fresh project → both write `lint-config.yaml.tmp` → `IOException` escapes (only the read path is guarded) and surfaces as a scan error. Also unguarded against an empty project dir (`LinterConfigCoordinator.cs:21-23` passes `Resolve() ?? ""`).
- **B8 — Debug log grows without bound and dumps full `mx diff` JSON per scan.** `DebugLog.cs:27` (append-only, no rotation), `ChangedElementsResolver.cs:135`. Multi-MB appends per scan inside the project folder. ✅ **FIXED (verbosity)** — added a `LogLevel` (Error [default] / Info / Trace) gate to `DebugLog.Write`, persisted via `lint-scan-settings.json` (`logLevel`, defaulting to `error`) and exposed as a dropdown in Settings > Configuration; all ~30 call sites classified (the full mx-diff dump and other high-volume writes moved to Trace). Unbounded growth at a given level (rotation/size cap) is unrelated and still open — see §9 item 18.
- **B9 — mxcli download: default 100 s `HttpClient.Timeout` aborts slow downloads; no cancel path** (`MxcliService.cs:112,135-151`; UI passes `ct: default` at `DockablePaneViewModel.cs:593`). Slow VPN → `TaskCanceledException` mid-stream, misleading error, retry hits the same wall.
- **B10 — Fire-and-forget dispatch swallows exceptions with no UI feedback.** `DockablePaneViewModel.cs:168,189,573-586` — `_ = DispatchMxcliMessageAsync(...)`; `BrowseMxcliPath`/`SetMxcliPath` cases unguarded, so a throw before the dialog opens makes the Browse button silently dead.

### Low

- **B11** — Trailing-backslash project path breaks quoted args: `git -C "C:\proj\"` misparses (`\"` escapes the quote), changed-files scan reports `NotGit` on a good repo (`ChangedElementsResolver.cs:186`, `BaselineStore.cs:84`).
- **B12** — `HttpListenerResponseUtils.cs:14-21`: `File.ReadAllBytesAsync` can throw after headers are set, response never closed; `Access-Control-Allow-Origin: *` on the extension's local port (`WebServerExtension.cs:36-43`).
- **B13** — `REFRESH CATALOG FULL` runs on *every* scan (300 s budget) even when nothing changed; `LinterConfigStore` loaded twice per scan (`LintScanService.cs:134,140,163`). ✅ **FIXED** — the `RefreshCatalogFull` call and method removed entirely; the next mxcli lint release no longer needs a full-catalog refresh for SEC010/SEC019 to see permission/attribute/activity data. `LinterConfigStore` is still loaded twice per scan (unrelated leftover).
- **B14** — `RequestRulesCatalog` has no in-flight guard — repeated Settings opens spawn concurrent `mxcli --list-rules` processes (`DockablePaneViewModel.cs:156`).
- **B15** — No pane teardown: CTS never disposed, `MessageReceived` never unsubscribed (whole `DockablePaneViewModel`).
- **B16** — mxcli sha256 verification is correct in mechanics (streamed hash, size check, refuses missing digest) but digest and URL come from the same untrusted GitHub API response — integrity, not authenticity. Acceptable trade-off; worth a comment. No version pin despite docs naming v0.12.0/v0.13.0.

---

## 4. Bugs & correctness — React UI

### High

**U1 — `save()` commits the unstripped pending config as "saved", desyncing UI state from the file the host writes.** ✅ **FIXED** — a shared `stripRules()` produces the canonical form; `save()` posts and commits the same stripped object.
`Settings.tsx:92-104` (verified). The `rules` object posted via `SaveLinterConfig` is stripped of no-op entries, but the local `SET_LINTER_CONFIG` commit stores the raw `pending` — including phantom entries like `{ enabled: undefined }` created by toggling a rule off and back on. The host file and the UI's "saved" copy now disagree. Fix: commit `rules` (the stripped object), not `pending`.

**U2 — `isPendingChanged` can return `false` with a real change pending, disabling Save.** ✅ **FIXED** — both sides are normalized via the same `stripRules()` then diffed symmetrically over the key union; verified via Playwright (off/on no-op no longer counts as a change, real changes always do).
`Settings.tsx:23-40` (verified). Keys present in `saved` but missing from `pending` are only caught by the key-count check. Scenario: rule B disabled in saved config → "Enable all" (deletes B from pending) → toggle rule A off/on (adds phantom key) → counts match, phantom compares equal → Save disabled despite rule B's genuine change. The milder inverse (spurious "Unsaved changes" after a no-op toggle) is the everyday symptom. A proper two-sided diff over the *stripped* representations fixes U1 and U2 together.

**U3 — Zero memoization: every dispatch re-renders the whole tree and re-runs all filter pipelines ~7×.** ✅ **FIXED** — derived lists memoized via a shared `activeViolationsDeps()` dependency list; FilterBar's five passes collapsed into one memo producing all six counts; `RuleCard`/`ViolationInstance` wrapped in `React.memo` (context updates still reach them — the dominant win is skipping the redundant recomputes).
No `useMemo`/`useCallback`/`React.memo` anywhere in `ui/src`. The context value is the entire `AppState` (`App.tsx:39`), so every action — including each streaming progress toast during Deepscan — re-renders `Report`, `SummaryCards`, `FilterBar`, `Toolbar`, and every card. Per render, `activeViolations(state)` is recomputed ~7 times (FilterBar alone calls it 5× with synthetic filter states, `FilterBar.tsx:14,27-29`), each rebuilding fingerprint `Set`s over all violations, exclusions, and baseline violations (`filters.ts:65-136`). On a large project this means typing lag in the filter box and jank during streaming. Fix: memoize the derived lists once at the top, `React.memo` on `RuleCard`/`ViolationInstance`, and consider per-slice context.

### Medium

- **U4 — `AppReducer.ts:27-35` force-casts every action into every slice's union** (`action as ScanAction` etc.), so the deliberate cross-slice action-name coupling (`HIDE_SETTINGS`, `SCAN_FINISHED`, `SELECT_BASELINE` each handled in two slices) is invisible to the type system. Renaming one side compiles green and silently breaks the other slice's behavior. Type the reducer against a single `AppAction` union.
- **U5 — `useMessageBus.ts:196` destructures `event.data` with no guard** and every handler casts payloads with `as` — a host-side shape change flows malformed data into the reducer and crashes far from the cause. One object-check plus array checks on `violations` is cheap hardening.
- **U6 — `scanIncomplete`/`scanStartMs` are dead state with a latent bug** (`scanSlice.ts:70,88`): `scanIncomplete` is sticky (never reset, ORed on each batch), never read by any component; `scanStartMs` never written. The moment someone renders `scanIncomplete`, every scan after one truncated scan shows a false warning. Wire it up with a reset on scan start, or delete both. (Cross-boundary note: the backend only ever emits `phase: "fast"` — the whole `describe`-phase/`progress` branch in `useMessageBus.ts:21-26` currently has no producer; see §6.)
- **U7 — All four dialogs lack Escape-to-close, focus trapping, and `role="dialog"`/`aria-modal`**; and interactive `div`/`tr`/`h3` elements throughout (`ViolationInstance.tsx:52-56`, `SummaryCards.tsx:47-131`, `Settings.tsx:184,258`) have no keyboard path at all — keyboard users cannot open documents or toggle filters. ✅ **FIXED** — a shared `DialogShell` (role="dialog"/aria-modal/aria-label, Escape-to-close, Tab focus trap, initial focus, focus restore to the opener — captured at first render so a child's `autoFocus` can't shadow it) now hosts all four dialog shells; the ViolationInstance doc line and the SummaryCards h3 toggles/count rows got `role="button"`/`tabIndex`/Enter+Space handlers plus `aria-expanded`/`aria-pressed` via a `keyActivate` helper. The Modules/Rules table rows already had a keyboard path through their focusable checkboxes. Verified with a 23-assertion Playwright run, zero console errors.

### Low

- **U8** — Collapsed module card shows the *unfiltered* total while category/severity cards show filtered totals — three "All" numbers disagree on one screen (`SummaryCards.tsx:158`). ✅ **FIXED** — collapsed module total now uses the same `filtered` count as the category/severity cards; additionally, the per-module breakdown (and the `grid-cols-3`/card-visibility threshold) now only lists modules not excluded in Settings, since an excluded module can never produce a violation.
- **U9** — `report.ts:36,49`: `workingDirectory`-derived project name interpolated into exported HTML without `esc()` (everything else in the file is escaped).
- **U10** — Impure reducer: module-level `++toastId` (`uiSlice.ts:26-31`) double-increments under StrictMode.
- **U11** — `Toolbar.tsx:38` non-null assertion on `scanCompletedAt`; "Xs ago" label never ticks.
- **U12** — Back button discards unsaved Settings changes with no guard, though `hasChanges` is computed right there (`Settings.tsx:315`).
- **U13** — The "where" string (`documentType + QN + elementName`) is built in five places (`ai.ts`, `ExcludeDialog.tsx`, `report.ts`, `ExcludedSection.tsx` ×2) — one `whereOf(v)` helper would serve all.
- **U14** — `Toast.tsx` has no `role="status"`/`aria-live`; keys of the form `fingerprint + index` mix identity with position (`RuleCard.tsx:66`).
- Note: the previously known tsc error at `AppReducer.ts:194` is **gone** — `npx tsc --noEmit` is clean (verified this review).

---

## 5. Bugs & correctness — Normalizer

### High

**N1 — `MxcliOutputParser.Parse` throws unguarded on truncated or type-mismatched JSON, contradicting its own "tolerant" contract.** ✅ **FIXED** — `Parse` never throws; array elements deserialize individually (one bad element no longer discards its siblings); a new `TryParse` combines detection + parsing so `ExtractJson` runs once; `LintScanService` uses it while preserving the "no JSON → mxcli failure" diagnostic.
`MxcliOutputParser.cs:40,59` (verified). Two scenarios: (a) mxcli killed mid-write at the 300 s timeout → stdout ends `{"ruleId": "MPR0` → `ContainsJson` returns true (it only checks a line starts with `{`/`[`) → `JsonDocument.Parse` throws; (b) one violation with `"severity": 3` (number) → the whole-list `Deserialize` throws, discarding every well-formed violation in the scan. `LintScanService.cs:158` catches it today, but the misclassification converts "mxcli crashed" into a misleading "could not parse JSON" diagnostic, and the TestHarness inherits the throw. The `ExtractJson` fallback (`:78`) also latches onto any `{` inside a status message (e.g. `[INFO] Progress {1/10}`). A `TryParse` that returns partial results — and would also stop calling `ExtractJson` twice per scan — fixes the family.

**N2 — JSON `null` for `ruleId` produces an NRE that escapes the consumer's try/catch.** ✅ **FIXED** — `MxcliViolation` setters coalesce explicit JSON `null` to `""`; violations with an empty ruleId are skipped in `Normalize` (documented choice: no catalog match, no targetable fingerprint).
`MxcliViolation.cs:12-17` + `MxcliNormalizer.cs:72-77`. The `= ""` initializers don't protect against explicit JSON `null` (System.Text.Json assigns it). `[{"ruleId": null, ...}]` → `Parse` succeeds → `Normalize` NREs at `ruleId.Length` — and `LintScanService.cs:161` calls `Normalize` *outside* the try that guards `Parse`, so this propagates out of the scan pipeline. Fix: `required`/`[JsonRequired]` or null-coalescing in `Normalize`. (Same pattern in `Exclusions.cs:16-22`: a null `fingerprint` from a hand-edited file survives deserialization.)

**N3 — Case-sensitive double-prefix guard corrupts qualified names and therefore fingerprints.** ✅ **FIXED** — comparison is now `OrdinalIgnoreCase`; on match the document is returned unchanged (casing never rewritten).
`MxcliNormalizer.cs:60`: `document.StartsWith(module + ".", StringComparison.Ordinal)`. If mxcli reports `module: "sales"` but `document: "Sales.Customer"` (casing differs between path-derived and model-derived values), the result is `"sales.Sales.Customer"` → a different fingerprint → an existing exclusion silently stops matching and a suppressed violation reappears. Mendix module names are case-insensitively unique; use `OrdinalIgnoreCase`.

### Medium / Low

- **N4** — `Violation.Severity` is an unvalidated free string; a casing change from a future mxcli (`"Warning"`) silently misclassifies in the UI's four-severity buckets. A canonicalizer parallel to `DocumentTypeCanonicalizer` closes it.
- **N5** — `MxcliRulesCatalogParser.cs:24`: category regex `([A-Za-z]+)` truncates `best-practices` → `"best"` (phantom UI group); the rule-line regex (`:21`) can match prose lines like `Warning (deprecated) - use --rules instead` as rules.
- **N6** — Fingerprint delimiter is injectable: `Compute("R","A|B","C") == Compute("R","A","B|C")` (`Fingerprint.cs:15`). Element names come from user model content. Length-prefix or escape to make collisions impossible; a collision silently excludes an unrelated violation.
- **N7** — `Exclusions.cs:37/64`: reads are case-insensitive but fingerprint comparison is ordinal — a hand-edited uppercase `SHA1:ABC…` never matches and is silently stale.
- **N8** — `DocumentTypeCanonicalizer.IsKnown` has zero callers; `Violation.DocumentationUrl` is never populated by the library; `ViolationKind` is a one-member enum. Dead contract weight — document or delete.
- Culture/encoding hygiene is **clean** throughout — explicitly checked; no culture-sensitive parsing anywhere in the library.

---

## 6. Cross-boundary contract (C# ↔ TS ↔ TestHarness)

The production contract is in good shape: serialization is consistently camelCase via `LintScanService.JsonOut`, the `Violation` shape matches field-for-field, and exclusion round-trips are exact. The drift is concentrated in the **harness** — which matters because CLAUDE.md's verification loop relies on Playwright against the harness:

- **X1 (high) — The mock fabricates a feature production never ships.** ✅ **FIXED** — mock `AppStoreModules` is now empty to match the real backend, with a comment to populate both sides together when the marketplace filter ships. Real backend hardcodes `AppStoreModules = Array.Empty<string>()` (`LintScanService.cs:185`, verified); the mock sends `["Administration", "System"]` (`MockFixtures.cs:131,149`). The UI's Marketplace filter checkbox only renders when that count > 0 (`FilterBar.tsx:15,45`) — so the filter works perfectly in `npm run dev` and **never appears in Studio Pro**. A dev-mode Playwright test "verifies" a production-dead feature. Either populate it for real (the data exists — `Modules` already carries `fromMarketplace`) or remove it from the mock.
- **X2 (medium) — `RuleSourcesSaved` is sent by C# (`DockablePaneViewModel.cs:649`) but has no `useMessageBus` case** — rule-source saves give no confirmation, unlike `LinterConfigSaved`'s toast.
- **X3 (medium) — Harness has no handlers for `CancelScan` or `BrowseMxcliPath`** ✅ **FIXED** — harness now tracks the in-flight scan's `CancellationTokenSource` in a `ScanCtsHolder` (cancelled-not-disposed on restart, mirroring `DockablePaneViewModel`) so `CancelScan` reaches it, and `BrowseMxcliPath` calls the same `NativeFileDialog.ShowExePicker` the extension uses (exposed to the harness via `InternalsVisibleTo`), then `SettingsCoordinator.ApplyMxcliPath` — both buttons now work in `npm run dev`. Both buttons were silent no-ops in every dev session, so regressions in those flows were invisible until Studio Pro testing.
- **X4 (low-med) — Harness `Modules` payload uses the legacy `string[]` shape** (`Program.cs:536`), which is the *only* reason the back-compat branch in `useMessageBus.ts:73-77` exists; the marketplace badge and "Exclude all marketplace" bulk action can never be exercised in dev. ~~Harness `RulesCatalog` also omits `ruleDescriptions`/`ruleStarContent`, so the RuleInfoDialog content path is untestable.~~ ✅ **FIXED** — `RulesCatalog` is now built by the shared `LintMessageRouter` (see item 15 below), so the harness gets `ruleDescriptions`/`ruleStarContent` for free. The `Modules` legacy-shape half of X4 is unrelated (Studio-Pro-only, stays open).
- **X5 (low) — Mock mode routes exclusions/baselines to real stores**: with no project configured, every Exclude attempt in out-of-the-box `npm run dev` throws an `ExclusionError` toast — the "no project required" dev loop lies for the exclusion flow.
- **X6 (low) — Dead deepscan remnants on both sides**: the UI handles `phase: "describe"`/`progress` payloads no backend ever sends (see U6); C# handles `RunLintScan`/`RunCommand` no UI ever sends (see B2).

The root cause of X1–X4 was structural: the harness re-implemented the message router by hand (§2). ✅ **FIXED** — see item 15: `LintMessageRouter` is now shared by both hosts, so this class of drift is structurally closed for every message it owns.

---

## 7. Test harness, build, CI, and repo setup

### High

- **S1 — Live PostBuild target with hardcoded personal path** ✅ **FIXED** — target now conditioned on `$(ClevrDeployDir)`; unset = no-op, and the comment is in English. (`Clevr.Lint.Extension.csproj:41-46`, verified). The Dutch comment says "set the path and remove the comment" — i.e. it was meant to stay commented — but the target is active: every build on every machine (including CI) tries to write `C:\Mendix\AcrToLintTest-main\extensions\clevrlint`. On a machine where creating folders under `C:\` needs elevation, **`dotnet build` fails on a fresh clone**; where it succeeds it can deploy a stale dev build into a real Mendix project. Gate it: `Condition="'$(ClevrDeployDir)' != ''"` with the path in a gitignored local props file.
- **S2 — Harness static file serving allows arbitrary local file read, cross-origin** ✅ **FIXED** — static paths resolved with `Path.GetFullPath` and confined to wwwroot; wildcard CORS replaced with localhost-origin echo; non-localhost Origins get 403 on `/api/message`. Both dev flows (direct 5174, Vite proxy) verified working. (`Program.cs:220,222,309-322`, verified). No check that the resolved path stays under `wwwroot`, and `Path.Combine` discards its first argument for rooted seconds — `GET /C:/Users/<user>/.ssh/id_rsa` serves the file. With `Access-Control-Allow-Origin: *` on every response, any web page open in the dev's browser can `fetch()` local files while `npm run dev` runs. Same wildcard CORS lets any site POST side-effectful commands to `/api/message` (`OpenUrl` launches the browser, `ExportHtml` writes and *opens* attacker HTML, `FetchRuleSource` writes downloads to disk). Fix: `Path.GetFullPath` + `StartsWith(wwwroot)`, drop the `*` header, reject non-localhost `Origin`/`Host`.
- **S3 — The documented `lint-scan-settings.json` dev flow does not work.** ✅ **FIXED** — `dev-harness.js` now resolves the project path from `CLEVR_DEV_PROJECT`, then `lint-scan-settings.json`'s `projectPath`, then falls back to `--mock`, printing which source won. Two independent breaks: `scripts/dev-harness.js:6-13` passes `--mock` whenever `CLEVR_DEV_PROJECT` is unset (the settings file is never consulted for mode selection), and the harness resolves the settings file in the extension's **Debug bin**, where the csproj deliberately never copies it. A new contributor following README/CLAUDE.md silently gets mock data with no hint why.

### Medium

- **S4 — TestHarness is never compiled in CI** (`.github/workflows/build-and-test.yml`): a refactor that renames a coordinator method breaks `Program.cs`'s dispatch while CI stays green; `npm run dev` breaks for the whole team. One build step (or building a solution — see §2) closes it. `Pack-Dist.ps1` is also never exercised in CI.
- **S5 — `Pack-Dist.ps1:41-49` hardcodes the artifact list** (`YamlDotNet.dll` by name). ✅ **FIXED** — the DLL/PDB/deps.json list is now discovered by globbing the Release output (excluding `Mendix.StudioPro*`, which the csproj already keeps out via `ExcludeAssets="runtime"`); a new NuGet dependency is picked up automatically. The Release output is now cleaned before each build so stale assemblies from a prior rename can't be globbed in and shipped forever. `wwwroot` is now a true mirror (deleted and recopied each run, not merged), so renamed/removed assets no longer linger in `dist\`.
- **S6 — Hardcoded port 5174 with unguarded `listener.Start()`** ✅ **FIXED** — `listener.Start()` is wrapped in a try/catch for `HttpListenerException`; a port already in use (e.g. an orphaned harness) now prints a clear message pointing at the likely cause and exits cleanly instead of an unhandled stack trace. (`Program.cs:134,157-159`, verified.)
- **S7 — Doc drift, verified this review:** ✅ **FIXED** — all items below corrected in CLAUDE.md/README.md/.gitignore. (Note: the README badge was pointed at the actual git remote `ClevrSolutions/mxcli-linter-studio-extension`, not the local folder name.)
  - CLAUDE.md claims **232 tests**; `dotnet test` reports **25 passed** (19 `[Fact]` + 6 `[InlineData]`).
  - CLAUDE.md says the debug log is `mxlint-debug.log`; code writes `clevr-lint-debug.log` (`DebugLog.cs:18`).
  - CLAUDE.md says the harness "reads `dist\clevrlint` directly"; actually `Program.cs:145` serves from the harness's own Debug output — the documented loop works only by coincidence (Pack-Dist's UI step refreshes `src/.../wwwroot`, which `dotnet run` re-copies).
  - mxcli version: README says v0.13.0, CLAUDE.md says v0.12.0, `MxcliService` always fetches latest. Three conflicting stories.
  - README badge points to `clevr/clevr-lint-extension` (repo is `clevr-acr-extension`); "extenion" typos; `dist/` described as an end-user package, which CLAUDE.md explicitly corrects.
  - `.gitignore:22` ignores `.clevr-acr/` but the code writes `.clevr-lint/` — stale ACR-era entry.

### Low

- **S8** — `Program.cs:174`: missing `$` — user is told to navigate to the literal text `{baseUrl}index` when the browser fails to auto-open. ✅ **FIXED**
- **S9** — Multi-line pwsh CI step only fails on the last command's exit code (`npm ci` failure masked unless `npm run build` also fails); no NuGet caching or concurrency group.
- **S10** — No `TreatWarningsAsErrors`/`AnalysisLevel` in any csproj — nullable violations scroll by as warnings.
- **S11** — Five ad-hoc `JsonSerializerOptions` copies in the harness; `RequestModules` returns hardcoded modules even in non-mock mode.

---

## 8. Test coverage

**Reality check: 25 tests, not 232** (verified via `dotnet test` — CLAUDE.md is badly stale; the doc has since been corrected). All 25 pass. **Update: the suite is now 34 tests** — this session added the golden fingerprint test, `MxcliOutputParser` tests (truncated JSON, mixed-validity array, brace-in-status-line, `TryParse` symmetry), and normalizer null/case-prefix tests. Coverage by file (pre-fix state; ✅ = addressed):

| File | Coverage | Highest-value missing tests |
|------|----------|------------------------------|
| `MxcliOutputParser.cs` | ✅ now covered (5 new tests) | truncated JSON; `[INFO]` line starting with `[`; `{` embedded in status text; each of the 5 envelope property names; `ContainsJson`/`Parse` symmetry |
| `Fingerprint.cs` | ✅ golden-value test added (`sha1:7b8aa6…`, computed independently) | fingerprint stability *is* the exclusions contract; a formula change silently invalidates every team's checked-in `exclusions.json` |
| `MxcliRulesCatalogParser.cs` | none | hyphenated category (currently fails, N5); rule with no category; prose line matching the rule regex |
| `MxcliNormalizer.cs` | partial | null `Module`/`Document` fallback chain; case-mismatched double-prefix (N3); `element` field (never set in any test) |
| `Exclusions.cs` | good | null-valued JSON fields; a golden serialization test (the on-disk format is version-controlled team data — format churn creates noisy diffs) |
| `MxDiffParser.cs` | good | nested mixed `onlyVisualChanges` flags (recursion only exercised one level deep) |
| Extension (all 24 files) | **zero** — no test project exists | the coordinators were explicitly designed to be testable (`IProgress<ScanEvent>`, typed `Resolution`) and have no tests; `ScanCoordinator` and `NavigationCoordinator` are the cheapest wins |
| React UI | **zero** automated | the slice reducers are pure functions begging for vitest; U1/U2 (Settings diff bugs) would have been caught by a 10-line reducer test |

---

## 9. Prioritized recommendations

### Quick wins (hours, high value) — ✅ ALL DONE (2026-07-07)

1. ✅ **Gate the PostBuild target** behind a local property (S1) — unbreaks fresh clones.
2. ✅ **Fix the harness path traversal + CORS** (S2) — three lines.
3. ✅ **Commit the stripped `rules` object in `Settings.save()`** and rewrite `isPendingChanged` as a two-sided diff (U1, U2) — verified via Playwright.
4. ✅ **Cancel-before-restart in `RunFullScan`** (B1, via generation counter) and add timeouts + token to `ChangedElementsResolver` calls (B3).
5. ✅ **Wrap `Parse` in a `TryParse`** returning partial results, null-coalesce `MxcliViolation` fields (N1, N2); switch the double-prefix guard to `OrdinalIgnoreCase` (N3).
6. ✅ **Delete the spike code**: `RunLintScan` handler, `RunCommand`/`RunSpikeCommand`, `ScanCoordinator.RunLintScan` (B2). *(UI-side deepscan message branches deliberately kept — deepscan is a documented planned feature; see X6.)*
7. ✅ **Fix the docs**: test count, debug log name, mxcli version story, dev-flow (fixed in code too — `dev-harness.js` now reads `lint-scan-settings.json`), `.gitignore` entry (S3, S7).
8. ✅ **Add a `Fingerprint` golden-value test** — plus 8 more normalizer tests; suite is now 34 green.

### Medium-term (days)

9. **Add a `.slnx` + `Directory.Build.props`**, build everything (incl. TestHarness) in CI, make Pack-Dist copy `*.dll` from build output (S4, S5, §2).
10. ✅ **DONE — Memoize the UI derived state** and `React.memo` the card components (U3) — pulled forward into the quick-win session.
11. **Make settings writes atomic** (B5), guard settings deserialization with a UI-visible error (B4), fix the `LinterConfigStore.Load` write-on-read race (B7).
12. **Fix harness fidelity**: ~~real `appStoreModules` decision (X1)~~ ✅ done (mock aligned to production's empty array); ~~`CancelScan`/`BrowseMxcliPath` handlers (X3)~~ ✅ done; still open: modern `Modules` shape, then delete the UI's back-compat branch (X4).
13. **Type `appReducer` against a single `AppAction` union** to make cross-slice action coupling compiler-checked (U4).
14. **Start a test project for the Extension coordinators** and vitest for the UI slices (§8).

### Longer-term (structural)

15. ✅ **DONE — Extract a shared message router** used by both `DockablePaneViewModel` and the TestHarness — eliminates the entire harness-drift class (§2, §6) and shrinks both 700+-line files. `LintMessageRouter` now owns exclusions, rules catalog, linter config, baselines, mxcli, rule sources, export/open-url, and log-level dispatch for both hosts; `ScanLifecycle` centralizes the cancel-not-dispose + generation-counter bookkeeping. `OpenDocument`/`RequestModules` stay host-specific (need Studio Pro's `IModel`/`IDockingWindowService`). `DockablePaneViewModel.cs`: 725 → 254 lines. `Program.cs`: 874 → 541 lines.
16. ✅ **DONE — Split `Settings.tsx`** into `ModulesTab`/`RulesTab` components (§2) — `Settings.tsx` is now just the tab shell + save/diff logic + dialogs; verified via Playwright (Modules/Rules tabs render, toggles work, rule info dialog opens, zero console errors).
17. ✅ **DONE — Dialog accessibility pass**: shared `DialogShell` with Escape, focus trap, `role="dialog"`, focus restore; keyboard paths for the click-only divs (U7).
18. **Debug log rotation/size cap** and stop dumping full diff JSON (B8).

---

*Review conducted 2026-07-07 across five parallel review passes (backend, UI, normalizer, harness/build, cross-boundary contract), with all high-severity findings independently re-verified against source. Line numbers reflect the tree at commit `1393940`.*

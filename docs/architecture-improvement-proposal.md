# Architecture improvement proposal — message dispatcher decomposition

Source: `/improve-codebase-architecture` review run 2026-07-01, HTML report
generated at `architecture-review-1782890426.html` (temp file, not checked
in). This doc captures that review plus the design decisions reached in the
follow-up grilling session, so implementation can resume without re-deriving
either.

Vocabulary used throughout (from the `codebase-design` skill — use these
terms exactly, don't drift into "component"/"service"/"API"/"boundary"):
**module**, **interface**, **implementation**, **depth**, **deep**/**shallow**,
**seam**, **adapter**, **leverage**, **locality**. Deletion test: deleting a
module should concentrate complexity elsewhere (real signal), not just make
it disappear (pass-through, not earning its keep).

## Status

| # | Candidate | Strength | Status |
|---|-----------|----------|--------|
| 1 | Collapse the message dispatcher into per-domain coordinators | Strong | **Partially implemented** — `ExclusionCoordinator` done (commit `30a1d96`). `NavigationCoordinator`, `LinterConfigCoordinator`, `SettingsCoordinator`, `ScanCoordinator` remain. |
| 2 | Give rule suppression one seam instead of two | Strong | **Resolved** — see below, no `FilterPolicy` built |
| 3 | Split the AppState bucket into domain slices | Strong | Not started |
| 4 | Give ChangedElementsResolver a pure diff-planning seam | Worth exploring | Not started |
| 5 | Make document-type resolution compositional | Worth exploring | Not started (folds into candidate #1's `NavigationCoordinator`) |
| 6 | Replace three copy-pasted stores with one generic project store | Speculative | Not started |

**Top recommendation from the review:** start with #1 — every other candidate
either lives inside `DockablePaneViewModel` (#5) or is instantiated ad hoc by
it (#2's `LintScanService`, #6's three stores), so deepening the dispatcher
first creates the seam the others need to become testable in isolation.

---

## Candidate 1 — Collapse the message dispatcher into per-domain coordinators

**Files:** `DockablePaneViewModel.cs` (was 1,094 lines), `ExclusionStore.cs`,
`LinterConfigStore.cs`, `BaselineStore.cs`, `LintScanService.cs`,
`useMessageBus.ts`.

**Problem:** the interface was one WebView delegate, but the implementation
was a 30-way case analysis with no internal seams — adding a message type
meant editing the same 1,094-line file, and no handler was reachable without
a real WebView2 and real Mendix APIs.

**Solution:** route by message-type into per-domain coordinators (exclusions,
navigation, config, scan), each a deep module with its own small interface
that owns its store/validation, independent of WebView plumbing.

**Wins:** locality (bugs concentrate in one coordinator, not spread across
duplicated try/catch blocks), leverage (one coordinator interface, testable
without WebView2 or Mendix API), interface shrinks while implementation
absorbs the repeated scaffolding.

### Design decisions (resolved via grilling, apply to every coordinator)

These were worked out against `ExclusionCoordinator` first but are the
**general shape all five coordinators should follow**:

1. **No `IWebView` in coordinator methods.** Coordinators return plain result
   values (or throw); the dispatcher is the only thing that calls
   `PostMessage`. Rationale: keeping `webView` in the signature just relocates
   the untestable shape one level down — one adapter (WebView) stays
   hypothetical. A plain return value makes a unit test the second adapter,
   which is what makes the seam real.

2. **Shared project-directory resolution is its own module.**
   `ExclusionsProjectDir()` was called by exclusions, baselines, linter
   config, rule sources, and the full scan — genuinely cross-cutting, not
   owned by any one coordinator. Extracted as `ProjectDirResolver` (done),
   constructed once in `DockablePaneViewModel`'s constructor and injected into
   every coordinator that needs it.

3. **Coordinators may be `async Task<TResult>` where the real work is async**
   (rule-source fetch, mxcli download, subprocess calls). The dispatcher's
   job in that case is only: await the coordinator, marshal the result to the
   UI thread via `_uiContext`, `PostMessage` it. Don't force everything
   synchronous and keep `Task.Run` wrapping in the dispatcher — the awaited
   network/subprocess call *is* the coordinator's job.

4. **Routing is an explicit table, not per-coordinator `TryHandle`.** A
   `Dictionary<string, ...>`-shaped (or switch-shaped, see `ExclusionCoordinator`'s
   dispatch) mapping lives in the dispatcher itself, in one place. Rejected
   alternative: each coordinator exposing `TryHandle(message, data)` and the
   router trying each in turn — that scatters "what does `SaveLinterConfig`
   do" across every coordinator instead of keeping routing locality in one
   readable place.

5. **Coordinators throw; the dispatcher catches once per message group.**
   The existing failure modes (file IO, JSON parse, network) are genuinely
   exceptional, not control flow callers branch on by kind — they just
   surface `ex.Message` to the UI. Modeling failures as a `Result<T,Error>`
   union was considered and rejected as ceremony without benefit. See
   `DispatchExclusionMessage` in `DockablePaneViewModel.cs` for the pattern:
   one `try/catch` wraps a `switch` over the coordinator calls.

6. **Coordinator constructors are wired once in `DockablePaneViewModel`'s
   constructor** (field, not `new` per message) — matches the existing
   pattern the stores already used (`_exclusions = new()`).

### Per-coordinator shape

**`ExclusionCoordinator`** — ✅ implemented (`src/Clevr.Lint.Extension/ExclusionCoordinator.cs`).
Interface: `List()`, `Add(ExclusionRequest, reason)`, `AddMany(IEnumerable<ExclusionRequest>, reason)`,
`Remove(fingerprint)`, `RemoveMany(IEnumerable<string>)`. `ExclusionRequest` is
a plain record (`Fingerprint`, `RuleId`, `DocumentQualifiedName`,
`ElementName`) — deliberately free of `System.Text.Json` types so the
coordinator's interface is plain C#. Validation (reason/fingerprint
required) and stamping (`ExcludedBy`, `Date`) live inside the coordinator.
The dispatcher (`DispatchExclusionMessage`) parses `JsonObject` →
`ExclusionRequest`, calls the coordinator, and does one `try/catch` → posts
`"Exclusions"` (serialized via `ExclusionsJson.Serialize`) or
`"ExclusionError"`.

The test harness (`src/Clevr.Lint.TestHarness/Program.cs`) had its own
**second, independent copy** of the exclusion dispatch logic that never
touched `DockablePaneViewModel` — a duplication the original review didn't
catch because it was scoped to the extension backend. Fixed as part of this
slice: the harness now builds its own `ExclusionCoordinator` +
`ProjectDirResolver` and routes through them, so "verify via the harness"
(per `CLAUDE.md`) actually exercises the real code path.

**`NavigationCoordinator`** — not started. Wraps `OpenDocument`/`ResolveUnit`
(`DockablePaneViewModel.cs` lines ~479–667 in the pre-refactor file — see
candidate #5 below for the detailed shape). Returns a
`Resolution { Unit, Focus, Route, Reason }` value. Decision from grilling:
`DebugLog.Write` calls must NOT live inside the coordinator — they're file
IO mixed into otherwise-pure resolution logic, which is exactly what blocks
a plain unit test. Instead the coordinator returns `Reason` as data, and the
dispatcher does `DebugLog.Write(projectDir, resolution.Reason)` once, in one
place (also fixes the duplication where nearly every branch hand-wrote its
own log string).

**`LinterConfigCoordinator`** and **`SettingsCoordinator`** — not started.
The original candidate proposed one "Config" coordinator; grilling split it
in two because they touch different stores/files and share no validation
logic (bundling them would recreate a mini god-object):
- `LinterConfigCoordinator` — wraps `LinterConfigStore` (rule enable/severity,
  excluded modules). Messages: `RequestLinterConfig`, `SaveLinterConfig`.
- `SettingsCoordinator` — wraps `lint-scan-settings.json` directly (mxcli
  path) and `RuleSourcesService` (async fetch/delete of rule `.star` files).
  Messages: `RequestMxcliInfo`, `BrowseMxcliPath`, `SetMxcliPath`,
  `DownloadMxcli`, `RequestRuleSources`, `SaveRuleSources`, `FetchRuleSource`,
  `DeleteRuleSourceFiles`. This one needs the async-coordinator shape
  (decision #3 above) since fetch/download are genuinely async.
- Both depend on `ProjectDirResolver` (already extracted).

**`ScanCoordinator`** — not started, **last** in the rollout order (highest
blast radius: threading, streaming, cancellation). `RunFullScan` doesn't fit
the single-request/single-result shape the other coordinators use — it posts
a *sequence* of messages over time (`ScanProgress`, `LintViolations`
batches, `UncommittedDocuments`, then `ScanError`/`ScanFinished`), runs on a
background thread, supports cancellation. Decision from grilling: keep the
"no `IWebView` in the coordinator" rule even here, via a streaming shape:
`ScanCoordinator.RunAsync(projectDir, IProgress<ScanEvent> progress, CancellationToken ct)`.
`IProgress<ScanEvent>` was chosen over `IAsyncEnumerable` because the
codebase already has a callback-streaming habit
(`RunScanStreaming(projectDir, batchJson => ...)` in `LintScanService`) — a
test can assert on the emitted `ScanEvent` sequence with a fake
`IProgress<T>`, no WebView, no thread marshaling inside the coordinator.
Messages: `RunLintScan`, `RunFullScan`, `CancelScan`.

**Also decided:** `RunCommand` (the original `cmd /c echo test` spike that
proves the message bus) doesn't fit any coordinator and should be **deleted
outright**, not carried forward — it's dead weight now that
`RunLintScan`/`RunFullScan` prove the bus works in production. Not yet done
(still present in `DockablePaneViewModel.cs`).

### Rollout order (risk-ordered, not dependency-ordered)

The extension backend currently has **zero automated tests** (only the
Normalizer's 26 xUnit tests — note: `CLAUDE.md` says "232 tests"; that figure
is stale and should be corrected separately, unrelated to this proposal).
Verification is manual, via `Pack-Dist.ps1` → `Install-ClevrLint.ps1` →
relaunch the harness (`CLAUDE.md`'s documented post-change checklist). Given
that, each coordinator should land as its own change, verified in the
harness, before starting the next:

1. ✅ `ExclusionCoordinator` — done, commit `30a1d96`.
2. `NavigationCoordinator` — no threading, but the largest case tree (7
   nested cases: project security, snippet, GUID lookup, name fallback ×
   entity/domain-model-element/document).
3. `LinterConfigCoordinator`, then `SettingsCoordinator`.
4. `ScanCoordinator` — last; only one with threading/streaming/cancellation.

Each step should stay buildable (`dotnet build`) and get a manual smoke pass
in the harness (`dotnet run --project src/Clevr.Lint.TestHarness -- --serve
"C:\Mendix\AcrToLintTest-main"`) before moving to the next coordinator.

---

## Candidate 2 — Give rule suppression one seam instead of two

**Files:** `LintScanService.cs`, `MxcliNormalizer.cs`, `LinterConfigStore.cs`.

**Problem:** two dimensions of "which violations does the user actually
see" live behind two different seams — rule suppression is hardcoded inside
`MxcliNormalizer` (`SuppressedRules`: QUAL003, CONV009, DESIGN001), module
exclusion is applied inside `LintScanService` from config
(`ExcludedModules`). A caller has to know both exist to reason about what's
filtered.

**Resolution (2026-07-01):** no `FilterPolicy` abstraction was needed. mxcli
runs with the project directory as its working directory, and
`lint-config.yaml` (written by `LinterConfigStore`, edited via Settings >
Rules/Modules) lives in that same directory — mxcli reads it directly and
already applies both rule-enable and module-exclusion filtering itself
before the extension ever sees its output. The actual fix was deleting both
seams instead of unifying them:
- `MxcliNormalizer`'s hardcoded `SuppressedRules` (`QUAL003`, `CONV009`,
  `DESIGN001`) — deleted. Those three rules now flow through like any other
  rule; a user who doesn't want them disables them manually in Settings,
  same as any other rule. (A "default-disabled rules" feature was considered
  and explicitly deferred — not built.)
- `LintScanService`'s manual `ExcludedModules` LINQ filter — deleted as
  redundant with what mxcli already does.
`MxcliNormalizer` was also converted to a `static` class (it had no instance
state) while making this change, for consistency with the other Normalizer
helpers.

**Bug found while verifying this:** deleting the redundant C# module filter
and then live-testing against the real mxcli binary showed `System`-module
violations were *not* actually excluded — `LinterConfigStore` was writing
the YAML key `excludedModules`, but mxcli's own config struct
(`mdl/linter/config.go` in the mxcli repo) expects `excludeModules` (no
"d"). Because YAML deserializes unknown keys silently, this had been a
latent bug all along, masked by the very C# filter this change removed.
Fixed with `[YamlMember(Alias = "excludeModules")]` on
`LinterConfigStore.RawConfig.ExcludedModules` so the on-disk key matches
mxcli's exactly; confirmed against the real binary that mxcli now excludes
the configured module and a disabled rule on its own. The `Rules` key
already matched mxcli's expected shape, so rule enable/severity was not
affected by this bug.

One remaining gap closed as part of this: `LinterConfigStore.Load`
now writes the default config to disk on first load (previously only an
in-memory default), so mxcli has a `lint-config.yaml` to read even on a
project's very first scan, before Settings has ever been opened.

---

## Candidate 3 — Split the AppState bucket into domain slices

**Files:** `AppReducer.ts` (357 lines), `AppContext.ts`, `useMessageBus.ts`
(254 lines), `filters.ts`, `exclusions.ts`, `Settings.tsx`, `Report.tsx`.

**Problem:** `AppState` is a flat bucket of 37 unrelated fields — config
(`linterConfig`/`pendingConfig`/`pendingExcludedModules`/`savedExcludedModules`),
scan state (`violations`, `scanStreaming`, `scanProgress`), filters
(`categoryEnabled`, `severityEnabled`, `moduleFilterEnabled`), and UI toggles
(`settingsVisible`, `showExcluded`) all interleave with no domain grouping.
Toggling one rule in Settings bounces through 5 files across 3 layers
(`Settings.tsx` → `AppReducer.ts` → `Settings.tsx` → `DockablePaneViewModel.cs`
→ `LinterConfigStore.cs`). The saved/pending duplication is reimplemented
per field instead of once (`linterConfig`/`pendingConfig` vs.
`savedExcludedModules`/`pendingExcludedModules` each have their own ad hoc
"has this changed" logic).

**Solution:** split `AppState` into slices (`ScanState`, `ConfigState`,
`FilterState`, `UIState`), each with its own reducer, plus a shared
`Draft<T>` abstraction for the saved/pending pattern so "is this changed" /
"commit" / "reset" are written once instead of per-field.

**Not yet designed via grilling.** This is independent of the C#-side
candidates (#1, #2) and could be tackled in parallel by a different session,
though landing `LinterConfigCoordinator` first would clarify the C#-side
config shape this slice needs to mirror.

---

## Candidate 4 — Give ChangedElementsResolver a pure diff-planning seam

**Files:** `ChangedElementsResolver.cs` (345 lines), `ProcessRunner.cs`,
`MxDiffParser.cs` (160 lines).

**Problem:** git interaction, tar extraction, mx.exe invocation, and diff
interpretation are entangled in one 345-line method with no internal seam.
`MxDiffParser.Parse` is already pure, but nothing else is, so the 8-state
`ChangedScanStatus` enum can only be exercised end-to-end with a real git
repo and mx.exe on PATH.

**Solution:** extract a pure `DiffPlan.From(rawDiffJson, gitState)` that owns
all status interpretation, leaving git/tar/mx.exe calls as thin adapters
that only fetch bytes.

**Not yet designed via grilling.**

---

## Candidate 5 — Make document-type resolution compositional

**Files:** `DockablePaneViewModel.cs` (`OpenDocument`, `ResolveUnit`,
`IsEntity`/`IsDomainModelElement`/`IsProjectSecurity`/`IsSnippet`/`IsEnumeration`).

**Problem:** what reads as one method is seven nested case analyses (project
security, snippet, GUID lookup, name fallback × 4 sub-cases: entity,
domain-model-element, document lookup), each a separate boolean helper, with
`DebugLog.Write` calls and hardcoded UI error strings woven through the
resolution logic itself.

**Solution:** this candidate is **absorbed into candidate #1's
`NavigationCoordinator`** — see the detailed design above (returns
`Resolution { Unit, Focus, Route, Reason }`, no `DebugLog.Write` inside the
coordinator). Not a separate implementation step.

---

## Candidate 6 — Replace three copy-pasted stores with one generic project store

**Files:** `ExclusionStore.cs`, `BaselineStore.cs`, `LinterConfigStore.cs`.

**Problem:** all three reimplement load/mutate/atomic-save (temp file +
move) with no shared abstraction.

**Solution:** a generic `ProjectStore<T>` handling atomic write once; each
concrete store becomes a thin adapter supplying serialization.

**Flagged as speculative in the original review:** only `ExclusionStore` and
`BaselineStore` are truly identical (JSON, list-shaped); `LinterConfigStore`
writes YAML with a different shape. Generalizing all three may just move the
copy-paste into generic-type gymnastics. Treat as speculative until a fourth
store shows up, or revisit once `LinterConfigCoordinator`/`SettingsCoordinator`
land and it's clearer whether more stores are coming.

---

## Domain model / ADR notes

No `CONTEXT.md` or `docs/adr/` exist in this repo yet. Nothing decided so far
sharpened business-domain vocabulary (the coordinators are architecture-level
names, not domain terms) or reversed a previously recorded decision, so
neither was created during this round. If a future session names a
deepened module after a concept worth codifying (e.g. if `FilterPolicy` from
candidate #2 becomes a term people use in conversation), add `CONTEXT.md`
then.

## Continuing this work

Next session: implement `NavigationCoordinator` per the shape above, then
resume the grilling loop (`/grilling`) if any part of that shape needs
re-deriving in more detail (e.g. the exact `Resolution` record fields, or
how `TryOpenEditor` interacts with the returned `Resolution`). The
`codebase-design` skill's vocabulary and this doc's "Design decisions"
section for candidate #1 should carry over unchanged to each remaining
coordinator — the open questions are `NavigationCoordinator`'s exact record
shape and `ScanCoordinator`'s exact `ScanEvent` union, not the overall
pattern.

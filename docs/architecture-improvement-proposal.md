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
| 1 | Collapse the message dispatcher into per-domain coordinators | Strong | **Implemented** — `ExclusionCoordinator` (commit `30a1d96`), `NavigationCoordinator`, `LinterConfigCoordinator`, `SettingsCoordinator`, and `ScanCoordinator` all done (2026-07-01). |
| 2 | Give rule suppression one seam instead of two | Strong | **Resolved** — see below, no `FilterPolicy` built |
| 3 | Split the AppState bucket into domain slices | Strong | **Implemented** |
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

**`NavigationCoordinator`** — ✅ implemented (`src/Clevr.Lint.Extension/NavigationCoordinator.cs`).
Interface: `Resolve(documentId, qualifiedName, documentType) → Resolution`, where
`Resolution { Unit, Focus, Route, Reason, IsEnumeration }` and `Route` is a
`NavigationRoute` enum (`Opened`, `NoModel`, `ProjectSecurity`, `Snippet`,
`NotFound`) — the same "return the outcome as data" shape used for
project-security/snippet/not-found in the pre-refactor code, now made
explicit instead of implicit in `if (unit == null)` branches. Per the
grilling decision, `DebugLog.Write` does NOT live inside the coordinator;
`ResolveUnit` (private, static) accumulates a `List<string>` of per-step
route reasons (GUID-OK/MISS/EMPTY, then the name-route outcome) and joins
them into `Resolution.Reason`, and `DockablePaneViewModel.OpenDocument`
calls `DebugLog.Write(projectDir, $"OpenDocument: {resolution.Reason}")`
exactly once. `IsEntity`/`IsDomainModelElement`/`IsProjectSecurity`/
`IsSnippet`/`IsEnumeration`/`FindDocument` all moved into the coordinator as
private statics — the dispatcher now only does message translation
(`switch` over `NavigationRoute` → `PostMessage`) and the one exception
catch. Verified: `dotnet build` clean, `Pack-Dist.ps1` → harness smoke pass
(scan runs, filters/settings work, zero console errors). The harness's own
`OpenDocument` handler already
stubs the message with `"...not available in the test harness."`
(`Program.cs` — no real Studio Pro model there), so it doesn't exercise
`NavigationCoordinator`'s resolution logic itself; unlike the
`ExclusionCoordinator` slice, there was no second copy of this logic to
deduplicate.

**`LinterConfigCoordinator`** and **`SettingsCoordinator`** — the original
candidate proposed one "Config" coordinator; grilling split it in two
because they touch different stores/files and share no validation logic
(bundling them would recreate a mini god-object):
- `LinterConfigCoordinator` — ✅ implemented
  (`src/Clevr.Lint.Extension/LinterConfigCoordinator.cs`). Interface:
  `Load() → LinterConfig`, `Save(LinterConfig)`. `LinterConfig`/
  `LinterConfigRule` (in `LinterConfigStore.cs`) were already plain C#
  records free of `System.Text.Json` types, so this coordinator is a thin
  wrapper — no validation of its own (any rule-override dictionary or
  module list is valid), the seam exists purely to keep project-dir
  resolution and persistence out of the dispatcher, matching every other
  coordinator. `DockablePaneViewModel.PostLinterConfig`/`SaveLinterConfig`
  now only translate `JsonObject` ⇄ `LinterConfig` and call the coordinator;
  `_linterConfig`/`LinterConfigStore` field was removed (replaced by the
  coordinator, constructed once in the constructor from a fresh
  `LinterConfigStore()` + the shared `_projectDirResolver`). Messages:
  `RequestLinterConfig`, `SaveLinterConfig`.
  Like `ExclusionCoordinator`, the test harness (`Program.cs`) had its own
  independent copy of this dispatch logic (`new LinterConfigStore()` called
  directly in the `RequestLinterConfig`/`SaveLinterConfig` cases) that never
  touched the real coordinator — fixed as part of this slice: the harness
  now builds one `LinterConfigCoordinator` (threaded through
  `RunServeModeAsync` → `HandleRequestAsync` → `DispatchMessage`, same
  wiring path as `exclusionCoordinator`) and routes both messages through
  it. Verified: `dotnet build` clean (both projects), 25 Normalizer tests
  green, `Pack-Dist.ps1` → harness smoke pass — opened Settings > Modules,
  toggled a module checkbox, clicked Save, confirmed `lint-config.yaml` was
  written with the correct `excludeModules:` YAML key and the harness log
  showed the real `SaveLinterConfig` dispatch; zero console errors.
- `SettingsCoordinator` — ✅ implemented
  (`src/Clevr.Lint.Extension/SettingsCoordinator.cs`). Wraps
  `lint-scan-settings.json` directly (mxcli path) and `RuleSourcesService`
  (async fetch/delete of rule `.star` files) — there was never a separate
  store type for this state, so the coordinator owns the file IO itself
  rather than delegating to one, same as `LinterConfigCoordinator`'s "thin
  wrapper" shape. Interface: `GetMxcliInfo()`, `CurrentMxcliPath()`,
  `ApplyMxcliPath(path)`, `DownloadMxcliAsync(onProgress, ct)`,
  `GetRuleSources()`, `SaveRuleSources(sources)`,
  `FetchRuleSourceAsync(url, replaceExisting, onProgress, ct)`,
  `DeleteRuleSourceFilesAsync(url, onProgress, ct)`. `RuleSourcesService`
  was widened from `internal` to `public` so the coordinator (constructed in
  both `DockablePaneViewModel` and the test harness) can hold a reference to
  it. Messages: `RequestMxcliInfo`, `BrowseMxcliPath`, `SetMxcliPath`,
  `DownloadMxcli`, `RequestRuleSources`, `SaveRuleSources`, `FetchRuleSource`,
  `DeleteRuleSourceFiles` — routed through two dispatcher methods
  (`DispatchMxcliMessageAsync`, `DispatchRuleSourcesMessageAsync`) since the
  two families don't share an error-message shape. Per decision #3, the
  genuinely-async coordinator calls (`DownloadMxcliAsync`,
  `FetchRuleSourceAsync`, `DeleteRuleSourceFilesAsync`) are awaited directly
  by the dispatcher with no extra `Task.Run` wrapper; the synchronous-but-
  blocking ones (`GetMxcliInfo`/`ApplyMxcliPath` shell out to `mxcli
  --version`/`where.exe`) keep a `Task.Run` since they aren't `async Task`
  themselves. `BrowseMxcliPath`'s native file dialog (`NativeFileDialog`)
  stays in the dispatcher, same rationale as `TryOpenEditor` staying out of
  `NavigationCoordinator` — it needs no coordinator state and calling it is
  a UI concern, not a settings concern. The test harness had no prior
  dispatch logic at all for these eight messages (not a duplicate-to-fix
  like Exclusion/LinterConfig, just a gap) — closed as part of this slice by
  wiring `SettingsCoordinator` into `Program.cs` alongside the other
  coordinators, so "verify via the harness" now actually exercises mxcli
  info and rule-source fetch/save, not just scan+exclusions+config.

**`ScanCoordinator`** — ✅ implemented
(`src/Clevr.Lint.Extension/ScanCoordinator.cs`). `RunFullScan` doesn't fit
the single-request/single-result shape the other coordinators use — it posts
a *sequence* of messages over time (`ScanProgress`, `LintViolations`
batches, `UncommittedDocuments`, then `ScanError`/`ScanFinished`), runs on a
background thread, supports cancellation. Kept the "no `IWebView` in the
coordinator" rule via the streaming shape decided during grilling:
`ScanCoordinator.RunFullScan(IProgress<ScanEvent> progress, CancellationToken ct)`,
where `ScanEvent { Kind, Data }` is a `ScanEventKind` enum
(`Progress`/`Violations`/`UncommittedDocuments`/`Error`/`Finished`) plus the
raw string payload — the same "return the outcome as data" shape used by
`NavigationCoordinator`'s `Resolution`. `IProgress<T>` over
`IAsyncEnumerable` was chosen for the reason anticipated in the original
proposal (matches `LintScanService`'s existing callback-streaming habit,
easy to fake in a test) — and it turned out to have a second benefit found
during implementation: `Progress<T>` captures `SynchronizationContext.Current`
at *construction* time and auto-marshals every `Report()` back onto it, so
`DockablePaneViewModel`'s dispatcher no longer needs the hand-rolled
`Post()`/`uiContext.Post(...)` closure the old `RunFullScan` method had —
constructing `new Progress<ScanEvent>(ev => PostScanEvent(webView, ev))` on
the UI thread (where `MessageReceived` fires) is enough. The one place scan
events touch the WebView is `PostScanEvent`, a `ScanEventKind → message
name` switch, mirroring the routing-table decision (#4) used everywhere
else. Also exposes `RunLintScan()` (the older single-shot "Scan" button,
mxcli only, no streaming) so both `RunLintScan` and `RunFullScan` sit behind
one coordinator; `CancelScan` stayed a one-line `_scanCts?.Cancel()` in the
dispatcher since there's no coordinator state to touch. Messages:
`RunLintScan`, `RunFullScan`, `CancelScan`.

Like `ExclusionCoordinator`/`LinterConfigCoordinator`, the test harness
(`Program.cs`) had its own **independent copy** of the `RunFullScan`
orchestration (git-diff task + mxcli streaming + JSON payload shape) that
never touched the real coordinator — fixed as part of this slice: the
harness now builds one `ScanCoordinator` and routes `RunFullScan` through it
via a small `SyncProgress<T> : IProgress<T>` adapter (`Progress<T>` itself
needs a `SynchronizationContext` to marshal onto, which this console app
doesn't have — `SyncProgress<T>` just invokes the callback synchronously and
in order, which is correct here since `DispatchMessage` already runs on its
own background thread). This also fixed a latent inconsistency: the
harness's old inline `UncommittedDocuments` payload used a bare
`JsonSerializerOptions { PropertyNamingPolicy = CamelCase }` (no enum
converter, no null-suppression), whereas the real extension path used
`LintScanService.JsonOut` (adds both) — now that both paths go through
`ScanCoordinator`, they serialize identically.

Verified for both coordinators: `dotnet build` clean across all three
projects (Extension, TestHarness, Normalizer.Tests), 25 Normalizer tests
green, `Pack-Dist.ps1` → harness smoke pass via Playwright — ran a full
scan (199 improvements rendered, zero console errors), opened Settings →
Configuration (mxcli source/path/version resolved via `SettingsCoordinator`,
no longer stuck on "Loading mxcli information…"), opened Settings → Sources
(rule-sources list loaded, empty-state rendered correctly).

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
2. ✅ `NavigationCoordinator` — done (2026-07-01).
3. ✅ `LinterConfigCoordinator` — done (2026-07-01).
4. ✅ `SettingsCoordinator` — done (2026-07-01).
5. ✅ `ScanCoordinator` — done (2026-07-01). Only one with threading/streaming/cancellation.

All five coordinators from candidate #1 are now implemented. `RunCommand`
(the spike, see "Also decided" above) is still present in
`DockablePaneViewModel.cs` — deleting it was decided but deferred, not part
of this candidate's coordinator work.

Each step should stay buildable (`dotnet build`) and get a manual smoke pass
in the harness (`npm run dev`, or `dotnet run --project src/Clevr.Lint.TestHarness
-- --serve --mock`) before moving to the next coordinator.

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

**Solution, as implemented (2026-07-01, resolved via grilling):** `AppState`
is now `{ scan: ScanState, config: ConfigState, filters: FilterState,
baseline: BaselineState, ui: UIState }` — five slices, not four; grilling
surfaced that `baselines`/`selectedBaselineId` (list + selection + filter
interaction) didn't force-fit into Scan or Filters without becoming a
sub-concern of a slice it isn't really about, so it got its own
`BaselineState`. `exclusions` landed in `ConfigState` (same lifecycle as
`linterConfig` — loaded once, edited via Settings). `appStoreVisible` and the
`uncommitted*` fields landed in `FilterState` (same family as
`categoryEnabled`/`severityEnabled` — visibility toggles that gate which
violations show).

Key design decisions from grilling:
- **One React Context, not four.** `useAppState()`/`useAppDispatch()` are
  unchanged; `AppState` is just nested now. A four-Context split was
  considered and rejected — nothing in this codebase needs selective
  re-rendering, and `filters.ts`'s `activeViolations()`/`baseViolations()`
  already read across every slice's fields in one call, which would have
  meant threading multiple hooks through those functions for no payoff.
- **One composed reducer, not four Contexts' worth of reducers.** Each slice
  lives in its own file under `context/slices/` (`scanSlice.ts`,
  `configSlice.ts`, `filterSlice.ts`, `baselineSlice.ts`, `uiSlice.ts`) with
  its own `State` type, initial state, action union, and reducer function.
  `AppReducer.ts` shrank to the composed `AppState`/`AppAction` types and a
  ~15-line top-level `appReducer` that calls all five sub-reducers per
  action (combineReducers-style) — each sub-reducer no-ops (returns the same
  state) on action types it doesn't own.
- **Cross-slice actions are handled independently in each owning
  sub-reducer**, not specially routed. A handful of actions touch two slices
  (`SCAN_FINISHED` sets Scan's streaming/progress AND triggers Baseline's
  auto-select; `SCAN_ERROR` clears Scan's progress AND sets UI's toast;
  `SHOW_SETTINGS`/`HIDE_SETTINGS` toggle UI's `settingsVisible` AND start/cancel
  Config's drafts; `SELECT_BASELINE` sets Baseline's `selectedBaselineId` AND
  clears Filter's `baselineFilter`). None of these needed one slice to read
  another slice's data to compute its own update, so no cross-slice
  coordination layer was needed beyond "both sub-reducers see the action."
- **`Draft<T> = { saved: T; pending: T }`** (`context/draft.ts`) plus four
  free functions (`startEdit`, `cancelEdit`, `commit`, `editPending`) —
  deliberately not a higher-order reducer, since generic action dispatch
  would have needed a field-targeting mechanism this codebase's action
  design doesn't otherwise have. `ConfigState.linterConfig` and
  `ConfigState.excludedModules` are now `Draft<...>` instead of four
  separate saved/pending fields with hand-rolled show/hide/commit logic
  duplicated per field.
- **Migration was one atomic change, not incremental with a shim.** Changing
  `AppState`'s shape made `tsc` fail loudly on every stale flat-field access
  across the 14 consumer components; each was fixed until the build was
  green, with no temporary flattening shim. Given this reducer has zero
  automated test coverage, the compiler was the safety net.

Fields kept as-is without further cleanup during this pass: `scanStartMs`
(dead — set but never read, pre-existing, out of scope for this candidate).

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
`NavigationCoordinator`** — ✅ done, see the detailed design above (returns
`Resolution { Unit, Focus, Route, Reason, IsEnumeration }`, no
`DebugLog.Write` inside the coordinator). Not a separate implementation step.

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

Candidate #1 (the coordinator decomposition) is now fully implemented — all
five coordinators land, `DockablePaneViewModel.cs` only routes messages and
translates results to `PostMessage` calls. Remaining work from this doc, in
order of what the "Top recommendation" reasoning would suggest next:

- Delete `RunCommand` (the spike) from `DockablePaneViewModel.cs` — decided,
  never executed (see candidate #1's "Also decided").
- Candidate #4 (`ChangedElementsResolver` pure diff-planning seam) — not
  started, not yet designed via grilling.
- Candidate #6 (generic `ProjectStore<T>`) — speculative; revisit now that
  `SettingsCoordinator` has landed and turned out to wrap
  `lint-scan-settings.json` directly rather than adding a fourth store, so
  the "wait for a fourth store" trigger condition has not fired.

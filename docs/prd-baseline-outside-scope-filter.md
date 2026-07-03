# PRD: Baseline Scope Awareness — "Outside Baseline" Filter

## Problem Statement

When a user saves a baseline snapshot, the module/rule scan configuration active at that moment (excluded modules, disabled rules) is never recorded — only the resulting `Violation[]` is stored. If the user later changes their scan scope (includes a module that was previously excluded, or enables a rule that was previously disabled) and runs a new scan, every violation surfaced by that newly-in-scope module/rule shows up under the **"New"** filter, indistinguishable from violations that are genuinely new regressions in code that was already being scanned. This misleads the user into thinking their codebase got worse, when in fact they simply started looking at parts of it for the first time.

## Solution

When a baseline is saved, also record the scan scope (excluded modules and disabled rules) that was active at that time. When comparing a later scan against that baseline, a violation missing from the baseline is split into two distinct buckets instead of one:

- **New** — the violation's module and rule were already in scope at baseline time, so its absence from the baseline is a genuine new finding.
- **Outside Baseline** — the violation's module or rule was *not* in scope at baseline time, so the baseline never had a chance to detect it; it isn't a regression, just newly-visible.

The existing filter bar (`All`, `New`, `Fixed`) gains a fourth button, **Outside Baseline**, positioned between `New` and `Fixed`. Baselines saved before this feature ships have no recorded scope and behave exactly as before (everything falls under `New`; `Outside Baseline` never appears for them).

## User Stories

1. As a lint report reviewer, I want violations from newly-included modules to be labeled differently from genuine regressions, so that I don't mistake "we started scanning this" for "this code got worse."
2. As a lint report reviewer, I want violations from newly-enabled rules to be labeled differently from genuine regressions, so that turning on a new rule doesn't look like a spike in new defects.
3. As a lint report reviewer, I want the "New" count to only reflect violations in code that was already being scanned, so that the "New" bucket stays a trustworthy regression signal over time.
4. As a lint report reviewer, I want a dedicated "Outside Baseline" filter, so that I can still review those newly-visible violations without them polluting the "New" bucket.
5. As a lint report reviewer, I want the "Outside Baseline" button to disappear when its count is zero, so that the filter bar stays uncluttered when scope hasn't changed between scans.
6. As a lint report reviewer comparing against an old baseline saved before this feature existed, I want the report to behave exactly as it did before (no "Outside Baseline" bucket), so that old baselines don't produce misleading zero-counts or broken comparisons.
7. As a lint report reviewer, I want the "Fixed" filter's existing behavior (violations resolved since the baseline) to remain unchanged, so that this feature doesn't regress an already-correct comparison.
8. As a lint report reviewer, I want "All" to continue showing exactly the current scan's violations (unchanged from today), so that this feature is additive and doesn't change what "All" means.
9. As a developer maintaining this codebase, I want the scope-comparison logic to reuse the existing fingerprint-matching mechanism already used for baseline/exclusion comparisons, so that the new logic is consistent with existing patterns rather than inventing a new comparison mechanism.
10. As a developer maintaining this codebase, I want the new baseline scope fields to be optional/nullable on deserialization, so that existing `baselines.json` files on disk don't break when loaded by the updated extension.

## Implementation Decisions

- **`BaselineEntry` gains two new fields** capturing the effective scan scope at save time:
  - `ExcludedModules: string[]` — snapshot of `LinterConfig.ExcludedModules` at save time.
  - `DisabledRuleIds: string[]` — a flattened list of rule IDs that were disabled at save time (derived from `LinterConfig.Rules` by filtering to `Enabled == false`; not the raw `Dictionary<string, LinterConfigRule>`, since severity overrides are irrelevant to scope).
  - Both fields are optional/nullable on deserialization; missing values are treated as empty arrays (no exclusions recorded), so old baselines behave exactly as they do today — everything not in the baseline's fingerprint set is classified as "New," and "Outside Baseline" never applies to them.
- **Save flow**: the handler that persists a new baseline (today only receiving `{ violations, savedAt }` from the UI) is extended to also capture the current `LinterConfig` (excluded modules + disabled rule IDs) at the moment of saving, so the snapshot reflects the scope that actually produced `violations`.
- **TS mirror**: the `BaselineEntry` type gains the corresponding `excludedModules: string[]` and `disabledRuleIds: string[]` fields (optional, defaulting to `[]` when absent) to match the C# record.
- **Classification rule** (pure function, alongside existing fingerprint-set logic): for a current violation `v` whose fingerprint is not in the selected baseline's fingerprint set:
  - Classify as **Outside Baseline** if `moduleOf(v)` is in `baseline.excludedModules` **or** `v.ruleId` is in `baseline.disabledRuleIds`.
  - Otherwise classify as **New**.
  - This reuses the same fingerprint/module/rule derivation already used by the existing "Fixed" scope-check, just applied to the "New" side.
- **Filter bar**: the `baselineFilter` value gains a fourth state, `"outside"`, alongside the existing `"new" | "fixed" | null`. A new "Outside Baseline" button renders between "New" and "Fixed," following the same conditional-rendering pattern as the rest of the filter bar (only shown when a baseline is selected).
- **Zero-count hiding**: the "Outside Baseline" button is hidden entirely when its count is `0` for the currently selected baseline. If the currently active filter is `"outside"` and the user switches to a baseline where the count is `0`, the filter resets to `null` ("All") — following the same reset pattern already used when `SELECT_BASELINE` changes.
- **"New," "Fixed," and "All" are unaffected by the zero-count hiding rule** — they remain always visible regardless of count, matching today's behavior. Only "Outside Baseline" gets this treatment, since it's the only bucket expected to commonly be empty (no scope change between scans is the common case).
- **"Fixed" logic is unchanged.** Its existing scope-check (dropping baseline violations whose module/rule is excluded/disabled in the *current* config before diffing) already handles its own asymmetric case and needs no modification.
- **"All" is unchanged.** It continues to represent the current scan's violations only (after existing exclusion/app-store filters); it does not become a superset including "Fixed" items. As a result, bucket totals reconcile as `New + Outside Baseline ≤ All` (equality only when no violation is unchanged from baseline), and `Fixed` remains a separately-reported count that is not part of `All`'s total, since fixed violations no longer exist in the current scan by definition.

## Testing Decisions

- A good test here exercises the **classification behavior**, not the internal derivation helpers: given a baseline with a known `excludedModules`/`disabledRuleIds` snapshot and a set of current violations, assert which fingerprints land in `New` vs `Outside Baseline` vs `Fixed` vs `All`. Tests should not assert on internal helper names or intermediate `Set` contents.
- **`Clevr.Lint.Normalizer`** is unaffected by this change (no changes to `Violation.cs` or normalization logic), so its existing 232-test suite should simply continue to pass unmodified.
- **`BaselineStore.cs` / `BaselineEntry`** and the filter classification logic (`filters.ts`) both live in `Clevr.Lint.Extension`, which — per project convention (`CLAUDE.md`) — has no committed unit test suite; verification is done via `Pack-Dist.ps1` rebuild + `Clevr.Lint.TestHarness --serve` + an ad hoc Playwright script driving the actual UI (save a baseline, change module/rule scope, rescan, verify the four filter buttons and their counts, verify old baselines without scope data still work). This matches the established pattern already used for prior filter/reducer changes in this codebase — do not introduce a new UI test runner as part of this change.
- The Playwright verification pass should specifically cover: (1) saving a baseline with modules/rules A enabled, B excluded; (2) re-enabling B and rescanning; (3) confirming B's violations appear under "Outside Baseline" and not "New"; (4) confirming the "Outside Baseline" button is absent when scope hasn't changed; (5) confirming an old-format baseline (manually edited `baselines.json` without the new fields) loads without error and shows no "Outside Baseline" button.

## Out of Scope

- Changing what "All" represents (it does not become a union of current + fixed violations).
- Adding a fifth "Unchanged" bucket for violations present in both baseline and current scan.
- Backfilling/migrating old baselines with inferred scope data — old baselines simply behave as they do today.
- Changing "Fixed" filter semantics or its existing current-config scope-check.
- Any severity-override tracking in the baseline snapshot (only exclusion/enablement scope is captured, not `LinterConfigRule.Severity`).
- Zero-count hiding for "New" or "Fixed" buttons — only "Outside Baseline" is hidden at zero.
- Introducing a committed UI unit/integration test suite as part of this change.

## Further Notes

- This feature is purely additive: existing baselines, the "All"/"New"/"Fixed" definitions, and the fingerprint-matching mechanism (`ruleId | documentQualifiedName | elementName` sha1) are all unchanged. The only new surface area is the two new `BaselineEntry` fields, the new classification branch, and the new filter button.
- The `moduleOf()` derivation (module name = prefix of `documentQualifiedName` before the first `.`) is currently a TypeScript-only convention (`ui/src/utils/origins.ts`); this PRD relies on that same convention rather than introducing a server-side equivalent.

import { GENERIC_CATEGORY_FALLBACK, GENERIC_PREFIX_FALLBACK, MXCLI_CATEGORY_TO_LINT } from "../constants";
import type { Violation } from "../types";
import { isAppStoreModule, moduleOf } from "./origins";
import { excludedFingerprintSet } from "./exclusions";
import type { AppState } from "../context/AppReducer";

export function displayCategory(v: Violation, ruleCategories: Record<string, string>): string {
  const mxcliCat = (ruleCategories[v.ruleId] ?? "").toLowerCase();
  if (mxcliCat && MXCLI_CATEGORY_TO_LINT[mxcliCat]) return MXCLI_CATEGORY_TO_LINT[mxcliCat]!;
  return GENERIC_PREFIX_FALLBACK[v.category] ?? GENERIC_CATEGORY_FALLBACK;
}

export function matches(v: Violation, q: string, ruleNames: Record<string, string>, ruleCategories: Record<string, string>): boolean {
  if (!q) return true;
  return [
    v.ruleId, ruleNames[v.ruleId], v.category,
    displayCategory(v, ruleCategories), v.severity,
    v.documentType, v.documentQualifiedName, v.elementName, v.reason, v.suggestion,
  ]
    .filter(Boolean)
    .join(" ")
    .toLowerCase()
    .includes(q);
}

export function passesFilters(
  v: Violation,
  q: string,
  categoryEnabled: Set<string>,
  severityEnabled: Set<string>,
  moduleFilterEnabled: Set<string>,
  ruleNames: Record<string, string>,
  ruleCategories: Record<string, string>,
): boolean {
  return (
    (categoryEnabled.size === 0 || categoryEnabled.has(displayCategory(v, ruleCategories))) &&
    (severityEnabled.size === 0 || severityEnabled.has(v.severity)) &&
    (moduleFilterEnabled.size === 0 || moduleFilterEnabled.has(moduleOf(v))) &&
    matches(v, q, ruleNames, ruleCategories)
  );
}

export function allDisplayViolations(state: AppState): Violation[] {
  return state.scan.violations;
}

export function baseViolations(state: AppState): Violation[] {
  let vs = allDisplayViolations(state)
    .filter((v) => state.filters.appStoreVisible || !isAppStoreModule(v, state.scan.appStoreModules));
  if (state.config.excludedModules.saved.includes("Project")) {
    const knownModules = new Set(state.config.modules.map((m) => m.name).filter((n) => n !== "Project"));
    vs = vs.filter((v) => knownModules.has(moduleOf(v)));
  }
  return vs;
}

function selectedBaseline(state: AppState) {
  return state.baseline.baselines.find((x) => x.id === state.baseline.selectedBaselineId);
}

function selectedBaselineViolations(state: AppState): Violation[] {
  return selectedBaseline(state)?.violations ?? [];
}

export function baselineFingerprintSet(state: AppState): Set<string> {
  return new Set(selectedBaselineViolations(state).map((v) => v.fingerprint));
}

// A violation missing from the baseline is "outside baseline" (not a regression) when its
// module or rule wasn't in scope for that baseline's scan — the baseline never had a chance
// to detect it. Baselines saved before this field existed have no recorded scope, so nothing
// is ever classified as outside baseline for them.
function isOutsideBaselineScope(v: Violation, state: AppState): boolean {
  const b = selectedBaseline(state);
  if (!b) return false;
  const excludedModules = b.excludedModules ?? [];
  const disabledRuleIds = b.disabledRuleIds ?? [];
  return excludedModules.includes(moduleOf(v)) || disabledRuleIds.includes(v.ruleId);
}

export function currentFingerprintSet(state: AppState): Set<string> {
  return new Set(state.scan.violations.map((v) => v.fingerprint));
}

export function activeViolations(state: AppState): Violation[] {
  if (state.filters.baselineFilter === "fixed" && state.baseline.selectedBaselineId) {
    // Use scoped current fingerprints — same view the user sees (app store + Project exclusion applied).
    const scopedCurrentFps = new Set(baseViolations(state).map((v) => v.fingerprint));

    // Scope baseline violations to match the current scan configuration so that
    // violations from excluded modules or disabled rules don't appear as "fixed."
    let baselineVs = selectedBaselineViolations(state);

    // 1. App-store visibility
    baselineVs = baselineVs.filter((v) => state.filters.appStoreVisible || !isAppStoreModule(v, state.scan.appStoreModules));

    // 2. Module scan scope — mirrors baseViolations + general excluded modules
    const savedExcludedModules = state.config.excludedModules.saved;
    if (savedExcludedModules.includes("Project")) {
      const knownModules = new Set(state.config.modules.map((m) => m.name).filter((n) => n !== "Project"));
      baselineVs = baselineVs.filter((v) => knownModules.has(moduleOf(v)));
    }
    const otherExcluded = new Set(savedExcludedModules.filter((m) => m !== "Project"));
    if (otherExcluded.size > 0) {
      baselineVs = baselineVs.filter((v) => !otherExcluded.has(moduleOf(v)));
    }

    // 3. Rule scan scope — disabled rules were not evaluated, so their violations are not "fixed"
    baselineVs = baselineVs.filter((v) => state.config.linterConfig.saved[v.ruleId]?.enabled !== false);

    return baselineVs.filter((v) => !scopedCurrentFps.has(v.fingerprint));
  }

  const ex = excludedFingerprintSet(state.config.exclusions);
  let vs = baseViolations(state).filter((v) => !ex.has(v.fingerprint));
  if (state.filters.uncommittedFilterActive && state.filters.uncommittedAvailable) {
    vs = vs.filter((v) => {
      const qnMatch = state.filters.uncommittedQualifiedNames.size > 0
        && state.filters.uncommittedQualifiedNames.has(v.documentQualifiedName.toLowerCase());
      const idMatch = !!v.documentId && state.filters.uncommittedDocumentIds.has(v.documentId.toLowerCase());
      return qnMatch || idMatch;
    });
  }
  if (
    (state.filters.baselineFilter === "new" || state.filters.baselineFilter === "outside")
    && state.baseline.selectedBaselineId
  ) {
    const baselineFps = baselineFingerprintSet(state);
    vs = vs.filter((v) => !baselineFps.has(v.fingerprint));
    vs = vs.filter((v) =>
      state.filters.baselineFilter === "outside"
        ? isOutsideBaselineScope(v, state)
        : !isOutsideBaselineScope(v, state));
  }
  return vs;
}

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
  return state.violations;
}

export function baseViolations(state: AppState): Violation[] {
  let vs = allDisplayViolations(state)
    .filter((v) => state.appStoreVisible || !isAppStoreModule(v, state.appStoreModules));
  if (state.savedExcludedModules.includes("Project")) {
    const knownModules = new Set(state.modules.map((m) => m.name).filter((n) => n !== "Project"));
    vs = vs.filter((v) => knownModules.has(moduleOf(v)));
  }
  return vs;
}

function selectedBaselineViolations(state: AppState): Violation[] {
  const b = state.baselines.find((x) => x.id === state.selectedBaselineId);
  return b?.violations ?? [];
}

export function baselineFingerprintSet(state: AppState): Set<string> {
  return new Set(selectedBaselineViolations(state).map((v) => v.fingerprint));
}

export function currentFingerprintSet(state: AppState): Set<string> {
  return new Set(state.violations.map((v) => v.fingerprint));
}

export function activeViolations(state: AppState): Violation[] {
  if (state.baselineFilter === "fixed" && state.selectedBaselineId) {
    const currentFps = currentFingerprintSet(state);
    return selectedBaselineViolations(state).filter((v) => !currentFps.has(v.fingerprint));
  }

  const ex = excludedFingerprintSet(state.exclusions);
  let vs = baseViolations(state).filter((v) => !ex.has(v.fingerprint));
  if (state.uncommittedFilterActive && state.uncommittedAvailable) {
    vs = vs.filter((v) => {
      const qnMatch = state.uncommittedQualifiedNames.size > 0
        && state.uncommittedQualifiedNames.has(v.documentQualifiedName.toLowerCase());
      const idMatch = !!v.documentId && state.uncommittedDocumentIds.has(v.documentId.toLowerCase());
      return qnMatch || idMatch;
    });
  }
  if (state.baselineFilter === "new" && state.selectedBaselineId) {
    const baselineFps = baselineFingerprintSet(state);
    vs = vs.filter((v) => !baselineFps.has(v.fingerprint));
  }
  return vs;
}

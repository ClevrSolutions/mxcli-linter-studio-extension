import { GENERIC_CATEGORY_FALLBACK, GENERIC_PREFIX_FALLBACK, MXCLI_CATEGORY_TO_LINT } from "../constants";
import type { Violation } from "../types";
import { originBadge, originOf, isSystemModule, isAppStoreModule } from "./origins";
import { openManualCheckViolations } from "./manualChecks";
import { excludedFingerprintSet } from "./exclusions";
import type { AppState } from "../context/AppReducer";

export function displayCategory(v: Violation, ruleCategories: Record<string, string>): string {
  if (v.kind === "manual") return v.category;
  const mxcliCat = (ruleCategories[v.ruleId] ?? "").toLowerCase();
  if (mxcliCat && MXCLI_CATEGORY_TO_LINT[mxcliCat]) return MXCLI_CATEGORY_TO_LINT[mxcliCat]!;
  return GENERIC_PREFIX_FALLBACK[v.category] ?? GENERIC_CATEGORY_FALLBACK;
}

export function matches(v: Violation, q: string, ruleNames: Record<string, string>, ruleCategories: Record<string, string>): boolean {
  if (!q) return true;
  return [
    v.ruleId, ruleNames[v.ruleId], v.category,
    displayCategory(v, ruleCategories), v.severity, originBadge(v),
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
  ruleNames: Record<string, string>,
  ruleCategories: Record<string, string>,
): boolean {
  return (
    (categoryEnabled.size === 0 || categoryEnabled.has(displayCategory(v, ruleCategories))) &&
    (severityEnabled.size === 0 || severityEnabled.has(v.severity)) &&
    matches(v, q, ruleNames, ruleCategories)
  );
}

export function allDisplayViolations(state: AppState): Violation[] {
  return state.violations.concat(openManualCheckViolations(state.manualAnswers));
}

export function baseViolations(state: AppState): Violation[] {
  return allDisplayViolations(state)
    .filter((v) => !isSystemModule(v))
    .filter((v) => state.appStoreVisible || !isAppStoreModule(v, state.appStoreModules));
}

export function activeViolations(state: AppState): Violation[] {
  const ex = excludedFingerprintSet(state.exclusions);
  let vs = baseViolations(state).filter((v) => !ex.has(v.fingerprint));
  if (state.uncommittedFilterActive && state.uncommittedAvailable) {
    vs = vs.filter((v) => !v.documentId || state.uncommittedDocumentIds.has(v.documentId));
  }
  return vs;
}

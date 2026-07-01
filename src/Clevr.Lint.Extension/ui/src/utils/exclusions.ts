import type { Exclusion, ExcludedView, Violation } from "../types";
import { baseViolations } from "./filters";
import type { AppState } from "../context/AppReducer";

export function excludedFingerprintSet(exclusions: Exclusion[]): Set<string> {
  return new Set(exclusions.map((e) => e.fingerprint));
}

export function sharedFingerprintCount(state: AppState, v: Violation): number {
  return baseViolations(state).filter((x) => x.fingerprint === v.fingerprint).length;
}

export function excludedView(state: AppState): ExcludedView {
  const base = baseViolations(state);
  const byFp = new Map<string, Violation[]>();
  for (const v of base) {
    if (!byFp.has(v.fingerprint)) byFp.set(v.fingerprint, []);
    byFp.get(v.fingerprint)!.push(v);
  }
  const ruleMap = new Map<string, { ruleId: string; entries: ExcludedView["groups"][number]["entries"]; findingCount: number; staleEntries: number }>();
  let matchedCount = 0;
  let staleCount = 0;
  for (const e of state.config.exclusions) {
    const vs = byFp.get(e.fingerprint) ?? [];
    const isStale = vs.length === 0;
    if (isStale) staleCount++; else matchedCount += vs.length;
    if (!ruleMap.has(e.ruleId)) {
      ruleMap.set(e.ruleId, { ruleId: e.ruleId, entries: [], findingCount: 0, staleEntries: 0 });
    }
    const g = ruleMap.get(e.ruleId)!;
    g.entries.push({ exclusion: e, violations: vs, isStale });
    g.findingCount += vs.length;
    if (isStale) g.staleEntries++;
  }
  const groups = [...ruleMap.values()].sort((a, b) => a.ruleId.localeCompare(b.ruleId));
  return { groups, matchedCount, staleCount };
}

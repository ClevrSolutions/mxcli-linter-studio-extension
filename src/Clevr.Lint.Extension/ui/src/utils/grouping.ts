import { SEVERITY_ORDER } from "../constants";
import type { RuleGroup, Violation } from "../types";
import { activeViolations } from "./filters";
import type { AppState } from "../context/AppReducer";

export function groupByRule(items: Violation[]): RuleGroup[] {
  const map = new Map<string, RuleGroup>();
  for (const v of items) {
    if (!map.has(v.ruleId)) map.set(v.ruleId, { rule: v, items: [] });
    map.get(v.ruleId)!.items.push(v);
  }
  return [...map.values()];
}

export function severityUniverse(state: AppState): string[] {
  const present = new Set<string>();
  for (const v of activeViolations(state)) present.add(v.severity);
  return [
    ...SEVERITY_ORDER.filter((s) => present.has(s)),
    ...[...present].filter((s) => !SEVERITY_ORDER.includes(s)).sort(),
  ];
}

export function previewText(text: string | undefined, max = 60): string {
  if (!text) return "";
  const t = text.trim();
  return t.length <= max ? t : t.slice(0, max).trimEnd() + "…";
}

export function ruleName(rule: Violation, ruleNames: Record<string, string>): string {
  return ruleNames[rule.ruleId] ?? "";
}

export function ruleLabelFor(rule: Violation, ruleNames: Record<string, string>): string {
  const name = ruleName(rule, ruleNames);
  return name ? `${rule.ruleId} (${name})` : rule.ruleId;
}

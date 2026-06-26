import { MANUAL_CHECK_EXPIRY_DAYS, MANUAL_CHECKS } from "../constants";
import type { ManualAnswer, ManualCheckDef, ManualCheckState, Violation } from "../types";

export function daysSince(dateStr: string | undefined): number {
  if (!dateStr) return Infinity;
  const then = new Date(dateStr + "T00:00:00");
  if (isNaN(then.getTime())) return Infinity;
  return Math.floor((Date.now() - then.getTime()) / 86400000);
}

export function addDays(dateStr: string, n: number): string {
  const d = new Date(dateStr + "T00:00:00");
  if (isNaN(d.getTime())) return dateStr;
  d.setDate(d.getDate() + n);
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
}

export function manualCheckState(def: ManualCheckDef, answers: ManualAnswer[]): ManualCheckState {
  const answer = answers.find((a) => a.id === def.id) ?? null;
  if (!answer) return { def, answer: null, status: "unanswered", open: true };
  if ((answer.answer ?? "").toLowerCase() === "no") return { def, answer, status: "no", open: true };
  const days = daysSince(answer.date);
  const recheckDate = addDays(answer.date, MANUAL_CHECK_EXPIRY_DAYS);
  if (days >= MANUAL_CHECK_EXPIRY_DAYS) {
    return { def, answer, status: "expired", open: true, recheckDate, daysOverdue: days - MANUAL_CHECK_EXPIRY_DAYS };
  }
  return { def, answer, status: "valid-yes", open: false, recheckDate, recheckInDays: MANUAL_CHECK_EXPIRY_DAYS - days };
}

export function manualChecksAll(answers: ManualAnswer[]): ManualCheckState[] {
  return MANUAL_CHECKS.map((def) => manualCheckState(def, answers));
}

export function answeredManualChecks(answers: ManualAnswer[]): ManualCheckState[] {
  return manualChecksAll(answers).filter((s) => !s.open);
}

export function manualStateLabel(s: ManualCheckState): string {
  if (s.status === "unanswered") return "Unanswered";
  if (s.status === "no") {
    const by = s.answer?.excludedBy ?? s.answer?.answeredBy ?? "?";
    return `Answered "No" on ${s.answer?.date} by ${by} — still open`;
  }
  if (s.status === "expired") return `Answered "Yes" on ${s.answer?.date} — recheck overdue since ${s.recheckDate}`;
  return `Answered "Yes" on ${s.answer?.date} — recheck due ${s.recheckDate}`;
}

export function manualCheckViolation(s: ManualCheckState): Violation {
  return {
    ruleId: s.def.id,
    kind: "manual",
    category: s.def.category,
    severity: s.def.severity,
    documentType: "Manual check",
    documentQualifiedName: s.def.id,
    elementName: "",
    reason: s.def.question,
    suggestion: s.def.context,
    fingerprint: "manual:" + s.def.id,
    manual: s,
  };
}

export function openManualCheckViolations(answers: ManualAnswer[]): Violation[] {
  return manualChecksAll(answers)
    .filter((s) => s.open)
    .map(manualCheckViolation);
}

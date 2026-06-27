import type { Violation } from "../types";

export function moduleOf(v: Violation): string {
  const qn = v.documentQualifiedName || "";
  const dot = qn.indexOf(".");
  return dot >= 0 ? qn.slice(0, dot) : qn;
}

export function isAppStoreModule(v: Violation, appStoreModules: Set<string>): boolean {
  return appStoreModules.has(moduleOf(v));
}

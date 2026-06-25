import type { Violation } from "../types";

export function originLabel(v: Violation): string {
  if (v.kind === "manual") return "Manual checks";
  if (v.kind === "acr") return "ACR (calibrated)";
  return "MxCLI";
}

export function originBadge(v: Violation): string {
  if (v.kind === "manual") return "Manual";
  if (v.kind === "acr") return "ACR";
  return "MxCLI";
}

export function originOf(v: Violation): "acr" | "mxcli" | "manual" {
  if (v.kind === "manual") return "manual";
  if (v.kind === "acr") return "acr";
  return "mxcli";
}

export function moduleOf(v: Violation): string {
  const qn = v.documentQualifiedName || "";
  const dot = qn.indexOf(".");
  return dot >= 0 ? qn.slice(0, dot) : qn;
}

export function isSystemModule(v: Violation): boolean {
  const qn = v.documentQualifiedName || "";
  return qn === "System" || qn.startsWith("System.");
}

export function isAppStoreModule(v: Violation, appStoreModules: Set<string>): boolean {
  return appStoreModules.has(moduleOf(v));
}

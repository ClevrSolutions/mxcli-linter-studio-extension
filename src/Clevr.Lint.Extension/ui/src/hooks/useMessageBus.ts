import { Dispatch, useEffect } from "react";
import type { AppAction, ScanDescribePayload, ScanFastPayload } from "../context/AppReducer";
import type { Exclusion, LinterConfigRule, ModuleInfo } from "../types";

export function post(message: string, data?: unknown): void {
  window.chrome?.webview?.postMessage({ message, data });
}

function handleMxcliResult(data: unknown, dispatch: Dispatch<AppAction>): void {
  let payload: (ScanFastPayload & { phase?: string; final?: boolean }) | null;
  try {
    payload = typeof data === "string" ? JSON.parse(data) : (data as typeof payload);
  } catch (e) {
    dispatch({ type: "SHOW_TOAST", text: "Could not parse mxcli payload: " + String(e), isError: true });
    return;
  }
  if (!payload || payload.ok === false) {
    dispatch({ type: "SCAN_ERROR", error: payload?.error ?? "mxcli scan failed (unknown error)." });
    return;
  }
  const phase = payload.phase ?? "fast";
  if (phase === "describe") {
    dispatch({ type: "SCAN_DESCRIBE_BATCH", payload: payload as ScanDescribePayload });
  } else {
    dispatch({ type: "SCAN_FAST_BATCH", payload });
  }
}

function handleExclusions(data: unknown, dispatch: Dispatch<AppAction>): void {
  let payload: unknown;
  try {
    payload = typeof data === "string" ? JSON.parse(data) : data;
  } catch (e) {
    dispatch({ type: "SHOW_TOAST", text: "Could not parse exclusions: " + String(e), isError: true });
    return;
  }
  const exclusions: Exclusion[] = Array.isArray(payload)
    ? (payload as Exclusion[])
    : ((payload as { exclusions?: Exclusion[] })?.exclusions ?? []);
  dispatch({ type: "SET_EXCLUSIONS", exclusions });
}

function handleLinterConfig(data: unknown, dispatch: Dispatch<AppAction>): void {
  let payload: { rules?: Record<string, LinterConfigRule>; excludedModules?: string[] } | null;
  try {
    payload = typeof data === "string" ? JSON.parse(data) : (data as typeof payload);
  } catch (e) { dispatch({ type: "SHOW_TOAST", text: "Could not parse linter config: " + String(e), isError: true }); return; }
  if (!payload) return;
  dispatch({ type: "SET_LINTER_CONFIG", config: payload.rules ?? {}, excludedModules: payload.excludedModules ?? [] });
}

function handleUncommittedDocuments(data: unknown, dispatch: Dispatch<AppAction>): void {
  let payload: { documentIds?: string[]; available?: boolean } | null;
  try {
    payload = typeof data === "string" ? JSON.parse(data) : (data as typeof payload);
  } catch (e) { dispatch({ type: "SHOW_TOAST", text: "Could not parse uncommitted documents: " + String(e), isError: true }); return; }
  if (!payload) return;
  dispatch({
    type: "SET_UNCOMMITTED_DOCUMENTS",
    documentIds: payload.documentIds ?? [],
    available: payload.available ?? false,
  });
}

function handleModules(data: unknown, dispatch: Dispatch<AppAction>): void {
  let payload: { modules?: Array<ModuleInfo | string> } | null;
  try {
    payload = typeof data === "string" ? JSON.parse(data) : (data as typeof payload);
  } catch (e) { dispatch({ type: "SHOW_TOAST", text: "Could not parse modules: " + String(e), isError: true }); return; }
  if (!payload) return;
  const modules: ModuleInfo[] = (payload.modules ?? []).map((m) =>
    typeof m === "string"
      ? { name: m, fromMarketplace: false, appStoreVersion: null }
      : m
  );
  dispatch({ type: "SET_MODULES", modules });
}

function handleRulesCatalog(data: unknown, dispatch: Dispatch<AppAction>): void {
  let payload: { ruleNames?: Record<string, string>; ruleCategories?: Record<string, string> } | null;
  try {
    payload = typeof data === "string" ? JSON.parse(data) : (data as typeof payload);
  } catch (e) { dispatch({ type: "SHOW_TOAST", text: "Could not parse rules catalog: " + String(e), isError: true }); return; }
  if (!payload) return;
  dispatch({
    type: "SET_RULES_CATALOG",
    ruleNames: payload.ruleNames ?? {},
    ruleCategories: payload.ruleCategories ?? {},
  });
}

export function useMessageBus(dispatch: Dispatch<AppAction>): void {
  useEffect(() => {
    function handleMessage(event: MessageEvent<{ message: string; data: unknown }>): void {
      const { message, data } = event.data;
      switch (message) {
        case "LintViolations":       return handleMxcliResult(data, dispatch);
        case "ScanProgress":        return dispatch({ type: "SHOW_TOAST", text: String(data), isError: false });
        case "ScanError":           return dispatch({ type: "SHOW_TOAST", text: "Scan failed: " + String(data), isError: true });
        case "ScanFinished":        return dispatch({ type: "SCAN_FINISHED", completedAt: Date.now() });
        case "ReportSaved":         return dispatch({ type: "SHOW_TOAST", text: "Report saved (and opened): " + String(data), isError: false });
        case "ReportError":         return dispatch({ type: "SHOW_TOAST", text: "Report export failed: " + String(data), isError: true });
        case "DocumentOpened":      return dispatch({ type: "SHOW_TOAST", text: "Opened in Studio Pro: " + String(data), isError: false });
        case "DocumentOpenError":   return dispatch({ type: "SHOW_TOAST", text: "Could not open document: " + String(data), isError: true });
        case "UrlOpened":           return dispatch({ type: "SHOW_TOAST", text: "Documentation opened: " + String(data), isError: false });
        case "UrlError":            return dispatch({ type: "SHOW_TOAST", text: "Could not open documentation link: " + String(data), isError: true });
        case "Exclusions":          return handleExclusions(data, dispatch);
        case "ExclusionError":      return dispatch({ type: "SHOW_TOAST", text: "Exclusion failed: " + String(data), isError: true });
        case "RulesCatalog":        return handleRulesCatalog(data, dispatch);
        case "LinterConfig":        return handleLinterConfig(data, dispatch);
        case "LinterConfigSaved":   return dispatch({ type: "SHOW_TOAST", text: "Settings saved", isError: false });
        case "LinterConfigError":   return dispatch({ type: "SHOW_TOAST", text: "Settings error: " + String(data), isError: true });
        case "Modules":             return handleModules(data, dispatch);
        case "UncommittedDocuments": return handleUncommittedDocuments(data, dispatch);
        case "ModulesError":        return dispatch({ type: "SHOW_TOAST", text: "Could not load modules: " + String(data), isError: true });
      }
    }

    window.chrome?.webview?.addEventListener("message", handleMessage);
    post("MessageListenerRegistered");
    post("RequestExclusions");
    post("RequestRulesCatalog");
    post("RequestLinterConfig");

    return () => window.chrome?.webview?.removeEventListener("message", handleMessage);
  }, [dispatch]);
}

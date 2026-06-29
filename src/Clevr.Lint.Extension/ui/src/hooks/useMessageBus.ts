import { Dispatch, useEffect } from "react";
import type { AppAction, ScanDescribePayload, ScanFastPayload } from "../context/AppReducer";
import type { BaselineEntry, Exclusion, LinterConfigRule, ModuleInfo, MxcliInfo, RuleSource, RuleSourceFetchStatus } from "../types";

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

function handleBaselinesLoaded(data: unknown, dispatch: Dispatch<AppAction>): void {
  let list: BaselineEntry[] | null;
  try {
    list = typeof data === "string" ? JSON.parse(data) : (data as BaselineEntry[] | null);
  } catch (e) {
    dispatch({ type: "SHOW_TOAST", text: "Could not parse baselines: " + String(e), isError: true });
    return;
  }
  dispatch({ type: "SET_BASELINES", baselines: Array.isArray(list) ? list : [] });
}

function handleMxcliInfo(data: unknown, dispatch: Dispatch<AppAction>): void {
  let payload: MxcliInfo | null;
  try {
    payload = typeof data === "string" ? JSON.parse(data) : (data as MxcliInfo | null);
  } catch (e) { dispatch({ type: "SHOW_TOAST", text: "Could not parse mxcli info: " + String(e), isError: true }); return; }
  if (!payload) return;
  dispatch({ type: "SET_MXCLI_INFO", info: payload });
}

function handleRuleSources(data: unknown, dispatch: Dispatch<AppAction>): void {
  let list: RuleSource[];
  try {
    list = typeof data === "string" ? JSON.parse(data) : (data as RuleSource[]);
    if (!Array.isArray(list)) list = [];
  } catch (e) {
    dispatch({ type: "SHOW_TOAST", text: "Could not parse rule sources: " + String(e), isError: true });
    return;
  }
  dispatch({ type: "SET_RULE_SOURCES", sources: list });
}

function handleRuleSourceFetched(data: unknown, dispatch: Dispatch<AppAction>): void {
  let payload: { id?: string; copied?: number; skipped?: number; failed?: number; errors?: string[] } | null;
  try {
    payload = typeof data === "string" ? JSON.parse(data) : (data as typeof payload);
  } catch { return; }
  if (!payload?.id) return;
  const status: RuleSourceFetchStatus = {
    fetching: false,
    lastResult: {
      copied: payload.copied ?? 0,
      skipped: payload.skipped ?? 0,
      failed: payload.failed ?? 0,
      errors: payload.errors ?? [],
    },
  };
  dispatch({ type: "SET_RULE_SOURCE_FETCH_STATUS", id: payload.id, status });
}

function handleRuleSourceFilesDeleted(data: unknown, dispatch: Dispatch<AppAction>): void {
  let payload: { id?: string; deleted?: number; notFound?: number; failed?: number; errors?: string[] } | null;
  try {
    payload = typeof data === "string" ? JSON.parse(data) : (data as typeof payload);
  } catch { return; }
  if (!payload?.id) return;
  dispatch({
    type: "SET_RULE_SOURCE_FETCH_STATUS",
    id: payload.id,
    status: {
      fetching: false,
      lastDeleteResult: {
        deleted: payload.deleted ?? 0,
        notFound: payload.notFound ?? 0,
        failed: payload.failed ?? 0,
        errors: payload.errors ?? [],
      },
    },
  });
}

function handleRuleSourceFetchError(data: unknown, dispatch: Dispatch<AppAction>): void {
  let payload: { id?: string; error?: string } | null;
  try {
    payload = typeof data === "string" ? JSON.parse(data) : (data as typeof payload);
  } catch { return; }
  if (!payload?.id) return;
  dispatch({
    type: "SET_RULE_SOURCE_FETCH_STATUS",
    id: payload.id,
    status: { fetching: false, error: payload.error ?? "Fetch failed" },
  });
}

function handleRuleSourceFetchProgress(data: unknown, dispatch: Dispatch<AppAction>): void {
  let payload: { id?: string; message?: string } | null;
  try {
    payload = typeof data === "string" ? JSON.parse(data) : (data as typeof payload);
  } catch { return; }
  if (!payload?.id) return;
  dispatch({
    type: "SET_RULE_SOURCE_FETCH_STATUS",
    id: payload.id,
    status: { fetching: true, progress: payload.message },
  });
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
        case "BaselinesLoaded":          return handleBaselinesLoaded(data, dispatch);
        case "BaselineError":            return dispatch({ type: "SHOW_TOAST", text: "Baseline error: " + String(data), isError: true });
        case "MxcliInfo":                return handleMxcliInfo(data, dispatch);
        case "MxcliDownloadProgress":    return dispatch({ type: "MXCLI_DOWNLOAD_PROGRESS", percent: Number(data) });
        case "MxcliDownloadError":       {
          dispatch({ type: "SET_MXCLI_INFO", info: { source: "notFound", resolvedPath: null, version: null, found: false, downloadedAt: null } });
          dispatch({ type: "SHOW_TOAST", text: "mxcli download failed: " + String(data), isError: true });
          return;
        }
        case "MxcliPathError":           return dispatch({ type: "SHOW_TOAST", text: "Could not apply mxcli path: " + String(data), isError: true });
        case "RuleSources":              return handleRuleSources(data, dispatch);
        case "RuleSourcesError":         return dispatch({ type: "SHOW_TOAST", text: "Rule sources error: " + String(data), isError: true });
        case "RuleSourceFetchStarted":   {
          try {
            const p = typeof data === "string" ? JSON.parse(data) as { id?: string } : data as { id?: string };
            if (p?.id) dispatch({ type: "SET_RULE_SOURCE_FETCH_STATUS", id: p.id, status: { fetching: true } });
          } catch { /* ignore malformed payload */ }
          return;
        }
        case "RuleSourceFetchProgress":  return handleRuleSourceFetchProgress(data, dispatch);
        case "RuleSourceFetched":        return handleRuleSourceFetched(data, dispatch);
        case "RuleSourceFilesDeleted":   return handleRuleSourceFilesDeleted(data, dispatch);
        case "RuleSourceFetchError":     return handleRuleSourceFetchError(data, dispatch);
      }
    }

    window.chrome?.webview?.addEventListener("message", handleMessage);
    post("MessageListenerRegistered");
    post("RequestExclusions");
    post("RequestBaselines");
    post("RequestRulesCatalog");
    post("RequestLinterConfig");
    post("RequestMxcliInfo");
    post("RequestRuleSources");

    return () => window.chrome?.webview?.removeEventListener("message", handleMessage);
  }, [dispatch]);
}

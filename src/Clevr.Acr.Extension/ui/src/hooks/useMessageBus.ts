import { Dispatch, useEffect } from "react";
import type { AppAction, ScanDescribePayload, ScanFastPayload } from "../context/AppReducer";
import type { Exclusion, ManualAnswer } from "../types";

export function post(message: string, data?: unknown): void {
  window.chrome.webview.postMessage({ message, data });
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

function handleManualChecks(data: unknown, dispatch: Dispatch<AppAction>): void {
  let payload: unknown;
  try {
    payload = typeof data === "string" ? JSON.parse(data) : data;
  } catch (e) {
    dispatch({ type: "SHOW_TOAST", text: "Could not parse manual checks: " + String(e), isError: true });
    return;
  }
  const answers: ManualAnswer[] = Array.isArray(payload)
    ? (payload as ManualAnswer[])
    : ((payload as { answers?: ManualAnswer[] })?.answers ?? []);
  dispatch({ type: "SET_MANUAL_ANSWERS", answers });
}

export function useMessageBus(dispatch: Dispatch<AppAction>): void {
  useEffect(() => {
    function handleMessage(event: MessageEvent<{ message: string; data: unknown }>): void {
      const { message, data } = event.data;
      switch (message) {
        case "AcrViolations":       return handleMxcliResult(data, dispatch);
        case "ScanProgress":        return dispatch({ type: "SHOW_TOAST", text: String(data), isError: false });
        case "ScanError":           return dispatch({ type: "SHOW_TOAST", text: "Scan failed: " + String(data), isError: true });
        case "ScanFinished":        return dispatch({ type: "SCAN_FINISHED" });
        case "ReportSaved":         return dispatch({ type: "SHOW_TOAST", text: "Report saved (and opened): " + String(data), isError: false });
        case "ReportError":         return dispatch({ type: "SHOW_TOAST", text: "Report export failed: " + String(data), isError: true });
        case "DocumentOpened":      return dispatch({ type: "SHOW_TOAST", text: "Opened in Studio Pro: " + String(data), isError: false });
        case "DocumentOpenError":   return dispatch({ type: "SHOW_TOAST", text: "Could not open document: " + String(data), isError: true });
        case "UrlOpened":           return dispatch({ type: "SHOW_TOAST", text: "Documentation opened: " + String(data), isError: false });
        case "UrlError":            return dispatch({ type: "SHOW_TOAST", text: "Could not open documentation link: " + String(data), isError: true });
        case "Exclusions":          return handleExclusions(data, dispatch);
        case "ExclusionError":      return dispatch({ type: "SHOW_TOAST", text: "Exclusion failed: " + String(data), isError: true });
        case "ManualCheckAnswers":  return handleManualChecks(data, dispatch);
        case "ManualCheckError":    return dispatch({ type: "SHOW_TOAST", text: "Manual check failed: " + String(data), isError: true });
      }
    }

    window.chrome.webview.addEventListener("message", handleMessage);
    post("MessageListenerRegistered");
    post("RequestExclusions");
    post("RequestManualChecks");

    return () => window.chrome.webview.removeEventListener("message", handleMessage);
  }, [dispatch]);
}

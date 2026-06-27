import { useAppDispatch, useAppState } from "../context/AppContext";
import { activeViolations } from "../utils/filters";
import { excludedView } from "../utils/exclusions";
import { post } from "../hooks/useMessageBus";
import { buildReportHtml } from "../utils/report";

function relativeTime(ts: number): string {
  const sec = Math.floor((Date.now() - ts) / 1000);
  if (sec < 5) return "just now";
  if (sec < 60) return `${sec}s ago`;
  if (sec < 3600) return `${Math.floor(sec / 60)}m ago`;
  return `${Math.floor(sec / 3600)}h ago`;
}

export function Toolbar() {
  const state = useAppState();
  const dispatch = useAppDispatch();

  const hasAnything =
    activeViolations(state).length > 0 ||
    excludedView(state).groups.length > 0;

  function startScan() {
    dispatch({ type: "SCAN_FAST_BATCH", payload: { violations: [], final: false } });
    post("RequestExclusions");
    post("RunFullScan");
  }

  function cancelScan() {
    post("CancelScan");
  }

  function exportReport() {
    if (!hasAnything) return;
    const html = buildReportHtml(state);
    post("ExportHtml", { html });
  }

  if (state.settingsVisible) return null;

  const statusText = state.scanStreaming
    ? (state.scanProgress ? `${state.scanProgress.label}` : "Analyzing…")
    : state.scanHasRun
      ? `${state.violations.length} improvement${state.violations.length !== 1 ? "s" : ""} · ${relativeTime(state.scanCompletedAt!)}`
      : "No scan yet";

  return (
    <div className="lint-toolbar">
      <span className="lint-scan-status" title={state.scanStreaming ? "Scan in progress" : state.scanHasRun ? "Last scan result" : "Run a scan to see improvements"}>
        {statusText}
      </span>
      <button
        type="button"
        disabled={state.scanStreaming}
        title="Scan: catalog rules and mxcli's own lint rules."
        onClick={() => startScan()}
      >
        Scan
      </button>
      {state.scanStreaming && (
        <>
          <span className="lint-spinner" aria-label="Scanning…" />
          <button
            type="button"
            className="lint-cancel-btn"
            title="Cancel the running scan"
            onClick={cancelScan}
          >
            Cancel
          </button>
        </>
      )}
      {hasAnything && !state.scanStreaming && (
        <button type="button" onClick={exportReport}>
          Export
        </button>
      )}
      <button
        type="button"
        disabled={state.scanStreaming}
        title="Rule settings — enable/disable rules and override severity"
        className="lint-toolbar-settings"
        onClick={() => dispatch({ type: "SHOW_SETTINGS" })}
      >
        ⚙ Settings
      </button>
    </div>
  );
}

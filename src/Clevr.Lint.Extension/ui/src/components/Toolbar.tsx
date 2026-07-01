import { useAppDispatch, useAppState } from "../context/AppContext";
import { activeViolations } from "../utils/filters";
import { excludedView } from "../utils/exclusions";
import { post } from "../hooks/useMessageBus";
import { buildReportHtml } from "../utils/report";
import { relativeTime } from "../utils/time";
import { btnPrimary, btnSecondary } from "../utils/classes";

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

  if (state.ui.settingsVisible) return null;

  const statusText = state.scan.scanStreaming
    ? (state.scan.scanProgress ? `${state.scan.scanProgress.label}` : "Analyzing…")
    : state.scan.scanHasRun
      ? `${state.scan.violations.length} improvement${state.scan.violations.length !== 1 ? "s" : ""} · ${relativeTime(state.scan.scanCompletedAt!)}`
      : "No scan yet";

  return (
    <div className="flex gap-2 items-center my-2 mb-[14px] flex-wrap">
      <span
        className="text-[12px] text-clevr-muted mr-1"
        title={state.scan.scanStreaming ? "Scan in progress" : state.scan.scanHasRun ? "Last scan result" : "Run a scan to see improvements"}
      >
        {statusText}
      </span>
      {state.scan.scanStreaming && (
        <>
          <span className="lint-spinner" aria-label="Scanning…" />
          <button
            type="button"
            className="px-3 py-1.5 text-[12px] font-medium bg-white text-sev-critical border border-sev-critical rounded-[6px] cursor-pointer hover:bg-[#fff0f0] disabled:opacity-45 disabled:cursor-not-allowed"
            title="Cancel the running scan"
            onClick={cancelScan}
          >
            Cancel
          </button>
        </>
      )}
      <button
        type="button"
        className={btnPrimary}
        disabled={state.scan.scanStreaming}
        title="Scan: catalog rules and mxcli's own lint rules."
        onClick={() => startScan()}
      >
        Scan
      </button>
      {hasAnything && !state.scan.scanStreaming && (
        <button type="button" className={btnSecondary} onClick={exportReport}>
          Export
        </button>
      )}
      {state.scan.scanHasRun && !state.scan.scanStreaming && (
        <button
          type="button"
          className={btnSecondary}
          title="Save the current scan result as a baseline. Future scans highlight only new or fixed violations."
          onClick={() => post("SaveBaseline", { violations: state.scan.violations, savedAt: Date.now() })}
        >
          Save Baseline
        </button>
      )}
      <span className="ml-auto" />
      <button
        type="button"
        className={btnSecondary}
        disabled={state.scan.scanStreaming}
        title="Rule settings — enable/disable rules and override severity"
        onClick={() => dispatch({ type: "SHOW_SETTINGS" })}
      >
        ⚙ Settings
      </button>
    </div>
  );
}

import { useAppDispatch, useAppState } from "../context/AppContext";
import { activeViolations } from "../utils/filters";
import { answeredManualChecks } from "../utils/manualChecks";
import { excludedView } from "../utils/exclusions";
import { post } from "../hooks/useMessageBus";
import { buildReportHtml } from "../utils/report";

export function Toolbar() {
  const state = useAppState();
  const dispatch = useAppDispatch();

  const hasAnything =
    activeViolations(state).length > 0 ||
    answeredManualChecks(state.manualAnswers).length > 0 ||
    excludedView(state).groups.length > 0;

  function startScan(deep: boolean) {
    dispatch({ type: "SCAN_FAST_BATCH", payload: { violations: [], final: false, deepScan: deep } });
    post("RequestExclusions");
    post("RequestManualChecks");
    post(deep ? "RunDeepScan" : "RunFullScan");
    dispatch({
      type: "SHOW_TOAST",
      text: deep
        ? "Deep analysis — scanning all microflows & entities. This can take ~3 minutes…"
        : "Starting scan… (this can take up to a minute)",
      isError: false,
    });
  }

  function exportReport() {
    if (!hasAnything) return;
    const html = buildReportHtml(state);
    post("ExportHtml", { html });
  }

  return (
    <div className="acr-toolbar">
      <button
        type="button"
        disabled={state.scanStreaming}
        title="Quick scan: catalog rules, mxcli's own lint rules and manual checks — runs in seconds."
        onClick={() => startScan(false)}
      >
        Scan
      </button>
      <button
        type="button"
        disabled={state.scanStreaming}
        title="Deep analysis: everything in a quick scan PLUS the microflow complexity, nested-if, empty-string and default-ReadWrite rules."
        onClick={() => startScan(true)}
      >
        Deepscan
      </button>
      {state.scanStreaming && <span className="acr-spinner" aria-label="Scanning…" />}
      {hasAnything && (
        <button type="button" onClick={exportReport}>
          Export report to HTML
        </button>
      )}
    </div>
  );
}

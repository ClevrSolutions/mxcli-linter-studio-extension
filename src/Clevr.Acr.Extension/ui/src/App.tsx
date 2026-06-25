import { useReducer, useEffect } from "react";
import { AppContext, AppDispatch } from "./context/AppContext";
import { appReducer, initialState } from "./context/AppReducer";
import { useMessageBus } from "./hooks/useMessageBus";
import { Toolbar } from "./components/Toolbar";
import { FilterBar } from "./components/FilterBar";
import { Report } from "./components/Report";
import { Toast } from "./components/Toast";
import { activeViolations } from "./utils/filters";
import { answeredManualChecks } from "./utils/manualChecks";
import { excludedView } from "./utils/exclusions";

export function App() {
  const [state, dispatch] = useReducer(appReducer, initialState);

  useMessageBus(dispatch);

  useEffect(() => {
    async function loadLogo() {
      try {
        const res = await fetch("./clevr-logo.png");
        if (!res.ok) return;
        const blob = await res.blob();
        const dataUri = await new Promise<string>((resolve, reject) => {
          const r = new FileReader();
          r.onloadend = () => resolve(r.result as string);
          r.onerror = reject;
          r.readAsDataURL(blob);
        });
        dispatch({ type: "SET_LOGO", dataUri });
      } catch {
        // logo is cosmetic
      }
    }
    void loadLogo();
  }, []);

  const hasAnything =
    activeViolations(state).length > 0 ||
    answeredManualChecks(state.manualAnswers).length > 0 ||
    excludedView(state).groups.length > 0;

  const statusText = (() => {
    if (state.scanStreaming) {
      const lbl = state.scanProgress ? ` — ${state.scanProgress.label}` : "";
      return `Scanning…${lbl} · ${activeViolations(state).length} improvements so far (partial)`;
    }
    if (state.violations.length === 0 && !hasAnything) return "No scan run yet.";
    const base = activeViolations(state);
    const oc = { acr: 0, mxcli: 0, manual: 0 };
    for (const v of base) { const k = v.kind === "acr" ? "acr" : v.kind === "manual" ? "manual" : "mxcli"; oc[k]++; }
    const ev = excludedView(state);
    const exNote = ev.matchedCount ? ` · ${ev.matchedCount} excluded` : "";
    return `${base.length} improvements (${oc.acr} ACR / ${oc.mxcli} MxCLI / ${oc.manual} Manual)${exNote}`;
  })();

  return (
    <AppContext.Provider value={state}>
      <AppDispatch.Provider value={dispatch}>
        <div className="acr-root">
          <div className="acr-header">
            <div className="acr-brand">
              <img className="acr-logo" src="./clevr-logo.png" alt="CLEVR" />
              <div>
                <h1 className="acr-title">CLEVR ACR Review</h1>
                <div className="acr-subtitle">Improvements from the project, via mxcli + the CLEVR rules</div>
              </div>
            </div>
          </div>

          <Toolbar />

          {hasAnything && (
            <>
              <input
                id="filter"
                className="acr-search"
                type="search"
                placeholder="Filter by rule, document, category, severity, reason…"
                value={state.filterQuery}
                onChange={(e) => dispatch({ type: "SET_FILTER_QUERY", query: e.target.value })}
                style={{ display: "block", width: "100%", marginBottom: 8 }}
              />
              <button
                type="button"
                title="Clear all filters (category, severity, source, text)"
                onClick={() => dispatch({ type: "RESET_FILTERS" })}
                style={{ marginBottom: 8 }}
              >
                Reset filters
              </button>
              <FilterBar />
            </>
          )}

          <div className="acr-status">{statusText}</div>
          <Report />

          <div className="acr-footer">
            Requires Mendix Studio Pro 11 or higher (Extensibility API 11.10, .NET 10, mxcli). · CLEVR ACR
          </div>
        </div>
        <Toast />
      </AppDispatch.Provider>
    </AppContext.Provider>
  );
}

import { useReducer, useEffect } from "react";
import { AppContext, AppDispatch } from "./context/AppContext";
import { appReducer, initialState } from "./context/AppReducer";
import { useMessageBus } from "./hooks/useMessageBus";
import { Toolbar } from "./components/Toolbar";
import { FilterBar } from "./components/FilterBar";
import { Report } from "./components/Report";
import { Settings } from "./components/Settings";
import { Toast } from "./components/Toast";
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

  return (
    <AppContext.Provider value={state}>
      <AppDispatch.Provider value={dispatch}>
        <div className="lint-root">
          <div className="lint-header">
            <div className="lint-brand">
              <img className="lint-logo" src="./clevr-logo.png" alt="CLEVR" />
              <div>
                <h1 className="lint-title">CLEVR Lint Review</h1>
                <div className="lint-subtitle">Improvements to the project, with mxcli linting rules</div>
              </div>
            </div>
          </div>

          <Toolbar />

          {state.settingsVisible ? (
            <Settings />
          ) : (
            <>
              {state.scanHasRun && (
                <>
                  <div className="lint-search-row">
                    <input
                      id="filter"
                      className="lint-search"
                      type="search"
                      placeholder="Filter by rule, document, category, severity, reason…"
                      value={state.filterQuery}
                      onChange={(e) => dispatch({ type: "SET_FILTER_QUERY", query: e.target.value })}
                    />
                    <button
                      type="button"
                      className="lint-search-reset"
                      title="Clear all filters (category, severity, source, text)"
                      onClick={() => dispatch({ type: "RESET_FILTERS" })}
                      aria-label="Reset filters"
                    >
                      Reset Filters
                    </button>
                  </div>
                  <FilterBar />
                </>
              )}
              <Report />
            </>
          )}

          <div className="lint-footer">
            Requires Mendix Studio Pro 11 or higher (Extensibility API 11.10, .NET 10, mxcli). · CLEVR Lint
          </div>
        </div>
        <Toast />
      </AppDispatch.Provider>
    </AppContext.Provider>
  );
}

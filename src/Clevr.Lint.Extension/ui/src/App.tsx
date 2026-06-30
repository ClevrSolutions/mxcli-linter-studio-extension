import { useReducer, useEffect } from "react";
import { AppContext, AppDispatch } from "./context/AppContext";
import { appReducer, initialState } from "./context/AppReducer";
import { useMessageBus, post } from "./hooks/useMessageBus";
import { Toolbar } from "./components/Toolbar";
import { FilterBar } from "./components/FilterBar";
import { Report } from "./components/Report";
import { Settings } from "./components/Settings";
import { Toast } from "./components/Toast";
import { btnSecondary } from "./utils/classes";

export function App() {
  const [state, dispatch] = useReducer(appReducer, initialState);

  useMessageBus(dispatch);

  useEffect(() => {
    post("RequestModules");
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
        <div className="px-3.5 pb-8 pt-3">
          <div className="flex items-baseline justify-between gap-3 mb-2">
            <div className="flex items-center gap-3">
              <img className="h-10 w-auto rounded-[6px] block" src="./clevr-logo.png" alt="CLEVR" />
              <div>
                <h1 className="text-[15px] font-semibold m-0">CLEVR Lint Review</h1>
                <div className="text-[12px] text-clevr-muted">Improvements to the project, with mxcli linting rules</div>
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
                  <div className="flex gap-2 items-center my-2">
                    <input
                      id="filter"
                      className="flex-1 border border-clevr-border rounded px-2.5 py-1.5 text-[13px] outline-none focus:border-clevr-accent"
                      type="search"
                      placeholder="Filter by rule, document, category, severity, reason…"
                      value={state.filterQuery}
                      onChange={(e) => dispatch({ type: "SET_FILTER_QUERY", query: e.target.value })}
                    />
                    <button
                      type="button"
                      className={btnSecondary}
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
        </div>
        <Toast />
      </AppDispatch.Provider>
    </AppContext.Provider>
  );
}

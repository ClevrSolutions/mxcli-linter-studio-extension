import { useAppDispatch, useAppState } from "../context/AppContext";
import { activeViolations } from "../utils/filters";
import { isAppStoreModule } from "../utils/origins";
import { post } from "../hooks/useMessageBus";
import { relativeTime } from "../utils/time";

const segBase = "px-3 py-0.5 text-[12px] bg-white text-clevr-muted border-r border-clevr-border last:border-r-0 cursor-pointer hover:bg-clevr-hover";
const segActive = "bg-clevr-selected text-clevr-accent font-semibold";

export function FilterBar() {
  const state = useAppState();
  const dispatch = useAppDispatch();

  const base = activeViolations({ ...state, filters: { ...state.filters, baselineFilter: null } });
  const asCount = base.filter((v) => isAppStoreModule(v, state.scan.appStoreModules)).length;
  const changedCount = state.filters.uncommittedAvailable
    ? base.filter((v) => {
        const qnMatch = state.filters.uncommittedQualifiedNames.size > 0
          && state.filters.uncommittedQualifiedNames.has(v.documentQualifiedName.toLowerCase());
        const idMatch = !!v.documentId && state.filters.uncommittedDocumentIds.has(v.documentId.toLowerCase());
        return qnMatch || idMatch;
      }).length
    : 0;

  const hasBaseline = state.baseline.baselines.length > 0 && state.scan.scanHasRun;

  const newCount = hasBaseline ? activeViolations({ ...state, filters: { ...state.filters, baselineFilter: "new" } }).length : 0;
  const fixedCount = hasBaseline ? activeViolations({ ...state, filters: { ...state.filters, baselineFilter: "fixed" } }).length : 0;
  const allCount = base.length;

  const selectedBaseline = state.baseline.baselines.find((b) => b.id === state.baseline.selectedBaselineId);
  const savedAgo = selectedBaseline ? relativeTime(new Date(selectedBaseline.savedAt).getTime()) : "";

  if (asCount === 0 && !state.filters.uncommittedAvailable && !hasBaseline) return null;

  return (
    <div className="flex flex-wrap items-center gap-x-4 gap-y-2 mb-3 text-[12px]">
      {asCount > 0 && (
        <label
          className="flex items-center gap-1.5 cursor-pointer select-none"
          title="Marketplace/app-store modules are scanned too; toggle to show or hide them."
        >
          <input
            type="checkbox"
            checked={state.filters.appStoreVisible}
            onChange={() => dispatch({ type: "TOGGLE_APPSTORE" })}
          />
          <span>Marketplace modules ({asCount})</span>
        </label>
      )}
      {state.filters.uncommittedAvailable && (
        <label
          className="flex items-center gap-1.5 cursor-pointer select-none"
          title="Show only improvements in documents that have uncommitted Git changes."
        >
          <input
            type="checkbox"
            checked={state.filters.uncommittedFilterActive}
            onChange={() => dispatch({ type: "TOGGLE_UNCOMMITTED_FILTER" })}
          />
          <span>Limit to uncommitted ({changedCount})</span>
        </label>
      )}
      {hasBaseline && (
        <div className="flex items-center gap-2 flex-wrap">
          <span className="text-clevr-muted whitespace-nowrap">vs baseline:</span>
          <select
            className="border border-clevr-border rounded px-2 py-0.5 text-[12px] bg-white outline-none focus:border-clevr-accent"
            value={state.baseline.selectedBaselineId ?? ""}
            onChange={(e) => dispatch({ type: "SELECT_BASELINE", id: e.target.value })}
            title="Select which baseline to compare against"
          >
            {state.baseline.baselines.map((b) => (
              <option key={b.id} value={b.id}>
                {relativeTime(new Date(b.savedAt).getTime())}
                {b.gitRevision ? ` (${b.gitRevision})` : ""}
              </option>
            ))}
          </select>
          <button
            type="button"
            className="bg-white border border-clevr-border rounded px-1.5 py-0.5 text-[14px] text-clevr-muted cursor-pointer hover:border-sev-critical hover:text-sev-critical"
            title={`Delete baseline from ${savedAgo}`}
            onClick={() => {
              if (state.baseline.selectedBaselineId) post("DeleteBaseline", { id: state.baseline.selectedBaselineId });
            }}
          >
            ×
          </button>
          <div className="flex border border-clevr-border rounded overflow-hidden">
            <button
              type="button"
              className={`${segBase}${!state.filters.baselineFilter ? ` ${segActive}` : ""}`}
              onClick={() => dispatch({ type: "SET_BASELINE_FILTER", filter: null })}
            >
              All ({allCount})
            </button>
            <button
              type="button"
              className={`${segBase}${state.filters.baselineFilter === "new" ? ` ${segActive}` : ""}`}
              onClick={() => dispatch({ type: "SET_BASELINE_FILTER", filter: "new" })}
              title="Show only violations introduced since the baseline"
            >
              New ({newCount})
            </button>
            <button
              type="button"
              className={`${segBase}${state.filters.baselineFilter === "fixed" ? ` ${segActive}` : ""}`}
              onClick={() => dispatch({ type: "SET_BASELINE_FILTER", filter: "fixed" })}
              title="Show violations that were in the baseline but are now resolved"
            >
              Fixed ({fixedCount})
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

import { useAppDispatch, useAppState } from "../context/AppContext";
import { activeViolations, baselineFingerprintSet, currentFingerprintSet } from "../utils/filters";
import { isAppStoreModule } from "../utils/origins";
import { post } from "../hooks/useMessageBus";
import { relativeTime } from "../utils/time";

export function FilterBar() {
  const state = useAppState();
  const dispatch = useAppDispatch();

  const base = activeViolations({ ...state, baselineFilter: null });
  const asCount = base.filter((v) => isAppStoreModule(v, state.appStoreModules)).length;
  const changedCount = state.uncommittedAvailable
    ? base.filter((v) => {
        const qnMatch = state.uncommittedQualifiedNames.size > 0
          && state.uncommittedQualifiedNames.has(v.documentQualifiedName.toLowerCase());
        const idMatch = !!v.documentId && state.uncommittedDocumentIds.has(v.documentId.toLowerCase());
        return qnMatch || idMatch;
      }).length
    : 0;

  const hasBaseline = state.baselines.length > 0 && state.scanHasRun;

  // Baseline counts — compute with override state to get "what would show"
  const newCount = hasBaseline
    ? activeViolations({ ...state, baselineFilter: "new" }).length
    : 0;
  const fixedCount = hasBaseline
    ? activeViolations({ ...state, baselineFilter: "fixed" }).length
    : 0;
  const allCount = base.length;

  const selectedBaseline = state.baselines.find((b) => b.id === state.selectedBaselineId);
  const savedAgo = selectedBaseline ? relativeTime(new Date(selectedBaseline.savedAt).getTime()) : "";

  if (asCount === 0 && !state.uncommittedAvailable && !hasBaseline) return null;

  return (
    <div className="lint-engine-filter">
      {asCount > 0 && (
        <label className="lint-origin-toggle" title="Marketplace/app-store modules are scanned too; toggle to show or hide them.">
          <input
            type="checkbox"
            checked={state.appStoreVisible}
            onChange={() => dispatch({ type: "TOGGLE_APPSTORE" })}
          />
          <span> Marketplace modules ({asCount})</span>
        </label>
      )}
      {state.uncommittedAvailable && (
        <label className="lint-origin-toggle" title="Show only improvements in documents that have uncommitted Git changes.">
          <input
            type="checkbox"
            checked={state.uncommittedFilterActive}
            onChange={() => dispatch({ type: "TOGGLE_UNCOMMITTED_FILTER" })}
          />
          <span> Limit to uncommitted ({changedCount})</span>
        </label>
      )}
      {hasBaseline && (
        <div className="lint-baseline-filter">
          <span className="lint-filter-label">vs baseline:</span>
          <select
            className="lint-baseline-select"
            value={state.selectedBaselineId ?? ""}
            onChange={(e) => dispatch({ type: "SELECT_BASELINE", id: e.target.value })}
            title="Select which baseline to compare against"
          >
            {state.baselines.map((b) => (
              <option key={b.id} value={b.id}>
                {relativeTime(new Date(b.savedAt).getTime())}
                {b.gitRevision ? ` (${b.gitRevision})` : ""}
              </option>
            ))}
          </select>
          <button
            type="button"
            className="lint-baseline-delete-btn"
            title={`Delete baseline from ${savedAgo}`}
            onClick={() => {
              if (state.selectedBaselineId) post("DeleteBaseline", { id: state.selectedBaselineId });
            }}
          >
            ×
          </button>
          <div className="lint-baseline-segs">
            <button
              type="button"
              className={"lint-baseline-seg" + (!state.baselineFilter ? " active" : "")}
              onClick={() => dispatch({ type: "SET_BASELINE_FILTER", filter: null })}
            >
              All ({allCount})
            </button>
            <button
              type="button"
              className={"lint-baseline-seg lint-baseline-new" + (state.baselineFilter === "new" ? " active" : "")}
              onClick={() => dispatch({ type: "SET_BASELINE_FILTER", filter: "new" })}
              title="Show only violations introduced since the baseline"
            >
              New ({newCount})
            </button>
            <button
              type="button"
              className={"lint-baseline-seg lint-baseline-fixed" + (state.baselineFilter === "fixed" ? " active" : "")}
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

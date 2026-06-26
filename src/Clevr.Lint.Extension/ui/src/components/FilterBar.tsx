import { useAppDispatch, useAppState } from "../context/AppContext";
import { allDisplayViolations } from "../utils/filters";
import { isAppStoreModule, isSystemModule } from "../utils/origins";

export function FilterBar() {
  const state = useAppState();
  const dispatch = useAppDispatch();

  const base = allDisplayViolations(state).filter((v) => !isSystemModule(v));
  const asCount = base.filter((v) => isAppStoreModule(v, state.appStoreModules)).length;
  const changedCount = state.uncommittedAvailable
    ? base.filter((v) => !v.documentId || state.uncommittedDocumentIds.has(v.documentId)).length
    : 0;

  if (asCount === 0 && !state.uncommittedAvailable) return null;

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
    </div>
  );
}

import { useAppDispatch, useAppState } from "../context/AppContext";
import { allDisplayViolations } from "../utils/filters";
import { isAppStoreModule, isSystemModule } from "../utils/origins";

export function FilterBar() {
  const state = useAppState();
  const dispatch = useAppDispatch();

  const base = allDisplayViolations(state).filter((v) => !isSystemModule(v));
  const asCount = base.filter((v) => isAppStoreModule(v, state.appStoreModules)).length;

  if (asCount === 0) return null;

  return (
    <div className="lint-engine-filter">
      <label className="lint-origin-toggle" title="Marketplace/app-store modules are scanned too; toggle to show or hide them.">
        <input
          type="checkbox"
          checked={state.appStoreVisible}
          onChange={() => dispatch({ type: "TOGGLE_APPSTORE" })}
        />
        <span> Marketplace modules ({asCount})</span>
      </label>
    </div>
  );
}

import { useAppDispatch, useAppState } from "../context/AppContext";
import { ORIGINS } from "../constants";
import { allDisplayViolations } from "../utils/filters";
import { isAppStoreModule, isSystemModule, originOf } from "../utils/origins";

export function FilterBar() {
  const state = useAppState();
  const dispatch = useAppDispatch();

  const base = allDisplayViolations(state).filter((v) => !isSystemModule(v));
  const asCount = base.filter((v) => isAppStoreModule(v, state.appStoreModules)).length;

  return (
    <div className="acr-engine-filter">
      <span className="acr-filter-label">Source:</span>
      {ORIGINS.map((o) => {
        const count = base.filter((v) => originOf(v) === o.key).length;
        const disabled = count === 0;
        return (
          <label key={o.key} className={"acr-origin-toggle" + (disabled ? " disabled" : "")}>
            <input
              type="checkbox"
              checked={state.originEnabled.has(o.key)}
              disabled={disabled}
              onChange={(e) => dispatch({ type: "SET_ORIGIN_ENABLED", origin: o.key, enabled: e.target.checked })}
            />
            <span> {o.label} ({count})</span>
          </label>
        );
      })}
      {asCount > 0 && (
        <label className="acr-origin-toggle" title="Marketplace/app-store modules are scanned too; toggle to show or hide them.">
          <input
            type="checkbox"
            checked={state.appStoreVisible}
            onChange={() => dispatch({ type: "TOGGLE_APPSTORE" })}
          />
          <span> Marketplace modules ({asCount})</span>
        </label>
      )}
    </div>
  );
}

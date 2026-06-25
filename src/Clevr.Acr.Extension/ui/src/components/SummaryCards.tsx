import { useAppDispatch, useAppState } from "../context/AppContext";
import { ACR_CATEGORIES } from "../constants";
import { activeViolations, displayCategory, passesFilters } from "../utils/filters";
import { originLabel } from "../utils/origins";
import { severityUniverse } from "../utils/grouping";

interface Props {
  interactive?: boolean;
}

export function SummaryCards({ interactive = true }: Props) {
  const state = useAppState();
  const dispatch = useAppDispatch();
  const q = state.filterQuery.trim().toLowerCase();
  const filtered = activeViolations(state).filter((v) =>
    passesFilters(v, q, state.originEnabled, state.categoryEnabled, state.severityEnabled, state.ruleNames, state.ruleCategories)
  );

  return (
    <div className="acr-summary acr-summary-3">
      <div className="acr-card">
        <h3>Improvements per category</h3>
        {ACR_CATEGORIES.map((c) => {
          const count = filtered.filter((v) => displayCategory(v, state.ruleCategories) === c).length;
          const selected = state.categoryEnabled.has(c);
          return interactive ? (
            <div key={c} className={"acr-countrow acr-clickable" + (selected ? " acr-selected" : "")} onClick={() => dispatch({ type: "TOGGLE_CATEGORY", category: c })}>
              <span className="label">{c}</span>
              <span className="count">{count}</span>
            </div>
          ) : (
            <div key={c} className="acr-countrow">
              <span className="label">{c}</span>
              <span className="count">{count}</span>
            </div>
          );
        })}
        <TotalRow count={filtered.length} streaming={state.scanStreaming} />
      </div>

      <div className="acr-card">
        <h3>Improvements per severity</h3>
        {severityUniverse(state).map((s) => {
          const count = filtered.filter((v) => v.severity === s).length;
          const selected = state.severityEnabled.has(s);
          const label = <span className="label"><span className={`sev sev-${s}`}>{s || "(none)"}</span></span>;
          return interactive ? (
            <div key={s} className={"acr-countrow acr-clickable" + (selected ? " acr-selected" : "")} onClick={() => dispatch({ type: "TOGGLE_SEVERITY", severity: s })}>
              {label}<span className="count">{count}</span>
            </div>
          ) : (
            <div key={s} className="acr-countrow">{label}<span className="count">{count}</span></div>
          );
        })}
        <TotalRow count={filtered.length} streaming={state.scanStreaming} />
      </div>

      <div className="acr-card">
        <h3>Improvements per source</h3>
        {(["ACR (calibrated)", "MxCLI", "Manual checks"] as const).map((o) => {
          const count = filtered.filter((v) => originLabel(v) === o).length;
          if (!count) return null;
          return (
            <div key={o} className="acr-countrow">
              <span className="label">{o}</span>
              <span className="count">{count}</span>
            </div>
          );
        })}
        <TotalRow count={filtered.length} streaming={state.scanStreaming} />
      </div>
    </div>
  );
}

function TotalRow({ count, streaming }: { count: number; streaming: boolean }) {
  return (
    <div className="acr-countrow acr-total">
      <span className="label">{streaming ? "Total (so far)" : "Total"}</span>
      <span className="count" title={streaming ? "Scan in progress — this count is partial and will keep rising." : undefined}>
        {streaming ? `${count}…` : count}
      </span>
    </div>
  );
}

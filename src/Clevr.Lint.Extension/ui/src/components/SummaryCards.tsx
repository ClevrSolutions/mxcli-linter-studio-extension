import { useAppDispatch, useAppState } from "../context/AppContext";
import { LINT_CATEGORIES } from "../constants";
import { activeViolations, displayCategory, passesFilters } from "../utils/filters";
import { severityUniverse } from "../utils/grouping";

interface Props {
  interactive?: boolean;
}

export function SummaryCards({ interactive = true }: Props) {
  const state = useAppState();
  const dispatch = useAppDispatch();
  const q = state.filterQuery.trim().toLowerCase();
  const filtered = activeViolations(state).filter((v) =>
    passesFilters(v, q, state.categoryEnabled, state.severityEnabled, state.ruleNames, state.ruleCategories)
  );

  return (
    <div className="lint-summary lint-summary-2">
      <div className="lint-card">
        <h3>Improvements per category</h3>
        {LINT_CATEGORIES.map((c) => {
          const count = filtered.filter((v) => displayCategory(v, state.ruleCategories) === c).length;
          const selected = state.categoryEnabled.has(c);
          return interactive ? (
            <div key={c} className={"lint-countrow lint-clickable" + (selected ? " lint-selected" : "")} onClick={() => dispatch({ type: "TOGGLE_CATEGORY", category: c })}>
              <span className="label">{c}</span>
              <span className="count">{count}</span>
            </div>
          ) : (
            <div key={c} className="lint-countrow">
              <span className="label">{c}</span>
              <span className="count">{count}</span>
            </div>
          );
        })}
        <TotalRow count={filtered.length} streaming={state.scanStreaming} />
      </div>

      <div className="lint-card">
        <h3>Improvements per severity</h3>
        {severityUniverse(state).map((s) => {
          const count = filtered.filter((v) => v.severity === s).length;
          const selected = state.severityEnabled.has(s);
          const label = <span className="label"><span className={`sev sev-${s}`}>{s || "(none)"}</span></span>;
          return interactive ? (
            <div key={s} className={"lint-countrow lint-clickable" + (selected ? " lint-selected" : "")} onClick={() => dispatch({ type: "TOGGLE_SEVERITY", severity: s })}>
              {label}<span className="count">{count}</span>
            </div>
          ) : (
            <div key={s} className="lint-countrow">{label}<span className="count">{count}</span></div>
          );
        })}
        <TotalRow count={filtered.length} streaming={state.scanStreaming} />
      </div>

    </div>
  );
}

function TotalRow({ count, streaming }: { count: number; streaming: boolean }) {
  return (
    <div className="lint-countrow lint-total">
      <span className="label">{streaming ? "Total (so far)" : "Total"}</span>
      <span className="count" title={streaming ? "Scan in progress — this count is partial and will keep rising." : undefined}>
        {streaming ? `${count}…` : count}
      </span>
    </div>
  );
}

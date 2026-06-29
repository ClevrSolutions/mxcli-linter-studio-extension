import { useState } from "react";
import { useAppDispatch, useAppState } from "../context/AppContext";
import { LINT_CATEGORIES } from "../constants";
import { activeViolations, displayCategory, passesFilters } from "../utils/filters";
import { severityUniverse } from "../utils/grouping";
import { moduleOf } from "../utils/origins";

interface Props {
  interactive?: boolean;
}

export function SummaryCards({ interactive = true }: Props) {
  const state = useAppState();
  const dispatch = useAppDispatch();
  const q = state.filterQuery.trim().toLowerCase();
  const filtered = activeViolations(state).filter((v) =>
    passesFilters(v, q, state.categoryEnabled, state.severityEnabled, state.moduleFilterEnabled, state.ruleNames, state.ruleCategories)
  );

  const base = activeViolations({ ...state, baselineFilter: null });
  const modulesWithCounts = (() => {
    const counts = new Map<string, number>();
    for (const v of base) {
      const m = moduleOf(v);
      if (m) counts.set(m, (counts.get(m) ?? 0) + 1);
    }
    return Array.from(counts.entries())
      .map(([name, count]) => ({ name, count }))
      .sort((a, b) => a.name.localeCompare(b.name));
  })();

  const [catExpanded, setCatExpanded] = useState(false);
  const [sevExpanded, setSevExpanded] = useState(false);
  const [modExpanded, setModExpanded] = useState(false);

  return (
    <div className={`lint-summary lint-summary-${modulesWithCounts.length >= 2 ? "3" : "2"}`}>
      <div className="lint-card">
        <h3 className={interactive ? "lint-card-toggle" : ""} onClick={interactive ? () => setCatExpanded(x => !x) : undefined}>
          Improvements per category
          {interactive && <span className="lint-toggle-chevron">{catExpanded ? "▾" : "▸"}</span>}
        </h3>
        {catExpanded ? (
          <>
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
          </>
        ) : (
          <div className="lint-countrow lint-total">
            <span className="label">All</span>
            <span className="count">{state.scanStreaming ? `${filtered.length}…` : filtered.length}</span>
          </div>
        )}
      </div>

      <div className="lint-card">
        <h3 className={interactive ? "lint-card-toggle" : ""} onClick={interactive ? () => setSevExpanded(x => !x) : undefined}>
          Improvements per severity
          {interactive && <span className="lint-toggle-chevron">{sevExpanded ? "▾" : "▸"}</span>}
        </h3>
        {sevExpanded ? (
          <>
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
          </>
        ) : (
          <div className="lint-countrow lint-total">
            <span className="label">All</span>
            <span className="count">{state.scanStreaming ? `${filtered.length}…` : filtered.length}</span>
          </div>
        )}
      </div>

      {modulesWithCounts.length >= 2 && (
        <div className="lint-card">
          <h3 className={interactive ? "lint-card-toggle" : ""} onClick={interactive ? () => setModExpanded(x => !x) : undefined}>
            Improvements per module
            {interactive && <span className="lint-toggle-chevron">{modExpanded ? "▾" : "▸"}</span>}
          </h3>
          {modExpanded ? (
            modulesWithCounts.map(({ name, count }) => {
              const selected = state.moduleFilterEnabled.has(name);
              return interactive ? (
                <div key={name} className={"lint-countrow lint-clickable" + (selected ? " lint-selected" : "")} onClick={() => dispatch({ type: "TOGGLE_MODULE_FILTER", moduleName: name })}>
                  <span className="label">{name}</span>
                  <span className="count">{count}</span>
                </div>
              ) : (
                <div key={name} className="lint-countrow">
                  <span className="label">{name}</span>
                  <span className="count">{count}</span>
                </div>
              );
            })
          ) : (
            <div className="lint-countrow lint-total">
              <span className="label">All</span>
              <span className="count">{base.length}</span>
            </div>
          )}
        </div>
      )}
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

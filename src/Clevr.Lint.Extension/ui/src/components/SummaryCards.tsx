import { useState } from "react";
import { useAppDispatch, useAppState } from "../context/AppContext";
import { LINT_CATEGORIES } from "../constants";
import { activeViolations, displayCategory, passesFilters } from "../utils/filters";
import { severityUniverse } from "../utils/grouping";
import { moduleOf } from "../utils/origins";
import { cardBase, countRow } from "../utils/classes";

interface Props {
  interactive?: boolean;
}

const cardTitle = "text-[11px] font-semibold uppercase tracking-[0.05em] text-clevr-muted m-0 mb-2";
const countTotal = `${countRow} mt-1.5 pt-1.5 border-t border-dashed border-clevr-border font-semibold`;
const countClickable = `${countRow} cursor-pointer px-[6px] mx-[-6px] rounded-[5px] hover:bg-clevr-hover`;
const countSelected = "bg-clevr-selected shadow-[inset_2px_0_0_var(--color-clevr-accent)]";

export function SummaryCards({ interactive = true }: Props) {
  const state = useAppState();
  const dispatch = useAppDispatch();
  const q = state.filterQuery.trim().toLowerCase();
  const filtered = activeViolations(state).filter((v) =>
    passesFilters(v, q, state.categoryEnabled, state.severityEnabled, state.moduleFilterEnabled, state.ruleNames, state.ruleCategories)
  );

  const base = activeViolations({ ...state, baselineFilter: null });
  const moduleViolationCounts = new Map<string, number>();
  for (const v of base) {
    const m = moduleOf(v);
    if (m) moduleViolationCounts.set(m, (moduleViolationCounts.get(m) ?? 0) + 1);
  }
  const moduleRows = state.modules
    .map(({ name }) => ({ name, count: moduleViolationCounts.get(name) ?? 0 }))
    .sort((a, b) => a.name.localeCompare(b.name));

  const [catExpanded, setCatExpanded] = useState(false);
  const [sevExpanded, setSevExpanded] = useState(false);
  const [modExpanded, setModExpanded] = useState(false);

  const gridCols = state.modules.length >= 2 ? "grid-cols-3" : "grid-cols-2";

  return (
    <div className={`grid gap-3 mb-4 max-[760px]:grid-cols-1 ${gridCols}`}>
      <div className={cardBase}>
        <h3
          className={interactive ? `${cardTitle} cursor-pointer flex justify-between items-center select-none` : cardTitle}
          onClick={interactive ? () => setCatExpanded(x => !x) : undefined}
        >
          Improvements per category
          {interactive && <span className="text-clevr-muted text-[18px] font-bold">{catExpanded ? "▾" : "▸"}</span>}          
        </h3>
        {catExpanded ? (
          <>
            {LINT_CATEGORIES.map((c) => {
              const count = filtered.filter((v) => displayCategory(v, state.ruleCategories) === c).length;
              const selected = state.categoryEnabled.has(c);
              return interactive ? (
                <div
                  key={c}
                  className={`${countClickable}${selected ? ` ${countSelected}` : ""}`}
                  onClick={() => dispatch({ type: "TOGGLE_CATEGORY", category: c })}
                >
                  <span className={selected ? "font-semibold" : ""}>{c}</span>
                  <span className="tabular-nums font-semibold min-w-[28px] text-right">{count}</span>
                </div>
              ) : (
                <div key={c} className={countRow}>
                  <span>{c}</span>
                  <span className="tabular-nums font-semibold min-w-[28px] text-right">{count}</span>
                </div>
              );
            })}
            <TotalRow count={filtered.length} streaming={state.scanStreaming} />
          </>
        ) : (
          <div className={countTotal}>
            <span>All</span>
            <span className="tabular-nums font-semibold min-w-[28px] text-right">
              {state.scanStreaming ? `${filtered.length}…` : filtered.length}
            </span>
          </div>
        )}
      </div>

      <div className={cardBase}>
        <h3
          className={interactive ? `${cardTitle} cursor-pointer flex justify-between items-center select-none` : cardTitle}
          onClick={interactive ? () => setSevExpanded(x => !x) : undefined}
        >
          Improvements per severity
          {interactive && <span className="text-clevr-muted text-[18px] font-bold">{sevExpanded ? "▾" : "▸"}</span>}
        </h3>
        {sevExpanded ? (
          <>
            {severityUniverse(state).map((s) => {
              const count = filtered.filter((v) => v.severity === s).length;
              const selected = state.severityEnabled.has(s);
              const label = <span className={selected ? "font-semibold" : ""}><span className={`sev sev-${s}`}>{s || "(none)"}</span></span>;
              return interactive ? (
                <div
                  key={s}
                  className={`${countClickable}${selected ? ` ${countSelected}` : ""}`}
                  onClick={() => dispatch({ type: "TOGGLE_SEVERITY", severity: s })}
                >
                  {label}
                  <span className="tabular-nums font-semibold min-w-[28px] text-right">{count}</span>
                </div>
              ) : (
                <div key={s} className={countRow}>
                  {label}
                  <span className="tabular-nums font-semibold min-w-[28px] text-right">{count}</span>
                </div>
              );
            })}
            <TotalRow count={filtered.length} streaming={state.scanStreaming} />
          </>
        ) : (
          <div className={countTotal}>
            <span>All</span>
            <span className="tabular-nums font-semibold min-w-[28px] text-right">
              {state.scanStreaming ? `${filtered.length}…` : filtered.length}
            </span>
          </div>
        )}
      </div>

      {state.modules.length >= 2 && (
        <div className={cardBase}>
          <h3
            className={interactive ? `${cardTitle} cursor-pointer flex justify-between items-center select-none` : cardTitle}
            onClick={interactive ? () => setModExpanded(x => !x) : undefined}
          >
            Improvements per module
            {interactive && <span className="text-clevr-muted text-[18px] font-bold">{modExpanded ? "▾" : "▸"}</span>}
          </h3>
          {modExpanded ? (
            moduleRows.map(({ name, count }) => {
              const selected = state.moduleFilterEnabled.has(name);
              return interactive ? (
                <div
                  key={name}
                  className={`${countClickable}${selected ? ` ${countSelected}` : ""}`}
                  onClick={() => dispatch({ type: "TOGGLE_MODULE_FILTER", moduleName: name })}
                >
                  <span className={selected ? "font-semibold" : ""}>{name}</span>
                  <span className="tabular-nums font-semibold min-w-[28px] text-right">{count}</span>
                </div>
              ) : (
                <div key={name} className={countRow}>
                  <span>{name}</span>
                  <span className="tabular-nums font-semibold min-w-[28px] text-right">{count}</span>
                </div>
              );
            })
          ) : (
            <div className={countTotal}>
              <span>All</span>
              <span className="tabular-nums font-semibold min-w-[28px] text-right">{base.length}</span>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function TotalRow({ count, streaming }: { count: number; streaming: boolean }) {
  return (
    <div className={countTotal}>
      <span>{streaming ? "Total (so far)" : "Total"}</span>
      <span
        className="tabular-nums font-semibold min-w-[28px] text-right"
        title={streaming ? "Scan in progress — this count is partial and will keep rising." : undefined}
      >
        {streaming ? `${count}…` : count}
      </span>
    </div>
  );
}

import { useAppState } from "../context/AppContext";
import { LINT_CATEGORIES } from "../constants";
import { activeViolations, displayCategory, passesFilters } from "../utils/filters";
import { SummaryCards } from "./SummaryCards";
import { CategoryGroup } from "./CategoryGroup";
import { ExcludedSection } from "./ExcludedSection";

export function Report() {
  const state = useAppState();
  const q = state.filters.filterQuery.trim().toLowerCase();
  const isFixed = state.filters.baselineFilter === "fixed";

  const all = activeViolations(state).filter((v) =>
    passesFilters(v, q, state.filters.categoryEnabled, state.filters.severityEnabled, state.filters.moduleFilterEnabled, state.scan.ruleNames, state.scan.ruleCategories)
  );

  return (
    <div id="report">
      <SummaryCards />
      {all.length === 0 ? (
        <div className="text-clevr-muted italic py-4">
          {state.scan.scanStreaming
            ? "Scanning — results will appear as they are found…"
            : !state.scan.scanHasRun
              ? "Run a scan to see improvements."
              : state.scan.violations.length === 0
                ? "No improvements found — great work!"
                : "No improvements match the current filter."}
        </div>
      ) : (
        LINT_CATEGORIES.map((c) => {
          const items = all.filter((v) => displayCategory(v, state.scan.ruleCategories) === c);
          return items.length ? <CategoryGroup key={c} category={c} items={items} isFixed={isFixed} /> : null;
        })
      )}
      <ExcludedSection />
    </div>
  );
}

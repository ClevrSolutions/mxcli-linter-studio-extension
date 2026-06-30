import { useAppState } from "../context/AppContext";
import { LINT_CATEGORIES } from "../constants";
import { activeViolations, displayCategory, passesFilters } from "../utils/filters";
import { SummaryCards } from "./SummaryCards";
import { CategoryGroup } from "./CategoryGroup";
import { ExcludedSection } from "./ExcludedSection";

export function Report() {
  const state = useAppState();
  const q = state.filterQuery.trim().toLowerCase();
  const isFixed = state.baselineFilter === "fixed";

  const all = activeViolations(state).filter((v) =>
    passesFilters(v, q, state.categoryEnabled, state.severityEnabled, state.moduleFilterEnabled, state.ruleNames, state.ruleCategories)
  );

  return (
    <div id="report">
      <SummaryCards />
      {all.length === 0 ? (
        <div className="text-clevr-muted italic py-4">
          {!state.scanHasRun
            ? "Run a scan to see improvements."
            : state.violations.length === 0
              ? "No improvements found — great work!"
              : "No improvements match the current filter."}
        </div>
      ) : (
        LINT_CATEGORIES.map((c) => {
          const items = all.filter((v) => displayCategory(v, state.ruleCategories) === c);
          return items.length ? <CategoryGroup key={c} category={c} items={items} isFixed={isFixed} /> : null;
        })
      )}
      <ExcludedSection />
    </div>
  );
}

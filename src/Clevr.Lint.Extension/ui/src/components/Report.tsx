import { useMemo } from "react";
import { useAppState } from "../context/AppContext";
import { LINT_CATEGORIES } from "../constants";
import { activeViolations, activeViolationsDeps, displayCategory, passesFilters } from "../utils/filters";
import { SummaryCards } from "./SummaryCards";
import { CategoryGroup } from "./CategoryGroup";
import { ExcludedSection } from "./ExcludedSection";

export function Report() {
  const state = useAppState();
  const q = state.filters.filterQuery.trim().toLowerCase();
  const isFixed = state.filters.baselineFilter === "fixed";

  // eslint-disable-next-line react-hooks/exhaustive-deps
  const active = useMemo(() => activeViolations(state), activeViolationsDeps(state));
  const all = useMemo(
    () => active.filter((v) =>
      passesFilters(v, q, state.filters.categoryEnabled, state.filters.severityEnabled, state.filters.moduleFilterEnabled, state.scan.ruleNames, state.scan.ruleCategories)
    ),
    [active, q, state.filters.categoryEnabled, state.filters.severityEnabled, state.filters.moduleFilterEnabled, state.scan.ruleNames, state.scan.ruleCategories],
  );
  // Reference-stable per-category arrays so memoized cards below can skip re-rendering.
  const categoryItems = useMemo(
    () => LINT_CATEGORIES.map((c) => ({
      category: c,
      items: all.filter((v) => displayCategory(v, state.scan.ruleCategories) === c),
    })),
    [all, state.scan.ruleCategories],
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
        categoryItems.map(({ category, items }) =>
          items.length ? <CategoryGroup key={category} category={category} items={items} isFixed={isFixed} /> : null
        )
      )}
      <ExcludedSection />
    </div>
  );
}

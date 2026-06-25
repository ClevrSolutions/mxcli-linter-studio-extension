import { useAppState } from "../context/AppContext";
import { ACR_CATEGORIES } from "../constants";
import { activeViolations, displayCategory, passesFilters } from "../utils/filters";
import { SummaryCards } from "./SummaryCards";
import { CategoryGroup } from "./CategoryGroup";
import { ExcludedSection } from "./ExcludedSection";
import { AnsweredChecksSection } from "./AnsweredChecksSection";

export function Report() {
  const state = useAppState();
  const q = state.filterQuery.trim().toLowerCase();

  const all = activeViolations(state).filter((v) =>
    passesFilters(v, q, state.originEnabled, state.categoryEnabled, state.severityEnabled, state.ruleNames, state.ruleCategories)
  );

  const streamingBanner = state.scanStreaming ? (
    <div
      style={{
        display: "flex", alignItems: "center", gap: ".6em", margin: "0 0 12px",
        padding: "10px 14px", borderRadius: "8px",
        background: "#fff7e6", border: "1px solid #f0c36d", color: "#7a5b00", fontWeight: 600,
      }}
    >
      <span style={{ fontSize: "1.1em" }}>⏳</span>
      <span>
        Scanning…{state.scanProgress ? ` ${state.scanProgress.label}` : " running the deep microflow & expression analysis…"} — counts below are PARTIAL until the scan finishes.
      </span>
      {state.scanIncomplete && (
        <div style={{ flexBasis: "100%", color: "#a00", fontWeight: 600, marginTop: 4 }}>
          ⚠ Some elements could not be described — final results may be incomplete (see the .clevr-acr log).
        </div>
      )}
    </div>
  ) : null;

  return (
    <div id="report">
      {streamingBanner}
      <SummaryCards />
      {!state.lastDeepScan && (
        <p className="acr-scan-note">
          Quick scan — the deep microflow &amp; expression analysis (complexity, nested ifs, empty-string checks, default ReadWrite access) is NOT included. Run a Deepscan for the full analysis.
        </p>
      )}
      {all.length === 0 ? (
        <div className="acr-empty">No improvements match the filter.</div>
      ) : (
        ACR_CATEGORIES.map((c) => {
          const items = all.filter((v) => displayCategory(v, state.ruleCategories) === c);
          return items.length ? <CategoryGroup key={c} category={c} items={items} /> : null;
        })
      )}
      <ExcludedSection />
      <AnsweredChecksSection />
    </div>
  );
}

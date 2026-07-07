import { useState } from "react";
import { useAppDispatch, useAppState } from "../context/AppContext";
import { LINT_CATEGORIES, MXCLI_CATEGORY_TO_LINT } from "../constants";
import type { LinterConfigRule } from "../types";
import { RuleInfoDialog } from "./dialogs/RuleInfoDialog";
import { bulkBtn, sectionHeading, settingsDesc, settingsEmpty, tableBase } from "../utils/classes";

const SEVERITY_OPTIONS = ["inherit", "error", "warning", "info", "hint"] as const;

const thCell = "text-left py-1.5 pr-3 text-clevr-muted font-medium border-b border-clevr-border";
const tdCell = "py-1.5 pr-3 border-b border-clevr-border";
const tdCenter = "py-1.5 text-center border-b border-clevr-border";

function displayCategory(ruleId: string, ruleCategories: Record<string, string>): string {
  const mxcliCat = (ruleCategories[ruleId] ?? "").toLowerCase();
  return MXCLI_CATEGORY_TO_LINT[mxcliCat] ?? "Other";
}

export function RulesTab() {
  const state = useAppState();
  const dispatch = useAppDispatch();
  const [infoRuleId, setInfoRuleId] = useState<string | null>(null);

  function updateRule(ruleId: string, patch: Partial<LinterConfigRule>) {
    dispatch({ type: "UPDATE_RULE_SETTING", ruleId, patch });
  }

  function enableRules(ruleIds: string[]) {
    dispatch({ type: "BULK_SET_RULES_ENABLED", ruleIds, enabled: undefined });
  }

  function disableRules(ruleIds: string[]) {
    dispatch({ type: "BULK_SET_RULES_ENABLED", ruleIds, enabled: false });
  }

  const ruleIds = Object.keys(state.scan.ruleNames).sort();

  const byCategory = new Map<string, string[]>();
  for (const ruleId of ruleIds) {
    const cat = displayCategory(ruleId, state.scan.ruleCategories);
    if (!byCategory.has(cat)) byCategory.set(cat, []);
    byCategory.get(cat)!.push(ruleId);
  }

  const orderedCategories = [
    ...LINT_CATEGORIES.filter((c) => byCategory.has(c)),
    ...(byCategory.has("Other") ? ["Other"] : []),
  ];

  if (ruleIds.length === 0) {
    return (
      <p className={settingsEmpty}>
        No rules found — make sure a project is open in Studio Pro, or add rule definitions in the{" "}
        <button
          type="button"
          className="bg-transparent border-0 p-0 text-clevr-accent underline cursor-pointer text-inherit font-inherit"
          // eslint-disable-next-line @typescript-eslint/no-explicit-any
          onClick={() => dispatch({ type: "SET_SETTINGS_TAB", tab: "sources" as any })}
        >
          Sources
        </button>{" "}
        tab.
      </p>
    );
  }

  return (
    <>
      <div className="flex items-start justify-between gap-3 mb-3">
        <p className={`${settingsDesc} m-0`}>
          Configure which rules are active and their severity. Changes are written to{" "}
          <code>lint-config.yaml</code> in your project directory and take effect on the next scan.
          Add more rules from the{" "}
          <button
            type="button"
            className="bg-transparent border-0 p-0 text-clevr-accent underline cursor-pointer text-inherit font-inherit"
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            onClick={() => dispatch({ type: "SET_SETTINGS_TAB", tab: "sources" as any })}
          >
            Sources
          </button>{" "}
          tab.
        </p>
        <div className="flex items-center gap-1 text-[11px] shrink-0">
          <button type="button" className={bulkBtn} onClick={() => enableRules(ruleIds)}>Enable all</button>
          <span className="text-clevr-muted">·</span>
          <button type="button" className={bulkBtn} onClick={() => disableRules(ruleIds)}>Disable all</button>
        </div>
      </div>

      {orderedCategories.map((category) => {
        const rules = byCategory.get(category) ?? [];
        return (
          <section key={category} className="mb-6">
            <div className="flex items-center justify-between mb-2">
              <h3 className={sectionHeading}>{category}</h3>
              <div className="flex items-center gap-1 text-[11px]">
                <button type="button" className={bulkBtn} onClick={() => enableRules(rules)}>Enable all</button>
                <span className="text-clevr-muted">·</span>
                <button type="button" className={bulkBtn} onClick={() => disableRules(rules)}>Disable all</button>
              </div>
            </div>
            <table className={tableBase}>
              <thead>
                <tr>
                  <th className={`${thCell} text-left`}>Rule</th>
                  <th className={`${thCell} w-[70px]`}>Enabled</th>
                  <th className={`${thCell} w-[100px]`}>Severity</th>
                </tr>
              </thead>
              <tbody>
                {rules.map((ruleId) => {
                  const name = state.scan.ruleNames[ruleId] ?? "";
                  const cfg = state.config.linterConfig.pending[ruleId] ?? {};
                  const isEnabled = cfg.enabled !== false;
                  const severity = cfg.severity ?? "inherit";

                  return (
                    <tr key={ruleId} className={`cursor-pointer select-none hover:bg-clevr-hover ${isEnabled ? "" : "opacity-50"}`} onClick={() => updateRule(ruleId, { enabled: isEnabled ? false : undefined })}>
                      <td className={tdCell}>
                        <div className="flex items-center gap-1.5">
                          <button
                            type="button"
                            title="Rule info"
                            className="shrink-0 flex items-center justify-center bg-transparent border-0 p-0 cursor-pointer text-clevr-accent opacity-60 hover:opacity-100"
                            onClick={(e) => { e.stopPropagation(); setInfoRuleId(ruleId); }}
                          >
                            <svg width="14" height="14" viewBox="0 0 16 16" fill="currentColor" aria-hidden="true">
                              <circle cx="8" cy="8" r="7.5" fill="none" stroke="currentColor" strokeWidth="1.2"/>
                              <rect x="7.25" y="7" width="1.5" height="5" rx="0.5"/>
                              <circle cx="8" cy="4.5" r="0.85"/>
                            </svg>
                          </button>
                          <span className="font-mono font-semibold text-[11px]">{ruleId}</span>
                          {name && <span className="text-clevr-muted">{name}</span>}
                        </div>
                      </td>
                      <td className={`${tdCenter} w-[70px]`}>
                        <input
                          type="checkbox"
                          checked={isEnabled}
                          onChange={(e) => updateRule(ruleId, { enabled: e.target.checked ? undefined : false })}
                          onClick={(e) => e.stopPropagation()}
                          title={isEnabled ? "Rule is enabled — click to disable" : "Rule is disabled — click to enable"}
                        />
                      </td>
                      <td className={`${tdCell} w-[100px]`}>
                        <select
                          className="border border-clevr-border rounded px-1.5 py-0.5 text-[12px] w-full outline-none focus:border-clevr-accent"
                          value={severity}
                          disabled={!isEnabled}
                          onChange={(e) => updateRule(ruleId, { severity: e.target.value || "inherit" })}
                          onClick={(e) => e.stopPropagation()}
                        >
                          {SEVERITY_OPTIONS.map((s) => (
                            <option key={s} value={s}>
                              {s === "inherit" ? "default" : s}
                            </option>
                          ))}
                        </select>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </section>
        );
      })}

      {infoRuleId && (
        <RuleInfoDialog
          ruleId={infoRuleId}
          name={state.scan.ruleNames[infoRuleId] ?? ""}
          description={state.scan.ruleDescriptions[infoRuleId] ?? ""}
          starContent={state.scan.ruleStarContent[infoRuleId]}
          onClose={() => setInfoRuleId(null)}
        />
      )}
    </>
  );
}

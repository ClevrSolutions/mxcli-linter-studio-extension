import { useEffect } from "react";
import { useAppDispatch, useAppState } from "../context/AppContext";
import { post } from "../hooks/useMessageBus";
import { LINT_CATEGORIES, MXCLI_CATEGORY_TO_LINT } from "../constants";
import type { LinterConfigRule } from "../types";

const SEVERITY_OPTIONS = ["inherit", "error", "warning", "info", "hint"] as const;

function displayCategory(ruleId: string, ruleCategories: Record<string, string>): string {
  const mxcliCat = (ruleCategories[ruleId] ?? "").toLowerCase();
  return MXCLI_CATEGORY_TO_LINT[mxcliCat] ?? "Other";
}

function isPendingChanged(
  pending: Record<string, LinterConfigRule>,
  saved: Record<string, LinterConfigRule>,
  pendingExcluded: string[],
  savedExcluded: string[],
): boolean {
  const pendingKeys = Object.keys(pending);
  const savedKeys = Object.keys(saved);
  if (pendingKeys.length !== savedKeys.length) return true;
  if (pendingKeys.some((k) => {
    const p = pending[k]!;
    const s = saved[k];
    return p.enabled !== s?.enabled || p.severity !== s?.severity;
  })) return true;
  if (pendingExcluded.length !== savedExcluded.length) return true;
  const savedSet = new Set(savedExcluded);
  return pendingExcluded.some((m) => !savedSet.has(m));
}

export function Settings() {
  const state = useAppState();
  const dispatch = useAppDispatch();
  useEffect(() => {
    post("RequestModules");
    post("RequestRulesCatalog");
  }, []);

  const hasChanges = isPendingChanged(
    state.pendingConfig, state.linterConfig,
    state.pendingExcludedModules, state.savedExcludedModules,
  );

  function updateRule(ruleId: string, patch: Partial<LinterConfigRule>) {
    dispatch({ type: "UPDATE_RULE_SETTING", ruleId, patch });
  }

  function toggleModule(moduleName: string) {
    dispatch({ type: "TOGGLE_MODULE_EXCLUSION", moduleName });
  }

  function selectAllModules() {
    dispatch({ type: "SET_EXCLUDED_MODULES", modules: [] });
  }

  function deselectAllModules() {
    dispatch({ type: "SET_EXCLUDED_MODULES", modules: state.modules.map((m) => m.name) });
  }

  function excludeAllMarketplace() {
    dispatch({ type: "SET_MARKETPLACE_MODULES_EXCLUDED", exclude: true });
  }

  function includeAllMarketplace() {
    dispatch({ type: "SET_MARKETPLACE_MODULES_EXCLUDED", exclude: false });
  }

  function enableRules(ruleIds: string[]) {
    dispatch({ type: "BULK_SET_RULES_ENABLED", ruleIds, enabled: undefined });
  }

  function disableRules(ruleIds: string[]) {
    dispatch({ type: "BULK_SET_RULES_ENABLED", ruleIds, enabled: false });
  }

  function save() {
    const rules: Record<string, LinterConfigRule> = {};
    for (const [ruleId, cfg] of Object.entries(state.pendingConfig)) {
      if (cfg.enabled === false || (cfg.severity && cfg.severity !== "inherit")) {
        rules[ruleId] = {
          enabled: cfg.enabled === false ? false : undefined,
          severity: cfg.severity && cfg.severity !== "inherit" ? cfg.severity : undefined,
        };
      }
    }
    post("SaveLinterConfig", { rules, excludedModules: state.pendingExcludedModules });
    dispatch({ type: "SET_LINTER_CONFIG", config: state.pendingConfig, excludedModules: state.pendingExcludedModules });
  }

  const ruleIds = Object.keys(state.ruleNames).sort();

  // Group rules by category
  const byCategory = new Map<string, string[]>();
  for (const ruleId of ruleIds) {
    const cat = displayCategory(ruleId, state.ruleCategories);
    if (!byCategory.has(cat)) byCategory.set(cat, []);
    byCategory.get(cat)!.push(ruleId);
  }

  // Order: known categories first, then "Other"
  const orderedCategories = [
    ...LINT_CATEGORIES.filter((c) => byCategory.has(c)),
    ...(byCategory.has("Other") ? ["Other"] : []),
  ];

  const settingsHeader = (
    <div className="lint-settings-header">
      <button type="button" className="lint-settings-back" onClick={() => dispatch({ type: "HIDE_SETTINGS" })}>
        ← Back
      </button>
      <h2>Settings</h2>
      <div className="lint-settings-header-actions">
        {hasChanges && <span className="lint-settings-unsaved">Unsaved changes</span>}
        <button type="button" className="lint-settings-save" disabled={!hasChanges} onClick={save}>
          Save
        </button>
      </div>
    </div>
  );

  const moduleSection = (
    <section className="lint-settings-section">
      <h3 className="lint-settings-category">Modules</h3>
      <p className="lint-settings-desc">
        Excluded modules are skipped on the next scan. Changes are written to{" "}
        <code>lint-config.yaml</code> in your project directory.
      </p>
      {state.modules.length === 0 ? (
        <p className="lint-settings-empty">No modules found — open a project in Studio Pro.</p>
      ) : (
        <table className="lint-settings-table">
          <thead>
            <tr>
              <th>Module</th>
              <th className="lint-settings-marketplace-cell">
                <div className="lint-settings-col-header">
                  <span>Marketplace</span>
                  {state.modules.some((m) => m.fromMarketplace) && (
                    <div className="lint-settings-bulk-actions">
                      {state.modules.filter((m) => m.fromMarketplace).every((m) => state.pendingExcludedModules.includes(m.name))
                        ? <button type="button" className="lint-settings-bulk-btn" onClick={includeAllMarketplace}>Include all</button>
                        : <button type="button" className="lint-settings-bulk-btn" onClick={excludeAllMarketplace}>Exclude all</button>
                      }
                    </div>
                  )}
                </div>
              </th>
              <th className="lint-settings-enabled-cell">
                <div className="lint-settings-col-header">
                  <span>Include in scan</span>
                  <div className="lint-settings-bulk-actions">
                    <button type="button" className="lint-settings-bulk-btn" onClick={selectAllModules}>All</button>
                    <span className="lint-settings-bulk-sep">·</span>
                    <button type="button" className="lint-settings-bulk-btn" onClick={deselectAllModules}>None</button>
                  </div>
                </div>
              </th>
            </tr>
          </thead>
          <tbody>
            {state.modules.map((module) => {
              const isExcluded = state.pendingExcludedModules.includes(module.name);
              return (
                <tr key={module.name} className={isExcluded ? "lint-settings-disabled-row" : ""}>
                  <td className="lint-settings-module-cell">{module.name}</td>
                  <td className="lint-settings-marketplace-cell">
                    {module.fromMarketplace && (
                      <span
                        className="lint-settings-marketplace-badge"
                        title={module.appStoreVersion ? `Mendix Marketplace v${module.appStoreVersion}` : "Mendix Marketplace"}
                      >
                        MX
                      </span>
                    )}
                  </td>
                  <td className="lint-settings-enabled-cell">
                    <input
                      type="checkbox"
                      checked={!isExcluded}
                      onChange={() => toggleModule(module.name)}
                      title={isExcluded ? "Module is excluded — click to include" : "Module is included — click to exclude"}
                    />
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      )}
    </section>
  );

  const tabBar = (
    <div className="lint-settings-tabs">
      <button
        type="button"
        className={`lint-settings-tab${state.settingsActiveTab === "modules" ? " active" : ""}`}
        onClick={() => dispatch({ type: "SET_SETTINGS_TAB", tab: "modules" })}
      >
        Modules
      </button>
      <button
        type="button"
        className={`lint-settings-tab${state.settingsActiveTab === "rules" ? " active" : ""}`}
        onClick={() => dispatch({ type: "SET_SETTINGS_TAB", tab: "rules" })}
      >
        Rules configuration
      </button>
    </div>
  );

  const rulesContent = ruleIds.length === 0 ? (
    <p className="lint-settings-empty">Loading rules — make sure a project is open in Studio Pro.</p>
  ) : (
    <>
      <div className="lint-settings-rules-header">
        <p className="lint-settings-desc" style={{ margin: 0 }}>
          Configure which rules are active and their severity. Changes are written to{" "}
          <code>lint-config.yaml</code> in your project directory and take effect on the next scan.
        </p>
        <div className="lint-settings-bulk-actions">
          <button type="button" className="lint-settings-bulk-btn" onClick={() => enableRules(ruleIds)}>Enable all</button>
          <span className="lint-settings-bulk-sep">·</span>
          <button type="button" className="lint-settings-bulk-btn" onClick={() => disableRules(ruleIds)}>Disable all</button>
        </div>
      </div>

      {orderedCategories.map((category) => {
        const rules = byCategory.get(category) ?? [];
        return (
          <section key={category} className="lint-settings-section">
            <div className="lint-settings-category-header">
              <h3 className="lint-settings-category">{category}</h3>
              <div className="lint-settings-bulk-actions">
                <button type="button" className="lint-settings-bulk-btn" onClick={() => enableRules(rules)}>Enable all</button>
                <span className="lint-settings-bulk-sep">·</span>
                <button type="button" className="lint-settings-bulk-btn" onClick={() => disableRules(rules)}>Disable all</button>
              </div>
            </div>
            <table className="lint-settings-table">
              <thead>
                <tr>
                  <th>Rule</th>
                  <th>Enabled</th>
                  <th>Severity</th>
                </tr>
              </thead>
              <tbody>
                {rules.map((ruleId) => {
                  const name = state.ruleNames[ruleId] ?? "";
                  const cfg = state.pendingConfig[ruleId] ?? {};
                  const isEnabled = cfg.enabled !== false;
                  const severity = cfg.severity ?? "inherit";

                  return (
                    <tr key={ruleId} className={isEnabled ? "" : "lint-settings-disabled-row"}>
                      <td className="lint-settings-rule-cell">
                        <span className="lint-settings-rule-id">{ruleId}</span>
                        {name && <span className="lint-settings-rule-name">{name}</span>}
                      </td>
                      <td className="lint-settings-enabled-cell">
                        <input
                          type="checkbox"
                          checked={isEnabled}
                          onChange={(e) => updateRule(ruleId, { enabled: e.target.checked ? undefined : false })}
                          title={isEnabled ? "Rule is enabled — click to disable" : "Rule is disabled — click to enable"}
                        />
                      </td>
                      <td className="lint-settings-severity-cell">
                        <select
                          value={severity}
                          disabled={!isEnabled}
                          onChange={(e) => updateRule(ruleId, { severity: e.target.value || "inherit" })}
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
    </>
  );

  return (
    <div className="lint-settings">
      {settingsHeader}
      {tabBar}
      {state.settingsActiveTab === "modules" ? moduleSection : rulesContent}
    </div>
  );
}

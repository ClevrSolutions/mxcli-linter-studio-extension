import { useEffect, useState } from "react";
import { useAppDispatch, useAppState } from "../context/AppContext";
import { post } from "../hooks/useMessageBus";
import { LINT_CATEGORIES, MXCLI_CATEGORY_TO_LINT } from "../constants";
import type { LinterConfigRule } from "../types";
import { ConfigurationTab } from "./ConfigurationTab";
import { RuleSourcesTab } from "./RuleSourcesTab";
import { AboutTab } from "./AboutTab";
import { SnapshotsTab } from "./SnapshotsTab";
import { RuleInfoDialog } from "./dialogs/RuleInfoDialog";
import {
  btnPrimary, btnSecondary, bulkBtn, sectionHeading,
  settingsDesc, settingsEmpty, tableBase,
} from "../utils/classes";

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

const thCell = "text-left py-1.5 pr-3 text-clevr-muted font-medium border-b border-clevr-border";
const tdCell = "py-1.5 pr-3 border-b border-clevr-border";
const tdCenter = "py-1.5 text-center border-b border-clevr-border";

export function Settings() {
  const state = useAppState();
  const dispatch = useAppDispatch();
  const [infoRuleId, setInfoRuleId] = useState<string | null>(null);
  useEffect(() => {
    post("RequestModules");
    post("RequestRulesCatalog");
  }, []);

  const hasChanges = isPendingChanged(
    state.config.linterConfig.pending, state.config.linterConfig.saved,
    state.config.excludedModules.pending, state.config.excludedModules.saved,
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
    dispatch({ type: "SET_EXCLUDED_MODULES", modules: state.config.modules.map((m) => m.name) });
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
    for (const [ruleId, cfg] of Object.entries(state.config.linterConfig.pending)) {
      if (cfg.enabled === false || (cfg.severity && cfg.severity !== "inherit")) {
        rules[ruleId] = {
          enabled: cfg.enabled === false ? false : undefined,
          severity: cfg.severity && cfg.severity !== "inherit" ? cfg.severity : undefined,
        };
      }
    }
    post("SaveLinterConfig", { rules, excludedModules: state.config.excludedModules.pending });
    dispatch({ type: "SET_LINTER_CONFIG", config: state.config.linterConfig.pending, excludedModules: state.config.excludedModules.pending });
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

  const isConfigTab = state.ui.settingsActiveTab === "configuration" || state.ui.settingsActiveTab === "sources" || state.ui.settingsActiveTab === "about" || state.ui.settingsActiveTab === "snapshots";

  const tabBtn = (tab: string, label: string) => {
    const active = state.ui.settingsActiveTab === tab;
    return (
      <button
        key={tab}
        type="button"
        className={[
          "bg-transparent border-0 border-b-2 px-[14px] py-[6px] text-[12px] font-medium cursor-pointer -mb-px",
          active
            ? "text-clevr-fg border-b-clevr-accent"
            : "text-clevr-muted border-b-transparent hover:text-clevr-fg",
        ].join(" ")}
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        onClick={() => dispatch({ type: "SET_SETTINGS_TAB", tab: tab as any })}      >
        {label}
      </button>
    );
  };

  const moduleSection = (
    <section className="mb-6">
      <h3 className={sectionHeading}>Modules</h3>
      <p className={settingsDesc}>
        Excluded modules are skipped on the next scan. Changes are written to{" "}
        <code>lint-config.yaml</code> in your project directory.
      </p>
      {state.config.modules.length === 0 ? (
        <p className={settingsEmpty}>No modules found — open a project in Studio Pro.</p>
      ) : (
        <table className={tableBase}>
          <thead>
            <tr>
              <th className={`${thCell} text-left`}>Module</th>
              <th className={`${thCell} w-[80px]`}>
                <div className="flex items-center gap-1">
                  <span>Marketplace</span>
                  {state.config.modules.some((m) => m.fromMarketplace) && (
                    <div className="flex items-center gap-1 ml-1 text-[11px]">
                      {state.config.modules.filter((m) => m.fromMarketplace).every((m) => state.config.excludedModules.pending.includes(m.name))
                        ? <button type="button" className={bulkBtn} onClick={includeAllMarketplace}>Include all</button>
                        : <button type="button" className={bulkBtn} onClick={excludeAllMarketplace}>Exclude all</button>
                      }
                    </div>
                  )}
                </div>
              </th>
              <th className={`${thCell} w-[110px]`}>
                <div className="flex items-center gap-1">
                  <span>Include in scan</span>
                  <div className="flex items-center gap-1 ml-1 text-[11px]">
                    <button type="button" className={bulkBtn} onClick={selectAllModules}>All</button>
                    <span className="text-clevr-muted">·</span>
                    <button type="button" className={bulkBtn} onClick={deselectAllModules}>None</button>
                  </div>
                </div>
              </th>
            </tr>
          </thead>
          <tbody>
            {state.config.modules.map((module) => {
              const isExcluded = state.config.excludedModules.pending.includes(module.name);
              return (
                <tr key={module.name} className={`cursor-pointer select-none hover:bg-clevr-hover ${isExcluded ? "opacity-50" : ""}`} onClick={() => toggleModule(module.name)}>
                  <td className={`${tdCell} font-medium`}>{module.name}</td>
                  <td className={`${tdCenter} w-[80px]`}>
                    {module.fromMarketplace && (
                      <span
                        className="inline-block px-[7px] py-px rounded-full text-[10px] font-bold tracking-[0.03em] bg-[#e7eef6] text-clevr-accent cursor-default select-none"
                        title={module.appStoreVersion ? `Mendix Marketplace v${module.appStoreVersion}` : "Mendix Marketplace"}
                      >
                        MX
                      </span>
                    )}
                  </td>
                  <td className={`${tdCenter} w-[110px]`}>
                    <input
                      type="checkbox"
                      checked={!isExcluded}
                      onChange={() => toggleModule(module.name)}
                      onClick={(e) => e.stopPropagation()}
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

  const rulesContent = ruleIds.length === 0 ? (
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
  ) : (
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
    </>
  );

  return (
    <div className="pb-8">
      <div className="flex items-center gap-3 mb-3 pb-2 border-b border-clevr-border">
        <button type="button" className={btnSecondary} onClick={() => dispatch({ type: "HIDE_SETTINGS" })}>
          ← Back
        </button>
        <h2 className="text-[15px] font-semibold m-0">Settings</h2>
        {!isConfigTab && (
          <div className="ml-auto flex items-center gap-2">
            {hasChanges && <span className="text-[12px] text-clevr-muted italic">Unsaved changes</span>}
            <button type="button" className={btnPrimary} disabled={!hasChanges} onClick={save}>
              Save
            </button>
          </div>
        )}
      </div>

      <div className="flex border-b border-clevr-border mb-4">
        {tabBtn("modules", "Modules")}
        {tabBtn("rules", "Rules")}
        {tabBtn("snapshots", "Snapshots")}
        {tabBtn("configuration", "Configuration")}
        {tabBtn("sources", "Sources")}
        {tabBtn("about", "About")}
      </div>

      {state.ui.settingsActiveTab === "modules" && moduleSection}
      {state.ui.settingsActiveTab === "rules" && rulesContent}
      {state.ui.settingsActiveTab === "snapshots" && <SnapshotsTab />}
      {state.ui.settingsActiveTab === "configuration" && <ConfigurationTab />}
      {state.ui.settingsActiveTab === "sources" && <RuleSourcesTab />}
      {state.ui.settingsActiveTab === "about" && <AboutTab />}

      {infoRuleId && (
        <RuleInfoDialog
          ruleId={infoRuleId}
          name={state.scan.ruleNames[infoRuleId] ?? ""}
          description={state.scan.ruleDescriptions[infoRuleId] ?? ""}
          starContent={state.scan.ruleStarContent[infoRuleId]}
          onClose={() => setInfoRuleId(null)}
        />
      )}
    </div>
  );
}

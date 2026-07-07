import { useEffect, useState } from "react";
import { useAppDispatch, useAppState } from "../context/AppContext";
import { post } from "../hooks/useMessageBus";
import type { LinterConfigRule } from "../types";
import { ModulesTab } from "./ModulesTab";
import { RulesTab } from "./RulesTab";
import { ConfigurationTab } from "./ConfigurationTab";
import { RuleSourcesTab } from "./RuleSourcesTab";
import { AboutTab } from "./AboutTab";
import { SnapshotsTab } from "./SnapshotsTab";
import { UnsavedSettingsDialog } from "./dialogs/UnsavedSettingsDialog";
import { btnPrimary, btnSecondary } from "../utils/classes";

// Strips a linter config down to the entries that are actually meaningful — a rule only
// needs to be recorded if it's disabled or has a concrete (non-"inherit") severity. This is
// exactly what gets posted to the host on save, so it's also the canonical form used to
// detect whether pending differs from saved (see isPendingChanged below).
function stripRules(rules: Record<string, LinterConfigRule>): Record<string, LinterConfigRule> {
  const stripped: Record<string, LinterConfigRule> = {};
  for (const [ruleId, cfg] of Object.entries(rules)) {
    if (cfg.enabled === false || (cfg.severity && cfg.severity !== "inherit")) {
      stripped[ruleId] = {
        enabled: cfg.enabled === false ? false : undefined,
        severity: cfg.severity && cfg.severity !== "inherit" ? cfg.severity : undefined,
      };
    }
  }
  return stripped;
}

function isPendingChanged(
  pending: Record<string, LinterConfigRule>,
  saved: Record<string, LinterConfigRule>,
  pendingExcluded: string[],
  savedExcluded: string[],
): boolean {
  const strippedPending = stripRules(pending);
  const strippedSaved = stripRules(saved);
  const allKeys = new Set([...Object.keys(strippedPending), ...Object.keys(strippedSaved)]);
  for (const k of allKeys) {
    const p = strippedPending[k];
    const s = strippedSaved[k];
    if (p?.enabled !== s?.enabled || p?.severity !== s?.severity) return true;
  }
  if (pendingExcluded.length !== savedExcluded.length) return true;
  const savedSet = new Set(savedExcluded);
  return pendingExcluded.some((m) => !savedSet.has(m));
}

export function Settings() {
  const state = useAppState();
  const dispatch = useAppDispatch();
  const [showUnsavedDialog, setShowUnsavedDialog] = useState(false);
  useEffect(() => {
    post("RequestModules");
    post("RequestRulesCatalog");
  }, []);

  const hasChanges = isPendingChanged(
    state.config.linterConfig.pending, state.config.linterConfig.saved,
    state.config.excludedModules.pending, state.config.excludedModules.saved,
  );

  function save() {
    const rules = stripRules(state.config.linterConfig.pending);
    post("SaveLinterConfig", { rules, excludedModules: state.config.excludedModules.pending });
    dispatch({ type: "SET_LINTER_CONFIG", config: rules, excludedModules: state.config.excludedModules.pending });
  }

  function handleBack() {
    if (hasChanges) {
      setShowUnsavedDialog(true);
    } else {
      dispatch({ type: "HIDE_SETTINGS" });
    }
  }

  function saveAndClose() {
    save();
    setShowUnsavedDialog(false);
    dispatch({ type: "HIDE_SETTINGS" });
  }

  function revertAndClose() {
    setShowUnsavedDialog(false);
    dispatch({ type: "HIDE_SETTINGS" });
  }

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

  return (
    <div className="pb-8">
      <div className="flex items-center gap-3 mb-3 pb-2 border-b border-clevr-border">
        <button type="button" className={btnSecondary} onClick={handleBack}>
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

      {state.ui.settingsActiveTab === "modules" && <ModulesTab />}
      {state.ui.settingsActiveTab === "rules" && <RulesTab />}
      {state.ui.settingsActiveTab === "snapshots" && <SnapshotsTab />}
      {state.ui.settingsActiveTab === "configuration" && <ConfigurationTab />}
      {state.ui.settingsActiveTab === "sources" && <RuleSourcesTab />}
      {state.ui.settingsActiveTab === "about" && <AboutTab />}

      {showUnsavedDialog && (
        <UnsavedSettingsDialog
          onSave={saveAndClose}
          onRevert={revertAndClose}
          onCancel={() => setShowUnsavedDialog(false)}
        />
      )}
    </div>
  );
}

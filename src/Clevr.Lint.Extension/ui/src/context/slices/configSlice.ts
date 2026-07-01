import type { Exclusion, LinterConfigRule, ModuleInfo, MxcliInfo, RuleSource, RuleSourceFetchStatus } from "../../types";
import { cancelEdit, commit, draftOf, editPending, startEdit, type Draft } from "../draft";

export interface ConfigState {
  exclusions: Exclusion[];
  linterConfig: Draft<Record<string, LinterConfigRule>>;
  excludedModules: Draft<string[]>;
  modules: ModuleInfo[];
  mxcliInfo: MxcliInfo | null;
  mxcliDownloading: boolean;
  mxcliDownloadProgress: number | null;
  ruleSources: RuleSource[];
  ruleSourceFetchStatus: Record<string, RuleSourceFetchStatus>;
}

export type ConfigAction =
  | { type: "SET_EXCLUSIONS"; exclusions: Exclusion[] }
  | { type: "SHOW_SETTINGS" }
  | { type: "HIDE_SETTINGS" }
  | { type: "SET_LINTER_CONFIG"; config: Record<string, LinterConfigRule>; excludedModules?: string[] }
  | { type: "UPDATE_RULE_SETTING"; ruleId: string; patch: Partial<LinterConfigRule> }
  | { type: "SET_MODULES"; modules: ModuleInfo[] }
  | { type: "TOGGLE_MODULE_EXCLUSION"; moduleName: string }
  | { type: "SET_EXCLUDED_MODULES"; modules: string[] }
  | { type: "SET_MARKETPLACE_MODULES_EXCLUDED"; exclude: boolean }
  | { type: "BULK_SET_RULES_ENABLED"; ruleIds: string[]; enabled: boolean | undefined }
  | { type: "SET_RULE_SOURCES"; sources: RuleSource[] }
  | { type: "ADD_RULE_SOURCE"; source: RuleSource }
  | { type: "REMOVE_RULE_SOURCE"; id: string }
  | { type: "SET_RULE_SOURCE_FETCH_STATUS"; id: string; status: RuleSourceFetchStatus }
  | { type: "SET_MXCLI_INFO"; info: MxcliInfo }
  | { type: "MXCLI_DOWNLOAD_STARTED" }
  | { type: "MXCLI_DOWNLOAD_PROGRESS"; percent: number };

export const initialConfigState: ConfigState = {
  exclusions: [],
  linterConfig: draftOf<Record<string, LinterConfigRule>>({}),
  excludedModules: draftOf<string[]>([]),
  modules: [],
  mxcliInfo: null,
  mxcliDownloading: false,
  mxcliDownloadProgress: null,
  ruleSources: [],
  ruleSourceFetchStatus: {},
};

export function configReducer(state: ConfigState, action: ConfigAction): ConfigState {
  switch (action.type) {
    case "SET_EXCLUSIONS":
      return { ...state, exclusions: action.exclusions };
    case "SHOW_SETTINGS":
      return { ...state, linterConfig: startEdit(state.linterConfig), excludedModules: startEdit(state.excludedModules) };
    case "HIDE_SETTINGS":
      return {
        ...state,
        linterConfig: cancelEdit(state.linterConfig),
        excludedModules: cancelEdit(state.excludedModules),
        mxcliDownloading: false,
        mxcliDownloadProgress: null,
      };
    case "SET_LINTER_CONFIG":
      return {
        ...state,
        linterConfig: commit(state.linterConfig, action.config),
        excludedModules: commit(state.excludedModules, action.excludedModules ?? []),
      };
    case "UPDATE_RULE_SETTING":
      return {
        ...state,
        linterConfig: editPending(state.linterConfig, {
          ...state.linterConfig.pending,
          [action.ruleId]: { ...state.linterConfig.pending[action.ruleId], ...action.patch },
        }),
      };
    case "SET_MODULES":
      return { ...state, modules: action.modules };
    case "TOGGLE_MODULE_EXCLUSION": {
      const current = state.excludedModules.pending;
      const next = current.includes(action.moduleName)
        ? current.filter((m) => m !== action.moduleName)
        : [...current, action.moduleName];
      return { ...state, excludedModules: editPending(state.excludedModules, next) };
    }
    case "SET_EXCLUDED_MODULES":
      return { ...state, excludedModules: editPending(state.excludedModules, action.modules) };
    case "SET_MARKETPLACE_MODULES_EXCLUDED": {
      const marketplaceNames = state.modules.filter((m) => m.fromMarketplace).map((m) => m.name);
      const current = state.excludedModules.pending;
      if (action.exclude) {
        const currentSet = new Set(current);
        const next = [...current, ...marketplaceNames.filter((n) => !currentSet.has(n))];
        return { ...state, excludedModules: editPending(state.excludedModules, next) };
      } else {
        const marketplaceSet = new Set(marketplaceNames);
        return { ...state, excludedModules: editPending(state.excludedModules, current.filter((n) => !marketplaceSet.has(n))) };
      }
    }
    case "BULK_SET_RULES_ENABLED": {
      const next: Record<string, LinterConfigRule> = { ...state.linterConfig.pending };
      for (const ruleId of action.ruleIds) {
        if (action.enabled === undefined) {
          // Enabling: remove the key if it would be all-defaults to avoid phantom changes
          const existing = next[ruleId];
          if (!existing) continue;
          const { enabled: _e, ...rest } = existing;
          const effectiveSeverity = rest.severity;
          if (!effectiveSeverity || effectiveSeverity === "inherit") {
            delete next[ruleId];
          } else {
            next[ruleId] = rest;
          }
        } else {
          next[ruleId] = { ...next[ruleId], enabled: action.enabled };
        }
      }
      return { ...state, linterConfig: editPending(state.linterConfig, next) };
    }
    case "SET_RULE_SOURCES":
      return { ...state, ruleSources: action.sources };
    case "ADD_RULE_SOURCE":
      return { ...state, ruleSources: [...state.ruleSources, action.source] };
    case "REMOVE_RULE_SOURCE": {
      const { [action.id]: _removed, ...restStatus } = state.ruleSourceFetchStatus;
      return {
        ...state,
        ruleSources: state.ruleSources.filter((s) => s.id !== action.id),
        ruleSourceFetchStatus: restStatus,
      };
    }
    case "SET_RULE_SOURCE_FETCH_STATUS":
      return { ...state, ruleSourceFetchStatus: { ...state.ruleSourceFetchStatus, [action.id]: action.status } };
    case "SET_MXCLI_INFO":
      return { ...state, mxcliInfo: action.info, mxcliDownloading: false, mxcliDownloadProgress: null };
    case "MXCLI_DOWNLOAD_STARTED":
      return { ...state, mxcliDownloading: true, mxcliDownloadProgress: 0 };
    case "MXCLI_DOWNLOAD_PROGRESS":
      return { ...state, mxcliDownloadProgress: action.percent };
    default:
      return state;
  }
}

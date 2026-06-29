import type { BaselineEntry, Exclusion, LinterConfigRule, ModuleInfo, MxcliInfo, RuleSource, RuleSourceFetchStatus, ScanMeta, ScanProgress, Violation } from "../types";


export interface AppState {
  violations: Violation[];
  ruleNames: Record<string, string>;
  ruleCategories: Record<string, string>;
  meta: ScanMeta;
  appStoreModules: Set<string>;
  exclusions: Exclusion[];
  scanStreaming: boolean;
  scanProgress: ScanProgress | null;
  scanIncomplete: boolean;
  scanStartMs: number;
  categoryEnabled: Set<string>;
  severityEnabled: Set<string>;
  moduleFilterEnabled: Set<string>;
  appStoreVisible: boolean;
  uncommittedDocumentIds: Set<string>;
  uncommittedAvailable: boolean;
  uncommittedFilterActive: boolean;
  showExcluded: boolean;
  filterQuery: string;
  toast: { text: string; isError: boolean; id: number } | null;
  logoDataUri: string;
  settingsVisible: boolean;
  scanHasRun: boolean;
  scanCompletedAt: number | null;
  settingsActiveTab: "modules" | "rules" | "configuration" | "sources" | "about";
  mxcliInfo: MxcliInfo | null;
  mxcliDownloading: boolean;
  mxcliDownloadProgress: number | null;
  linterConfig: Record<string, LinterConfigRule>;
  pendingConfig: Record<string, LinterConfigRule>;
  modules: ModuleInfo[];
  savedExcludedModules: string[];
  pendingExcludedModules: string[];
  baselines: BaselineEntry[];
  selectedBaselineId: string | null;
  baselineFilter: "new" | "fixed" | null;
  ruleSources: RuleSource[];
  ruleSourceFetchStatus: Record<string, RuleSourceFetchStatus>;
}

export type AppAction =
  | { type: "SCAN_FAST_BATCH"; payload: ScanFastPayload }
  | { type: "SCAN_DESCRIBE_BATCH"; payload: ScanDescribePayload }
  | { type: "SCAN_FINISHED"; completedAt?: number }
  | { type: "SCAN_ERROR"; error: string }
  | { type: "SET_EXCLUSIONS"; exclusions: Exclusion[] }
  | { type: "TOGGLE_CATEGORY"; category: string }
  | { type: "TOGGLE_SEVERITY"; severity: string }
  | { type: "TOGGLE_MODULE_FILTER"; moduleName: string }
  | { type: "TOGGLE_APPSTORE" }
  | { type: "SET_UNCOMMITTED_DOCUMENTS"; documentIds: string[]; available: boolean }
  | { type: "TOGGLE_UNCOMMITTED_FILTER" }
  | { type: "TOGGLE_SHOW_EXCLUDED" }
  | { type: "SET_FILTER_QUERY"; query: string }
  | { type: "RESET_FILTERS" }
  | { type: "SHOW_TOAST"; text: string; isError: boolean }
  | { type: "CLEAR_TOAST" }
  | { type: "SET_LOGO"; dataUri: string }
  | { type: "SET_RULES_CATALOG"; ruleNames: Record<string, string>; ruleCategories: Record<string, string> }
  | { type: "SHOW_SETTINGS" }
  | { type: "HIDE_SETTINGS" }
  | { type: "SET_LINTER_CONFIG"; config: Record<string, LinterConfigRule>; excludedModules?: string[] }
  | { type: "UPDATE_RULE_SETTING"; ruleId: string; patch: Partial<LinterConfigRule> }
  | { type: "SET_MODULES"; modules: ModuleInfo[] }
  | { type: "TOGGLE_MODULE_EXCLUSION"; moduleName: string }
  | { type: "SET_EXCLUDED_MODULES"; modules: string[] }
  | { type: "SET_MARKETPLACE_MODULES_EXCLUDED"; exclude: boolean }
  | { type: "BULK_SET_RULES_ENABLED"; ruleIds: string[]; enabled: boolean | undefined }
  | { type: "SET_SETTINGS_TAB"; tab: "modules" | "rules" | "configuration" | "sources" | "about" }
  | { type: "SET_RULE_SOURCES"; sources: RuleSource[] }
  | { type: "ADD_RULE_SOURCE"; source: RuleSource }
  | { type: "REMOVE_RULE_SOURCE"; id: string }
  | { type: "SET_RULE_SOURCE_FETCH_STATUS"; id: string; status: RuleSourceFetchStatus }
  | { type: "SET_MXCLI_INFO"; info: MxcliInfo }
  | { type: "MXCLI_DOWNLOAD_STARTED" }
  | { type: "MXCLI_DOWNLOAD_PROGRESS"; percent: number }
  | { type: "SET_BASELINES"; baselines: BaselineEntry[] }
  | { type: "SELECT_BASELINE"; id: string | null }
  | { type: "SET_BASELINE_FILTER"; filter: "new" | "fixed" | null };

export interface ScanFastPayload {
  violations?: Violation[];
  ruleNames?: Record<string, string>;
  ruleCategories?: Record<string, string>;
  workingDirectory?: string;
  rawCount?: number;
  exitCode?: number;
  appStoreModules?: string[];
  progress?: { processed: number; total: number; label: string; requested?: number; returned?: number };
  final?: boolean;
  ok?: boolean;
  error?: string;
}

export interface ScanDescribePayload {
  violations?: Violation[];
  progress?: { processed: number; total: number; label: string; requested?: number; returned?: number };
  final?: boolean;
}

export const initialState: AppState = {
  violations: [],
  ruleNames: {},
  ruleCategories: {},
  meta: {},
  appStoreModules: new Set(),
  exclusions: [],
  scanStreaming: false,
  scanProgress: null,
  scanIncomplete: false,
  scanStartMs: 0,
  categoryEnabled: new Set(),
  severityEnabled: new Set(),
  moduleFilterEnabled: new Set(),
  appStoreVisible: true,
  uncommittedDocumentIds: new Set<string>(),
  uncommittedAvailable: false,
  uncommittedFilterActive: false,
  showExcluded: false,
  filterQuery: "",
  toast: null,
  logoDataUri: "",
  settingsVisible: false,
  scanHasRun: false,
  scanCompletedAt: null,
  settingsActiveTab: "modules",
  linterConfig: {},
  pendingConfig: {},
  modules: [],
  savedExcludedModules: [],
  pendingExcludedModules: [],
  baselines: [],
  selectedBaselineId: null,
  baselineFilter: null,
  mxcliInfo: null,
  mxcliDownloading: false,
  mxcliDownloadProgress: null,
  ruleSources: [],
  ruleSourceFetchStatus: {},
};

function toggleSet(set: Set<string>, key: string): Set<string> {
  const next = new Set(set);
  if (next.has(key)) next.delete(key); else next.add(key);
  return next;
}

let toastId = 0;

export function appReducer(state: AppState, action: AppAction): AppState {
  switch (action.type) {
    case "SCAN_FAST_BATCH": {
      const p = action.payload;
      const newViolations = p.violations ?? [];
      const progress = p.progress
        ? { processed: p.progress.processed, total: p.progress.total, label: p.progress.label, requested: p.progress.requested, returned: p.progress.returned }
        : state.scanProgress;
      const scanIncomplete = state.scanIncomplete || (!!p.progress?.requested && (p.progress.returned ?? 0) < (p.progress.requested ?? 0));
      return {
        ...state,
        violations: newViolations,
        ruleNames: { ...state.ruleNames, ...(p.ruleNames ?? {}) },
        ruleCategories: { ...state.ruleCategories, ...(p.ruleCategories ?? {}) },
        meta: { workingDirectory: p.workingDirectory, rawCount: p.rawCount, exitCode: p.exitCode },
        appStoreModules: new Set(p.appStoreModules ?? []),
        scanStreaming: p.final === false,
        scanProgress: progress,
        scanIncomplete,
      };
    }
    case "SCAN_DESCRIBE_BATCH": {
      const p = action.payload;
      const progress = p.progress
        ? { processed: p.progress.processed, total: p.progress.total, label: p.progress.label, requested: p.progress.requested, returned: p.progress.returned }
        : state.scanProgress;
      const scanIncomplete = state.scanIncomplete || (!!p.progress?.requested && (p.progress.returned ?? 0) < (p.progress.requested ?? 0));
      return {
        ...state,
        violations: state.violations.concat(p.violations ?? []),
        scanStreaming: p.final === false,
        scanProgress: progress,
        scanIncomplete,
      };
    }
    case "SCAN_FINISHED": {
      const autoSelect = state.baselines.length > 0 ? (state.selectedBaselineId ?? state.baselines[0].id) : null;
      return { ...state, scanStreaming: false, scanProgress: null, scanHasRun: true, scanCompletedAt: action.completedAt ?? state.scanCompletedAt, selectedBaselineId: autoSelect };
    }
    case "SCAN_ERROR":
      return { ...state, scanStreaming: false, scanProgress: null, toast: { text: action.error, isError: true, id: ++toastId } };
    case "SET_EXCLUSIONS":
      return { ...state, exclusions: action.exclusions };
    case "TOGGLE_CATEGORY":
      return { ...state, categoryEnabled: toggleSet(state.categoryEnabled, action.category) };
    case "TOGGLE_SEVERITY":
      return { ...state, severityEnabled: toggleSet(state.severityEnabled, action.severity) };
    case "TOGGLE_MODULE_FILTER":
      return { ...state, moduleFilterEnabled: toggleSet(state.moduleFilterEnabled, action.moduleName) };
    case "TOGGLE_APPSTORE":
      return { ...state, appStoreVisible: !state.appStoreVisible };
    case "SET_UNCOMMITTED_DOCUMENTS":
      return {
        ...state,
        uncommittedDocumentIds: new Set(action.documentIds.map(id => id.toLowerCase())),
        uncommittedAvailable: action.available,
        uncommittedFilterActive: action.available ? state.uncommittedFilterActive : false,
      };
    case "TOGGLE_UNCOMMITTED_FILTER":
      return { ...state, uncommittedFilterActive: !state.uncommittedFilterActive };
    case "TOGGLE_SHOW_EXCLUDED":
      return { ...state, showExcluded: !state.showExcluded };
    case "SET_FILTER_QUERY":
      return { ...state, filterQuery: action.query };
    case "RESET_FILTERS":
      return {
        ...state,
        categoryEnabled: new Set(),
        severityEnabled: new Set(),
        moduleFilterEnabled: new Set(),
        filterQuery: "",
        uncommittedFilterActive: false,
        baselineFilter: null,
      };
    case "SHOW_TOAST":
      return { ...state, toast: { text: action.text, isError: action.isError, id: ++toastId } };
    case "CLEAR_TOAST":
      return { ...state, toast: null };
    case "SET_LOGO":
      return { ...state, logoDataUri: action.dataUri };
    case "SET_RULES_CATALOG":
      return {
        ...state,
        ruleNames: { ...state.ruleNames, ...action.ruleNames },
        ruleCategories: { ...state.ruleCategories, ...action.ruleCategories },
      };
    case "SHOW_SETTINGS":
      return {
        ...state,
        settingsVisible: true,
        pendingConfig: { ...state.linterConfig },
        pendingExcludedModules: [...state.savedExcludedModules],
      };
    case "HIDE_SETTINGS":
      return { ...state, settingsVisible: false, pendingConfig: {}, pendingExcludedModules: [], mxcliDownloading: false, mxcliDownloadProgress: null };
    case "SET_SETTINGS_TAB":
      return { ...state, settingsActiveTab: action.tab };
    case "SET_LINTER_CONFIG":
      return {
        ...state,
        linterConfig: action.config,
        pendingConfig: { ...action.config },
        savedExcludedModules: action.excludedModules ?? [],
        pendingExcludedModules: action.excludedModules ?? [],
      };
    case "UPDATE_RULE_SETTING":
      return {
        ...state,
        pendingConfig: {
          ...state.pendingConfig,
          [action.ruleId]: { ...state.pendingConfig[action.ruleId], ...action.patch },
        },
      };
    case "SET_MODULES":
      return { ...state, modules: action.modules };
    case "TOGGLE_MODULE_EXCLUSION": {
      const next = state.pendingExcludedModules.includes(action.moduleName)
        ? state.pendingExcludedModules.filter((m) => m !== action.moduleName)
        : [...state.pendingExcludedModules, action.moduleName];
      return { ...state, pendingExcludedModules: next };
    }
    case "SET_EXCLUDED_MODULES":
      return { ...state, pendingExcludedModules: action.modules };
    case "SET_MARKETPLACE_MODULES_EXCLUDED": {
      const marketplaceNames = state.modules.filter((m) => m.fromMarketplace).map((m) => m.name);
      if (action.exclude) {
        const currentSet = new Set(state.pendingExcludedModules);
        const next = [...state.pendingExcludedModules, ...marketplaceNames.filter((n) => !currentSet.has(n))];
        return { ...state, pendingExcludedModules: next };
      } else {
        const marketplaceSet = new Set(marketplaceNames);
        return { ...state, pendingExcludedModules: state.pendingExcludedModules.filter((n) => !marketplaceSet.has(n)) };
      }
    }
    case "BULK_SET_RULES_ENABLED": {
      const next: Record<string, LinterConfigRule> = { ...state.pendingConfig };
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
      return { ...state, pendingConfig: next };
    }
    case "SET_BASELINES": {
      const ids = new Set(action.baselines.map((b) => b.id));
      const selectedId = state.selectedBaselineId && ids.has(state.selectedBaselineId)
        ? state.selectedBaselineId
        : action.baselines[0]?.id ?? null;
      return { ...state, baselines: action.baselines, selectedBaselineId: selectedId };
    }
    case "SELECT_BASELINE":
      return { ...state, selectedBaselineId: action.id, baselineFilter: null };
    case "SET_BASELINE_FILTER":
      return { ...state, baselineFilter: action.filter };
    case "SET_MXCLI_INFO":
      return { ...state, mxcliInfo: action.info, mxcliDownloading: false, mxcliDownloadProgress: null };
    case "MXCLI_DOWNLOAD_STARTED":
      return { ...state, mxcliDownloading: true, mxcliDownloadProgress: 0 };
    case "MXCLI_DOWNLOAD_PROGRESS":
      return { ...state, mxcliDownloadProgress: action.percent };
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
      return {
        ...state,
        ruleSourceFetchStatus: { ...state.ruleSourceFetchStatus, [action.id]: action.status },
      };
    default:
      return state;
  }
}

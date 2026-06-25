import { ORIGINS } from "../constants";
import type { Exclusion, ManualAnswer, ScanMeta, ScanProgress, Violation } from "../types";

export interface AppState {
  violations: Violation[];
  ruleNames: Record<string, string>;
  ruleCategories: Record<string, string>;
  meta: ScanMeta;
  appStoreModules: Set<string>;
  exclusions: Exclusion[];
  manualAnswers: ManualAnswer[];
  scanStreaming: boolean;
  scanProgress: ScanProgress | null;
  scanIncomplete: boolean;
  scanStartMs: number;
  categoryEnabled: Set<string>;
  severityEnabled: Set<string>;
  originEnabled: Set<string>;
  appStoreVisible: boolean;
  showExcluded: boolean;
  showAnswered: boolean;
  lastDeepScan: boolean;
  filterQuery: string;
  toast: { text: string; isError: boolean; id: number } | null;
  logoDataUri: string;
}

export type AppAction =
  | { type: "SCAN_FAST_BATCH"; payload: ScanFastPayload }
  | { type: "SCAN_DESCRIBE_BATCH"; payload: ScanDescribePayload }
  | { type: "SCAN_FINISHED" }
  | { type: "SCAN_ERROR"; error: string }
  | { type: "SET_EXCLUSIONS"; exclusions: Exclusion[] }
  | { type: "SET_MANUAL_ANSWERS"; answers: ManualAnswer[] }
  | { type: "TOGGLE_CATEGORY"; category: string }
  | { type: "TOGGLE_SEVERITY"; severity: string }
  | { type: "TOGGLE_ORIGIN"; origin: string }
  | { type: "SET_ORIGIN_ENABLED"; origin: string; enabled: boolean }
  | { type: "TOGGLE_APPSTORE" }
  | { type: "TOGGLE_SHOW_EXCLUDED" }
  | { type: "TOGGLE_SHOW_ANSWERED" }
  | { type: "SET_FILTER_QUERY"; query: string }
  | { type: "RESET_FILTERS" }
  | { type: "SHOW_TOAST"; text: string; isError: boolean }
  | { type: "CLEAR_TOAST" }
  | { type: "SET_LOGO"; dataUri: string };

export interface ScanFastPayload {
  violations?: Violation[];
  ruleNames?: Record<string, string>;
  ruleCategories?: Record<string, string>;
  workingDirectory?: string;
  rawCount?: number;
  exitCode?: number;
  appStoreModules?: string[];
  deepScan?: boolean;
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
  manualAnswers: [],
  scanStreaming: false,
  scanProgress: null,
  scanIncomplete: false,
  scanStartMs: 0,
  categoryEnabled: new Set(),
  severityEnabled: new Set(),
  originEnabled: new Set(ORIGINS.map((o) => o.key)),
  appStoreVisible: true,
  showExcluded: false,
  showAnswered: false,
  lastDeepScan: true,
  filterQuery: "",
  toast: null,
  logoDataUri: "",
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
      const newViolations = state.violations
        .filter((v) => v.kind !== "acr" && v.kind !== "mxcli")
        .concat(p.violations ?? []);
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
        lastDeepScan: !!p.deepScan,
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
    case "SCAN_FINISHED":
      return { ...state, scanStreaming: false, scanProgress: null };
    case "SCAN_ERROR":
      return { ...state, scanStreaming: false, scanProgress: null, toast: { text: action.error, isError: true, id: ++toastId } };
    case "SET_EXCLUSIONS":
      return { ...state, exclusions: action.exclusions };
    case "SET_MANUAL_ANSWERS":
      return { ...state, manualAnswers: action.answers };
    case "TOGGLE_CATEGORY":
      return { ...state, categoryEnabled: toggleSet(state.categoryEnabled, action.category) };
    case "TOGGLE_SEVERITY":
      return { ...state, severityEnabled: toggleSet(state.severityEnabled, action.severity) };
    case "TOGGLE_ORIGIN":
      return { ...state, originEnabled: toggleSet(state.originEnabled, action.origin) };
    case "SET_ORIGIN_ENABLED": {
      const next = new Set(state.originEnabled);
      if (action.enabled) next.add(action.origin); else next.delete(action.origin);
      return { ...state, originEnabled: next };
    }
    case "TOGGLE_APPSTORE":
      return { ...state, appStoreVisible: !state.appStoreVisible };
    case "TOGGLE_SHOW_EXCLUDED":
      return { ...state, showExcluded: !state.showExcluded };
    case "TOGGLE_SHOW_ANSWERED":
      return { ...state, showAnswered: !state.showAnswered };
    case "SET_FILTER_QUERY":
      return { ...state, filterQuery: action.query };
    case "RESET_FILTERS":
      return {
        ...state,
        categoryEnabled: new Set(),
        severityEnabled: new Set(),
        originEnabled: new Set(ORIGINS.map((o) => o.key)),
        filterQuery: "",
      };
    case "SHOW_TOAST":
      return { ...state, toast: { text: action.text, isError: action.isError, id: ++toastId } };
    case "CLEAR_TOAST":
      return { ...state, toast: null };
    case "SET_LOGO":
      return { ...state, logoDataUri: action.dataUri };
    default:
      return state;
  }
}

import type { ScanMeta, ScanProgress, Violation } from "../../types";

export interface ScanState {
  violations: Violation[];
  ruleNames: Record<string, string>;
  ruleCategories: Record<string, string>;
  ruleDescriptions: Record<string, string>;
  ruleStarContent: Record<string, string>;
  meta: ScanMeta;
  appStoreModules: Set<string>;
  scanStreaming: boolean;
  scanProgress: ScanProgress | null;
  scanIncomplete: boolean;
  scanStartMs: number;
  scanHasRun: boolean;
  scanCompletedAt: number | null;
}

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

export type ScanAction =
  | { type: "SCAN_FAST_BATCH"; payload: ScanFastPayload }
  | { type: "SCAN_DESCRIBE_BATCH"; payload: ScanDescribePayload }
  | { type: "SCAN_FINISHED"; completedAt?: number }
  | { type: "SCAN_ERROR"; error: string }
  | { type: "SET_RULES_CATALOG"; ruleNames: Record<string, string>; ruleCategories: Record<string, string>; ruleDescriptions?: Record<string, string>; ruleStarContent?: Record<string, string> };

export const initialScanState: ScanState = {
  violations: [],
  ruleNames: {},
  ruleCategories: {},
  ruleDescriptions: {},
  ruleStarContent: {},
  meta: {},
  appStoreModules: new Set(),
  scanStreaming: false,
  scanProgress: null,
  scanIncomplete: false,
  scanStartMs: 0,
  scanHasRun: false,
  scanCompletedAt: null,
};

export function scanReducer(state: ScanState, action: ScanAction): ScanState {
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
    case "SCAN_FINISHED":
      return { ...state, scanStreaming: false, scanProgress: null, scanHasRun: true, scanCompletedAt: action.completedAt ?? state.scanCompletedAt };
    case "SCAN_ERROR":
      return { ...state, scanStreaming: false, scanProgress: null };
    case "SET_RULES_CATALOG":
      return {
        ...state,
        ruleNames: { ...state.ruleNames, ...action.ruleNames },
        ruleCategories: { ...state.ruleCategories, ...action.ruleCategories },
        ruleDescriptions: { ...state.ruleDescriptions, ...(action.ruleDescriptions ?? {}) },
        ruleStarContent: { ...state.ruleStarContent, ...(action.ruleStarContent ?? {}) },
      };
    default:
      return state;
  }
}

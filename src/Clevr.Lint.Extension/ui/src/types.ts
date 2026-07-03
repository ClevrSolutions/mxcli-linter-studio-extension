export type ViolationKind = "mxcli";

export interface Violation {
  ruleId: string;
  kind: ViolationKind;
  category: string;
  severity: string;
  documentType: string;
  documentQualifiedName: string;
  elementName?: string;
  documentId?: string;
  reason: string;
  suggestion?: string;
  fingerprint: string;
  documentationUrl?: string;
}

export interface Exclusion {
  fingerprint: string;
  ruleId: string;
  documentQualifiedName: string;
  elementName: string;
  reason: string;
  excludedBy?: string;
  date: string;
}

export interface ScanMeta {
  workingDirectory?: string;
  rawCount?: number;
  exitCode?: number;
}

export interface ScanProgress {
  processed: number;
  total: number;
  label: string;
  requested?: number;
  returned?: number;
}

export interface ExclusionEntry {
  exclusion: Exclusion;
  violations: Violation[];
  isStale: boolean;
}

export interface ExclusionRuleGroup {
  ruleId: string;
  entries: ExclusionEntry[];
  findingCount: number;
  staleEntries: number;
}

export interface ExcludedView {
  groups: ExclusionRuleGroup[];
  matchedCount: number;
  staleCount: number;
}

export interface RuleGroup {
  rule: Violation;
  items: Violation[];
}

export interface ModuleInfo {
  name: string;
  fromMarketplace: boolean;
  appStoreVersion: string | null;
}

export interface LinterConfigRule {
  enabled?: boolean;
  severity?: string;
}

export interface LinterConfig {
  rules: Record<string, LinterConfigRule>;
  excludedModules: string[];
}

export interface BaselineEntry {
  id: string;
  savedAt: string; // ISO 8601 from DateTimeOffset
  gitRevision: string | null;
  violations: Violation[];
  excludedModules?: string[] | null;
  disabledRuleIds?: string[] | null;
}

export interface MxcliInfo {
  source: "path" | "clevrLint" | "custom" | "notFound";
  resolvedPath: string | null;
  version: string | null;
  found: boolean;
  downloadedAt: string | null;
}

export interface RuleSource {
  id: string;
  url: string;
  label?: string;
}

export interface RuleSourceFetchStatus {
  fetching: boolean;
  progress?: string;
  error?: string;
  lastResult?: { copied: number; skipped: number; failed: number; errors: string[] };
  lastDeleteResult?: { deleted: number; notFound: number; failed: number; errors: string[] };
}

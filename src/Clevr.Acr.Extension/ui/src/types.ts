export type ViolationKind = "acr" | "mxcli" | "manual";

export interface ManualCheckDef {
  id: string;
  category: string;
  severity: string;
  question: string;
  context: string;
}

export interface ManualAnswer {
  id: string;
  answer: string;
  note: string;
  answeredBy?: string;
  excludedBy?: string;
  date: string;
}

export interface ManualCheckState {
  def: ManualCheckDef;
  answer: ManualAnswer | null;
  status: "unanswered" | "no" | "expired" | "valid-yes";
  open: boolean;
  recheckDate?: string;
  recheckInDays?: number;
  daysOverdue?: number;
}

export interface Violation {
  ruleId: string;
  kind: ViolationKind;
  source: string;
  acrCode?: string;
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
  manual?: ManualCheckState;
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

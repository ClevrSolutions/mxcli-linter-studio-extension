import type { ManualCheckDef } from "./types";

export const LINT_CATEGORIES = [
  "Project hygiene",
  "Maintainability",
  "Performance",
  "Architecture",
  "Reliability",
  "Security",
] as const;

export type LintCategory = (typeof LINT_CATEGORIES)[number];

export const SEVERITY_ORDER = [
  "Minor", "Major", "Critical", "Blocker",
  "error", "warning", "info", "hint",
];

export const MANUAL_CHECK_EXPIRY_DAYS = 30;

export const MANUAL_CHECKS: ManualCheckDef[] = [
  {
    id: "MC-PERF-RECOMMENDER",
    category: "Performance",
    severity: "Major",
    question:
      "Have you reviewed the Best Practice Recommender (Performance) in Studio Pro and resolved or consciously assessed the relevant findings?",
    context:
      "The Best Practice Recommender currently covers performance only and is not machine-readable via mxcli or the Extensibility API, so it cannot be checked automatically — hence this manual check.",
  },
];

export const MXCLI_CATEGORY_TO_LINT: Record<string, string> = {
  security: "Security",
  naming: "Project hygiene",
  style: "Project hygiene",
  quality: "Maintainability",
  complexity: "Maintainability",
  maintainability: "Maintainability",
  design: "Architecture",
  architecture: "Architecture",
  correctness: "Reliability",
  performance: "Performance",
};

export const GENERIC_CATEGORY_FALLBACK = "Maintainability";

export const GENERIC_PREFIX_FALLBACK: Record<string, string> = {
  SEC: "Security",
  ARCH: "Architecture",
  DESIGN: "Architecture",
  PERF: "Performance",
};

export const ORIGINS = [
  { key: "lint" as const, label: "Lint" },
  { key: "mxcli" as const, label: "MxCLI" },
  { key: "manual" as const, label: "Manual checks" },
];

export const MAIA_RULE_CAP = 50;

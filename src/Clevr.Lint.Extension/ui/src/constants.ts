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

export const AI_RULE_CAP = 50;

export const KNOWN_RULE_SOURCES: { label: string; url: string }[] = [
  { label: "Mendix Default", url: "https://github.com/mendixlabs/mxcli/tree/main/.claude/lint-rules" },
  { label: "CLEVR ACR", url: "https://github.com/ClevrSolutions/mxcli-linter-studio-extension/tree/main/rules" },
];

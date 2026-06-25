import { MAIA_RULE_CAP } from "../constants";
import type { Violation } from "../types";
import { displayCategory } from "./filters";
import { ruleLabelFor } from "./grouping";

function sourceEngineLabel(v: Violation): string {
  if (v.kind === "lint") return "CLEVR Lint";
  return "mxcli (Mendix lint)";
}

function maiaFindingBlock(n: number, v: Violation): string {
  const where = v.elementName
    ? `${v.documentType} ${v.documentQualifiedName} › ${v.elementName}`
    : `${v.documentType} ${v.documentQualifiedName}`;
  let s = `${n}. ${where}\n   Issue: ${v.reason || "(no description)"}`;
  if (v.suggestion) s += `\n   Suggested fix: ${v.suggestion}`;
  return s;
}

export function maiaPromptForRule(rule: Violation, items: Violation[], ruleNames: Record<string, string>, ruleCategories: Record<string, string>): string {
  const label = ruleLabelFor(rule, ruleNames);
  const n = items.length;
  const lines = [
    `I have ${n} code-quality finding${n === 1 ? "" : "s"} for rule ${label} in my Mendix app, please help me resolve them.`,
    "",
    `Rule: ${label}`,
    `Category: ${displayCategory(rule, ruleCategories)}`,
    `Severity: ${rule.severity || "n/a"}`,
    `Source engine: ${sourceEngineLabel(rule)}`,
  ];
  if (rule.documentationUrl) lines.push(`Documentation: ${rule.documentationUrl}`);
  lines.push("", `Findings (${n}):`);
  items.slice(0, MAIA_RULE_CAP).forEach((v, i) => lines.push(maiaFindingBlock(i + 1, v)));
  if (n > MAIA_RULE_CAP) lines.push(`... and ${n - MAIA_RULE_CAP} more`);
  lines.push("", "Please explain how to fix these and, where possible, give the concrete steps in Mendix Studio Pro.");
  return lines.join("\n");
}

export function maiaPromptForFinding(rule: Violation, v: Violation, ruleNames: Record<string, string>, ruleCategories: Record<string, string>): string {
  const where = v.elementName
    ? `${v.documentType} ${v.documentQualifiedName} › ${v.elementName}`
    : `${v.documentType} ${v.documentQualifiedName}`;
  const lines = [
    "I have a code-quality finding in my Mendix app, please help me resolve it.",
    "",
    `Rule: ${ruleLabelFor(rule, ruleNames)}`,
    `Category: ${displayCategory(v, ruleCategories)}`,
    `Severity: ${v.severity || "n/a"}`,
    `Source engine: ${sourceEngineLabel(v)}`,
    `Document: ${where}`,
    `Issue: ${v.reason || "(no description)"}`,
  ];
  if (v.suggestion) lines.push(`Suggested fix: ${v.suggestion}`);
  if (v.documentationUrl) lines.push(`Documentation: ${v.documentationUrl}`);
  lines.push("", "Please explain how to fix this and, where possible, give the concrete steps in Mendix Studio Pro.");
  return lines.join("\n");
}

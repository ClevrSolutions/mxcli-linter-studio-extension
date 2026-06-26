import { AI_RULE_CAP } from "../constants";
import type { Violation } from "../types";
import { displayCategory } from "./filters";
import { ruleLabelFor } from "./grouping";


function aiFindingBlock(n: number, v: Violation): string {
  const where = v.elementName
    ? `${v.documentType} ${v.documentQualifiedName} › ${v.elementName}`
    : `${v.documentType} ${v.documentQualifiedName}`;
  let s = `${n}. ${where}\n   Issue: ${v.reason || "(no description)"}`;
  if (v.suggestion) s += `\n   Suggested fix: ${v.suggestion}`;
  return s;
}

export function aiPromptForRule(rule: Violation, items: Violation[], ruleNames: Record<string, string>, ruleCategories: Record<string, string>): string {
  const label = ruleLabelFor(rule, ruleNames);
  const n = items.length;
  const lines = [
    `I have ${n} code-quality finding${n === 1 ? "" : "s"} for rule ${label} in my Mendix app, please help me resolve them.`,
    "",
    `Rule: ${label}`,
    `Category: ${displayCategory(rule, ruleCategories)}`,
    `Severity: ${rule.severity || "n/a"}`,
  ];
  if (rule.documentationUrl) lines.push(`Documentation: ${rule.documentationUrl}`);
  lines.push("", `Findings (${n}):`);
  items.slice(0, AI_RULE_CAP).forEach((v, i) => lines.push(aiFindingBlock(i + 1, v)));
  if (n > AI_RULE_CAP) lines.push(`... and ${n - AI_RULE_CAP} more`);
  lines.push("", "Please explain how to fix these and, where possible, give the concrete steps in Mendix Studio Pro.");
  return lines.join("\n");
}

export function aiPromptForFinding(rule: Violation, v: Violation, ruleNames: Record<string, string>, ruleCategories: Record<string, string>): string {
  const where = v.elementName
    ? `${v.documentType} ${v.documentQualifiedName} › ${v.elementName}`
    : `${v.documentType} ${v.documentQualifiedName}`;
  const lines = [
    "I have a code-quality finding in my Mendix app, please help me resolve it.",
    "",
    `Rule: ${ruleLabelFor(rule, ruleNames)}`,
    `Category: ${displayCategory(v, ruleCategories)}`,
    `Severity: ${v.severity || "n/a"}`,
    `Document: ${where}`,
    `Issue: ${v.reason || "(no description)"}`,
  ];
  if (v.suggestion) lines.push(`Suggested fix: ${v.suggestion}`);
  if (v.documentationUrl) lines.push(`Documentation: ${v.documentationUrl}`);
  lines.push("", "Please explain how to fix this and, where possible, give the concrete steps in Mendix Studio Pro.");
  return lines.join("\n");
}

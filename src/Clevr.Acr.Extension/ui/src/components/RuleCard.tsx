import { useState } from "react";
import { useAppDispatch, useAppState } from "../context/AppContext";
import type { Violation } from "../types";
import { copyToClipboard } from "../hooks/useClipboard";
import { maiaPromptForRule } from "../utils/maia";
import { originBadge } from "../utils/origins";
import { previewText, ruleName } from "../utils/grouping";
import { ExcludeRuleDialog } from "./dialogs/ExcludeRuleDialog";
import { ViolationInstance } from "./ViolationInstance";

interface Props {
  rule: Violation;
  items: Violation[];
  interactive?: boolean;
}

export function RuleCard({ rule, items, interactive = true }: Props) {
  const state = useAppState();
  const dispatch = useAppDispatch();
  const [showExcludeRule, setShowExcludeRule] = useState(false);
  const sev = rule.severity ?? "";
  const isAcr = rule.kind === "acr";
  const name = ruleName(rule, state.ruleNames);
  const preview = previewText(items[0]?.reason);

  async function copyMaia(e: React.MouseEvent) {
    e.preventDefault();
    e.stopPropagation();
    const prompt = maiaPromptForRule(rule, items, state.ruleNames, state.ruleCategories);
    const ok = await copyToClipboard(prompt);
    dispatch({
      type: "SHOW_TOAST",
      text: ok ? "Maia prompt copied — paste it into Maia" : "Could not copy the Maia prompt to the clipboard.",
      isError: !ok,
    });
  }

  function openExcludeRule(e: React.MouseEvent) {
    e.preventDefault();
    e.stopPropagation();
    setShowExcludeRule(true);
  }

  return (
    <>
      <details className={`acr-rule sevbar-${sev}`}>
        <summary>
          <span className={`sev sev-${sev}`}>{sev}</span>
          <span className={"origin-badge" + (isAcr ? " origin-acr" : "")}>{originBadge(rule)}</span>
          <span className="acr-ruleid">{rule.ruleId}</span>
          {name && <span className="acr-acrcode">{name}</span>}
          {preview && <span className="acr-rule-preview">{preview}</span>}
          <span className="acr-rule-count">{items.length} improvements</span>
          {interactive && (
            <>
              <button type="button" className="acr-maia-btn" title="Generate an English prompt for Maia and copy it to the clipboard" onClick={copyMaia}>
                Copy Maia prompt
              </button>
              <button type="button" className="acr-exclude-rule-btn" title="Exclude all findings for this rule with one reason" onClick={openExcludeRule}>
                Exclude rule
              </button>
            </>
          )}
        </summary>
        <div className="acr-rule-body">
          {items.map((v, i) => (
            <ViolationInstance key={v.fingerprint + i} rule={rule} v={v} interactive={interactive} />
          ))}
        </div>
      </details>
      {showExcludeRule && <ExcludeRuleDialog rule={rule} items={items} onClose={() => setShowExcludeRule(false)} />}
    </>
  );
}

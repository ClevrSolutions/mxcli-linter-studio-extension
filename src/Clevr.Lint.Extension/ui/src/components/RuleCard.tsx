import { useState } from "react";
import { useAppDispatch, useAppState } from "../context/AppContext";
import type { Violation } from "../types";
import { copyToClipboard } from "../hooks/useClipboard";
import { aiPromptForRule } from "../utils/ai";
import { previewText, ruleName } from "../utils/grouping";
import { ExcludeRuleDialog } from "./dialogs/ExcludeRuleDialog";
import { ViolationInstance } from "./ViolationInstance";
import { btnPillAccent, btnPillMuted } from "../utils/classes";

interface Props {
  rule: Violation;
  items: Violation[];
  interactive?: boolean;
  isFixed?: boolean;
}

export function RuleCard({ rule, items, interactive = true, isFixed = false }: Props) {
  const state = useAppState();
  const dispatch = useAppDispatch();
  const [showExcludeRule, setShowExcludeRule] = useState(false);
  const sev = rule.severity ?? "";
  const name = ruleName(rule, state.ruleNames);
  const preview = previewText(items[0]?.reason);

  async function copyAiPrompt(e: React.MouseEvent) {
    e.preventDefault();
    e.stopPropagation();
    const prompt = aiPromptForRule(rule, items, state.ruleNames, state.ruleCategories);
    const ok = await copyToClipboard(prompt);
    dispatch({
      type: "SHOW_TOAST",
      text: ok ? "AI prompt copied — paste it into AI chat" : "Could not copy the AI prompt to the clipboard.",
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
      <details className={`border border-clevr-border border-l-[3px] border-l-clevr-muted rounded-[6px] mb-2 bg-clevr-bg sevbar-${sev}`}>
        <summary className="cursor-pointer [list-style:revert] p-[8px_10px] flex items-center gap-2 flex-wrap">
          <span className={`sev sev-${sev}`}>{sev}</span>
          <span className="font-semibold font-mono text-[12px]">{rule.ruleId}</span>
          {name && <span className="text-clevr-muted text-[12px]">{name}</span>}
          {preview && <span className="text-clevr-muted text-[12px] truncate flex-1 min-w-0">{preview}</span>}
          <span className="ml-auto text-clevr-muted text-[12px] tabular-nums whitespace-nowrap">{items.length} improvements</span>
          {interactive && (
            <>
              <button type="button" className={btnPillAccent} title="Generate an English prompt for AI and copy it to the clipboard" onClick={copyAiPrompt}>
                Copy AI prompt
              </button>
              <button type="button" className={btnPillMuted} title="Exclude all findings for this rule with one reason" onClick={openExcludeRule}>
                Exclude rule
              </button>
            </>
          )}
        </summary>
        <div className="px-[10px] pb-2 pl-[14px]">
          {items.map((v, i) => (
            <ViolationInstance key={v.fingerprint + i} rule={rule} v={v} interactive={interactive} isFixed={isFixed} />
          ))}
        </div>
      </details>
      {showExcludeRule && <ExcludeRuleDialog rule={rule} items={items} onClose={() => setShowExcludeRule(false)} />}
    </>
  );
}

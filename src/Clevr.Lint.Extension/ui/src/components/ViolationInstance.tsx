import { useState } from "react";
import { useAppDispatch, useAppState } from "../context/AppContext";
import type { Violation } from "../types";
import { post } from "../hooks/useMessageBus";
import { copyToClipboard } from "../hooks/useClipboard";
import { aiPromptForFinding } from "../utils/ai";
import { ExcludeDialog } from "./dialogs/ExcludeDialog";

interface Props {
  rule: Violation;
  v: Violation;
  interactive?: boolean;
  isFixed?: boolean;
}

export function ViolationInstance({ rule, v, interactive = true, isFixed = false }: Props) {
  const state = useAppState();
  const dispatch = useAppDispatch();
  const [showExclude, setShowExclude] = useState(false);

  async function copyAiPrompt() {
    const prompt = aiPromptForFinding(rule, v, state.ruleNames, state.ruleCategories);
    const ok = await copyToClipboard(prompt);
    dispatch({
      type: "SHOW_TOAST",
      text: ok ? "AI prompt copied — paste it into your AI chat" : "Could not copy the AI prompt to the clipboard.",
      isError: !ok,
    });
  }

  function openDoc() {
    post("OpenDocument", {
      documentId: v.documentId ?? "",
      documentQualifiedName: v.documentQualifiedName ?? "",
      documentType: v.documentType ?? "",
    });
  }

  function openUrl(url: string, e: React.MouseEvent) {
    e.preventDefault();
    post("OpenUrl", { url });
  }

  return (
    <div className="lint-instance">
      <div
        className={"lint-doc" + (interactive && !isFixed ? " lint-doc-clickable" : "")}
        title={interactive && !isFixed ? "Open this document in Studio Pro" : undefined}
        onClick={interactive && !isFixed ? openDoc : undefined}
      >
        <span className="doctype">{v.documentType}: </span>
        <span className="qname">{v.documentQualifiedName}</span>
        {v.elementName && <><span> › </span><span className="elem">{v.elementName}</span></>}
        {isFixed && <span className="lint-fixed-badge">FIXED</span>}
        {interactive && !isFixed && <span className="lint-open-hint"> ↗ open</span>}
      </div>
      <div className="lint-reason">{v.reason}</div>
      {v.suggestion && <div className="lint-suggestion">{v.suggestion}</div>}
      {interactive && !isFixed && (
        <div className="lint-instance-actions">
          <button type="button" className="lint-ai-btn" title="Generate an English prompt for AI and copy it to the clipboard" onClick={copyAiPrompt}>
            Copy AI prompt
          </button>
          <button type="button" className="lint-exclude-btn" title="Exclude this improvement (a reason is required)" onClick={() => setShowExclude(true)}>
            Exclude
          </button>
        </div>
      )}
      {v.documentationUrl && (
        <div className="lint-docslink">
          <a
            href={v.documentationUrl}
            target="_blank"
            rel="noreferrer"
            onClick={interactive ? (e) => openUrl(v.documentationUrl!, e) : undefined}
          >
            Documentation
          </a>
        </div>
      )}
      {showExclude && <ExcludeDialog v={v} onClose={() => setShowExclude(false)} />}
    </div>
  );
}

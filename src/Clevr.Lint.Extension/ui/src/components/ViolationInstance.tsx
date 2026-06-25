import { useState } from "react";
import { useAppDispatch, useAppState } from "../context/AppContext";
import type { Violation } from "../types";
import { post } from "../hooks/useMessageBus";
import { copyToClipboard } from "../hooks/useClipboard";
import { maiaPromptForFinding } from "../utils/maia";
import { manualStateLabel } from "../utils/manualChecks";
import { ExcludeDialog } from "./dialogs/ExcludeDialog";
import { ManualCheckDialog } from "./dialogs/ManualCheckDialog";

interface Props {
  rule: Violation;
  v: Violation;
  interactive?: boolean;
}

export function ViolationInstance({ rule, v, interactive = true }: Props) {
  const state = useAppState();
  const dispatch = useAppDispatch();
  const [showExclude, setShowExclude] = useState(false);
  const [showManual, setShowManual] = useState(false);

  async function copyMaia() {
    const prompt = maiaPromptForFinding(rule, v, state.ruleNames, state.ruleCategories);
    const ok = await copyToClipboard(prompt);
    dispatch({
      type: "SHOW_TOAST",
      text: ok ? "Maia prompt copied — paste it into Maia" : "Could not copy the Maia prompt to the clipboard.",
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
      {v.kind === "manual" && v.manual ? (
        <div className="lint-manual-state">{manualStateLabel(v.manual)}</div>
      ) : (
        <div
          className={"lint-doc" + (interactive ? " lint-doc-clickable" : "")}
          title={interactive ? "Open this document in Studio Pro" : undefined}
          onClick={interactive ? openDoc : undefined}
        >
          <span className="doctype">{v.documentType}: </span>
          <span className="qname">{v.documentQualifiedName}</span>
          {v.elementName && <><span> › </span><span className="elem">{v.elementName}</span></>}
          {interactive && <span className="lint-open-hint"> ↗ open</span>}
        </div>
      )}
      <div className="lint-reason">{v.reason}</div>
      {v.suggestion && <div className="lint-suggestion">{v.suggestion}</div>}
      {interactive && (
        <div className="lint-instance-actions">
          {v.kind === "manual" && v.manual && (
            <button type="button" className="lint-answer-btn" onClick={() => setShowManual(true)}>
              {v.manual.status === "unanswered" ? "Answer" : "Re-answer"}
            </button>
          )}
          <button type="button" className="lint-maia-btn" title="Generate an English prompt for Maia and copy it to the clipboard" onClick={copyMaia}>
            Copy Maia prompt
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
      {showManual && v.manual && <ManualCheckDialog state={v.manual} onClose={() => setShowManual(false)} />}
    </div>
  );
}

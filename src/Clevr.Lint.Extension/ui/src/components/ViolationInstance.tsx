import { useState } from "react";
import { useAppDispatch, useAppState } from "../context/AppContext";
import type { Violation } from "../types";
import { post } from "../hooks/useMessageBus";
import { copyToClipboard } from "../hooks/useClipboard";
import { aiPromptForFinding } from "../utils/ai";
import { ExcludeDialog } from "./dialogs/ExcludeDialog";
import { btnPillAccent, btnPillMuted } from "../utils/classes";

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
    const prompt = aiPromptForFinding(rule, v, state.scan.ruleNames, state.scan.ruleCategories);
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

  const docClass = [
    "text-[12px] text-clevr-muted leading-relaxed",
    interactive && !isFixed ? "cursor-pointer hover:text-clevr-accent" : "",
  ].filter(Boolean).join(" ");

  return (
    <div className="border-t border-dashed border-clevr-border py-1.5">
      <div
        className={docClass}
        title={interactive && !isFixed ? "Open this document in Studio Pro" : undefined}
        onClick={interactive && !isFixed ? openDoc : undefined}
      >
        <span className="font-medium text-clevr-fg">{v.documentType}: </span>
        <span>{v.documentQualifiedName}</span>
        {v.elementName && <><span> › </span><span className="font-medium">{v.elementName}</span></>}
        {isFixed && (
          <span className="inline-block px-[7px] py-px rounded-full text-[10px] font-bold tracking-[0.03em] bg-green-action text-white whitespace-nowrap ml-2 align-middle">
            FIXED
          </span>
        )}
        {interactive && !isFixed && (
          <span className="text-clevr-accent text-[11px] ml-1">↗ open</span>
        )}
      </div>
      <div className="my-1">{v.reason}</div>
      {v.suggestion && (
        <div className="text-[12px] text-clevr-muted before:content-['→_']">{v.suggestion}</div>
      )}
      {interactive && !isFixed && (
        <div className="flex gap-2 mt-1.5">
          <button
            type="button"
            className={btnPillAccent}
            title="Generate an English prompt for AI and copy it to the clipboard"
            onClick={copyAiPrompt}
          >
            Copy AI prompt
          </button>
          <button
            type="button"
            className={btnPillMuted}
            title="Exclude this improvement (a reason is required)"
            onClick={() => setShowExclude(true)}
          >
            Exclude
          </button>
        </div>
      )}
      {v.documentationUrl && (
        <div className="mt-1 text-[12px]">
          <a
            href={v.documentationUrl}
            className="text-clevr-accent hover:underline"
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

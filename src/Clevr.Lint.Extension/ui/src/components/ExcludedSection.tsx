import { useState } from "react";
import { useAppDispatch, useAppState } from "../context/AppContext";
import { excludedView } from "../utils/exclusions";
import { previewText, ruleName } from "../utils/grouping";
import { post } from "../hooks/useMessageBus";
import { ConfirmDialog } from "./dialogs/ConfirmDialog";
import type { ExclusionRuleGroup } from "../types";
import { btnPillMuted } from "../utils/classes";

export function ExcludedSection() {
  const state = useAppState();
  const dispatch = useAppDispatch();
  const ev = excludedView(state);

  if (!ev.groups.length) return null;

  const staleNote = ev.staleCount ? ` + ${ev.staleCount} stale` : "";

  return (
    <>
      <div className="my-2">
        <button
          type="button"
          className="bg-transparent border-0 text-[12px] text-clevr-muted underline cursor-pointer p-0 hover:text-clevr-fg"
          onClick={() => dispatch({ type: "TOGGLE_SHOW_EXCLUDED" })}
        >
          {state.filters.showExcluded ? "Hide" : "Show"} excluded ({ev.matchedCount}{staleNote})
        </button>
      </div>
      {state.filters.showExcluded && (
        <div className="mt-4">
          <div className="flex items-baseline gap-2 mb-2 pb-[5px] border-b-2 border-clevr-fg">
            <h2 className="text-[15px] font-semibold m-0">Excluded improvements</h2>
            <span className="text-[12px] text-clevr-muted">
              {ev.matchedCount} excluded{ev.staleCount ? ` · ${ev.staleCount} stale` : ""}
            </span>
          </div>
          <div className="text-[12px] text-clevr-muted italic mb-4">
            Intentionally not fixed — each with a reason, shared with the team via version control.
          </div>
          {ev.groups.map((g) => (
            <ExcludedRuleGroup key={g.ruleId} g={g} />
          ))}
        </div>
      )}
    </>
  );
}

function ExcludedRuleGroup({ g }: { g: ExclusionRuleGroup }) {
  const state = useAppState();
  const dispatch = useAppDispatch();
  const [showConfirm, setShowConfirm] = useState(false);
  const name = ruleName({ ruleId: g.ruleId, kind: "mxcli", category: "", severity: "", documentType: "", documentQualifiedName: "", reason: "", fingerprint: "" }, state.scan.ruleNames);
  const staleNote = g.staleEntries ? ` · ${g.staleEntries} stale` : "";

  function handleRemoveAll() {
    const fingerprints = g.entries.map((e) => e.exclusion.fingerprint);
    post("RemoveExclusions", { fingerprints });
    dispatch({
      type: "SHOW_TOAST",
      text: `Restored rule ${g.ruleId} — ${g.findingCount} ${g.findingCount === 1 ? "finding" : "findings"}`,
      isError: false,
    });
  }

  const restoreMsg = g.findingCount > 0
    ? `This will restore all ${g.findingCount} excluded findings for this rule.`
    : `This will clear all ${g.staleEntries} stale exclusion ${g.staleEntries === 1 ? "entry" : "entries"} for this rule.`;
  const staleClause = g.findingCount > 0 && g.staleEntries
    ? ` It also clears ${g.staleEntries} stale ${g.staleEntries === 1 ? "entry" : "entries"}.`
    : "";

  return (
    <div className="mb-4">
      <div className="flex items-center gap-2 mb-2 flex-wrap">
        <span className="font-semibold font-mono text-[12px]">{g.ruleId}</span>
        {name && <span className="text-[12px] text-clevr-muted">{name}</span>}
        <span className="text-[12px] text-clevr-muted">{g.findingCount} excluded{staleNote}</span>
        <button
          type="button"
          className={btnPillMuted}
          title="Restore all excluded findings for this rule"
          onClick={() => setShowConfirm(true)}
        >
          Remove rule exclusion
        </button>
      </div>
      {g.entries.map((entry, i) => (
        <div
          key={entry.exclusion.fingerprint + i}
          className={[
            "bg-clevr-card border border-clevr-border rounded-[6px] p-3 mb-2",
            entry.isStale ? "opacity-60 border-dashed" : "",
          ].filter(Boolean).join(" ")}
        >
          <div className="flex items-center gap-2 flex-wrap mb-1">
            <span className="font-semibold font-mono text-[12px]">{entry.exclusion.ruleId}</span>
            {state.scan.ruleNames[entry.exclusion.ruleId] && (
              <span className="text-[12px] text-clevr-muted">{state.scan.ruleNames[entry.exclusion.ruleId]}</span>
            )}
            {(entry.exclusion.elementName
              ? `${entry.exclusion.documentQualifiedName} › ${entry.exclusion.elementName}`
              : entry.exclusion.documentQualifiedName) && (
              <span className="text-[12px] text-clevr-muted">
                {entry.exclusion.elementName
                  ? `${entry.exclusion.documentQualifiedName} › ${entry.exclusion.elementName}`
                  : entry.exclusion.documentQualifiedName}
              </span>
            )}
            {entry.isStale ? (
              <span className="inline-block px-2 py-px rounded-full text-[11px] font-semibold bg-sev-major text-white">
                stale — no longer matches
              </span>
            ) : entry.violations.length > 1 ? (
              <span className="text-[11px] text-clevr-muted">applies to {entry.violations.length} findings</span>
            ) : null}
            <button
              type="button"
              className={btnPillMuted}
              title="Remove this exclusion — the improvement reappears"
              onClick={() => {
                post("RemoveExclusion", { fingerprint: entry.exclusion.fingerprint });
                dispatch({ type: "SHOW_TOAST", text: "Exclusion removed", isError: false });
              }}
            >
              Remove exclusion
            </button>
          </div>
          <div className="text-[12px] text-clevr-muted">Reason: {entry.exclusion.reason}</div>
          {[entry.exclusion.excludedBy ? `by ${entry.exclusion.excludedBy}` : "", entry.exclusion.date].filter(Boolean).join(" · ") && (
            <div className="text-[11px] text-clevr-muted mt-0.5">
              {[entry.exclusion.excludedBy ? `by ${entry.exclusion.excludedBy}` : "", entry.exclusion.date].filter(Boolean).join(" · ")}
            </div>
          )}
          {entry.violations.length > 0 && (
            <div className="mt-1.5">
              {entry.violations.map((v, vi) => (
                <div key={vi} className="text-[12px] text-clevr-muted truncate">{previewText(v.reason, 120)}</div>
              ))}
            </div>
          )}
        </div>
      ))}
      {showConfirm && (
        <ConfirmDialog
          title="Remove rule exclusion"
          message={restoreMsg + staleClause}
          confirmLabel="Remove rule exclusion"
          onConfirm={handleRemoveAll}
          onClose={() => setShowConfirm(false)}
        />
      )}
    </div>
  );
}

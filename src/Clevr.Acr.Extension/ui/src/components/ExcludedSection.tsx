import { useState } from "react";
import { useAppDispatch, useAppState } from "../context/AppContext";
import { excludedView } from "../utils/exclusions";
import { previewText, ruleName } from "../utils/grouping";
import { post } from "../hooks/useMessageBus";
import { ConfirmDialog } from "./dialogs/ConfirmDialog";
import type { ExclusionRuleGroup } from "../types";

export function ExcludedSection() {
  const state = useAppState();
  const dispatch = useAppDispatch();
  const ev = excludedView(state);

  if (!ev.groups.length) return null;

  const staleNote = ev.staleCount ? ` + ${ev.staleCount} stale` : "";

  return (
    <>
      <div className="acr-excluded-toggle">
        <button type="button" onClick={() => dispatch({ type: "TOGGLE_SHOW_EXCLUDED" })}>
          {state.showExcluded ? "Hide" : "Show"} excluded ({ev.matchedCount}{staleNote})
        </button>
      </div>
      {state.showExcluded && (
        <div className="acr-excluded">
          <div className="acr-section-head">
            <h2>Excluded improvements</h2>
            <span className="acr-section-count">
              {ev.matchedCount} excluded{ev.staleCount ? ` · ${ev.staleCount} stale` : ""}
            </span>
          </div>
          <div className="acr-section-note">
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
  const name = ruleName({ ruleId: g.ruleId, kind: "mxcli", source: "", category: "", severity: "", documentType: "", documentQualifiedName: "", reason: "", fingerprint: "" }, state.ruleNames);
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
    <div className="acr-excluded-rule">
      <div className="acr-excluded-rule-head">
        <span className="acr-ruleid">{g.ruleId}</span>
        {name && <span className="acr-acrcode">{name}</span>}
        <span className="acr-excluded-rule-count">{g.findingCount} excluded{staleNote}</span>
        <button type="button" className="acr-unexclude-rule-btn" title="Restore all excluded findings for this rule" onClick={() => setShowConfirm(true)}>
          Remove rule exclusion
        </button>
      </div>
      {g.entries.map((entry, i) => (
        <div key={entry.exclusion.fingerprint + i} className={"acr-excluded-card" + (entry.isStale ? " stale" : "")}>
          <div className="acr-excluded-top">
            <span className="acr-ruleid">{entry.exclusion.ruleId}</span>
            {state.ruleNames[entry.exclusion.ruleId] && (
              <span className="acr-acrcode">{state.ruleNames[entry.exclusion.ruleId]}</span>
            )}
            {(entry.exclusion.elementName
              ? `${entry.exclusion.documentQualifiedName} › ${entry.exclusion.elementName}`
              : entry.exclusion.documentQualifiedName) && (
              <span className="acr-excluded-where">
                {entry.exclusion.elementName
                  ? `${entry.exclusion.documentQualifiedName} › ${entry.exclusion.elementName}`
                  : entry.exclusion.documentQualifiedName}
              </span>
            )}
            {entry.isStale ? (
              <span className="acr-stale-badge">stale — no longer matches</span>
            ) : entry.violations.length > 1 ? (
              <span className="acr-excluded-applies">applies to {entry.violations.length} findings</span>
            ) : null}
            <button
              type="button"
              className="acr-unexclude-btn"
              title="Remove this exclusion — the improvement reappears"
              onClick={() => {
                post("RemoveExclusion", { fingerprint: entry.exclusion.fingerprint });
                dispatch({ type: "SHOW_TOAST", text: "Exclusion removed", isError: false });
              }}
            >
              Remove exclusion
            </button>
          </div>
          <div className="acr-excluded-reason">Reason: {entry.exclusion.reason}</div>
          {[entry.exclusion.excludedBy ? `by ${entry.exclusion.excludedBy}` : "", entry.exclusion.date].filter(Boolean).join(" · ") && (
            <div className="acr-excluded-meta">
              {[entry.exclusion.excludedBy ? `by ${entry.exclusion.excludedBy}` : "", entry.exclusion.date].filter(Boolean).join(" · ")}
            </div>
          )}
          {entry.violations.length > 0 && (
            <div className="acr-excluded-items">
              {entry.violations.map((v, vi) => (
                <div key={vi} className="acr-excluded-item">{previewText(v.reason, 120)}</div>
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

import { useAppDispatch, useAppState } from "../context/AppContext";
import { answeredManualChecks } from "../utils/manualChecks";
import { post } from "../hooks/useMessageBus";
import { ManualCheckDialog } from "./dialogs/ManualCheckDialog";
import { useState } from "react";
import type { ManualCheckState } from "../types";

export function AnsweredChecksSection() {
  const state = useAppState();
  const dispatch = useAppDispatch();
  const answered = answeredManualChecks(state.manualAnswers);

  if (!answered.length) return null;

  return (
    <>
      <div className="acr-excluded-toggle">
        <button type="button" onClick={() => dispatch({ type: "TOGGLE_SHOW_ANSWERED" })}>
          {state.showAnswered ? "Hide" : "Show"} answered checks ({answered.length})
        </button>
      </div>
      {state.showAnswered && (
        <div className="acr-excluded">
          <div className="acr-section-head">
            <h2>Answered manual checks</h2>
            <span className="acr-section-count">{answered.length} answered (valid)</span>
          </div>
          <div className="acr-section-note">
            Consciously assessed — re-checked every 30 days; shared with the team via version control.
          </div>
          {answered.map((s) => (
            <AnsweredCard key={s.def.id} s={s} />
          ))}
        </div>
      )}
    </>
  );
}

function AnsweredCard({ s }: { s: ManualCheckState }) {
  const dispatch = useAppDispatch();
  const [showReAnswer, setShowReAnswer] = useState(false);

  return (
    <div className="acr-excluded-card">
      <div className="acr-excluded-top">
        <span className="acr-ruleid">{s.def.id}</span>
        <span className="acr-acrcode">{s.def.category}</span>
        <span className="acr-excluded-applies">
          answered "Yes" on {s.answer?.date} — recheck due {s.recheckDate}
        </span>
        <button type="button" className="acr-unexclude-btn" title="Answer this manual check again" onClick={() => setShowReAnswer(true)}>
          Re-answer
        </button>
        <button
          type="button"
          className="acr-unexclude-btn"
          title="Clear the answer — the check becomes open again"
          onClick={() => {
            post("ClearManualCheck", { id: s.def.id });
            dispatch({ type: "SHOW_TOAST", text: "Manual check answer cleared", isError: false });
          }}
        >
          Clear answer
        </button>
      </div>
      <div className="acr-excluded-reason">{s.def.question}</div>
      <div className="acr-excluded-meta">
        Answer: {s.answer?.note} — by {s.answer?.answeredBy ?? "?"}
      </div>
      {showReAnswer && <ManualCheckDialog state={s} onClose={() => setShowReAnswer(false)} />}
    </div>
  );
}

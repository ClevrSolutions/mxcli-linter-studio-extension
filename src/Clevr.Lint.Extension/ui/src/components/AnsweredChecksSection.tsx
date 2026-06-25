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
      <div className="lint-excluded-toggle">
        <button type="button" onClick={() => dispatch({ type: "TOGGLE_SHOW_ANSWERED" })}>
          {state.showAnswered ? "Hide" : "Show"} answered checks ({answered.length})
        </button>
      </div>
      {state.showAnswered && (
        <div className="lint-excluded">
          <div className="lint-section-head">
            <h2>Answered manual checks</h2>
            <span className="lint-section-count">{answered.length} answered (valid)</span>
          </div>
          <div className="lint-section-note">
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
    <div className="lint-excluded-card">
      <div className="lint-excluded-top">
        <span className="lint-ruleid">{s.def.id}</span>
        <span className="lint-acrcode">{s.def.category}</span>
        <span className="lint-excluded-applies">
          answered "Yes" on {s.answer?.date} — recheck due {s.recheckDate}
        </span>
        <button type="button" className="lint-unexclude-btn" title="Answer this manual check again" onClick={() => setShowReAnswer(true)}>
          Re-answer
        </button>
        <button
          type="button"
          className="lint-unexclude-btn"
          title="Clear the answer — the check becomes open again"
          onClick={() => {
            post("ClearManualCheck", { id: s.def.id });
            dispatch({ type: "SHOW_TOAST", text: "Manual check answer cleared", isError: false });
          }}
        >
          Clear answer
        </button>
      </div>
      <div className="lint-excluded-reason">{s.def.question}</div>
      <div className="lint-excluded-meta">
        Answer: {s.answer?.note} — by {s.answer?.answeredBy ?? "?"}
      </div>
      {showReAnswer && <ManualCheckDialog state={s} onClose={() => setShowReAnswer(false)} />}
    </div>
  );
}

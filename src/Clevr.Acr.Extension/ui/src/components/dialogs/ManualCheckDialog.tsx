import { useAppDispatch } from "../../context/AppContext";
import type { ManualCheckState } from "../../types";
import { post } from "../../hooks/useMessageBus";
import { ReasonDialog } from "./ReasonDialog";

interface Props {
  state: ManualCheckState;
  onClose: () => void;
}

export function ManualCheckDialog({ state: s, onClose }: Props) {
  const dispatch = useAppDispatch();
  const def = s.def;
  const prev = s.answer ? ` Previously answered "${s.answer.answer}" on ${s.answer.date}.` : "";

  return (
    <ReasonDialog
      title="Answer manual check"
      metaText={def.question}
      noteText={def.context + prev}
      fieldLabel="Explanation (Yes) or reason (No) — required:"
      placeholder="Yes: what did you review/assess?  No: why not yet?"
      confirmButtons={[
        {
          label: "Answer: Yes",
          className: "acr-modal-confirm-yes",
          onConfirm: (note) => {
            post("AnswerManualCheck", { id: def.id, answer: "yes", note });
            dispatch({ type: "SHOW_TOAST", text: "Manual check answered — Yes", isError: false });
          },
        },
        {
          label: "Answer: No",
          className: "acr-modal-confirm-no",
          onConfirm: (note) => {
            post("AnswerManualCheck", { id: def.id, answer: "no", note });
            dispatch({ type: "SHOW_TOAST", text: "Manual check answered — No (stays open)", isError: false });
          },
        },
      ]}
      onClose={onClose}
    />
  );
}

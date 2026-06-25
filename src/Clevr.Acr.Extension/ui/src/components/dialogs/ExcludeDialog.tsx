import { useAppDispatch, useAppState } from "../../context/AppContext";
import { sharedFingerprintCount } from "../../utils/exclusions";
import { ruleName } from "../../utils/grouping";
import type { Violation } from "../../types";
import { post } from "../../hooks/useMessageBus";
import { ReasonDialog } from "./ReasonDialog";

interface Props {
  v: Violation;
  onClose: () => void;
}

export function ExcludeDialog({ v, onClose }: Props) {
  const state = useAppState();
  const dispatch = useAppDispatch();
  const shared = sharedFingerprintCount(state, v);
  const label = ruleName(v, state.ruleNames)
    ? `${v.ruleId} (${ruleName(v, state.ruleNames)})`
    : v.ruleId;
  const where = v.elementName
    ? `${v.documentType} ${v.documentQualifiedName} › ${v.elementName}`
    : `${v.documentType} ${v.documentQualifiedName}`;

  function handleConfirm(reason: string) {
    post("AddExclusion", {
      fingerprint: v.fingerprint,
      ruleId: v.ruleId,
      documentQualifiedName: v.documentQualifiedName,
      elementName: v.elementName ?? "",
      reason,
    });
    dispatch({
      type: "SHOW_TOAST",
      text: shared > 1 ? `Excluded — hides ${shared} findings on this document` : "Improvement excluded",
      isError: false,
    });
  }

  return (
    <ReasonDialog
      title="Exclude improvement"
      metaText={`${label} — ${where}`}
      noteText={shared > 1 ? `Note: this exclusion shares one fingerprint with ${shared} findings on this document and will hide all ${shared}.` : undefined}
      confirmLabel="Exclude"
      onConfirm={handleConfirm}
      onClose={onClose}
    />
  );
}

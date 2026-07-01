import { useAppDispatch, useAppState } from "../../context/AppContext";
import { ruleName } from "../../utils/grouping";
import type { Violation } from "../../types";
import { post } from "../../hooks/useMessageBus";
import { ReasonDialog } from "./ReasonDialog";

interface Props {
  rule: Violation;
  items: Violation[];
  onClose: () => void;
}

export function ExcludeRuleDialog({ rule, items, onClose }: Props) {
  const state = useAppState();
  const dispatch = useAppDispatch();

  const byFp = new Map<string, Violation>();
  for (const v of items) if (!byFp.has(v.fingerprint)) byFp.set(v.fingerprint, v);
  const uniqueCount = byFp.size;
  const label = ruleName(rule, state.scan.ruleNames)
    ? `${rule.ruleId} (${ruleName(rule, state.scan.ruleNames)})`
    : rule.ruleId;
  const bundleNote = items.length > uniqueCount
    ? ` Some findings share a fingerprint; ${items.length} findings map to ${uniqueCount} exclusion entries.`
    : "";

  function handleConfirm(reason: string) {
    const specs = [...byFp.values()].map((v) => ({
      fingerprint: v.fingerprint,
      ruleId: v.ruleId,
      documentQualifiedName: v.documentQualifiedName,
      elementName: v.elementName ?? "",
    }));
    post("AddExclusions", { reason, items: specs });
    dispatch({
      type: "SHOW_TOAST",
      text: `Excluded rule ${rule.ruleId} — ${uniqueCount} ${uniqueCount === 1 ? "entry" : "entries"}`,
      isError: false,
    });
  }

  return (
    <ReasonDialog
      title="Exclude rule"
      metaText={label}
      noteText={`This will exclude all ${uniqueCount} findings for this rule with the same reason.${bundleNote}`}
      placeholder="Why is this rule intentionally not addressed? (shared with the team via version control)"
      confirmLabel="Exclude rule"
      onConfirm={handleConfirm}
      onClose={onClose}
    />
  );
}

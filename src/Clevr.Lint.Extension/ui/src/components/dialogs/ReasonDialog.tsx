import { useRef, useState } from "react";
import { btnDanger, btnSecondary, inputBase } from "../../utils/classes";

interface ConfirmButton {
  label: string;
  variant?: "danger" | "primary";
  onConfirm: (text: string) => void;
}

interface Props {
  title: string;
  metaText?: string;
  noteText?: string;
  fieldLabel?: string;
  placeholder?: string;
  confirmLabel?: string;
  onConfirm?: (text: string) => void;
  confirmButtons?: ConfirmButton[];
  onClose: () => void;
}

export function ReasonDialog({ title, metaText, noteText, fieldLabel, placeholder, confirmLabel, onConfirm, confirmButtons, onClose }: Props) {
  const [text, setText] = useState("");
  const overlayRef = useRef<HTMLDivElement>(null);

  const specs: ConfirmButton[] = confirmButtons?.length
    ? confirmButtons
    : [{ label: confirmLabel ?? "Confirm", variant: "danger", onConfirm: onConfirm ?? (() => {}) }];

  const disabled = text.trim().length === 0;

  function handleOverlayClick(e: React.MouseEvent) {
    if (e.target === overlayRef.current) onClose();
  }

  function handleConfirm(spec: ConfirmButton) {
    if (disabled) return;
    spec.onConfirm(text.trim());
    onClose();
  }

  return (
    <div
      className="fixed inset-0 z-[10000] bg-[rgba(31,41,51,0.45)] flex items-center justify-center"
      ref={overlayRef}
      onClick={handleOverlayClick}
    >
      <div
        className="bg-clevr-bg rounded-[10px] p-6 w-[min(520px,92vw)] shadow-[0_12px_40px_rgba(0,0,0,0.35)]"
        onClick={(e) => e.stopPropagation()}
      >
        <h3 className="text-[14px] font-semibold m-0 mb-3">{title}</h3>
        {metaText && <div className="text-[12px] text-clevr-muted mb-2">{metaText}</div>}
        {noteText && (
          <div className="text-[12px] bg-[#fff7e6] border border-[#f0c36d] text-[#7a5b00] rounded p-2 mb-3">
            {noteText}
          </div>
        )}
        <div className="text-[12px] font-medium mb-1">{fieldLabel ?? "Reason (required):"}</div>
        <textarea
          className={`${inputBase} w-full resize-none mb-1`}
          rows={3}
          placeholder={placeholder ?? "Why is this intentionally not fixed? (shared with the team via version control)"}
          value={text}
          onChange={(e) => setText(e.target.value)}
          autoFocus
        />
        <div className="flex justify-end gap-2 mt-3">
          <button type="button" className={btnSecondary} onClick={onClose}>Cancel</button>
          {specs.map((spec) => (
            <button
              key={spec.label}
              type="button"
              className={btnDanger}
              disabled={disabled}
              onClick={() => handleConfirm(spec)}
            >
              {spec.label}
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}

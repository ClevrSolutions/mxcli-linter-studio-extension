import { useRef, useState } from "react";

interface ConfirmButton {
  label: string;
  className: string;
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
    : [{ label: confirmLabel ?? "Confirm", className: "lint-modal-confirm", onConfirm: onConfirm ?? (() => {}) }];

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
    <div className="lint-modal-overlay" ref={overlayRef} onClick={handleOverlayClick}>
      <div className="lint-modal" onClick={(e) => e.stopPropagation()}>
        <h3>{title}</h3>
        {metaText && <div className="lint-modal-meta">{metaText}</div>}
        {noteText && <div className="lint-modal-warn">{noteText}</div>}
        <div className="lint-modal-fieldlabel">{fieldLabel ?? "Reason (required):"}</div>
        <textarea
          className="lint-modal-input"
          rows={3}
          placeholder={placeholder ?? "Why is this intentionally not fixed? (shared with the team via version control)"}
          value={text}
          onChange={(e) => setText(e.target.value)}
          autoFocus
        />
        <div className="lint-modal-actions">
          <button type="button" className="lint-modal-cancel" onClick={onClose}>Cancel</button>
          {specs.map((spec) => (
            <button
              key={spec.label}
              type="button"
              className={spec.className}
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

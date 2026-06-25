import { useRef } from "react";

interface Props {
  title: string;
  message: string;
  confirmLabel?: string;
  onConfirm: () => void;
  onClose: () => void;
}

export function ConfirmDialog({ title, message, confirmLabel, onConfirm, onClose }: Props) {
  const overlayRef = useRef<HTMLDivElement>(null);

  function handleOverlayClick(e: React.MouseEvent) {
    if (e.target === overlayRef.current) onClose();
  }

  function handleConfirm() {
    onConfirm();
    onClose();
  }

  return (
    <div className="acr-modal-overlay" ref={overlayRef} onClick={handleOverlayClick}>
      <div className="acr-modal" onClick={(e) => e.stopPropagation()}>
        <h3>{title}</h3>
        <div className="acr-modal-message">{message}</div>
        <div className="acr-modal-actions">
          <button type="button" className="acr-modal-cancel" onClick={onClose}>Cancel</button>
          <button type="button" className="acr-modal-confirm" onClick={handleConfirm} autoFocus>
            {confirmLabel ?? "Confirm"}
          </button>
        </div>
      </div>
    </div>
  );
}

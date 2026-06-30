import { useRef } from "react";
import { btnDanger, btnSecondary } from "../../utils/classes";

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
        <div className="text-[13px] text-clevr-fg mb-4">{message}</div>
        <div className="flex justify-end gap-2">
          <button type="button" className={btnSecondary} onClick={onClose}>Cancel</button>
          <button type="button" className={btnDanger} onClick={handleConfirm} autoFocus>
            {confirmLabel ?? "Confirm"}
          </button>
        </div>
      </div>
    </div>
  );
}

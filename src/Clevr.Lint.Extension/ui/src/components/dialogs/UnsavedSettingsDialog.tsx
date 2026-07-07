import { useRef } from "react";
import { btnDanger, btnPrimary, btnSecondary } from "../../utils/classes";

interface Props {
  onSave: () => void;
  onRevert: () => void;
  onCancel: () => void;
}

export function UnsavedSettingsDialog({ onSave, onRevert, onCancel }: Props) {
  const overlayRef = useRef<HTMLDivElement>(null);

  function handleOverlayClick(e: React.MouseEvent) {
    if (e.target === overlayRef.current) onCancel();
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
        <h3 className="text-[14px] font-semibold m-0 mb-3">Unsaved changes</h3>
        <div className="text-[13px] text-clevr-fg mb-4">
          You have unsaved changes in Settings. Save them before leaving, discard them, or stay here.
        </div>
        <div className="flex justify-end gap-2">
          <button type="button" className={btnSecondary} onClick={onCancel}>Cancel</button>
          <button type="button" className={btnDanger} onClick={onRevert}>Revert changes</button>
          <button type="button" className={btnPrimary} onClick={onSave} autoFocus>Save</button>
        </div>
      </div>
    </div>
  );
}

import { btnDanger, btnSecondary } from "../../utils/classes";
import { DialogShell } from "./DialogShell";

interface Props {
  title: string;
  message: string;
  confirmLabel?: string;
  onConfirm: () => void;
  onClose: () => void;
}

export function ConfirmDialog({ title, message, confirmLabel, onConfirm, onClose }: Props) {
  function handleConfirm() {
    onConfirm();
    onClose();
  }

  return (
    <DialogShell label={title} onClose={onClose}>
      <h3 className="text-[14px] font-semibold m-0 mb-3">{title}</h3>
      <div className="text-[13px] text-clevr-fg mb-4">{message}</div>
      <div className="flex justify-end gap-2">
        <button type="button" className={btnSecondary} onClick={onClose}>Cancel</button>
        <button type="button" className={btnDanger} onClick={handleConfirm} autoFocus>
          {confirmLabel ?? "Confirm"}
        </button>
      </div>
    </DialogShell>
  );
}

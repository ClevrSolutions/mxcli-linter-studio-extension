import { btnDanger, btnPrimary, btnSecondary } from "../../utils/classes";
import { DialogShell } from "./DialogShell";

interface Props {
  onSave: () => void;
  onRevert: () => void;
  onCancel: () => void;
}

export function UnsavedSettingsDialog({ onSave, onRevert, onCancel }: Props) {
  return (
    <DialogShell label="Unsaved changes" onClose={onCancel}>
      <h3 className="text-[14px] font-semibold m-0 mb-3">Unsaved changes</h3>
      <div className="text-[13px] text-clevr-fg mb-4">
        You have unsaved changes in Settings. Save them before leaving, discard them, or stay here.
      </div>
      <div className="flex justify-end gap-2">
        <button type="button" className={btnSecondary} onClick={onCancel}>Cancel</button>
        <button type="button" className={btnDanger} onClick={onRevert}>Revert changes</button>
        <button type="button" className={btnPrimary} onClick={onSave} autoFocus>Save</button>
      </div>
    </DialogShell>
  );
}

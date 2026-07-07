import { DialogShell } from "./DialogShell";

interface Props {
  ruleId: string;
  name: string;
  description: string;
  starContent?: string;
  onClose: () => void;
}

export function RuleInfoDialog({ ruleId, name, description, starContent, onClose }: Props) {
  return (
    <DialogShell
      label={name ? `${ruleId} — ${name}` : ruleId}
      onClose={onClose}
      panelClassName="bg-white rounded-lg shadow-xl w-[92vw] max-w-[700px] max-h-[80vh] flex flex-col"
    >
      <div className="flex items-center justify-between px-5 pt-4 pb-3 border-b border-clevr-border shrink-0">
        <div>
          <span className="font-mono font-semibold text-[13px] text-clevr-fg">{ruleId}</span>
          {name && <span className="ml-2 text-[13px] text-clevr-muted">{name}</span>}
        </div>
        <button
          type="button"
          className="text-clevr-muted hover:text-clevr-fg bg-transparent border-0 text-[18px] cursor-pointer leading-none"
          onClick={onClose}
          aria-label="Close"
        >
          ×
        </button>
      </div>

      <div className="overflow-y-auto px-5 py-4 flex flex-col gap-4">
        <div>
          <p className="text-[12px] font-semibold uppercase tracking-[0.05em] text-clevr-muted m-0 mb-1">Description</p>
          <p className="text-[13px] text-clevr-fg m-0">
            {description || <span className="italic text-clevr-muted">No description available.</span>}
          </p>
        </div>

        {starContent && (
          <div>
            <p className="text-[12px] font-semibold uppercase tracking-[0.05em] text-clevr-muted m-0 mb-1">Rule source (.star)</p>
            <pre
              className="text-[11px] font-mono bg-clevr-hover border border-clevr-border rounded p-3 m-0 overflow-x-auto overflow-y-auto whitespace-pre leading-relaxed text-clevr-fg"
              style={{ maxHeight: "380px" }}
            >
              {starContent}
            </pre>
          </div>
        )}
      </div>

      <div className="px-5 py-3 border-t border-clevr-border shrink-0 flex justify-end">
        <button
          type="button"
          className="px-3 py-1.5 text-[12px] font-medium bg-white text-clevr-fg border border-clevr-border rounded-[6px] cursor-pointer hover:bg-clevr-hover"
          onClick={onClose}
        >
          Close
        </button>
      </div>
    </DialogShell>
  );
}

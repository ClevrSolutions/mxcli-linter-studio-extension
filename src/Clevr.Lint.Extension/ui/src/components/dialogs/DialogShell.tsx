import { useEffect, useRef, useState } from "react";

const FOCUSABLE =
  'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

export const dialogPanelBase =
  "bg-clevr-bg rounded-[10px] p-6 w-[min(520px,92vw)] shadow-[0_12px_40px_rgba(0,0,0,0.35)]";

interface Props {
  label: string;
  onClose: () => void;
  panelClassName?: string;
  children: React.ReactNode;
}

/**
 * Modal overlay shared by all dialogs: role="dialog"/aria-modal, click-outside and
 * Escape to close, Tab focus trap, initial focus inside the panel (unless a child
 * already claimed it via autoFocus), and focus restore to the opener on close.
 */
export function DialogShell({ label, onClose, panelClassName, children }: Props) {
  const overlayRef = useRef<HTMLDivElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);

  // Captured during the first render, before React commits the dialog — a child's
  // autoFocus fires at commit, so reading activeElement in the effect would be too late.
  const [opener] = useState(() =>
    document.activeElement instanceof HTMLElement ? document.activeElement : null,
  );

  useEffect(() => {
    const panel = panelRef.current;
    if (panel && !panel.contains(document.activeElement)) {
      const first = panel.querySelector<HTMLElement>(FOCUSABLE);
      (first ?? panel).focus();
    }
    return () => {
      if (opener?.isConnected) opener.focus();
    };
  }, [opener]);

  function handleKeyDown(e: React.KeyboardEvent) {
    if (e.key === "Escape") {
      e.stopPropagation();
      onClose();
      return;
    }
    if (e.key !== "Tab") return;
    const panel = panelRef.current;
    if (!panel) return;
    const focusable = Array.from(panel.querySelectorAll<HTMLElement>(FOCUSABLE));
    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    if (!first || !last) {
      e.preventDefault();
      return;
    }
    const active = document.activeElement;
    if (e.shiftKey && (active === first || active === panel)) {
      e.preventDefault();
      last.focus();
    } else if (!e.shiftKey && active === last) {
      e.preventDefault();
      first.focus();
    }
  }

  function handleOverlayClick(e: React.MouseEvent) {
    if (e.target === overlayRef.current) onClose();
  }

  return (
    <div
      className="fixed inset-0 z-[10000] bg-[rgba(31,41,51,0.45)] flex items-center justify-center"
      ref={overlayRef}
      onClick={handleOverlayClick}
      onKeyDown={handleKeyDown}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-label={label}
        tabIndex={-1}
        className={panelClassName ?? dialogPanelBase}
        ref={panelRef}
        onClick={(e) => e.stopPropagation()}
      >
        {children}
      </div>
    </div>
  );
}

import { useEffect, useRef, useState } from "react";
import { useAppDispatch, useAppState } from "../context/AppContext";

export function Toast() {
  const { toast } = useAppState();
  const dispatch = useAppDispatch();
  const [visible, setVisible] = useState(false);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const fadeRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    if (!toast) return;
    setVisible(true);
    if (timerRef.current) clearTimeout(timerRef.current);
    if (fadeRef.current) clearTimeout(fadeRef.current);
    timerRef.current = setTimeout(() => {
      setVisible(false);
      fadeRef.current = setTimeout(() => dispatch({ type: "CLEAR_TOAST" }), 400);
    }, 2800);
    return () => {
      if (timerRef.current) clearTimeout(timerRef.current);
      if (fadeRef.current) clearTimeout(fadeRef.current);
    };
  }, [toast, dispatch]);

  if (!toast) return null;

  const base =
    "fixed z-[9999] max-w-[min(420px,90vw)] px-3 py-2 rounded-[8px] text-[12px] leading-[1.4] shadow-[0_4px_16px_rgba(0,0,0,0.28)] whitespace-pre-wrap pointer-events-none toast-transition";
  const bg = toast.isError ? "bg-sev-blocker text-white" : "bg-[#1f2933] text-white";
  const opacity = visible ? "opacity-100" : "opacity-0";

  return (
    <div
      className={`${base} ${bg} ${opacity}`}
      style={{
        position: "fixed",
        left: "50%",
        top: "16px",
        transform: visible ? "translateX(-50%) translateY(0)" : "translateX(-50%) translateY(4px)",
      }}
    >
      {toast.text}
    </div>
  );
}

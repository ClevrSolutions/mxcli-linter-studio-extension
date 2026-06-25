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

  return (
    <div
      className={"acr-toast" + (toast.isError ? " err" : "") + (visible ? " show" : "")}
      style={{ position: "fixed", maxWidth: "min(420px, 90vw)", left: "50%", transform: visible ? "translateX(-50%) translateY(0)" : "translateX(-50%) translateY(4px)", top: "16px" }}
    >
      {toast.text}
    </div>
  );
}

"use client";

import {
  createContext,
  useCallback,
  useContext,
  useState,
  useEffect,
  useRef,
} from "react";
import { CheckCircle, AlertCircle, Info, X } from "lucide-react";

type ToastType = "success" | "error" | "info";

interface ToastItem {
  id: number;
  message: string;
  type: ToastType;
  duration: number;
  exiting: boolean;
}

interface ToastContextValue {
  toast: (message: string, type?: ToastType, duration?: number) => void;
}

const ToastContext = createContext<ToastContextValue | null>(null);

export function useToast(): ToastContextValue {
  const ctx = useContext(ToastContext);
  if (!ctx) throw new Error("useToast must be used within a ToastProvider");
  return ctx;
}

export function ToastProvider({ children }: { children: React.ReactNode }) {
  const [toasts, setToasts] = useState<ToastItem[]>([]);
  const nextId = useRef(0);

  const removeToast = useCallback((id: number) => {
    setToasts((prev) =>
      prev.map((t) => (t.id === id ? { ...t, exiting: true } : t)),
    );
    setTimeout(() => {
      setToasts((prev) => prev.filter((t) => t.id !== id));
    }, 300);
  }, []);

  const toast = useCallback(
    (message: string, type: ToastType = "success", duration = 3000) => {
      const id = nextId.current++;
      setToasts((prev) => [
        ...prev,
        { id, message, type, duration, exiting: false },
      ]);
    },
    [],
  );

  return (
    <ToastContext.Provider value={{ toast }}>
      {children}
      <div className="fixed bottom-20 left-1/2 -translate-x-1/2 z-50 flex flex-col-reverse gap-2 pointer-events-none">
        {toasts.map((t) => (
          <ToastNotification
            key={t.id}
            item={t}
            onDismiss={() => removeToast(t.id)}
          />
        ))}
      </div>
    </ToastContext.Provider>
  );
}

function ToastNotification({
  item,
  onDismiss,
}: {
  item: ToastItem;
  onDismiss: () => void;
}) {
  useEffect(() => {
    const timer = setTimeout(onDismiss, item.duration);
    return () => clearTimeout(timer);
  }, [item.duration, onDismiss]);

  const Icon =
    item.type === "success"
      ? CheckCircle
      : item.type === "error"
        ? AlertCircle
        : Info;

  const iconColor =
    item.type === "success"
      ? "text-emerald-400"
      : item.type === "error"
        ? "text-red-400"
        : "text-blue-400";

  return (
    <div
      className={`pointer-events-auto flex items-center gap-2 px-4 py-3 rounded-lg bg-bg-elevated border border-border shadow-lg min-w-[280px] max-w-[90vw] transition-all duration-300 ${
        item.exiting
          ? "opacity-0 translate-y-2"
          : "opacity-100 translate-y-0 animate-slide-up"
      }`}
    >
      <Icon size={18} className={iconColor} />
      <span className="text-sm text-text-primary flex-1">{item.message}</span>
      <button
        onClick={onDismiss}
        className="text-text-muted hover:text-text-secondary transition-colors shrink-0"
      >
        <X size={14} />
      </button>
    </div>
  );
}

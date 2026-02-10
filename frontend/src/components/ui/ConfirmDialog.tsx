"use client";

import { useEffect } from "react";
import { createPortal } from "react-dom";
import { X } from "lucide-react";

interface ConfirmDialogProps {
  open: boolean;
  title: string;
  message?: string;
  confirmLabel?: string;
  confirmVariant?: "primary" | "danger";
  isLoading?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
  children?: React.ReactNode;
}

export function ConfirmDialog({
  open,
  title,
  message,
  confirmLabel = "Confirm",
  confirmVariant = "primary",
  isLoading = false,
  onConfirm,
  onCancel,
  children,
}: ConfirmDialogProps) {
  useEffect(() => {
    if (!open) return;
    function handleKey(e: KeyboardEvent) {
      if (e.key === "Escape") onCancel();
    }
    document.addEventListener("keydown", handleKey);
    document.body.style.overflow = "hidden";
    return () => {
      document.removeEventListener("keydown", handleKey);
      document.body.style.overflow = "";
    };
  }, [open, onCancel]);

  if (!open) return null;

  const confirmBg =
    confirmVariant === "danger"
      ? "bg-red-500 hover:bg-red-600"
      : "bg-gold hover:bg-gold/80";

  const confirmText =
    confirmVariant === "danger" ? "text-white" : "text-bg-primary";

  return createPortal(
    <div className="fixed inset-0 z-[90] flex items-center justify-center p-4">
      {/* Backdrop */}
      <div
        className="absolute inset-0 bg-black/60"
        onClick={isLoading ? undefined : onCancel}
      />

      {/* Dialog */}
      <div className="relative bg-bg-surface border border-border rounded-xl shadow-2xl max-w-md w-full animate-slide-up">
        {/* Header */}
        <div className="flex items-center justify-between px-5 py-4 border-b border-border">
          <h3 className="text-lg font-semibold text-text-primary">{title}</h3>
          <button
            onClick={onCancel}
            disabled={isLoading}
            className="text-text-muted hover:text-text-secondary transition-colors disabled:opacity-50"
          >
            <X size={18} />
          </button>
        </div>

        {/* Body */}
        <div className="px-5 py-4 space-y-4">
          {message && (
            <p className="text-sm text-text-secondary">{message}</p>
          )}
          {children}
        </div>

        {/* Footer */}
        <div className="flex items-center justify-end gap-3 px-5 py-4 border-t border-border">
          <button
            onClick={onCancel}
            disabled={isLoading}
            className="px-4 py-2 text-sm font-medium text-text-secondary hover:text-text-primary transition-colors disabled:opacity-50"
          >
            Cancel
          </button>
          <button
            onClick={onConfirm}
            disabled={isLoading}
            className={`px-4 py-2 text-sm font-medium rounded-lg transition-colors disabled:opacity-50 ${confirmBg} ${confirmText}`}
          >
            {isLoading ? "..." : confirmLabel}
          </button>
        </div>
      </div>
    </div>,
    document.body,
  );
}

"use client";

import { useEffect, useId, useRef } from "react";
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

/**
 * A native modal dialog: the browser owns the top layer, Escape, the focus trap and returning
 * focus to the opener. The backdrop is the dialog's own ::backdrop.
 */
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
  const dialogRef = useRef<HTMLDialogElement>(null);
  const titleId = useId();

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) return;
    if (open && !dialog.open) dialog.showModal();
    else if (!open && dialog.open) dialog.close();
  }, [open]);

  useEffect(() => {
    if (!open) return;
    document.body.style.overflow = "hidden";
    return () => {
      document.body.style.overflow = "";
    };
  }, [open]);

  const confirmBg =
    confirmVariant === "danger"
      ? "bg-red-600 hover:bg-red-700"
      : "bg-gold hover:bg-gold/80";

  const confirmText =
    confirmVariant === "danger" ? "text-white" : "text-bg-primary";

  return (
    <dialog
      ref={dialogRef}
      aria-labelledby={titleId}
      onCancel={(event) => {
        // Escape: the browser would close the dialog itself; the caller owns the open state.
        event.preventDefault();
        if (!isLoading) onCancel();
      }}
      onClick={(event) => {
        // A click on the backdrop targets the dialog element itself.
        if (event.target === event.currentTarget && !isLoading) onCancel();
      }}
      className="m-auto w-full max-w-md bg-transparent p-0 text-inherit backdrop:bg-black/60"
    >
      {open && (
        <div className="bg-bg-surface border border-border rounded-xl shadow-2xl w-full animate-slide-up">
          {/* Header */}
          <div className="flex items-center justify-between px-5 py-4 border-b border-border">
            <h3 id={titleId} className="text-lg font-semibold text-text-primary">
              {title}
            </h3>
            <button
              type="button"
              onClick={onCancel}
              disabled={isLoading}
              aria-label="Close"
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
              type="button"
              onClick={onCancel}
              disabled={isLoading}
              className="px-4 py-2 text-sm font-medium text-text-secondary hover:text-text-primary transition-colors disabled:opacity-50"
            >
              Cancel
            </button>
            <button
              type="button"
              onClick={onConfirm}
              disabled={isLoading}
              className={`px-4 py-2 text-sm font-medium rounded-lg transition-colors disabled:opacity-50 ${confirmBg} ${confirmText}`}
            >
              {isLoading ? "..." : confirmLabel}
            </button>
          </div>
        </div>
      )}
    </dialog>
  );
}

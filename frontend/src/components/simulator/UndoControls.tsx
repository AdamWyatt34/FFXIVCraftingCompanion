"use client";

import { useState, useCallback } from "react";
import { Undo2, RotateCcw, FileDown, Link2, Check } from "lucide-react";
import { ConfirmDialog } from "@/components/ui";

interface UndoControlsProps {
  canUndo: boolean;
  stepCount: number;
  isLoading: boolean;
  onUndo: () => void;
  onReset: () => Promise<void>;
  onOpenMacroExport: () => void;
  onShareRotation?: () => void;
  shareConfirmation?: boolean;
}

export function UndoControls({
  canUndo,
  stepCount,
  isLoading,
  onUndo,
  onReset,
  onOpenMacroExport,
  onShareRotation,
  shareConfirmation,
}: UndoControlsProps) {
  const [showResetConfirm, setShowResetConfirm] = useState(false);

  const handleResetConfirm = useCallback(async () => {
    setShowResetConfirm(false);
    await onReset();
  }, [onReset]);

  return (
    <>
      <div className="flex items-center gap-2">
        <button
          onClick={onUndo}
          disabled={!canUndo || isLoading}
          className="flex items-center gap-1.5 px-3 py-1.5 bg-bg-elevated text-text-secondary font-medium rounded-lg hover:text-text-primary border border-border transition-colors text-sm disabled:opacity-40 disabled:cursor-not-allowed"
          title="Undo Last (Ctrl+Z)"
          aria-label="Undo last action"
        >
          <Undo2 size={14} />
          <span className="hidden sm:inline">Undo Last</span>
        </button>
        <button
          onClick={() => setShowResetConfirm(true)}
          disabled={!canUndo || isLoading}
          className="flex items-center gap-1.5 px-3 py-1.5 bg-bg-elevated text-text-secondary font-medium rounded-lg hover:text-text-primary border border-border transition-colors text-sm disabled:opacity-40 disabled:cursor-not-allowed"
          title="Reset Craft (Esc)"
          aria-label="Reset craft"
        >
          <RotateCcw size={14} />
          <span className="hidden sm:inline">Reset Craft</span>
        </button>
        <div className="flex-1" />
        {onShareRotation && (
          <button
            onClick={onShareRotation}
            disabled={stepCount === 0 || isLoading}
            className="flex items-center gap-1.5 px-3 py-1.5 bg-bg-elevated text-text-secondary font-medium rounded-lg hover:text-text-primary border border-border transition-colors text-sm disabled:opacity-40 disabled:cursor-not-allowed"
            aria-label="Share rotation link"
          >
            {shareConfirmation ? (
              <>
                <Check size={14} className="text-emerald-400" />
                <span className="hidden sm:inline text-emerald-400">Copied!</span>
              </>
            ) : (
              <>
                <Link2 size={14} />
                <span className="hidden sm:inline">Share</span>
              </>
            )}
          </button>
        )}
        <button
          onClick={onOpenMacroExport}
          disabled={stepCount === 0 || isLoading}
          className="flex items-center gap-1.5 px-3 py-1.5 bg-bg-elevated text-text-secondary font-medium rounded-lg hover:text-text-primary border border-border transition-colors text-sm disabled:opacity-40 disabled:cursor-not-allowed"
          aria-label="Export macro"
        >
          <FileDown size={14} />
          <span className="hidden sm:inline">Export Macro</span>
        </button>
      </div>

      <ConfirmDialog
        open={showResetConfirm}
        title="Reset Craft?"
        message="This will reset all progress and start the craft over."
        confirmLabel="Reset"
        confirmVariant="danger"
        onConfirm={handleResetConfirm}
        onCancel={() => setShowResetConfirm(false)}
      />
    </>
  );
}

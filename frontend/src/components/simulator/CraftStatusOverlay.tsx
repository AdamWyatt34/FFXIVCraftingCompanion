"use client";

import { CheckCircle, XCircle, FileDown, RotateCcw, Undo2, Link2, BookmarkPlus, Loader2, Check } from "lucide-react";
import type { CraftingState, StepResult } from "@/types/simulator";

interface CraftStatusOverlayProps {
  state: CraftingState;
  stateHistory: StepResult[];
  onOpenMacroExport: () => void;
  onReset: () => void;
  onUndo: () => void;
  onShareRotation?: () => void;
  onSaveToLibrary?: () => void;
  isSaving?: boolean;
  saveSuccess?: boolean;
}

function getHQColor(pct: number): string {
  if (pct >= 100) return "text-gold";
  if (pct >= 90) return "text-emerald-400";
  if (pct >= 61) return "text-emerald-500";
  if (pct >= 31) return "text-amber-400";
  return "text-red-400";
}

export function CraftStatusOverlay({
  state,
  stateHistory,
  onOpenMacroExport,
  onReset,
  onUndo,
  onShareRotation,
  onSaveToLibrary,
  isSaving,
  saveSuccess,
}: CraftStatusOverlayProps) {
  if (state.status === "InProgress") return null;

  const isSuccess = state.status === "Completed";

  const lastStep = stateHistory[stateHistory.length - 1];
  const failReason =
    lastStep?.failReason ?? "Durability reached 0 before completing progress.";

  return (
    <div
      className={`rounded-lg border p-5 animate-scale-in sm:relative sm:bottom-auto ${
        isSuccess
          ? "border-emerald-500/30 bg-emerald-500/5"
          : "border-red-500/30 bg-red-500/5"
      }`}
    >
      {/* Header */}
      <div className="flex items-center gap-2 mb-4">
        {isSuccess ? (
          <CheckCircle size={24} className="text-emerald-400" />
        ) : (
          <XCircle size={24} className="text-red-400" />
        )}
        <h2
          className={`text-lg font-bold ${
            isSuccess ? "text-emerald-400" : "text-red-400"
          }`}
        >
          {isSuccess ? "Craft Complete!" : "Craft Failed"}
        </h2>
      </div>

      {/* Stats */}
      {isSuccess ? (
        <div className="grid grid-cols-3 gap-3 mb-4">
          <div className="bg-bg-elevated rounded-md p-3 text-center">
            <p className="text-lg font-bold text-emerald-400">
              {state.quality.toLocaleString()} / {state.maxQuality.toLocaleString()}
            </p>
            <p className="text-xs text-text-muted">
              Quality ({Math.round(state.qualityPercent)}%)
            </p>
          </div>
          <div className="bg-bg-elevated rounded-md p-3 text-center">
            <p className={`text-lg font-bold ${getHQColor(state.hqPercent)}`}>
              {state.hqPercent}%
            </p>
            <p className="text-xs text-text-muted">HQ Probability</p>
          </div>
          <div className="bg-bg-elevated rounded-md p-3 text-center">
            <p className="text-lg font-bold text-text-primary">
              {stateHistory.length}
            </p>
            <p className="text-xs text-text-muted">Steps Used</p>
          </div>
        </div>
      ) : (
        <div className="mb-4 space-y-2">
          <p className="text-sm text-text-secondary">{failReason}</p>
          <div className="grid grid-cols-2 gap-3">
            <div className="bg-bg-elevated rounded-md p-3 text-center">
              <p className="text-lg font-bold text-blue-400">
                {state.progress.toLocaleString()} /{" "}
                {state.maxProgress.toLocaleString()}
              </p>
              <p className="text-xs text-text-muted">
                Progress ({Math.round(state.progressPercent)}%)
              </p>
            </div>
            <div className="bg-bg-elevated rounded-md p-3 text-center">
              <p className="text-lg font-bold text-text-primary">
                {stateHistory.length}
              </p>
              <p className="text-xs text-text-muted">Steps Used</p>
            </div>
          </div>
        </div>
      )}

      {/* Action buttons */}
      <div className="flex flex-wrap gap-2">
        {isSuccess && (
          <button
            onClick={onOpenMacroExport}
            className="flex items-center gap-1.5 px-4 py-2 bg-gold text-bg-primary font-medium rounded-lg hover:bg-gold-light transition-colors text-sm"
          >
            <FileDown size={16} />
            Export Macro
          </button>
        )}
        {isSuccess && onSaveToLibrary && (
          <button
            onClick={onSaveToLibrary}
            disabled={isSaving || saveSuccess}
            className="flex items-center gap-1.5 px-4 py-2 bg-bg-elevated text-text-secondary font-medium rounded-lg hover:text-text-primary border border-border transition-colors text-sm disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {isSaving ? (
              <Loader2 size={16} className="animate-spin" />
            ) : saveSuccess ? (
              <Check size={16} className="text-emerald-400" />
            ) : (
              <BookmarkPlus size={16} />
            )}
            {saveSuccess ? "Saved!" : "Save to Library"}
          </button>
        )}
        {onShareRotation && (
          <button
            onClick={onShareRotation}
            className="flex items-center gap-1.5 px-4 py-2 bg-bg-elevated text-text-secondary font-medium rounded-lg hover:text-text-primary border border-border transition-colors text-sm"
          >
            <Link2 size={16} />
            Share Rotation
          </button>
        )}
        <button
          onClick={onUndo}
          className="flex items-center gap-1.5 px-4 py-2 bg-bg-elevated text-text-secondary font-medium rounded-lg hover:text-text-primary border border-border transition-colors text-sm"
        >
          <Undo2 size={16} />
          Undo Last
        </button>
        <button
          onClick={onReset}
          className="flex items-center gap-1.5 px-4 py-2 bg-bg-elevated text-text-secondary font-medium rounded-lg hover:text-text-primary border border-border transition-colors text-sm"
        >
          <RotateCcw size={16} />
          Reset Craft
        </button>
      </div>
    </div>
  );
}

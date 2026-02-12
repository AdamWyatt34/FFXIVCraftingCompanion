"use client";

import { useState, useCallback, useMemo, useRef } from "react";
import { QuickActionBar, QUICK_BAR_ACTION_NAMES } from "./QuickActionBar";
import { ActionGrid } from "./ActionGrid";
import { ConfirmDialog } from "@/components/ui";
import { useKeyboardShortcuts } from "@/hooks/useKeyboardShortcuts";
import type { ActionInfo, ActionAvailability } from "@/types/simulator";

interface ActionPanelProps {
  actions: ActionInfo[];
  availableActions: ActionAvailability[];
  isLoading: boolean;
  onExecute: (actionId: number) => Promise<void>;
  onUndo: () => void;
  onReset: () => Promise<void>;
}

export function ActionPanel({
  actions,
  availableActions,
  isLoading,
  onExecute,
  onUndo,
  onReset,
}: ActionPanelProps) {
  const [executingActionId, setExecutingActionId] = useState<number | null>(null);
  const [justUsedActionId, setJustUsedActionId] = useState<number | null>(null);
  const [showResetConfirm, setShowResetConfirm] = useState(false);
  const flashTimeoutRef = useRef<ReturnType<typeof setTimeout>>(undefined);
  const containerRef = useRef<HTMLDivElement>(null);

  const availabilityMap = useMemo(() => {
    const map = new Map<number, ActionAvailability>();
    for (const a of availableActions) map.set(a.actionId, a);
    return map;
  }, [availableActions]);

  const quickBarActions = useMemo(() => {
    const actionsByName = new Map<string, ActionInfo>();
    for (const a of actions) actionsByName.set(a.name, a);
    return QUICK_BAR_ACTION_NAMES
      .map((name) => actionsByName.get(name))
      .filter((a): a is ActionInfo => a !== undefined);
  }, [actions]);

  const handleExecute = useCallback(
    async (actionId: number) => {
      if (executingActionId !== null) return; // prevent double-execution

      setExecutingActionId(actionId);
      try {
        await onExecute(actionId);
        // Success flash
        if (flashTimeoutRef.current) clearTimeout(flashTimeoutRef.current);
        setJustUsedActionId(actionId);
        flashTimeoutRef.current = setTimeout(() => setJustUsedActionId(null), 400);
        // Return focus to the action grid area
        requestAnimationFrame(() => {
          const target = containerRef.current?.querySelector<HTMLElement>('button[tabindex="0"]');
          target?.focus();
        });
      } catch {
        // Error is handled by useSimulator hook (sets error state on page)
      } finally {
        setExecutingActionId(null);
      }
    },
    [onExecute, executingActionId],
  );

  const handleResetRequest = useCallback(() => {
    setShowResetConfirm(true);
  }, []);

  const handleResetConfirm = useCallback(async () => {
    setShowResetConfirm(false);
    await onReset();
  }, [onReset]);

  const handleResetCancel = useCallback(() => {
    setShowResetConfirm(false);
  }, []);

  useKeyboardShortcuts({
    quickBarActions,
    availabilityMap,
    onExecute: handleExecute,
    onUndo,
    onResetRequest: handleResetRequest,
    enabled: !isLoading && executingActionId === null,
  });

  return (
    <div
      ref={containerRef}
      className="space-y-3 animate-fade-in"
      aria-busy={executingActionId !== null}
    >
      <QuickActionBar
        actions={actions}
        availableActions={availableActions}
        executingActionId={executingActionId}
        justUsedActionId={justUsedActionId}
        onExecute={handleExecute}
      />

      <ActionGrid
        actions={actions}
        availableActions={availableActions}
        executingActionId={executingActionId}
        justUsedActionId={justUsedActionId}
        onExecute={handleExecute}
      />

      <ConfirmDialog
        open={showResetConfirm}
        title="Reset Craft?"
        message="This will reset all progress and start the craft over."
        confirmLabel="Reset"
        confirmVariant="danger"
        onConfirm={handleResetConfirm}
        onCancel={handleResetCancel}
      />
    </div>
  );
}

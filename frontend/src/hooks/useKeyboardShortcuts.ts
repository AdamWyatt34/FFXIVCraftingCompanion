"use client";

import { useEffect } from "react";
import type { ActionInfo, ActionAvailability } from "@/types/simulator";

interface UseKeyboardShortcutsOptions {
  quickBarActions: ActionInfo[];
  availabilityMap: Map<number, ActionAvailability>;
  onExecute: (actionId: number) => void;
  onUndo: () => void;
  onResetRequest: () => void;
  enabled: boolean;
}

export function useKeyboardShortcuts({
  quickBarActions,
  availabilityMap,
  onExecute,
  onUndo,
  onResetRequest,
  enabled,
}: UseKeyboardShortcutsOptions) {
  useEffect(() => {
    if (!enabled) return;

    function handleKeyDown(e: KeyboardEvent) {
      const tag = (e.target as HTMLElement)?.tagName;
      if (tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT") return;

      // Number keys 1-9 for quick bar actions
      if (e.key >= "1" && e.key <= "9" && !e.ctrlKey && !e.metaKey && !e.altKey) {
        const index = parseInt(e.key, 10) - 1;
        if (index < quickBarActions.length) {
          const action = quickBarActions[index];
          const avail = availabilityMap.get(action.id);
          if (avail?.isAvailable) {
            e.preventDefault();
            onExecute(action.id);
          }
        }
        return;
      }

      // Ctrl+Z / Cmd+Z for undo
      if ((e.ctrlKey || e.metaKey) && e.key === "z" && !e.shiftKey) {
        e.preventDefault();
        onUndo();
        return;
      }

      // Escape for reset confirmation
      if (e.key === "Escape") {
        e.preventDefault();
        onResetRequest();
      }
    }

    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [enabled, quickBarActions, availabilityMap, onExecute, onUndo, onResetRequest]);
}

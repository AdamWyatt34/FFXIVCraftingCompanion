"use client";

import { useMemo } from "react";
import { ActionButton } from "./ActionButton";
import type { ActionInfo, ActionAvailability } from "@/types/simulator";

interface QuickActionBarProps {
  actions: ActionInfo[];
  availableActions: ActionAvailability[];
  executingActionId: number | null;
  justUsedActionId: number | null;
  onExecute: (actionId: number) => void;
}

const QUICK_BAR_ACTION_NAMES = [
  "BasicSynthesis",
  "BasicTouch",
  "Innovation",
  "Veneration",
  "ByregotsBlessing",
  "Groundwork",
  "GreatStrides",
  "Manipulation",
];

export function QuickActionBar({
  actions,
  availableActions,
  executingActionId,
  justUsedActionId,
  onExecute,
}: QuickActionBarProps) {
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

  if (quickBarActions.length === 0) return null;

  return (
    <div className="bg-bg-surface rounded-lg border border-border p-2">
      <div className="flex items-center gap-1 mb-1.5">
        <h4 className="text-[10px] font-semibold uppercase tracking-wider text-text-muted">
          Quick Actions
        </h4>
        <span className="text-[10px] text-text-muted hidden lg:inline">
          (keys 1-{quickBarActions.length})
        </span>
      </div>
      <div
        className="flex gap-1.5 overflow-x-auto pb-0.5"
        style={{ scrollbarWidth: "none" }}
      >
        {quickBarActions.map((action, index) => (
          <ActionButton
            key={action.id}
            action={action}
            availability={availabilityMap.get(action.id)}
            isExecuting={executingActionId === action.id}
            justUsed={justUsedActionId === action.id}
            compact
            shortcutHint={String(index + 1)}
            onExecute={onExecute}
          />
        ))}
      </div>
    </div>
  );
}

// Export the quick bar action names for keyboard shortcuts to consume
export { QUICK_BAR_ACTION_NAMES };

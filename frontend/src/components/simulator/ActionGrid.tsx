"use client";

import { useMemo, useState } from "react";
import { ActionButton } from "./ActionButton";
import { ACTION_CATEGORY_COLORS } from "@/lib/action-icons";
import type { ActionInfo, ActionAvailability, ActionCategory } from "@/types/simulator";

interface ActionGridProps {
  actions: ActionInfo[];
  availableActions: ActionAvailability[];
  executingActionId: number | null;
  justUsedActionId: number | null;
  onExecute: (actionId: number) => void;
}

const CATEGORY_ORDER: ActionCategory[] = ["Progress", "Quality", "Buff", "Repair", "Other"];

const MOBILE_TABS: { label: string; categories: ActionCategory[] }[] = [
  { label: "Progress", categories: ["Progress"] },
  { label: "Quality", categories: ["Quality"] },
  { label: "Buffs", categories: ["Buff"] },
  { label: "Other", categories: ["Repair", "Other"] },
];

export function ActionGrid({
  actions,
  availableActions,
  executingActionId,
  justUsedActionId,
  onExecute,
}: ActionGridProps) {
  const [activeMobileTab, setActiveMobileTab] = useState(0);

  const availabilityMap = useMemo(() => {
    const map = new Map<number, ActionAvailability>();
    for (const a of availableActions) map.set(a.actionId, a);
    return map;
  }, [availableActions]);

  const groupedActions = useMemo(() => {
    const groups: Record<string, ActionInfo[]> = {};
    for (const cat of CATEGORY_ORDER) groups[cat] = [];
    for (const action of actions) {
      const cat = action.category as ActionCategory;
      if (groups[cat]) groups[cat].push(action);
    }
    for (const cat of Object.keys(groups)) {
      groups[cat].sort((a, b) => a.levelRequirement - b.levelRequirement);
    }
    return groups;
  }, [actions]);

  if (actions.length === 0) return null;

  const mobileCategories = MOBILE_TABS[activeMobileTab]?.categories ?? ["Progress"];
  const mobileActions = mobileCategories.flatMap((cat) => groupedActions[cat] ?? []);

  return (
    <div>
      {/* Desktop: all categories as sections */}
      <div className="hidden lg:block space-y-4">
        {CATEGORY_ORDER.map((category) => {
          const categoryActions = groupedActions[category];
          if (!categoryActions || categoryActions.length === 0) return null;
          const color = ACTION_CATEGORY_COLORS[category];
          const label = category === "Buff" ? "Buffs" : category === "Other" ? "Other" : category;

          return (
            <div key={category} role="group" aria-label={`${label} actions`}>
              <div className="flex items-center gap-2 mb-2">
                <div
                  className="w-2 h-2 rounded-full shrink-0"
                  style={{ backgroundColor: color }}
                />
                <h4 className="text-xs font-semibold uppercase tracking-wider text-text-secondary">
                  {label}
                </h4>
                <div className="flex-1 h-px bg-border" />
              </div>
              <div className="grid grid-cols-2 xl:grid-cols-3 gap-1.5">
                {categoryActions.map((action) => (
                  <ActionButton
                    key={action.id}
                    action={action}
                    availability={availabilityMap.get(action.id)}
                    isExecuting={executingActionId === action.id}
                    justUsed={justUsedActionId === action.id}
                    onExecute={onExecute}
                  />
                ))}
              </div>
            </div>
          );
        })}
      </div>

      {/* Tablet (sm to lg): all categories with section headers, compact layout */}
      <div className="hidden sm:block lg:hidden space-y-3">
        {CATEGORY_ORDER.map((category) => {
          const categoryActions = groupedActions[category];
          if (!categoryActions || categoryActions.length === 0) return null;
          const color = ACTION_CATEGORY_COLORS[category];
          const label = category === "Buff" ? "Buffs" : category === "Other" ? "Other" : category;

          return (
            <div key={category} role="group" aria-label={`${label} actions`}>
              <div className="flex items-center gap-2 mb-1.5">
                <div
                  className="w-2 h-2 rounded-full shrink-0"
                  style={{ backgroundColor: color }}
                />
                <h4 className="text-xs font-semibold uppercase tracking-wider text-text-secondary">
                  {label}
                </h4>
                <div className="flex-1 h-px bg-border" />
              </div>
              <div className="grid grid-cols-5 gap-1.5">
                {categoryActions.map((action) => (
                  <ActionButton
                    key={action.id}
                    action={action}
                    availability={availabilityMap.get(action.id)}
                    isExecuting={executingActionId === action.id}
                    justUsed={justUsedActionId === action.id}
                    compact
                    onExecute={onExecute}
                  />
                ))}
              </div>
            </div>
          );
        })}
      </div>

      {/* Mobile: tab-based categories */}
      <div className="sm:hidden">
        {/* Tab bar */}
        <div className="flex gap-1 mb-3 overflow-x-auto pb-1" role="toolbar" aria-label="Action categories">
          {MOBILE_TABS.map((tab, index) => {
            const isActive = activeMobileTab === index;
            const primaryCategory = tab.categories[0];
            const color = ACTION_CATEGORY_COLORS[primaryCategory];
            return (
              <button
                key={tab.label}
                type="button"
                onClick={() => setActiveMobileTab(index)}
                aria-pressed={isActive}
                className={`
                  px-3 py-1.5 rounded-full text-xs font-medium whitespace-nowrap transition-all shrink-0
                  ${isActive
                    ? "text-white"
                    : "text-text-secondary hover:text-text-primary bg-bg-elevated"
                  }
                `}
                style={isActive ? { backgroundColor: `${color}cc` } : undefined}
              >
                {tab.label}
              </button>
            );
          })}
        </div>

        {/* Action grid for active tab */}
        <div className="grid grid-cols-3 gap-1.5">
          {mobileActions.map((action) => (
            <ActionButton
              key={action.id}
              action={action}
              availability={availabilityMap.get(action.id)}
              isExecuting={executingActionId === action.id}
              justUsed={justUsedActionId === action.id}
              compact
              onExecute={onExecute}
            />
          ))}
        </div>
      </div>
    </div>
  );
}

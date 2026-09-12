"use client";

import { useState, memo } from "react";
import Image from "next/image";
import { LoadingSpinner } from "@/components/ui";
import { getActionIconUrl, ACTION_CATEGORY_COLORS } from "@/lib/action-icons";
import type {
  ActionInfo,
  ActionAvailability,
  ActionCategory,
} from "@/types/simulator";

interface ActionButtonProps {
  action: ActionInfo;
  availability: ActionAvailability | undefined;
  isExecuting: boolean;
  justUsed: boolean;
  compact?: boolean;
  shortcutHint?: string;
  onExecute: (actionId: number) => void;
}

function ActionButtonInner({
  action,
  availability,
  isExecuting,
  justUsed,
  compact = false,
  shortcutHint,
  onExecute,
}: ActionButtonProps) {
  const [iconError, setIconError] = useState(false);

  const isAvailable = availability?.isAvailable ?? false;
  const isDisabled = !isAvailable || isExecuting;
  const categoryColor =
    ACTION_CATEGORY_COLORS[action.category as ActionCategory] ?? "#9ca3af";
  const cpCost = availability?.cpCost ?? action.baseCPCost;
  const durabilityCost =
    availability?.durabilityCost ?? action.baseDurabilityCost;
  const successRate = availability?.successRate ?? action.baseSuccessRate;

  // Build aria-label with cost and availability info
  const ariaLabelParts: string[] = [action.inGameName];
  if (cpCost > 0) ariaLabelParts.push(`${cpCost} CP`);
  if (durabilityCost > 0) ariaLabelParts.push(`${durabilityCost} durability`);
  if (successRate < 100) ariaLabelParts.push(`${successRate}% success`);
  if (!isAvailable && availability?.unavailableReason) {
    ariaLabelParts.push(`Unavailable: ${availability.unavailableReason}`);
  }
  const ariaLabel = ariaLabelParts.join(", ");

  const tooltipParts: string[] = [action.inGameName];
  if (cpCost > 0) tooltipParts.push(`${cpCost} CP`);
  if (durabilityCost > 0) tooltipParts.push(`${durabilityCost} durability`);
  if (successRate < 100) tooltipParts.push(`${successRate}% success`);
  if (!isAvailable && availability?.unavailableReason) {
    tooltipParts.push(`\u2014 ${availability.unavailableReason}`);
  }
  if (shortcutHint) tooltipParts.push(`[${shortcutHint}]`);
  const tooltip = tooltipParts.join(" \u00b7 ");

  const handleClick = () => {
    if (!isDisabled) onExecute(action.id);
  };

  if (compact) {
    return (
      <span title={tooltip}>
        <button
          type="button"
          onClick={handleClick}
          disabled={isDisabled}
          aria-label={ariaLabel}
          aria-disabled={isDisabled}
          tabIndex={isAvailable ? 0 : -1}
          className={`
            relative flex flex-col items-center gap-0.5 p-1.5 rounded-md border transition-all duration-150
            w-[4.25rem] min-h-[4.5rem] focus-ring
            ${justUsed ? "animate-action-ripple" : ""}
            ${
              isAvailable && !isExecuting
                ? "border-border hover:border-border-focus hover:bg-bg-elevated hover:scale-[1.02] cursor-pointer"
                : "border-border opacity-40 cursor-not-allowed"
            }
          `}
          style={{
            backgroundColor: isAvailable ? `${categoryColor}08` : undefined,
            borderLeftWidth: "3px",
            borderLeftColor: categoryColor,
          }}
        >
          {/* Shortcut hint */}
          {shortcutHint && (
            <span className="absolute top-0.5 right-1 text-[10px] text-text-muted font-mono hidden lg:block">
              {shortcutHint}
            </span>
          )}

          {/* Icon */}
          <div className="relative w-7 h-7 shrink-0">
            {isExecuting ? (
              <div className="flex items-center justify-center w-full h-full">
                <LoadingSpinner size={18} />
              </div>
            ) : iconError ? (
              <div
                className="w-full h-full rounded"
                style={{ backgroundColor: `${categoryColor}30` }}
              />
            ) : (
              <Image
                src={getActionIconUrl(action.name)}
                alt=""
                width={28}
                height={28}
                className={`w-7 h-7 rounded ${!isAvailable ? "grayscale" : ""}`}
                onError={() => setIconError(true)}
                draggable={false}
                unoptimized
              />
            )}
          </div>

          {/* Name */}
          <span className="text-[10px] leading-tight text-text-secondary text-center line-clamp-2 w-full">
            {action.inGameName}
          </span>

          {/* CP cost */}
          <span
            className={`text-[9px] ${cpCost > 0 ? "text-violet-400" : "text-text-muted"}`}
          >
            {cpCost} CP
          </span>
        </button>
      </span>
    );
  }

  // Standard layout
  return (
    <span title={tooltip}>
      <button
        type="button"
        onClick={handleClick}
        disabled={isDisabled}
        aria-label={ariaLabel}
        aria-disabled={isDisabled}
        tabIndex={isAvailable ? 0 : -1}
        className={`
          relative flex items-center gap-2.5 px-2.5 py-2 rounded-md border transition-all duration-150 w-full focus-ring
          ${justUsed ? "animate-action-ripple" : ""}
          ${
            isAvailable && !isExecuting
              ? "border-border hover:border-border-focus hover:bg-bg-elevated hover:scale-[1.02] cursor-pointer"
              : "border-border opacity-40 cursor-not-allowed"
          }
        `}
        style={{
          backgroundColor: isAvailable ? `${categoryColor}08` : undefined,
          borderLeftWidth: "3px",
          borderLeftColor: categoryColor,
        }}
      >
        {/* Icon */}
        <div className="relative w-9 h-9 shrink-0">
          {isExecuting ? (
            <div className="flex items-center justify-center w-full h-full">
              <LoadingSpinner size={22} />
            </div>
          ) : iconError ? (
            <div
              className="w-full h-full rounded"
              style={{ backgroundColor: `${categoryColor}30` }}
            />
          ) : (
            <Image
              src={getActionIconUrl(action.name)}
              alt=""
              width={36}
              height={36}
              className={`w-9 h-9 rounded ${!isAvailable ? "grayscale" : ""}`}
              onError={() => setIconError(true)}
              draggable={false}
              unoptimized
            />
          )}
        </div>

        {/* Content */}
        <div className="flex-1 min-w-0">
          <div className="text-sm font-medium text-text-primary truncate">
            {action.inGameName}
          </div>
          <div className="flex items-center gap-2 mt-0.5">
            <span
              className={`text-xs ${cpCost > 0 ? "text-violet-400" : "text-text-muted"}`}
            >
              {cpCost} CP
            </span>
            {durabilityCost > 0 && (
              <span className="text-xs text-amber-400/70">
                {durabilityCost} dur
              </span>
            )}
            {successRate < 100 && (
              <span className="text-xs px-1 py-px rounded bg-amber-500/15 text-amber-400">
                {successRate}%
              </span>
            )}
          </div>
        </div>
      </button>
    </span>
  );
}

// onExecute changes identity with the crafting state; a button that kept an older one would
// execute its action on the state that handler closed over, not the current step's.
export const ActionButton = memo(ActionButtonInner, (prev, next) => {
  return (
    prev.action.id === next.action.id &&
    prev.onExecute === next.onExecute &&
    prev.availability?.isAvailable === next.availability?.isAvailable &&
    prev.availability?.cpCost === next.availability?.cpCost &&
    prev.availability?.durabilityCost === next.availability?.durabilityCost &&
    prev.availability?.successRate === next.availability?.successRate &&
    prev.isExecuting === next.isExecuting &&
    prev.justUsed === next.justUsed &&
    prev.compact === next.compact
  );
});

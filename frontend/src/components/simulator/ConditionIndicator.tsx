"use client";

import { CONDITION_COLORS } from "@/lib/action-icons";
import type { CraftCondition } from "@/types/simulator";

const CONDITION_DESCRIPTIONS: Record<CraftCondition, string> = {
  Normal: "No special effects",
  Good: "Quality actions are 1.5x more effective. Tricks of the Trade available.",
  Excellent: "Quality actions are 4x more effective",
  Poor: "Quality actions are 0.5x less effective",
  Centered: "Action success rate +25%",
  Sturdy: "Durability cost halved",
  Pliant: "CP cost halved",
  Malleable: "Progress actions are 1.5x more effective",
  Primed: "Buff durations +2",
  GoodOmen: "Next condition will be Good",
};

const CONDITION_DISPLAY_NAMES: Partial<Record<CraftCondition, string>> = {
  GoodOmen: "Good Omen",
};

interface ConditionIndicatorProps {
  condition: CraftCondition;
}

export function ConditionIndicator({ condition }: ConditionIndicatorProps) {
  const color = CONDITION_COLORS[condition];
  const displayName = CONDITION_DISPLAY_NAMES[condition] ?? condition;
  const isPulsing = condition === "Good" || condition === "Excellent";

  return (
    <div
      className="flex items-center gap-2 shrink-0"
      role="status"
      aria-live="polite"
      aria-label={`Condition: ${displayName}. ${CONDITION_DESCRIPTIONS[condition]}`}
    >
      <span
        className={`text-lg ${isPulsing ? "animate-pulse-glow" : ""}`}
        style={{ color, transition: "color 0.3s ease" }}
        aria-hidden="true"
      >
        &#x25C6;
      </span>
      <span
        className="text-sm font-medium"
        style={{ color, transition: "color 0.3s ease" }}
      >
        {displayName}
      </span>
    </div>
  );
}

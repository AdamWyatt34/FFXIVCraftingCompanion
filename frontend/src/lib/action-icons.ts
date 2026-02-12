import type { ActionCategory, CraftCondition } from "@/types/simulator";

// ── Action icon URLs from XIVAPI CraftAction sheet ──
// Pattern: https://xivapi.com/i/{folder}/{id}.png

const ACTION_ICON_MAP: Record<string, string> = {
  // Progress
  BasicSynthesis: "/i/001000/001042.png",
  CarefulSynthesis: "/i/002000/002920.png",
  RapidSynthesis: "/i/002000/002921.png",
  FocusedSynthesis: "/i/002000/002931.png",
  Groundwork: "/i/002000/002932.png",
  IntensiveSynthesis: "/i/002000/002929.png",
  PrudentSynthesis: "/i/002000/002936.png",
  MuscleMemory: "/i/002000/002934.png",
  DelicateSynthesis: "/i/002000/002930.png",
  // Quality
  BasicTouch: "/i/001000/001043.png",
  StandardTouch: "/i/002000/002913.png",
  AdvancedTouch: "/i/002000/002933.png",
  HastyTouch: "/i/002000/002914.png",
  FocusedTouch: "/i/002000/002919.png",
  PreparatoryTouch: "/i/002000/002926.png",
  PrudentTouch: "/i/002000/002922.png",
  Reflect: "/i/002000/002935.png",
  ByregotsBlessing: "/i/002000/002917.png",
  TrainedFinesse: "/i/002000/002937.png",
  TrainedEye: "/i/002000/002927.png",
  // Buffs
  InnerQuiet: "/i/002000/002916.png",
  WasteNot: "/i/002000/002910.png",
  WasteNotII: "/i/002000/002923.png",
  Veneration: "/i/002000/002925.png",
  Innovation: "/i/002000/002918.png",
  GreatStrides: "/i/002000/002911.png",
  Manipulation: "/i/002000/002912.png",
  FinalAppraisal: "/i/002000/002928.png",
  Observe: "/i/001000/001046.png",
  HeartAndSoul: "/i/002000/002938.png",
  TrainedPerfection: "/i/002000/002940.png",
  QuickInnovation: "/i/002000/002939.png",
  // Repair
  MastersMend: "/i/001000/001044.png",
  ImmaculateMend: "/i/002000/002941.png",
  // Other
  TricksOfTheTrade: "/i/002000/002915.png",
  CarefulObservation: "/i/002000/002924.png",
};

const XIVAPI_ICON_BASE = "https://xivapi.com";

export function getActionIconUrl(actionName: string): string {
  const path = ACTION_ICON_MAP[actionName];
  if (path) return `${XIVAPI_ICON_BASE}${path}`;
  return `${XIVAPI_ICON_BASE}/i/000000/000405.png`; // fallback generic icon
}

// ── Category colors ──

export const ACTION_CATEGORY_COLORS: Record<ActionCategory, string> = {
  Progress: "#60a5fa",
  Quality: "#4ade80",
  Buff: "#fbbf24",
  Repair: "#f87171",
  Other: "#a78bfa",
};

// ── Condition colors ──

export const CONDITION_COLORS: Record<CraftCondition, string> = {
  Normal: "#9ca3af",
  Good: "#60a5fa",
  Excellent: "#fbbf24",
  Poor: "#ef4444",
  Centered: "#f59e0b",
  Sturdy: "#3b82f6",
  Pliant: "#22c55e",
  Malleable: "#a855f7",
  Primed: "#06b6d4",
  GoodOmen: "#818cf8",
};

// ── Buff display info ──

export const BUFF_DISPLAY: Record<
  string,
  { label: string; color: string; abbreviation: string }
> = {
  "Waste Not": { label: "Waste Not", color: "#3b82f6", abbreviation: "WN" },
  "Waste Not II": { label: "Waste Not II", color: "#2563eb", abbreviation: "WN2" },
  Veneration: { label: "Veneration", color: "#ef4444", abbreviation: "Ven" },
  Innovation: { label: "Innovation", color: "#22c55e", abbreviation: "Inn" },
  "Great Strides": { label: "Great Strides", color: "#f59e0b", abbreviation: "GS" },
  Manipulation: { label: "Manipulation", color: "#a855f7", abbreviation: "Man" },
  "Final Appraisal": { label: "Final Appraisal", color: "#ec4899", abbreviation: "FA" },
  "Muscle Memory": { label: "Muscle Memory", color: "#f97316", abbreviation: "MM" },
  "Inner Quiet": { label: "Inner Quiet", color: "#14b8a6", abbreviation: "IQ" },
  "Heart and Soul": { label: "Heart and Soul", color: "#e879f9", abbreviation: "H&S" },
  "Trained Perfection": { label: "Trained Perfection", color: "#8b5cf6", abbreviation: "TP" },
};

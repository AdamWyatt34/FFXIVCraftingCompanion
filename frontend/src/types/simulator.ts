import type { RecipeSummaryDto } from "./api";

// ── Enums (string unions matching backend) ──

export type CraftCondition =
  | "Normal"
  | "Good"
  | "Excellent"
  | "Poor"
  | "Centered"
  | "Sturdy"
  | "Pliant"
  | "Malleable"
  | "Primed"
  | "GoodOmen";

export type CraftStatus = "InProgress" | "Completed" | "Failed";

export type ActionCategory = "Progress" | "Quality" | "Buff" | "Repair" | "Other";

// ── Response DTOs ──

export interface ActiveBuff {
  name: string;
  remainingTurns: number;
  stacks: number | null;
}

export interface CraftingState {
  progress: number;
  maxProgress: number;
  progressPercent: number;
  quality: number;
  maxQuality: number;
  qualityPercent: number;
  durability: number;
  maxDurability: number;
  cp: number;
  maxCP: number;
  step: number;
  condition: CraftCondition;
  status: CraftStatus;
  hqPercent: number;
  activeBuffs: ActiveBuff[];
  innerQuietStacks: number;
  // Hidden state for DTO roundtrip (step-by-step mode)
  comboAction: number;
  expedienceActive: boolean;
  isFirstStep: boolean;
  quickInnovationUsed: boolean;
  heartAndSoulUsed: boolean;
  trainedPerfectionUsed: boolean;
  carefulObservationCount: number;
}

export interface StepDeltas {
  progress: number;
  quality: number;
  durability: number;
  cp: number;
}

export interface StepResult {
  stepNumber: number;
  actionName: string;
  actionId: number;
  actionCategory: string;
  succeeded: boolean;
  failReason: string | null;
  deltas: StepDeltas;
  stateAfter: CraftingState;
}

export interface SimulationResult {
  success: boolean;
  totalSteps: number;
  finalQualityPercent: number;
  finalHQPercent: number;
  finalState: CraftingState;
  steps: StepResult[];
  macros: string[];
  recipe: RecipeSummaryDto;
}

export interface ActionInfo {
  id: number;
  name: string;
  inGameName: string;
  levelRequirement: number;
  category: string;
  baseCPCost: number;
  baseDurabilityCost: number;
  baseSuccessRate: number;
  /** Removed from the game: a saved rotation still replays it, but the palette does not offer it. */
  isLegacy: boolean;
}

export interface ActionAvailability {
  actionId: number;
  actionName: string;
  isAvailable: boolean;
  unavailableReason: string | null;
  cpCost: number;
  durabilityCost: number;
  successRate: number;
  category: string;
}

export interface ExportMacroResult {
  macros: string[];
  totalActions: number;
  macroCount: number;
}

// ── Request DTOs ──

export interface CrafterStatsInput {
  level: number;
  craftsmanship: number;
  control: number;
  cp: number;
  isSpecialist?: boolean;
}

export interface ConsumableChoice {
  itemId: number;
  hq: boolean;
}

export interface ConsumableSelection {
  meal: ConsumableChoice | null;
  medicine: ConsumableChoice | null;
}

export interface SimulationOptions {
  assumeAllSuccess?: boolean;
  assumeNormalCondition?: boolean;
  randomSeed?: number;
  forceCondition?: string;
}

export interface HQIngredientInput {
  itemId: number;
  hqQuantity: number;
}

export interface SimulateRequest {
  recipeId: number;
  stats?: CrafterStatsInput;
  useProfileStats?: boolean;
  actionIds: number[];
  actionNames?: string[];
  options?: SimulationOptions;
  hqIngredients?: HQIngredientInput[];
}

export interface ExecuteActionRequest {
  state: CraftingState;
  actionId: number;
  stats: CrafterStatsInput;
  recipeId: number;
}

export interface AvailableActionsRequest {
  state: CraftingState;
  stats: CrafterStatsInput;
  recipeId: number;
}

export interface MacroOptions {
  linesPerMacro?: number;
  soundEffect?: number;
  completeSoundEffect?: number;
  includeEcho?: boolean;
}

export interface ExportMacroRequest {
  actionIds: number[];
  options?: MacroOptions;
}

// ── Buff change tracking (for step history display) ──

export interface BuffChange {
  name: string;
  type: "added" | "removed" | "changed";
  turnsAfter?: number;
  stacksBefore?: number;
  stacksAfter?: number;
}

// ── Forward-looking types (no backend endpoint yet) ──

export interface ReliabilityRequest {
  recipeId: number;
  stats: CrafterStatsInput;
  actionIds: number[];
  runs?: number;
}

export interface ReliabilityReport {
  totalRuns: number;
  successfulRuns: number;
  successRate: number;
  averageQualityPercent: number;
  minQualityPercent: number;
  maxQualityPercent: number;
  averageHQPercent: number;
  medianHQPercent: number;
}

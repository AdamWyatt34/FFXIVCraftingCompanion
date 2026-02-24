import type { CrafterStatsInput, HQIngredientInput, SimulationResult } from "./simulator";

// ── Enums (string unions matching backend) ──

export type SolverJobStatus =
  | "Queued"
  | "Running"
  | "Completed"
  | "Failed"
  | "TimedOut";

// ── Response DTOs ──

export interface SolverMetrics {
  algorithmUsed: string;
  solveTimeMs: number;
  totalSteps: number;
  finalQualityPercent: number;
  finalHQPercent: number;
  craftCompleted: boolean;
  nodesExplored: number;
  iterationsCompleted: number;
}

export interface SolverResult {
  actionIds: number[];
  actionNames: string[];
  simulation: SimulationResult | null;
  macros: string[];
  metrics: SolverMetrics;
  errorMessage: string | null;
}

export interface SolverJob {
  jobId: string;
  status: SolverJobStatus;
  result: SolverResult | null;
  elapsedMs: number;
  errorMessage: string | null;
}

/** Returned on 202 Accepted when a new solve job is enqueued. */
export interface SolveJobResponse {
  jobId: string;
  status: string;
}

// ── Request DTOs ──

export interface SolverConstraints {
  mustComplete?: boolean;
  maximizeQuality?: boolean;
  minimizeSteps?: boolean;
  targetQualityPercent?: number;
  allowUnsafeActions?: boolean;
  maxSteps?: number;
}

export interface SolverSettings {
  algorithm?: "auto" | "greedy" | "mcts";
  timeBudgetMs?: number;
  randomSeed?: number;
}

export interface SolveRequest {
  recipeId: number;
  stats?: CrafterStatsInput;
  useProfileStats?: boolean;
  constraints?: SolverConstraints;
  settings?: SolverSettings;
  hqIngredients?: HQIngredientInput[];
}

export interface QuickSolveParams {
  recipeId: number;
  craftsmanship: number;
  control: number;
  cp: number;
  level: number;
}

// ── Type Guards ──

/**
 * Discriminates a full SolverJob (200 cached) from a SolveJobResponse (202 enqueued).
 * The 200 response always includes `elapsedMs` (number); the 202 response only has jobId + status.
 */
export function isSolverJob(
  obj: SolverJob | SolveJobResponse,
): obj is SolverJob {
  return "elapsedMs" in obj;
}

// ── Rotation Library ──

export interface RotationListItem {
  id: number;
  name: string | null;
  recipeId: number;
  recipeName: string;
  recipeLevel: number;
  craftType: string;
  actionIds: number[];
  qualityPercent: number;
  hqPercent: number;
  totalSteps: number;
  craftCompleted: boolean;
  source: string;
  authorName: string | null;
  upvotes: number;
  createdAt: string;
  craftsmanship: number;
  control: number;
  cp: number;
  crafterLevel: number;
}

export interface RotationDetail extends RotationListItem {
  notes: string | null;
  actionNames: string[];
  algorithm: string | null;
  macros: string[];
}

export interface RotationListResult {
  rotations: RotationListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface UpvoteResult {
  upvoted: boolean;
  upvotes: number;
}

export interface SubmitRotationRequest {
  recipeId: number;
  actionIds: number[];
  stats: CrafterStatsInput;
  source?: string;
  name?: string;
  notes?: string;
  algorithm?: string;
}

export interface RotationSearchParams {
  recipeId?: number;
  craftType?: string;
  minLevel?: number;
  maxLevel?: number;
  source?: string;
  sortBy?: "quality" | "steps" | "recent" | "popular";
  page?: number;
  pageSize?: number;
}

// ── What If Types ──

export interface WhatIfStatSet {
  craftsmanship: number;
  control: number;
  cp: number;
  level: number;
}

export interface WhatIfResult {
  id: string;
  label: string;
  stats: WhatIfStatSet;
  isLoading: boolean;
  error: string | null;
  result: SolverResult | null;
  hqPercent: number;
  steps: number;
  craftCompleted: boolean;
}

export interface WhatIfPreset {
  key: string;
  label: string;
  description: string;
  buffs: { craftsmanship: number; control: number; cp: number };
}

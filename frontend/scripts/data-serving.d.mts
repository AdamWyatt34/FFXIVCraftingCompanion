export function defaultDataBase(repo: string, sha: string): string;
export function fallbackDataBase(base: string): string | null;
export function toleratedRefusals(total: number): number;

export interface WarmedFiles {
  total: number;
  /** Relative paths the CDN still refused after the warming rounds. */
  refused: string[];
}

export interface ServingInputs {
  forceLocal: boolean;
  base: string;
  probedVersion: string | null;
  expectedVersion: string;
  warmed?: WarmedFiles | null;
}

export function decideServing(
  inputs: ServingInputs,
): { mode: "remote"; reason: null; warning: string | null } | { mode: "local"; reason: string };

export function defaultDataBase(repo: string, sha: string): string;

export interface ServingInputs {
  forceLocal: boolean;
  base: string;
  probedVersion: string | null;
  expectedVersion: string;
}

export function decideServing(inputs: ServingInputs): { mode: "remote"; reason: null } | { mode: "local"; reason: string };
